namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane07SystemControl.CacheMaintenance;

public sealed partial class IcacheInvalInstruction
{
    public const bool RequiresReplayStableInvalidationModel = true;
    public const bool RequiresRollbackConformance = true;
    public const bool RequiresInvalidationOrderingModel = true;
    public const bool NoSpeculativeMaintenancePublication = true;
    public const bool NoRetireSideEffectBeforeAuthority = true;
}
