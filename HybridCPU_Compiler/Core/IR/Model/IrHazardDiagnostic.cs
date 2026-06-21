using System;
using System.Collections.Generic;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU.Compiler.Core.IR
{
    /// <summary>
    /// Describes one machine-checkable legality failure discovered during hazard analysis.
    /// </summary>
    public sealed record IrHazardDiagnostic(
        IrHazardCategory Category,
        IrHazardReason Reason,
        int? LeftInstructionIndex,
        int? RightInstructionIndex,
        string Message,
        IrDataHazardKind DataHazard = IrDataHazardKind.None,
        byte RequiredLatencyCycles = 0,
        IrIssueSlotMask RelevantSlots = IrIssueSlotMask.None,
        IrStructuralResource RelevantResources = IrStructuralResource.None,
        IrInstructionDependencyKind DependencyKind = IrInstructionDependencyKind.None,
        IrMemoryDependencyPrecision MemoryPrecision = IrMemoryDependencyPrecision.None,
        HazardEffectKind DominantEffectKind = HazardEffectKind.RegisterData);

    /// <summary>
    /// Represents the legality result for a candidate cycle or bundle group.
    /// </summary>
    public sealed record IrBundleLegalityResult(IReadOnlyList<IrHazardDiagnostic> Hazards)
    {
        /// <summary>
        /// Gets a value indicating whether the candidate group is legal under the current hazard model.
        /// </summary>
        public bool IsLegal => Hazards.Count == 0;

        /// <summary>
        /// Returns a legal result without hazards.
        /// </summary>
        public static IrBundleLegalityResult Legal { get; } = new(Array.Empty<IrHazardDiagnostic>());
    }
}
