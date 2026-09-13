using YAKSys_Hybrid_CPU.Core.Registers;

namespace YAKSys_Hybrid_CPU.Core;

/// <summary>
/// Live PRF/rename backend containment. The referenced register structures
/// retain their allocation, restore and retire publication protocols.
/// </summary>
internal sealed class BackendState
{
    internal PhysicalRegisterFile PhysicalRegisters = null!;
    internal RenameMap RenameMap = null!;
    internal CommitMap CommitMap = null!;
    internal FreeList FreeList = null!;
}
