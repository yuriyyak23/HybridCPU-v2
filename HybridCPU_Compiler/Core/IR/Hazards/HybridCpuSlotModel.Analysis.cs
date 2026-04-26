using System;
using System.Collections.Generic;
using YAKSys_Hybrid_CPU.Arch;

namespace HybridCPU.Compiler.Core.IR
{
    public static partial class HybridCpuSlotModel
    {
        /// <summary>
        /// Total number of issue slots currently modeled by the compiler layer.
        /// </summary>
        public const int SlotCount = 8;

        /// <summary>
        /// Returns the legal slot mask for an instruction resource class.
        /// </summary>
        public static IrIssueSlotMask GetLegalSlots(IrResourceClass resourceClass, OpcodeInfo? opcodeInfo = null)
        {
            return resourceClass switch
            {
                IrResourceClass.VectorAlu => IrIssueSlotMask.Vector,
                IrResourceClass.LoadStore => IrIssueSlotMask.Memory,
                IrResourceClass.ControlFlow => IrIssueSlotMask.Control,
                IrResourceClass.System => IrIssueSlotMask.System,
                IrResourceClass.DmaStream => IrIssueSlotMask.Memory,
                _ => IrIssueSlotMask.Scalar
            };
        }

        /// <summary>
        /// Checks whether a candidate group has at least one valid slot assignment.
        /// </summary>
        public static bool HasLegalAssignment(IReadOnlyList<IrIssueSlotMask> legalSlots)
        {
            return AnalyzeAssignment(legalSlots).HasLegalAssignment;
        }

        /// <summary>
        /// Analyzes slot feasibility for a candidate group without assigning physical slots.
        /// </summary>
        public static IrSlotAssignmentAnalysis AnalyzeAssignment(IReadOnlyList<IrIssueSlotMask> legalSlots)
        {
            ArgumentNullException.ThrowIfNull(legalSlots);

            IrIssueSlotMask combinedMask = IrIssueSlotMask.None;
            foreach (IrIssueSlotMask slotMask in legalSlots)
            {
                combinedMask |= slotMask;
            }

            if (legalSlots.Count == 0)
            {
                return new IrSlotAssignmentAnalysis(0, IrIssueSlotMask.None, 0, true, Array.Empty<IrIssueSlotMask>());
            }

            if (legalSlots.Count > SlotCount)
            {
                return new IrSlotAssignmentAnalysis(legalSlots.Count, combinedMask, GetSlotCount(combinedMask), false, legalSlots);
            }

            List<int> order = BuildAssignmentOrder(legalSlots);
            var usedSlots = new bool[SlotCount];
            bool hasLegalAssignment = TryAssign(order, 0, legalSlots, usedSlots);
            return new IrSlotAssignmentAnalysis(legalSlots.Count, combinedMask, GetSlotCount(combinedMask), hasLegalAssignment, legalSlots);
        }

        /// <summary>
        /// Materializes one deterministic physical slot assignment for a legal candidate group.
        /// </summary>
        public static IrMaterializedSlotAssignment MaterializeAssignment(IReadOnlyList<IrIssueSlotMask> legalSlots)
        {
            return MaterializeAssignment(legalSlots, previousInstructionSlots: null);
        }

        /// <summary>
        /// Materializes one deterministic physical slot assignment for a legal candidate group with adjacent-bundle context.
        /// </summary>
        public static IrMaterializedSlotAssignment MaterializeAssignment(IReadOnlyList<IrIssueSlotMask> legalSlots, IReadOnlyList<int>? previousInstructionSlots)
        {
            return SearchAssignments(legalSlots, previousInstructionSlots).MaterializeBestAssignment();
        }

        internal static IrMaterializedSlotAssignment MaterializeAssignment(
            IReadOnlyList<IrIssueSlotMask> legalSlots,
            IReadOnlyList<int>? previousInstructionSlots,
            HybridCpuBackendPlacementTieBreakContext tieBreakContext)
        {
            return SearchAssignments(legalSlots, previousInstructionSlots, tieBreakContext).MaterializeBestAssignment();
        }
    }
}
