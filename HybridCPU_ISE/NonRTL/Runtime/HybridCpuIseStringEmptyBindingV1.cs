using HybridCPU.ManagedRuntime;
using HybridCPU.Platform.Contracts;

namespace HybridCPU_ISE.NonRTL.Runtime;

/// <summary>Pre-CPU materialization of the compiler's exact runtime-preinitialized String.Empty layout.</summary>
public static class HybridCpuIseStringEmptyBindingV1
{
    public static bool TryInitialize(HybridCpuManagedTypeSystemV1 types, HybridCpuManagedStringRuntimeV1 strings,
        IReadOnlyList<HybridCpuModuleInitializerRegistrationV1> initializers, out string reason)
    {
        ArgumentNullException.ThrowIfNull(types);
        ArgumentNullException.ThrowIfNull(strings);
        ArgumentNullException.ThrowIfNull(initializers);
        reason = string.Empty;
        var candidates = types.Descriptors.Where(static type => type.StableIdentity == "System.String").ToArray();
        if (candidates.Length == 0) return true;
        if (candidates.Length != 1)
        { reason = "String.Empty requires one exact image-owned System.String descriptor."; return false; }
        var type = candidates[0];
        if (type.StaticLayout.Fields.Count == 0) return true;
        if (type.Kind != HybridCpuManagedTypeKindV1.String ||
            type.StringShape is not { LengthOffsetBytes: 16, DataOffsetBytes: 20, CharacterSizeBytes: 2, IsImmutable: true } ||
            type.StaticLayout is not { SizeBytes: 8, AlignmentBytes: 8 } layout || layout.Fields.Count != 1 ||
            layout.Fields[0] is not { Identity: "Empty", StorageKind: HybridCpuManagedStorageKindV1.ObjectReference,
                OffsetBytes: 0, SizeBytes: 8, AlignmentBytes: 8 } ||
            !layout.ObjectReferenceOffsets.SequenceEqual(new[] { 0 }) ||
            types.TypeHandle(type.TypeId) is not ulong handle)
        { reason = "String.Empty requires the exact immutable UTF-16 shape and single pointer-sized static root layout."; return false; }
        if (initializers.Any(row => row.TypeId == type.TypeId))
        { reason = "Runtime-preinitialized String.Empty cannot bypass an image initializer for System.String."; return false; }
        if (types.InitializationState(type.TypeId) != HybridCpuManagedTypeInitializationStateV1.Uninitialized)
        { reason = "String.Empty binding requires a fresh uninitialized runtime type state."; return false; }

        string failure = string.Empty;
        var initialized = new HybridCpuManagedTypeInitializerRuntimeV1(types).EnsureInitialized(type.TypeId, () =>
        {
            var empty = strings.MaterializeLiteral(handle, string.Empty);
            if (!empty.IsSuccess || empty.ObjectReference == 0)
            { failure = "String.Empty materialization failed: " + empty.Reason; return false; }
            var stored = new HybridCpuManagedStaticFieldRuntimeV1(types).StoreReference(handle, 0, empty.ObjectReference);
            if (!stored.IsSuccess) failure = "String.Empty static root binding failed: " + stored.Reason;
            return stored.IsSuccess;
        });
        if (!initialized.IsSuccess) reason = string.IsNullOrEmpty(failure) ? initialized.Reason : failure;
        return initialized.IsSuccess;
    }
}
