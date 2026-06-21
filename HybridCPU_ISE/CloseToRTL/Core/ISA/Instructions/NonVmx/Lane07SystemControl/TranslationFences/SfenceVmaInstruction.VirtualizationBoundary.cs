namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane07SystemControl.TranslationFences;

public sealed partial class SfenceVmaInstruction
{
    public const bool RequiresFutureVirtualizationBoundaryPolicy = true;
    public const bool RequiresImmediateVmxProjection = false;
    public const bool VmxTranslationEvidenceIsInsufficient = true;
    public const bool VmxMigrationCheckpointEvidenceIsInsufficient = true;
}
