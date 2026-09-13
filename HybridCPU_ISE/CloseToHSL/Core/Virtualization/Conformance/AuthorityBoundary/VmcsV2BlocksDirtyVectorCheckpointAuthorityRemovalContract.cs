using System;

namespace YAKSys_Hybrid_CPU.Core;

public static class VmcsV2BlocksDirtyVectorCheckpointAuthorityRemovalContract
{
    private static readonly string[] ForbiddenVmcsBlocksHelperMarkerTable =
    {
        "public ulong ConfigurePolicy(",
        "public bool CanUseSaveMask(",
        "public ulong BindStreamDescriptorTable(",
        "public VectorStreamSnapshot CaptureSnapshot(",
        "public bool TryRestoreSnapshot(",
        "public void RestoreCheckpointSnapshot(",
        "public ulong MarkVectorDirty(",
        "public ulong AdvanceStreamReplayEpoch(",
        "public ulong AdvanceStreamQueueEpoch(",
        "public ulong AdvanceStreamCompletionEpoch(",
        "public VmxVectorExceptionInfo RecordVectorException(",
        "public VmxStreamDescriptorFaultInfo RecordStreamDescriptorFault(",
        "public bool IsDescriptorAddressInRange(",
        "private ulong AdvancePolicyEpoch(",
        "private ulong AdvanceArchitecturalEpoch(",
        "private ulong AdvanceStreamEpoch(",
        "private ulong NextSequence(",
        "private readonly HashSet<ulong> _dirtyPageIndices",
        "public VmcsV2ValidationResult Configure(VmxDirtyLogConfiguration",
        "public bool TryMarkDirtyRange(",
        "public bool TrySnapshot(",
        "public bool TryClear(",
        "public void RestoreSnapshot(VmxDirtyLogSnapshot",
        "private void RecordAcceptedWrite(",
        "private void RecordOverflow(",
        "private void AdvanceGeneration(",
        "private static bool IsPowerOfTwo(",
    };

    private static readonly string[] ForbiddenDescriptorHelperMarkerTable =
    {
        "DirtyLog.Configure(",
        "TryCreateGuestCheckpoint",
        "SnapshotMigratableScalarFields",
        "RestoreGuestStateForMigration",
        "RestoreScalarFieldForMigration",
    };

    private static readonly string[] ForbiddenNewRuntimeOwnerMarkerTable =
    {
        "VmcsV2DirtyLogRuntime",
        "VmcsDirtyTrackingService",
        "VmcsDirtyLogService",
        "VmcsVectorStreamRuntime",
        "VmcsVectorStreamService",
        "VmcsCheckpointBlockService",
        "VmcsProjectionRuntimeManager",
        "VmcsV2RuntimeManager",
    };

    public const string VmcsBlocksPath =
        "Core/VMX/Compatibility/Generated/VmcsProjection/VmcsV2Blocks.cs";

    public const string VmcsDescriptorPath =
        "NonRTL/Core/System/Vmcs/V2/VmcsV2Descriptor.cs";

    public const string NeutralVectorStreamRuntimePath =
        "Core/Runtime/Domains/Admission/VectorStream/VectorStreamDomainRuntime.cs";

    public const string NeutralDomainCheckpointImagePath =
        "Core/Runtime/Migration/Checkpoint/DomainCheckpointImage.cs";

    public const string VmxCompatibilityAliasesPath =
        "Core/VMX/Compatibility/Adapters/IO/IommuVmxCompatibilityAliases.cs";

    public static bool RemovedWithoutReplacement => true;

    public static bool RejectsVmcsBlocksDirtyLogMutationAuthority => true;

    public static bool RejectsVmcsBlocksVectorSaveRestoreAuthority => true;

    public static bool RejectsVmcsBlocksCheckpointRestoreAuthority => true;

    public static bool KeepsVmcsBlocksVocabularyProjectionOnly => true;

    public static bool GenericVectorStreamRuntimeRemainsNeutralOwner => true;

    public static bool GenericDomainCheckpointRemainsNeutralOwner => true;

    public static bool CompatibilityDirtyAccountingRemainsFailClosed => true;

    public static ReadOnlySpan<string> ForbiddenVmcsBlocksHelperMarkers =>
        ForbiddenVmcsBlocksHelperMarkerTable;

    public static ReadOnlySpan<string> ForbiddenDescriptorHelperMarkers =>
        ForbiddenDescriptorHelperMarkerTable;

    public static ReadOnlySpan<string> ForbiddenNewRuntimeOwnerMarkers =>
        ForbiddenNewRuntimeOwnerMarkerTable;
}
