using HybridCPU.Platform.Contracts;
using HybridCPU.RuntimeKernel;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Pipeline;
using YAKSys_Hybrid_CPU.Core.Registers;

namespace HybridCPU_ISE.NonRTL.Runtime;

public sealed record HybridCpuIseClockReadEcallObservationV1(ulong Pc, ulong ReturnAddress,
    ulong Operation, HybridCpuExternalServiceStatusV1 Status, ulong Value, string Reason);

/// <summary>Register transport for existing synchronous kernel clock reads. No clock or scheduler authority.</summary>
public static class HybridCpuIseClockReadEcallBindingV1
{
    public static bool TryHandle(IHybridCpuRuntimeKernelV1 kernel, EcallEvent ecall,
        ICanonicalCpuState state, PrivilegeLevel privilege, out HybridCpuIseClockReadEcallObservationV1? observation)
    {
        observation = null;
        ulong Read(int register) => unchecked((ulong)state.ReadRegister(ecall.VtId, register));
        ulong pc = state.ReadPc(ecall.VtId), operation = Read(11);
        var context = kernel.CurrentContext();
        if (ecall.EcallCode != (long)HybridCpuExternalServiceEcallContractV1.EcallNumber ||
            Read(10) != (ulong)HybridCpuHostServiceV1.Clock ||
            operation is not (HybridCpuVirtualClockServiceContractV1.ReadTicksOperation or
                HybridCpuVirtualClockServiceContractV1.ReadDoomTicsOperation) ||
            privilege != PrivilegeLevel.User || context is null || context.VirtualThreadCarrier != ecall.VtId ||
            pc > ulong.MaxValue - 256) return false;

        var status = HybridCpuExternalServiceStatusV1.InvalidRequest;
        ulong value = 0;
        int error = 0;
        string reason = "Clock read requires no arguments and a valid buffer-access envelope.";
        if (Read(15) == 0 && Read(14) <= byte.MaxValue)
        {
            var draft = new HybridCpuExternalServiceRequestV1(context.ContextId, HybridCpuHostServiceV1.Clock,
                operation, HybridCpuPrivilegeModeV1.User, Read(12), Read(13),
                (HybridCpuHostBufferAccessV1)Read(14), [], string.Empty);
            var result = kernel.ExternalServiceTransition(draft with
                { TransitionDigest = HybridCpuManagedInteropContractV1.ComputeTransitionDigest(draft) });
            status = result.Status; value = result.ReturnValue; error = result.ErrorCode; reason = result.Reason;
        }
        observation = new(pc, Read(1), operation, status, value, reason);
        state.WriteRegister(ecall.VtId, HybridCpuExternalServiceEcallContractV1.ResultValueRegister, value);
        state.WriteRegister(ecall.VtId, HybridCpuExternalServiceEcallContractV1.ResultStatusRegister, (ulong)status);
        state.WriteRegister(ecall.VtId, HybridCpuExternalServiceEcallContractV1.ResultErrorRegister, unchecked((ulong)(long)error));
        state.WritePc(ecall.VtId, pc + 256);
        return true;
    }
}
