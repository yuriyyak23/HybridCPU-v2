using System.Collections.Generic;

namespace HybridCPU.Compiler.Core.IR
{
    /// <summary>
    /// Summarizes compiler-visible slot feasibility for a candidate cycle group.
    /// </summary>
    public sealed record IrSlotAssignmentAnalysis(
        int CandidateInstructionCount,
        IrIssueSlotMask CombinedLegalSlots,
        int DistinctLegalSlotCount,
        bool HasLegalAssignment,
        IReadOnlyList<IrIssueSlotMask> InstructionLegalSlots);
}
