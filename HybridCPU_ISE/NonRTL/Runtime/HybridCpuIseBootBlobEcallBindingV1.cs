using HybridCPU.Platform.Contracts;
using HybridCPU.RuntimeKernel;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Pipeline;
using YAKSys_Hybrid_CPU.Core.Registers;

namespace HybridCPU_ISE.NonRTL.Runtime;

public sealed record HybridCpuIseBootBlobEcallObservationV1(ulong Pc, ulong ReturnAddress,
    ulong BlobId, ulong ArgumentCount, HybridCpuExternalServiceStatusV1 Status, ulong Reference, string Reason);

/// <summary>Retirement bridge wiring for the existing non-parking File/BLOB kernel contract.</summary>
public static class HybridCpuIseBootBlobEcallBindingV1
{
    public static bool TryHandle(IHybridCpuRuntimeKernelV1 kernel, EcallEvent ecall,
        ICanonicalCpuState state, PrivilegeLevel privilege, out HybridCpuIseBootBlobEcallObservationV1? observation)
    {
        observation = null;
        ulong Read(int register) => unchecked((ulong)state.ReadRegister(ecall.VtId, register));
        ulong pc = state.ReadPc(ecall.VtId);
        var context = kernel.CurrentContext();
        if (ecall.EcallCode != (long)HybridCpuExternalServiceEcallContractV1.EcallNumber ||
            Read(10) != (ulong)HybridCpuHostServiceV1.File || Read(11) != HybridCpuBootBlobServiceContractV1.GetOperation ||
            privilege != PrivilegeLevel.User || context is null || context.VirtualThreadCarrier != ecall.VtId ||
            pc > ulong.MaxValue - 256) return false;
        ulong count = Read(15), blobId = Read(16), access = Read(14);
        HybridCpuExternalServiceStatusV1 status = HybridCpuExternalServiceStatusV1.InvalidRequest;
        ulong value = 0;
        int error = 0;
        string reason = "BLOB requires exactly one argument and a valid buffer-access envelope.";
        if (count == 1 && access <= byte.MaxValue)
        {
            var unsigned = new HybridCpuExternalServiceRequestV1(context.ContextId, HybridCpuHostServiceV1.File,
                HybridCpuBootBlobServiceContractV1.GetOperation, HybridCpuPrivilegeModeV1.User,
                Read(12), Read(13), (HybridCpuHostBufferAccessV1)access, [blobId], string.Empty);
            var request = unsigned with { TransitionDigest = HybridCpuManagedInteropContractV1.ComputeTransitionDigest(unsigned) };
            var result = kernel.ExternalServiceTransition(request);
            status = result.Status; value = result.ReturnValue; error = result.ErrorCode; reason = result.Reason;
        }
        observation = new(pc, Read(1), blobId, count, status, value, reason);
        state.WriteRegister(ecall.VtId, HybridCpuExternalServiceEcallContractV1.ResultValueRegister, value);
        state.WriteRegister(ecall.VtId, HybridCpuExternalServiceEcallContractV1.ResultStatusRegister, (ulong)status);
        state.WriteRegister(ecall.VtId, HybridCpuExternalServiceEcallContractV1.ResultErrorRegister, unchecked((ulong)(long)error));
        state.WritePc(ecall.VtId, pc + 256);
        return true;
    }
}
