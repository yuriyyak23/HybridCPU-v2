namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane07SystemControl.AcceleratorControl.Lifecycle;

public sealed partial class AccelOpenInstruction
{
    public const bool RequiresRetireOwnedSideEffectPublication = true;
    public const bool RequiresReplayStableLifecycleModel = true;
    public const bool RequiresRollbackConformance = true;
    public const bool RequiresMigrationCheckpointPolicy = true;
    public const bool RequiresFutureVirtualizationBoundaryPolicy = true;
    public const bool RequiresImmediateVmxProjection = false;
    public const bool VmxBackendAuthorityEvidenceIsInsufficient = true;
    public const bool VmxMigrationCheckpointEvidenceIsInsufficient = true;
    public const bool NoLifecycleStatePublicationBeforeRetire = true;
    public const bool NoBackendAdmissionBeforeAuthority = true;
}
