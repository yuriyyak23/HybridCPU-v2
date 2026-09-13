using HybridCPU.Compiler.Cil;
using HybridCPU.Compiler.Core.Target.Managed;
using HybridCPU.Compiler.Core.Target.Object;
using HybridCPU.ManagedRuntime;
using HybridCPU.Platform.Contracts;
using HybridCPU.RuntimeKernel;

internal static class IsInstanceSmoke
{
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }

    public static void Run()
    {
        HybridCpuRuntimeHelperV1? helper = HybridCpuManagedAbiFamilyV1.Default.ResolveRuntimeHelper(
            HybridCpuManagedIsInstanceEmitterV1.Symbol);
        var objectFile = HybridCpuManagedIsInstanceEmitterV1.EmitObject();
        Check(HybridCpuManagedAbiFamilyV1.SchemaMinor == 61 &&
              helper is { Support: HybridCpuManagedAbiSupportV1.Supported,
                  GcTransition: HybridCpuRuntimeHelperGcTransitionV1.None, MayThrow: false } &&
              objectFile.Status == HybridCpuObjectStatusV1.Success &&
              objectFile.Symbols.Any(s => s.Name == HybridCpuManagedIsInstanceEmitterV1.Symbol && s.IsDefinition),
            "isinst must have an exact nonallocating, nonthrowing CPU-callable runtime helper");

        var builder = new HybridCpuManagedTypeSystemBuilderV1();
        var built = builder.Build([
            new("Root", HybridCpuManagedTypeKindV1.Class, null, [], []),
            new("Base", HybridCpuManagedTypeKindV1.Class, "Root", [], []),
            new("Derived", HybridCpuManagedTypeKindV1.Class, "Base", [], []),
            new("Other", HybridCpuManagedTypeKindV1.Class, "Root", [], [])
        ]);
        Check(built.IsSuccess, built.Reason);
        var types = built.TypeSystem!;
        ulong Handle(string name) => types.TypeHandle(types.Descriptors.Single(t => t.StableIdentity == name).TypeId)!.Value;
        byte[] pe = File.ReadAllBytes(typeof(IsInstanceFixture).Assembly.Location);
        var fixtureBinding = new RestrictedCilTypeTestBindingV1(typeof(IsInstanceFixtureBase).MetadataToken,
            types.Descriptors.Single(t => t.StableIdentity == "Base"), Handle("Base"));
        var importer = new RestrictedCilImporterV1(mode: RestrictedCilImportModeV1.ScalarControlFlowV2,
            typeTestBindings: [fixtureBinding]);
        var imported = importer.ImportImage(pe,
            new(typeof(IsInstanceFixture).FullName!, nameof(IsInstanceFixture.AsBase)));
        Check(imported.Status == RestrictedCilImportStatusV1.Success && imported.Program!.Instructions.Any(i =>
            i.Annotation.BranchTargetSymbolName == HybridCpuManagedIsInstanceEmitterV1.Symbol),
            string.Join(";", imported.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var kernel = new DeterministicRuntimeKernelV1();
        Check(kernel.Boot(new(HybridCpuPlatformContractV1.ContractDigest, new string('b', 64),
            0x100000, 4096, 0x100000, 0x200000, 4096, 0, 1000)).IsSuccess, "boot");
        var heap = new HybridCpuManagedHeapAllocatorV1(kernel, types,
            HybridCpuManagedHeapOptionsV1.Create(0x41000000, 16384, 4096, -3));
        Check(heap.Initialize().IsSuccess, "heap");
        var runtime = new HybridCpuManagedTypeTestRuntimeV1(types, heap);
        ulong derived = heap.Allocate(Handle("Derived")).ObjectReference;
        ulong other = heap.Allocate(Handle("Other")).ObjectReference;

        Check(runtime.IsInstance(0, Handle("Base")) is { IsSuccess: true, ObjectReference: 0 }, "null isinst");
        Check(runtime.IsInstance(derived, Handle("Base")) is { IsSuccess: true, ObjectReference: var baseResult } &&
              baseResult == derived, "derived is base");
        Check(runtime.IsInstance(derived, Handle("Root")) is { IsSuccess: true, ObjectReference: var rootResult } &&
              rootResult == derived, "derived is root");
        Check(runtime.IsInstance(other, Handle("Base")) is { IsSuccess: true, ObjectReference: 0 }, "other is not base");
        Check(runtime.IsInstance(derived, ulong.MaxValue).Status == HybridCpuManagedShapeStatusV1.InvalidType,
            "unknown target handle fails closed");
        Check(runtime.IsInstance(0xdeadbeef, Handle("Base")).Status == HybridCpuManagedShapeStatusV1.InvalidType,
            "unknown receiver fails closed");
        Console.WriteLine("PASS isinst lowering, exact type-handle resolution and fail-closed object validation");
    }
}

public class IsInstanceFixtureBase { }
public sealed class IsInstanceFixtureDerived : IsInstanceFixtureBase { }
public sealed class IsInstanceFixtureOther { }
public static class IsInstanceFixture
{
    public static IsInstanceFixtureBase? AsBase(object? value) => value as IsInstanceFixtureBase;
}
