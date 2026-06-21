namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane07SystemControl.AcceleratorControl.Topology;

public sealed partial class AccelQueryTopologyInstruction
{
    public const bool RequiresBackendCapabilityAuthority = true;
    public const bool RequiresGuestVisibleCapabilityPolicy = true;
    public const bool RequiresFutureVirtualizationBoundaryPolicy = true;
    public const bool RequiresImmediateVmxProjection = false;
    public const bool VmxCapabilityEvidenceIsInsufficient = true;
    public const bool VmxMigrationCheckpointEvidenceIsInsufficient = true;
    public const bool NoCapabilityPublicationBeforeAuthority = true;
}
