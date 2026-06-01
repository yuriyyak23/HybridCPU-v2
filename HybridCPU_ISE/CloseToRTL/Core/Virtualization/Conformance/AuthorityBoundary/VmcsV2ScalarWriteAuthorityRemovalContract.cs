using System;

namespace YAKSys_Hybrid_CPU.Core;

public static class VmcsV2ScalarWriteAuthorityRemovalContract
{
    private static readonly string[] ForbiddenDescriptorMutationMarkerTable =
    {
        "public bool TryWriteScalarField(",
        "ValidateScalarWrite(",
        "descriptor.AccessPolicy.CanWriteViaVmWrite",
        "VMWRITE is not permitted for this VMCSv2 field.",
        "Boolean VMCSv2 fields accept only 0 or 1.",
        "Reserved VMCSv2 bits must be zero.",
    };

    private static readonly string[] ForbiddenNewRuntimeOwnerMarkerTable =
    {
        "VmcsV2ScalarWriteRuntime",
        "VmcsV2ScalarWriteService",
        "VmcsScalarFieldStore",
        "VmcsV2FieldStore",
        "VmcsFieldStore",
        "VmcsProjectionRuntimeManager",
        "VmcsV2RuntimeManager",
        "VmcsScalarMutationManager",
    };

    public const string VmcsDescriptorPath =
        "NonRTL/Core/System/Vmcs/V2/VmcsV2Descriptor.cs";

    public const string ProjectionGatePath =
        "Core/VMX/Compatibility/Generated/VmcsProjection/VmcsV2DescriptorProjection.cs";

    public const string FieldProjectionSchemaPath =
        "Core/VMX/Compatibility/Generated/VmcsProjection/VmcsFieldProjectionSchema.cs";

    public static bool RemovedWithoutReplacement => true;

    public static bool RejectsVmcsV2PublicScalarWriteAuthority => true;

    public static bool KeepsVmcsV2DescriptorReadProjectionOnly => true;

    public static bool RejectsVmcsFieldStoreReplacementOwner => true;

    public static bool KeepsFrozenFieldSchemaVocabularyOnly => true;

    public static ReadOnlySpan<string> ForbiddenDescriptorMutationMarkers =>
        ForbiddenDescriptorMutationMarkerTable;

    public static ReadOnlySpan<string> ForbiddenNewRuntimeOwnerMarkers =>
        ForbiddenNewRuntimeOwnerMarkerTable;
}
