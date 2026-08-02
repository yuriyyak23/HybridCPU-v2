using System;

namespace YAKSys_Hybrid_CPU.Core;

public static class VmcsV2VectorStreamProjectionAuthorityRemovalContract
{
    private static readonly string[] RemovedPathTable =
    {
        RemovedStateManagerPath,
        RemovedDescriptorValidatorPath,
    };

    private static readonly string[] ForbiddenProductionOwnerMarkerTable =
    {
        "VmxVectorStreamStateManager",
        "VmxStreamDescriptorValidator",
        "VmxStreamDescriptorValidationRequest",
        "VmxStreamDescriptorValidationResult",
        "VmxStreamDescriptorValidationEvidence",
        "VmxGuestStreamDescriptorParser",
        "VmxGuestStreamDescriptor",
        "SaveGuestState(VmcsV2Descriptor",
        "RestoreGuestState(VmcsV2Descriptor",
        "MarkDirty(VmcsV2Descriptor",
    };

    private static readonly string[] ForbiddenNewRuntimeOwnerMarkerTable =
    {
        "VmcsV2Runtime",
        "VmcsProjectionRuntimeManager",
        "VmcsV2RuntimeManager",
        "ActiveV2Descriptor",
        "TryMarkVectorStreamDirty",
    };

    public const string RemovedStateManagerPath =
        "NonRTL/Core/Execution/VectorStream/VmxVectorStreamStateManager.cs";

    public const string RemovedDescriptorValidatorPath =
        "NonRTL/Core/Execution/VectorStream/VmxStreamDescriptorValidator.cs";

    public const string NeutralRuntimePath =
        "Core/Runtime/Domains/Admission/VectorStream/VectorStreamDomainRuntime.cs";

    public const string ProjectionGatePath =
        "Core/VMX/Compatibility/Generated/VmcsProjection/VmcsV2DescriptorProjection.cs";

    public static bool RemovedWithoutReplacement => true;

    public static bool RejectsVmcsDescriptorOwnedVectorSaveRestore => true;

    public static bool RejectsVmcsDescriptorOwnedStreamValidationEvidence => true;

    public static bool KeepsFrozenVectorStreamVocabularyProjectionOnly => true;

    public static bool GenericVectorStreamRuntimeRemainsNeutralOwner => true;

    public static ReadOnlySpan<string> RemovedPaths => RemovedPathTable;

    public static ReadOnlySpan<string> ForbiddenProductionOwnerMarkers =>
        ForbiddenProductionOwnerMarkerTable;

    public static ReadOnlySpan<string> ForbiddenNewRuntimeOwnerMarkers =>
        ForbiddenNewRuntimeOwnerMarkerTable;
}
