using System.Collections.Generic;

namespace HybridCPU.Compiler.Core.IR
{
    /// <summary>
    /// Physical slot assignment chosen for one legal candidate group.
    /// </summary>
    public sealed record IrMaterializedSlotAssignment(
        IrSlotAssignmentAnalysis Analysis,
        IReadOnlyList<int> InstructionSlots,
        IrBundlePlacementQuality Quality,
        IrBundlePlacementSearchSummary SearchSummary,
        IrBundleTransitionQuality TransitionQuality)
    {
        /// <summary>
        /// Gets a value indicating whether the candidate group has a valid physical slot assignment.
        /// </summary>
        public bool HasLegalAssignment => Analysis.HasLegalAssignment;
    }
}
