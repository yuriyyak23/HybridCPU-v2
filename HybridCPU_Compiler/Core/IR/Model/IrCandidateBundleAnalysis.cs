using System.Collections.Generic;

namespace HybridCPU.Compiler.Core.IR
{
    /// <summary>
    /// Provides a compiler-facing legality analysis surface for one candidate cycle group.
    /// </summary>
    public sealed record IrCandidateBundleAnalysis(
        IReadOnlyList<IrInstruction> Instructions,
        IrBundleLegalityResult Legality,
        IrSlotAssignmentAnalysis SlotAnalysis,
        IrStructuralResourceAnalysis StructuralAnalysis,
        IrClassCapacityResult? ClassCapacityResult = null)
    {
        /// <summary>
        /// Gets a value indicating whether the candidate group is legal.
        /// </summary>
        public bool IsLegal => Legality.IsLegal;
    }
}
