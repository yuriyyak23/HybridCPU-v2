using System.Reflection;
using HybridCPU.Platform.Contracts;
using HybridCPU.RuntimeKernel;
using HybridCPU_ISE.CloseToHSL.Core.Runtime.Managed;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Pipeline;
using YAKSys_Hybrid_CPU.Core.Registers;

internal static class ClockReadBridgeRegression
{
    public static void Verify(HybridCpuIseManagedImageLoadResultV1 load, IHybridCpuRuntimeKernelV1 kernel,
        Action<bool, string> check)
    {
        check(kernel.AdvanceMonotonicTime(1_000_000).IsSuccess, "component explicitly advances kernel virtual time");
        var ecall = new EcallEvent { VtId = 0, BundleSerial = 1,
            EcallCode = (long)HybridCpuExternalServiceEcallContractV1.EcallNumber };
        ICanonicalCpuState Envelope(ulong operation)
        {
            var state = DispatchProxy.Create<ICanonicalCpuState, RegisterState>();
            state.WritePc(0, 0x10000);
            state.WriteRegister(0, 10, (ulong)HybridCpuHostServiceV1.Clock);
            state.WriteRegister(0, 11, operation);
            state.WriteRegister(0, 18, 0x1234);
            state.WriteRegister(0, 19, 0x5678);
            return state;
        }
        foreach (ulong operation in new[] { HybridCpuVirtualClockServiceContractV1.ReadDoomTicsOperation,
            HybridCpuVirtualClockServiceContractV1.ReadTicksOperation })
        {
            var draft = new HybridCpuExternalServiceRequestV1(load.Context!.ContextId,
                HybridCpuHostServiceV1.Clock, operation, HybridCpuPrivilegeModeV1.User,
                0, 0, HybridCpuHostBufferAccessV1.None, [], string.Empty);
            var expected = kernel.ExternalServiceTransition(draft with
                { TransitionDigest = HybridCpuManagedInteropContractV1.ComputeTransitionDigest(draft) });
            check(expected.Status == HybridCpuExternalServiceStatusV1.Success && expected.ReturnValue > 0,
                "existing kernel supplies nonzero clock result");
            var state = Envelope(operation);
            check(load.EcallBridge!.TryHandle(ecall, state, PrivilegeLevel.User), "ISE bridge admits synchronous clock read");
            check(unchecked((ulong)state.ReadRegister(0, 10)) == expected.ReturnValue &&
                state.ReadRegister(0, 11) == 0 && state.ReadRegister(0, 12) == 0 && state.ReadPc(0) == 0x10100,
                "clock bridge returns exact kernel value and advances one bundle");
            check(state.ReadRegister(0, 18) == 0x1234 && state.ReadRegister(0, 19) == 0x5678,
                "clock bridge preserves x18/x19");
            var malformed = Envelope(operation); malformed.WriteRegister(0, 15, 1);
            check(load.EcallBridge.TryHandle(ecall, malformed, PrivilegeLevel.User) && malformed.ReadRegister(0, 11) != 0,
                "clock arguments cannot be silently dropped");
            var privileged = Envelope(operation);
            check(!load.EcallBridge.TryHandle(ecall, privileged, PrivilegeLevel.Machine) && privileged.ReadPc(0) == 0x10000,
                "clock read does not admit machine privilege");
        }
        var wait = Envelope(HybridCpuVirtualClockServiceContractV1.WaitUntilDoomTicOperation);
        check(!load.EcallBridge!.TryHandle(ecall, wait, PrivilegeLevel.User) && wait.ReadPc(0) == 0x10000,
            "parking clock operation requires separate scheduling binding");
    }
}
