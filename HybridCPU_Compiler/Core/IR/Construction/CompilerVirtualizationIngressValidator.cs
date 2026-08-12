using HybridCPU_ISE.Arch;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU.Compiler.Core.IR;

/// <summary>
/// Compiler-only provenance binding between one frontend intent and one encoded carrier.
/// It is consumed during IR construction and is never serialized or published to runtime.
/// </summary>
public sealed record CompilerVirtualizationIntentBinding(
    int InstructionIndex,
    CompilerExactProbeEmissionPlan Plan);

internal static class CompilerVirtualizationIngressValidator
{
    internal static IReadOnlyDictionary<int, CompilerExactProbeEmissionPlan> Validate(
        ReadOnlySpan<VLIW_Instruction> instructions,
        IReadOnlyList<CompilerVirtualizationIntentBinding>? bindings)
    {
        var validated = new Dictionary<int, CompilerExactProbeEmissionPlan>();

        if (bindings is not null)
        {
            for (int index = 0; index < bindings.Count; index++)
            {
                CompilerVirtualizationIntentBinding binding =
                    bindings[index] ?? throw new InvalidOperationException(
                        "Compiler virtualization intent binding cannot be null.");
                if ((uint)binding.InstructionIndex >= (uint)instructions.Length)
                {
                    throw new InvalidOperationException(
                        $"Compiler virtualization intent binding index {binding.InstructionIndex} is outside the instruction stream.");
                }

                if (!validated.TryAdd(binding.InstructionIndex, binding.Plan))
                {
                    throw new InvalidOperationException(
                        $"Duplicate compiler virtualization intent binding at instruction {binding.InstructionIndex}.");
                }

                CompilerExactProbeEmissionResult revalidated =
                    CompilerExactProbeEmissionLowerer.Lower(binding.Plan.Request);
                CompilerExactProbeEmissionPlan canonical = revalidated.Plan ??
                    throw new InvalidOperationException(
                        $"Compiler virtualization intent binding at instruction {binding.InstructionIndex} no longer passes its exact emission decision.");

                if (binding.Plan.Request != canonical.Request ||
                    binding.Plan.Intent != canonical.Intent ||
                    binding.Plan.LegalityFacts != canonical.LegalityFacts ||
                    binding.Plan.Metadata != canonical.Metadata ||
                    !MatchesCarrier(binding.Plan.EncodedInstruction, canonical.EncodedInstruction) ||
                    !MatchesCarrier(instructions[binding.InstructionIndex], canonical.EncodedInstruction))
                {
                    throw new InvalidOperationException(
                        $"Compiler virtualization intent binding at instruction {binding.InstructionIndex} is forged, stale or does not match the exact carrier.");
                }
            }
        }

        for (int instructionIndex = 0; instructionIndex < instructions.Length; instructionIndex++)
        {
            bool isVmCall = instructions[instructionIndex].OpCode == (uint)InstructionsEnum.VMCALL;
            bool hasBinding = validated.ContainsKey(instructionIndex);
            if (isVmCall != hasBinding)
            {
                throw new InvalidOperationException(
                    isVmCall
                        ? $"Raw VMCALL at instruction {instructionIndex} is denied; canonical IR requires an exact CompilerEmissionDecisionV1 intent binding."
                        : $"Compiler virtualization intent binding at instruction {instructionIndex} cannot attach to a non-VMCALL carrier.");
            }
        }

        return validated;
    }

    private static bool MatchesCarrier(VLIW_Instruction actual, VLIW_Instruction expected)
    {
        const ulong virtualThreadTransportHintMask = 0x3UL << 48;
        return actual.OpCode == expected.OpCode &&
               actual.Word0 == expected.Word0 &&
               actual.Word1 == expected.Word1 &&
               actual.Word2 == expected.Word2 &&
               (actual.Word3 & ~virtualThreadTransportHintMask) ==
               (expected.Word3 & ~virtualThreadTransportHintMask);
    }
}
