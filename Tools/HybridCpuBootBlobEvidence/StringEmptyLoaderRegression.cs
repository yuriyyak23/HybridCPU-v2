using System.Reflection;
using HybridCPU.ManagedRuntime;
using HybridCPU.Platform.Contracts;
using HybridCPU_ISE.CloseToHSL.Core.Runtime.Managed;
using HybridCPU_ISE.NonRTL.Runtime;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Pipeline;
using YAKSys_Hybrid_CPU.Core.Registers;

public static class StringEmptyLoaderRegression
{
    public static void Verify(HybridCpuIseManagedImageLoadResultV1 load, Action<bool, string> check)
    {
        var type = load.TypeSystem!.Descriptors.Single(row => row.StableIdentity == "System.String");
        ulong handle = load.TypeSystem.TypeHandle(type.TypeId)!.Value;
        check(load.TypeSystem.InitializationState(type.TypeId) == HybridCpuManagedTypeInitializationStateV1.Initialized,
            "exact runtime-preinitialized System.String is initialized by the loader");
        var statics = new HybridCpuManagedStaticFieldRuntimeV1(load.TypeSystem);
        var value = statics.LoadReference(handle, 0);
        ulong reference = unchecked((ulong)value.ScalarValue);
        check(value.IsSuccess && reference != 0 && load.Strings!.Length(reference) is { IsSuccess: true, ScalarValue: 0 },
            "String.Empty static slot holds a non-null empty runtime-owned string");
        check(load.Strings!.MaterializeLiteral(handle, "").ObjectReference == reference,
            "empty literal and static Empty retain the same stable runtime reference");
        check(load.PendingInitializers!.All(row => row.TypeId != type.TypeId) &&
            load.PendingInitializers.Where(row => row.TypeId.HasValue).Any(row =>
                load.TypeSystem.InitializationState(row.TypeId!.Value) == HybridCpuManagedTypeInitializationStateV1.Uninitialized),
            "ordinary image initializers remain pending, not silently preinitialized");
        var state = DispatchProxy.Create<ICanonicalCpuState, RegisterState>();
        state.WritePc(0, 0x10000);
        state.WriteRegister(0, 10, (ulong)HybridCpuHostServiceV1.ManagedRuntime);
        state.WriteRegister(0, 11, HybridCpuManagedRuntimeEcallContractV1.EnsureTypeInitializedOperation);
        state.WriteRegister(0, 15, 1);
        state.WriteRegister(0, HybridCpuExternalServiceEcallContractV1.FirstArgumentRegister, handle);
        check(load.EcallBridge!.TryHandle(new EcallEvent { VtId = 0, BundleSerial = 1,
            EcallCode = (long)HybridCpuExternalServiceEcallContractV1.EcallNumber }, state, PrivilegeLevel.User) &&
            state.ReadRegister(0, 11) == 0 && state.ReadRegister(0, 12) == 0,
            "EnsureTypeInitialized ECALL accepts the completed exact String type");
        // Keep other literals alive explicitly, excluding Empty and the string-cache root source.
        var otherRoots = load.Strings.Literals.Values.Where(item => item != reference)
            .Select((item, index) => new HybridCpuManagedGcRootV1("other-literal:" + index,
                HybridCpuManagedGcRootSourceV1.Handle, item)).Concat(load.ProcessRoots ?? []).ToArray();
        for (int pass = 0; pass < 2; pass++)
        {
            var gc = load.Gc!.Collect(new([], [], otherRoots), HybridCpuManagedNonMovingGcOptionsV1.Qualification);
            check(gc.IsSuccess && gc.Roots.Contains(reference) && gc.ReachableObjects.Contains(reference),
                "static pointer map alone retains String.Empty, pass " + pass);
        }
        int allocations = load.Heap!.ActiveAllocations().Count;
        check(!HybridCpuIseStringEmptyBindingV1.TryInitialize(load.TypeSystem, load.Strings,
            [new("conflicting-image", "string-cctor", 0, type.TypeId)], out string conflict) &&
            conflict.Contains("cannot bypass an image initializer", StringComparison.Ordinal) &&
            load.Heap.ActiveAllocations().Count == allocations,
            "a conflicting image String initializer is rejected, never bypassed");
        check(!HybridCpuIseStringEmptyBindingV1.TryInitialize(load.TypeSystem, load.Strings, [], out _) &&
            load.Heap.ActiveAllocations().Count == allocations && statics.LoadReference(handle, 0).ScalarValue == value.ScalarValue,
            "repeat binding fails closed without replacing the static root");
    }
}
