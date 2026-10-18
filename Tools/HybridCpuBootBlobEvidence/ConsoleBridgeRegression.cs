using System.Reflection;
using HybridCPU.Platform.Contracts;
using HybridCPU.RuntimeKernel;
using HybridCPU_ISE.CloseToHSL.Core.Runtime.Managed;
using HybridCPU_ISE.NonRTL.Runtime;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Pipeline;
using YAKSys_Hybrid_CPU.Core.Registers;

internal static class ConsoleBridgeRegression
{
    public static void Verify(HybridCpuIseManagedImageLoadResultV1 load, IHybridCpuRuntimeKernelV1 kernel,
        HybridCpuIseConsoleProviderV1 sink, Action<bool, string> check)
    {
        var type = load.TypeSystem!.Descriptors.Single(t => t.StableIdentity == "System.String");
        var text = load.Strings!.MaterializeLiteral(load.TypeSystem.TypeHandle(type.TypeId)!.Value, "Ωx");
        check(text.IsSuccess, "console regression materializes guest-owned UTF-16");
        ulong address = text.ObjectReference + (ulong)type.StringShape!.DataOffsetBytes;
        var draft = new HybridCpuExternalServiceRequestV1(load.Context!.ContextId, HybridCpuHostServiceV1.Console,
            HybridCpuConsoleServiceContractV1.SetTitleUtf16Operation, HybridCpuPrivilegeModeV1.User,
            address, 4, HybridCpuHostBufferAccessV1.Read, [], string.Empty);
        var direct = kernel.ExternalServiceTransition(draft with
            { TransitionDigest = HybridCpuManagedInteropContractV1.ComputeTransitionDigest(draft) });
        check(direct.Status == HybridCpuExternalServiceStatusV1.Success, "kernel console reaches a real sink: " + direct.Reason);
        check(sink.Output.Count == 1 && sink.Output[0].Text == "Ωx" && sink.Output[0].Utf16LittleEndianHex == "A9037800",
            "console sink records exact UTF-16 bytes and title operation");
        ICanonicalCpuState Envelope()
        {
            var state = DispatchProxy.Create<ICanonicalCpuState, RegisterState>();
            state.WritePc(0, 0x10000);
            state.WriteRegister(0, 10, (ulong)HybridCpuHostServiceV1.Console);
            state.WriteRegister(0, 11, HybridCpuConsoleServiceContractV1.WriteUtf16Operation);
            state.WriteRegister(0, 12, address); state.WriteRegister(0, 13, 4);
            state.WriteRegister(0, 14, (ulong)HybridCpuHostBufferAccessV1.Read);
            state.WriteRegister(0, 18, 0x1234); state.WriteRegister(0, 19, 0x5678);
            return state;
        }
        var ecall = new EcallEvent { VtId = 0, BundleSerial = 1,
            EcallCode = (long)HybridCpuExternalServiceEcallContractV1.EcallNumber };
        var state = Envelope();
        check(load.EcallBridge!.TryHandle(ecall, state, PrivilegeLevel.User) && state.ReadRegister(0, 11) == 0 &&
            state.ReadPc(0) == 0x10100 && sink.Output.Count == 2 && sink.Output[1].Text == "Ωx",
            "console write retires through production bridge into the sink");
        check(state.ReadRegister(0, 18) == 0x1234 && state.ReadRegister(0, 19) == 0x5678, "console preserves x18/x19");
        foreach (var mutation in new (int Register, ulong Value)[] { (12, ulong.MaxValue - 1), (13, 3), (14, 2), (15, 1) })
        {
            var bad = Envelope(); bad.WriteRegister(0, mutation.Register, mutation.Value);
            check(load.EcallBridge.TryHandle(ecall, bad, PrivilegeLevel.User) && bad.ReadRegister(0, 11) != 0 &&
                sink.Output.Count == 2, "invalid console envelope does not reach sink: x" + mutation.Register);
        }
        var privileged = Envelope();
        check(!load.EcallBridge.TryHandle(ecall, privileged, PrivilegeLevel.Machine) && privileged.ReadPc(0) == 0x10000,
            "console bridge does not admit machine privilege");
        var unknown = Envelope(); unknown.WriteRegister(0, 11, 123);
        check(!load.EcallBridge.TryHandle(ecall, unknown, PrivilegeLevel.User), "unknown console operation remains unbound");
        var limited = new HybridCpuIseConsoleProviderV1((_, _) => [0xa9, 3, 0x78, 0], maximumBytes: 2);
        check(limited.Invoke(draft).Status == HybridCpuExternalServiceStatusV1.ProviderFailure && limited.Output.Count == 0,
            "bounded sink fails closed before exceeding capacity");
    }
}
