using System;
using System.Collections.Generic;
using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU.Arch;

namespace HybridCPU.Compiler.Core.IR
{
    /// <summary>
    /// Applies compiler-owned symbolic control-flow relocations after physical bundle placement.
    /// </summary>
    public static class HybridCpuControlFlowRelocationResolver
    {
        private const int VliwSlotSizeBytes = 32;

        private readonly record struct InstructionPlacement(
            int BundleIndex,
            int SlotIndex,
            ulong BundleBaseAddress,
            ulong SlotAddress);

        /// <summary>
        /// Patches branch immediates to physical bundle/slot-relative displacements.
        /// </summary>
        public static IReadOnlyList<VLIW_Bundle> ApplyRelocations(
            IrProgramBundlingResult bundleLayout,
            IReadOnlyList<VLIW_Bundle> loweredBundles)
        {
            ArgumentNullException.ThrowIfNull(bundleLayout);
            ArgumentNullException.ThrowIfNull(loweredBundles);

            var relocatedBundles = new VLIW_Bundle[loweredBundles.Count];
            for (int index = 0; index < loweredBundles.Count; index++)
            {
                relocatedBundles[index] = loweredBundles[index];
            }

            Dictionary<int, InstructionPlacement> placements =
                BuildInstructionPlacementMap(bundleLayout, loweredBundles.Count);

            foreach (IrInstruction instruction in bundleLayout.Program.Instructions)
            {
                if (!RequiresControlFlowRelocation(instruction))
                {
                    continue;
                }

                int targetInstructionIndex =
                    instruction.Annotation.ResolvedBranchTargetInstructionIndex!.Value;

                if (!placements.TryGetValue(instruction.Index, out InstructionPlacement sourcePlacement))
                {
                    throw new InvalidOperationException(
                        $"Control-flow relocation source instruction {instruction.Index} was not materialized into a lowered VLIW bundle.");
                }

                if (!placements.TryGetValue(targetInstructionIndex, out InstructionPlacement targetPlacement))
                {
                    throw new InvalidOperationException(
                        $"Control-flow relocation target instruction {targetInstructionIndex} was not materialized into a lowered VLIW bundle.");
                }

                long displacement = checked((long)targetPlacement.SlotAddress - (long)sourcePlacement.BundleBaseAddress);
                if (displacement < short.MinValue || displacement > short.MaxValue)
                {
                    throw new InvalidOperationException(
                        $"Control-flow relocation from instruction {instruction.Index} to instruction {targetInstructionIndex} exceeds the signed 16-bit Immediate field.");
                }

                VLIW_Bundle sourceBundle = relocatedBundles[sourcePlacement.BundleIndex];
                VLIW_Instruction loweredInstruction =
                    sourceBundle.GetInstruction(sourcePlacement.SlotIndex);
                if (loweredInstruction.Src2Pointer != 0)
                {
                    throw new InvalidOperationException(
                        $"Control-flow relocation for instruction {instruction.Index} would preserve legacy Src2Pointer target sideband. " +
                        "Compiler branch emission must publish targets only through Immediate.");
                }

                loweredInstruction.Immediate = unchecked((ushort)(short)displacement);
                loweredInstruction.Src2Pointer = 0;
                sourceBundle.SetInstruction(
                    sourcePlacement.SlotIndex,
                    loweredInstruction);
                relocatedBundles[sourcePlacement.BundleIndex] = sourceBundle;
            }

            return Array.AsReadOnly(relocatedBundles);
        }

        private static bool RequiresControlFlowRelocation(IrInstruction instruction)
        {
            return (instruction.Annotation.ControlFlowKind is
                       IrControlFlowKind.ConditionalBranch or
                       IrControlFlowKind.UnconditionalBranch) &&
                   !string.IsNullOrWhiteSpace(instruction.Annotation.BranchTargetSymbolName) &&
                   instruction.Annotation.ResolvedBranchTargetInstructionIndex.HasValue;
        }

        private static Dictionary<int, InstructionPlacement> BuildInstructionPlacementMap(
            IrProgramBundlingResult bundleLayout,
            int loweredBundleCount)
        {
            var placements = new Dictionary<int, InstructionPlacement>();
            int bundleIndex = 0;

            foreach (IrBasicBlockBundlingResult blockResult in bundleLayout.BlockResults)
            {
                foreach (IrMaterializedBundle bundle in blockResult.Bundles)
                {
                    if (bundleIndex >= loweredBundleCount)
                    {
                        throw new InvalidOperationException(
                            "Control-flow relocation observed more materialized bundles than lowered backend bundles.");
                    }

                    ulong bundleBaseAddress =
                        (ulong)bundleIndex * (ulong)HybridCpuBundleSerializer.BundleSizeBytes;

                    foreach (IrMaterializedBundleSlot slot in bundle.Slots)
                    {
                        if (slot.Instruction is null)
                        {
                            continue;
                        }

                        placements[slot.Instruction.Index] = new InstructionPlacement(
                            bundleIndex,
                            slot.SlotIndex,
                            bundleBaseAddress,
                            bundleBaseAddress + ((ulong)slot.SlotIndex * VliwSlotSizeBytes));
                    }

                    bundleIndex++;
                }
            }

            if (bundleIndex != loweredBundleCount)
            {
                throw new InvalidOperationException(
                    "Control-flow relocation observed a lowered backend bundle count mismatch.");
            }

            return placements;
        }
    }
}
