namespace YAKSys_Hybrid_CPU.Core;

public sealed partial class Lane6HostOwnedEvidenceBoundaryRemovalContract
{
    public const string RemovedVmxQueueVirtualizerPath =
        "NonRTL/Core/Execution/DmaStreamCompute/VmxLane6QueueVirtualizer.cs";

    public const string Lane6StatePath =
        "Core/Runtime/Lanes/Lane6/Lane6StateBlock.cs";

    public const string NeutralQueueRuntimePath =
        "Core/Runtime/Lanes/Lane6/Lane6QueueRuntime.cs";

    public const string NeutralHostEvidenceStorePath =
        "Core/Runtime/Lanes/Lane6/HostOwnedEvidence/Lane6HostOwnedEvidenceStore.cs";

    public const string MigrationDescriptorPath =
        "Core/Runtime/Migration/Format/MigrationDescriptor.cs";

    public const string DomainCheckpointImagePath =
        "Core/Runtime/Migration/Checkpoint/DomainCheckpointImage.cs";

    public static bool RemovedVmxShapedHostTokenCarrier => true;

    public static bool RequiresHostEvidenceRebuildAfterRestore => true;

    public static bool RejectsEpochWraparound => true;

    public static string[] ForbiddenNeutralEvidenceStoreMarkers { get; } =
    {
        "Vmcs",
        "Vmx",
        "Snapshot",
        "Serialize",
        "RestoreHostHandle",
        "ImportHost",
    };

    public static string[] ForbiddenLane6StateOwnerMarkers { get; } =
    {
        "VmxLane6QueueVirtualizer",
        "QueueVirtualizer",
        "_hostTokens",
    };

    public static string[] RequiredRebuildMarkers { get; } =
    {
        "PrepareForRestore",
        "NativeTokenEvidence",
        "RestoreRequiresRecompute",
        "_nativeTokenBindings.Clear()",
        "RebuildAfterRestore",
    };

    public static string[] RequiredFailClosedEpochMarkers { get; } =
    {
        "DmaFaultKind.EpochExhausted",
        "TryAdvanceEpoch",
        "epoch == ulong.MaxValue",
        "refuses wraparound",
    };
}
