using System.Collections.Generic;
using YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.MatrixTile;
using YAKSys_Hybrid_CPU.Core.Execution.DmaStreamCompute;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators;
using YAKSys_Hybrid_CPU.Memory;

namespace YAKSys_Hybrid_CPU.Core;

/// <summary>
/// Namespace-only containment for references to distinct extension owners.
/// It deliberately provides no common execution, commit or fallback ABI.
/// </summary>
internal sealed class ExtensionState
{
    internal MatrixTileState MatrixTile = new();
    internal MatrixTileArchitecturalTileRegisterFile? MatrixTileRegisterFile;
    internal StreamRegisterFile? MatrixTileStreamRegisterFile;
    internal Dictionary<ulong, MatrixTileReplayRollbackJournal>? MatrixTileReplayJournals;
    internal DmaStreamComputeTokenStore? DmaStreamComputeTokenStore;
    internal ExternalAcceleratorRuntime? ExternalAcceleratorRuntime;
}
