namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane07SystemControl.CacheMaintenance;

public sealed partial class DcacheCleanInstruction
{
    public const bool RequiresFutureVirtualizationBoundaryPolicy = true;
    public const bool RequiresImmediateVmxProjection = false;
    public const bool VmxCacheEvidenceIsInsufficient = true;
    public const bool VmxMigrationCheckpointEvidenceIsInsufficient = true;
}
