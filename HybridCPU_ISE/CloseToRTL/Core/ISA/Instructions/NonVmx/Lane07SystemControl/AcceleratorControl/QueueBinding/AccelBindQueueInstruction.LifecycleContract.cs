namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane07SystemControl.AcceleratorControl.QueueBinding;

public sealed partial class AccelBindQueueInstruction
{
    public const bool RequiresRetireOwnedSideEffectPublication = true;
    public const bool RequiresReplayStableQueueBindingModel = true;
    public const bool RequiresRollbackConformance = true;
    public const bool RequiresQueueBindUnbindOrderingModel = true;
    public const bool RequiresMigrationCheckpointPolicy = true;
    public const bool RequiresFutureVirtualizationBoundaryPolicy = true;
    public const bool RequiresImmediateVmxProjection = false;
    public const bool VmxBackendAuthorityEvidenceIsInsufficient = true;
    public const bool VmxMigrationCheckpointEvidenceIsInsufficient = true;
    public const bool NoQueueBindingPublicationBeforeRetire = true;
    public const bool NoQueueBindingBeforeTokenAuthority = true;
}
