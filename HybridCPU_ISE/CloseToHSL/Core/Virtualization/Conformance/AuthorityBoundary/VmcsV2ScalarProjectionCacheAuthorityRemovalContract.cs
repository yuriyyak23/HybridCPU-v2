using System;

namespace YAKSys_Hybrid_CPU.Core;

public static class VmcsV2ScalarProjectionCacheAuthorityRemovalContract
{
    private static readonly string[] ForbiddenDescriptorCacheMarkerTable =
    {
        "_scalarValues",
        "_scalarWritten",
        "WriteKnownScalar(",
        "HasScalarFieldValue(",
        "TryGetScalarFieldValue(",
        "Array.Clear(_scalarValues)",
        "Array.Clear(_scalarWritten)",
        "value = _scalarWritten",
    };

    private static readonly string[] ForbiddenNewRuntimeOwnerMarkerTable =
    {
        "VmcsV2ScalarProjectionCache",
        "VmcsV2ScalarProjectionStore",
        "VmcsScalarProjectionCache",
        "VmcsScalarProjectionStore",
        "VmcsScalarReadStore",
        "VmcsV2ReadCacheRuntime",
        "VmcsProjectionRuntimeManager",
        "VmcsV2RuntimeManager",
        "VmcsFieldStore",
    };

    public const string VmcsDescriptorPath =
        "NonRTL/Core/System/Vmcs/V2/VmcsV2Descriptor.cs";

    public const string ProjectionGatePath =
        "Core/VMX/Compatibility/Generated/VmcsProjection/VmcsV2DescriptorProjection.cs";

    public const string FieldProjectionSchemaPath =
        "Core/VMX/Compatibility/Generated/VmcsProjection/VmcsFieldProjectionSchema.cs";

    public static bool RemovedWithoutReplacement => true;

    public static bool RejectsDescriptorOwnedScalarProjectionCache => true;

    public static bool KeepsTryReadScalarFieldAsDeniedCompatibilityAbi => true;

    public static bool RequiresGeneratedReadOnlyProjectionOverNeutralOwners => true;

    public static bool RejectsScalarProjectionStoreReplacementOwner => true;

    public static ReadOnlySpan<string> ForbiddenDescriptorCacheMarkers =>
        ForbiddenDescriptorCacheMarkerTable;

    public static ReadOnlySpan<string> ForbiddenNewRuntimeOwnerMarkers =>
        ForbiddenNewRuntimeOwnerMarkerTable;
}
