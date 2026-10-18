using HybridCPU.Platform.Contracts;
using HybridCPU.RuntimeKernel;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Pipeline;
using YAKSys_Hybrid_CPU.Core.Registers;

namespace HybridCPU_ISE.NonRTL.Runtime;

public sealed record HybridCpuIseConsoleEcallObservationV1(ulong Pc, ulong ReturnAddress,
    ulong Operation, ulong BufferAddress, ulong BufferLength, HybridCpuExternalServiceStatusV1 Status, string Reason);

/// <summary>Existing synchronous console contract transported at retirement; kernel owns mapping validation.</summary>
public static class HybridCpuIseConsoleEcallBindingV1
{
    public static bool TryHandle(IHybridCpuRuntimeKernelV1 kernel, EcallEvent ecall,
        ICanonicalCpuState state, PrivilegeLevel privilege, out HybridCpuIseConsoleEcallObservationV1? observation)
    {
        observation = null;
        ulong Read(int register) => unchecked((ulong)state.ReadRegister(ecall.VtId, register));
        ulong pc = state.ReadPc(ecall.VtId), operation = Read(11);
        var context = kernel.CurrentContext();
        if (ecall.EcallCode != (long)HybridCpuExternalServiceEcallContractV1.EcallNumber ||
            Read(10) != (ulong)HybridCpuHostServiceV1.Console || operation is not
                (HybridCpuConsoleServiceContractV1.WriteUtf16Operation or HybridCpuConsoleServiceContractV1.SetTitleUtf16Operation) ||
            privilege != PrivilegeLevel.User || context is null || context.VirtualThreadCarrier != ecall.VtId ||
            pc > ulong.MaxValue - 256) return false;
        var status = HybridCpuExternalServiceStatusV1.InvalidRequest;
        ulong value = 0;
        int error = 0;
        string reason = "Console requires no scalar arguments and a valid buffer-access envelope.";
        if (Read(15) == 0 && Read(14) <= byte.MaxValue)
        {
            var draft = new HybridCpuExternalServiceRequestV1(context.ContextId, HybridCpuHostServiceV1.Console,
                operation, HybridCpuPrivilegeModeV1.User, Read(12), Read(13),
                (HybridCpuHostBufferAccessV1)Read(14), [], string.Empty);
            var result = kernel.ExternalServiceTransition(draft with
                { TransitionDigest = HybridCpuManagedInteropContractV1.ComputeTransitionDigest(draft) });
            status = result.Status; value = result.ReturnValue; error = result.ErrorCode; reason = result.Reason;
        }
        observation = new(pc, Read(1), operation, Read(12), Read(13), status, reason);
        state.WriteRegister(ecall.VtId, HybridCpuExternalServiceEcallContractV1.ResultValueRegister, value);
        state.WriteRegister(ecall.VtId, HybridCpuExternalServiceEcallContractV1.ResultStatusRegister, (ulong)status);
        state.WriteRegister(ecall.VtId, HybridCpuExternalServiceEcallContractV1.ResultErrorRegister, unchecked((ulong)(long)error));
        state.WritePc(ecall.VtId, pc + 256);
        return true;
    }
}
