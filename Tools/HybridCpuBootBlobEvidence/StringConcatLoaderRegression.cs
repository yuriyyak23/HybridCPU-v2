using System.Reflection;
using HybridCPU.ManagedRuntime;
using HybridCPU.Platform.Contracts;
using HybridCPU_ISE.CloseToHSL.Core.Runtime.Managed;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Pipeline;
using YAKSys_Hybrid_CPU.Core.Registers;

internal static class StringConcatLoaderRegression
{
    public static void Verify(HybridCpuIseManagedImageLoadResultV1 load, ulong nonString, Action<bool, string> check)
    {
        var type = load.TypeSystem!.Descriptors.Single(t => t.StableIdentity == "System.String");
        ulong handle = load.TypeSystem.TypeHandle(type.TypeId)!.Value;
        var first = load.Strings!.MaterializeLiteral(handle, "Ω");
        var second = load.Strings.MaterializeLiteral(handle, "x");
        check(first.IsSuccess && second.IsSuccess, "production loader materializes exact UTF-16 test operands");
        var concat = load.Strings.Concat3(0, first.ObjectReference, second.ObjectReference);
        check(concat.IsSuccess, "production-loaded String.Concat3 accepts null and exact strings: " + concat.Reason);
        check(load.Strings.Length(concat.ObjectReference).ScalarValue == 2 &&
            load.Strings.Character(concat.ObjectReference, 0).ScalarValue == 'Ω' &&
            load.Strings.Character(concat.ObjectReference, 1).ScalarValue == 'x', "concat result has exact UTF-16 payload");
        var empty = load.Strings.Concat2(0, 0);
        check(empty.IsSuccess && load.Strings.Length(empty.ObjectReference).ScalarValue == 0,
            "all-null concat produces an exact empty runtime string");
        check(load.Strings.Concat2(nonString, first.ObjectReference).Status == HybridCpuManagedShapeStatusV1.InvalidType,
            "non-string heap receiver remains rejected");
        var state = DispatchProxy.Create<ICanonicalCpuState, RegisterState>();
        state.WritePc(0, 0x10000);
        state.WriteRegister(0, 10, (ulong)HybridCpuHostServiceV1.ManagedRuntime);
        state.WriteRegister(0, 11, HybridCpuManagedRuntimeEcallContractV1.StringConcat3Operation);
        state.WriteRegister(0, 15, 3);
        state.WriteRegister(0, HybridCpuExternalServiceEcallContractV1.SecondArgumentRegister, first.ObjectReference);
        state.WriteRegister(0, HybridCpuExternalServiceEcallContractV1.ThirdArgumentRegister, second.ObjectReference);
        check(load.EcallBridge!.TryHandle(new EcallEvent { VtId = 0, BundleSerial = 1,
            EcallCode = (long)HybridCpuExternalServiceEcallContractV1.EcallNumber }, state, PrivilegeLevel.User) &&
            state.ReadRegister(0, 11) == 0 && state.ReadRegister(0, 12) == 0 && state.ReadPc(0) == 0x10100 &&
            load.Strings.Length(unchecked((ulong)state.ReadRegister(0, 10))).ScalarValue == 2,
            "production concat3 ECALL bridge returns exact runtime string");
    }
}
