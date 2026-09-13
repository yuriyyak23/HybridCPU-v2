using System;
using System.Collections.Generic;
using System.Linq;

namespace HybridCPU.Compiler.Core.IR
{
    /// <summary>
    /// Applies compiler-owned symbolic control-flow relocations after physical bundle placement.
    /// </summary>
    public static class HybridCpuControlFlowRelocationResolver
    {
        private readonly record struct InstructionPlacement(
            int BundleIndex,
            int SlotIndex,
            ulong BundleBaseAddress);

        /// <summary>
        /// Patches branch immediates to physical bundle-base-relative displacements.
        /// </summary>
        public static IReadOnlyList<HybridCpuInstructionBundle> ApplyRelocations(
            IrProgramBundlingResult bundleLayout,
            IReadOnlyList<HybridCpuInstructionBundle> loweredBundles)
        {
            ArgumentNullException.ThrowIfNull(bundleLayout);
            ArgumentNullException.ThrowIfNull(loweredBundles);

            var relocatedBundles = new HybridCpuInstructionBundle[loweredBundles.Count];
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

                // The ISE fetch contract addresses whole 256-byte bundles. A branch to a
                // physical slot address would fetch a new 256-byte window from the middle
                // of the serialized bundle instead of resuming at the target block.
                long displacement = checked((long)targetPlacement.BundleBaseAddress - (long)sourcePlacement.BundleBaseAddress);
                if (IsLongBranchLow(bundleLayout.Program, instruction))
                {
                    ApplyLongBranchRelocation(bundleLayout, relocatedBundles, placements, instruction,
                        sourcePlacement, targetInstructionIndex, targetPlacement);
                    continue;
                }
                if (displacement < short.MinValue || displacement > short.MaxValue)
                {
                    IrInstruction targetInstruction = bundleLayout.Program.Instructions.Single(candidate =>
                        candidate.Index == targetInstructionIndex);
                    throw new InvalidOperationException(
                        $"Control-flow relocation from instruction {instruction.Index} ('{instruction.StableIdentity}') at bundle base " +
                        $"{sourcePlacement.BundleBaseAddress} to instruction {targetInstructionIndex} ('{targetInstruction.StableIdentity}') at bundle base " +
                        $"{targetPlacement.BundleBaseAddress} requires displacement {displacement}, outside signed 16-bit [{short.MinValue},{short.MaxValue}].");
                }

                HybridCpuInstructionBundle sourceBundle = relocatedBundles[sourcePlacement.BundleIndex];
                HybridCpuInstructionWord loweredInstruction =
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

        private static void ApplyLongBranchRelocation(
            IrProgramBundlingResult bundleLayout,
            HybridCpuInstructionBundle[] relocatedBundles,
            IReadOnlyDictionary<int, InstructionPlacement> placements,
            IrInstruction lowInstruction,
            InstructionPlacement lowPlacement,
            int targetInstructionIndex,
            InstructionPlacement targetPlacement)
        {
            string baseIdentity = lowInstruction.StableIdentity.EndsWith(":long-branch-low", StringComparison.Ordinal)
                ? lowInstruction.StableIdentity[..^":long-branch-low".Length]
                : lowInstruction.StableIdentity;
            string highIdentity = baseIdentity + ":long-branch-high";
            IrInstruction[] highCandidates = bundleLayout.Program.Instructions
                .Where(candidate => string.Equals(candidate.StableIdentity, highIdentity, StringComparison.Ordinal))
                .ToArray();
            if (highCandidates.Length != 1 || highCandidates[0].Opcode != HybridCpuOpcode.AUIPC ||
                !placements.TryGetValue(highCandidates[0].Index, out InstructionPlacement highPlacement))
                throw new InvalidOperationException(
                    $"Long-branch relocation '{lowInstruction.StableIdentity}' lacks one materialized AUIPC high instruction '{highIdentity}'.");
            if (lowInstruction.Opcode != HybridCpuOpcode.JALR ||
                !highCandidates[0].Annotation.Defs.Any(static operand =>
                    operand.Kind == IrOperandKind.ArchitecturalRegister && operand.Value == 5) ||
                !lowInstruction.Annotation.Uses.Any(static operand =>
                    operand.Kind == IrOperandKind.ArchitecturalRegister && operand.Value == 5) ||
                highCandidates[0].Annotation.Defs.Any(static operand =>
                    operand.Kind == IrOperandKind.ArchitecturalRegister && operand.Value != 5) ||
                lowInstruction.Annotation.Uses.Any(static operand =>
                    operand.Kind == IrOperandKind.ArchitecturalRegister && operand.Value != 5))
                throw new InvalidOperationException(
                    $"Long-branch relocation '{lowInstruction.StableIdentity}' does not carry the exact AUIPC x5/JALR x5 structural contract.");

            long displacement = checked((long)targetPlacement.BundleBaseAddress - (long)highPlacement.BundleBaseAddress);
            long high = checked((displacement + 0x800L) >> 12);
            long low = checked(displacement - (high << 12));
            if (high < short.MinValue || high > short.MaxValue || low < short.MinValue || low > short.MaxValue)
                throw new InvalidOperationException(
                    $"Long-branch relocation from AUIPC instruction {highCandidates[0].Index} ('{highIdentity}') at bundle base " +
                    $"{highPlacement.BundleBaseAddress} through JALR instruction {lowInstruction.Index} at bundle base {lowPlacement.BundleBaseAddress} " +
                    $"to instruction {targetInstructionIndex} at bundle base {targetPlacement.BundleBaseAddress} requires displacement {displacement}, " +
                    $"outside exact signed high/low 16-bit AUIPC/JALR reach.");

            HybridCpuInstructionBundle highBundle = relocatedBundles[highPlacement.BundleIndex];
            HybridCpuInstructionWord highWord = highBundle.GetInstruction(highPlacement.SlotIndex);
            HybridCpuInstructionBundle lowBundle = relocatedBundles[lowPlacement.BundleIndex];
            HybridCpuInstructionWord lowWord = lowBundle.GetInstruction(lowPlacement.SlotIndex);
            if (highWord.Src2Pointer != 0 || lowWord.Src2Pointer != 0)
                throw new InvalidOperationException(
                    $"Long-branch relocation '{lowInstruction.StableIdentity}' observed nonzero legacy target sideband.");
            highWord.Immediate = unchecked((ushort)(short)high);
            lowWord.Immediate = unchecked((ushort)(short)low);
            highWord.Src2Pointer = 0;
            lowWord.Src2Pointer = 0;
            highBundle.SetInstruction(highPlacement.SlotIndex, highWord);
            lowBundle.SetInstruction(lowPlacement.SlotIndex, lowWord);
            relocatedBundles[highPlacement.BundleIndex] = highBundle;
            relocatedBundles[lowPlacement.BundleIndex] = lowBundle;
        }

        private static bool IsLongBranchLow(IrProgram program, IrInstruction instruction)
        {
            if (instruction.Opcode != HybridCpuOpcode.JALR ||
                instruction.Annotation.ControlFlowKind != IrControlFlowKind.UnconditionalBranch ||
                string.IsNullOrWhiteSpace(instruction.Annotation.BranchTargetSymbolName))
                return false;
            string baseIdentity = instruction.StableIdentity.EndsWith(":long-branch-low", StringComparison.Ordinal)
                ? instruction.StableIdentity[..^":long-branch-low".Length]
                : instruction.StableIdentity;
            return program.Instructions.Any(candidate => candidate.Opcode == HybridCpuOpcode.AUIPC &&
                string.Equals(candidate.StableIdentity, baseIdentity + ":long-branch-high", StringComparison.Ordinal));
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
                            bundleBaseAddress);
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
