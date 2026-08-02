using System;

namespace YAKSys_Hybrid_CPU.Core;

public static class VmxCheckpointVmcsScalarAuthorityRemovalContract
{
    private static readonly string[] RemovedPathTable =
    {
        RemovedCheckpointImagePath,
        RemovedVmcsCheckpointDtoPath,
    };

    private static readonly string[] ForbiddenProductionOwnerMarkerTable =
    {
        "VmxCheckpointImage",
        "VmxCheckpointWriter",
        "VmxCheckpointReader",
        "VmxCheckpointBlockKind",
        "VmxRuntimeCapabilitySet",
        "VmxMigrationSecurityPolicy",
        "VmxCapabilityNegotiator",
        "VmcsV2Checkpoint",
        "VmcsV2ScalarFieldSnapshot",
        "TryCreateGuestCheckpoint",
        "SnapshotMigratableScalarFields",
        "RestoreGuestStateForMigration",
        "RestoreScalarFieldForMigration",
        "ScalarVmcsFields",
        "ApplyScalarFields",
    };

    private static readonly string[] ForbiddenNewRuntimeOwnerMarkerTable =
    {
        "VmcsCheckpointService",
        "VmcsV2CheckpointService",
        "VmcsScalarCheckpointService",
        "VmcsProjectionCheckpointManager",
        "VmcsV2Runtime",
        "VmcsProjectionRuntimeManager",
        "VmcsV2RuntimeManager",
        "VmcsV2Descriptor as source of truth",
    };

    public const string RemovedCheckpointImagePath =
        "NonRTL/Core/System/Migration/VmxCheckpointImage.cs";

    public const string RemovedVmcsCheckpointDtoPath =
        "NonRTL/Core/System/Vmcs/V2/VmcsV2Checkpoint.cs";

    public const string VmcsDescriptorPath =
        "NonRTL/Core/System/Vmcs/V2/VmcsV2Descriptor.cs";

    public const string DomainCheckpointImagePath =
        "Core/Runtime/Migration/Checkpoint/DomainCheckpointImage.cs";

    public const string RestoreValidationServicePath =
        "Core/Runtime/Migration/Restore/RestoreValidationService.cs";

    public static bool RemovedWithoutReplacement => true;

    public static bool RejectsVmcsScalarCheckpointImageAuthority => true;

    public static bool RejectsVmcsDescriptorScalarRestoreAuthority => true;

    public static bool KeepsVmcsVocabularyProjectionOnly => true;

    public static bool GenericDomainCheckpointRemainsNeutralOwner => true;

    public static ReadOnlySpan<string> RemovedPaths => RemovedPathTable;

    public static ReadOnlySpan<string> ForbiddenProductionOwnerMarkers =>
        ForbiddenProductionOwnerMarkerTable;

    public static ReadOnlySpan<string> ForbiddenNewRuntimeOwnerMarkers =>
        ForbiddenNewRuntimeOwnerMarkerTable;
}
