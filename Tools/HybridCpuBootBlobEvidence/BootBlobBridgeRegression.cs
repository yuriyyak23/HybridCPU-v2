using System.Reflection;
using HybridCPU.Platform.Contracts;
using HybridCPU_ISE.CloseToHSL.Core.Runtime.Managed;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Pipeline;
using YAKSys_Hybrid_CPU.Core.Registers;

internal static class BootBlobBridgeRegression
{
    public static void Verify(HybridCpuIseManagedImageLoadResultV1 load, ulong reference, Action<bool, string> check)
    {
        var ecall = new EcallEvent { VtId = 0, BundleSerial = 1,
            EcallCode = (long)HybridCpuExternalServiceEcallContractV1.EcallNumber };
        ICanonicalCpuState Envelope(ulong id = 1, ulong count = 1)
        {
            var state = DispatchProxy.Create<ICanonicalCpuState, RegisterState>();
            state.WritePc(0, 0x10000);
            state.WriteRegister(0, 10, (ulong)HybridCpuHostServiceV1.File);
            state.WriteRegister(0, 11, HybridCpuBootBlobServiceContractV1.GetOperation);
            state.WriteRegister(0, 15, count);
            state.WriteRegister(0, 16, id);
            state.WriteRegister(0, 18, 0x1234); // Caller-owned, unused argument registers are not envelope data.
            state.WriteRegister(0, 19, 0x5678);
            return state;
        }
        var state = Envelope();
        check(load.EcallBridge!.TryHandle(ecall, state, PrivilegeLevel.User), "production ISE bridge handles exact File/BLOB envelope");
        check(unchecked((ulong)state.ReadRegister(0, 10)) == reference && state.ReadRegister(0, 11) == 0 &&
            state.ReadPc(0) == 0x10100, "bridge returns rooted WAD and advances one fixed-width bundle");
        check(state.ReadRegister(0, 18) == 0x1234 && state.ReadRegister(0, 19) == 0x5678, "bridge preserves unused/callee-saved registers");
        foreach (ulong id in new ulong[] { 0, 2, 1025 })
        {
            var missing = Envelope(id);
            check(load.EcallBridge.TryHandle(ecall, missing, PrivilegeLevel.User) && missing.ReadRegister(0, 10) == 0 &&
                missing.ReadRegister(0, 11) != 0, "invalid/missing blob fails closed through status: " + id);
        }
        var malformed = Envelope(count: 2);
        check(load.EcallBridge.TryHandle(ecall, malformed, PrivilegeLevel.User) && malformed.ReadRegister(0, 11) != 0,
            "malformed BLOB argument count cannot be truncated into a valid request");
        var privileged = Envelope();
        check(!load.EcallBridge.TryHandle(ecall, privileged, PrivilegeLevel.Machine) && privileged.ReadPc(0) == 0x10000,
            "non-user envelope retains architectural trap ownership");
        var unknown = Envelope(); unknown.WriteRegister(0, 11, 123);
        check(!load.EcallBridge.TryHandle(ecall, unknown, PrivilegeLevel.User), "unimplemented file operation is not admitted");
    }
}

// Component-only state. Fail if the bridge touches any surface beyond register/PC transport.
public class RegisterState : DispatchProxy
{
    private readonly Dictionary<(byte Vt, int Register), ulong> _registers = [];
    private readonly Dictionary<byte, ulong> _pc = [];
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        byte vt = (byte)args![0]!;
        switch (method!.Name)
        {
            case "ReadRegister": return unchecked((long)_registers.GetValueOrDefault((vt, (int)args[1]!)));
            case "WriteRegister": _registers[(vt, (int)args[1]!)] = (ulong)args[2]!; return null;
            case "ReadPc": return _pc.GetValueOrDefault(vt);
            case "WritePc": _pc[vt] = (ulong)args[1]!; return null;
            default: throw new NotSupportedException(method.Name);
        }
    }
}
