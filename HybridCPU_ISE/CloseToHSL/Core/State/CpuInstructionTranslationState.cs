namespace YAKSys_Hybrid_CPU.Core;

internal sealed class CpuInstructionTranslationState
{
    internal Memory.CpuInstructionTranslationOwner Owner = null!;
    internal Memory.CpuInstructionTranslationFault? PendingFetchFault;
}
