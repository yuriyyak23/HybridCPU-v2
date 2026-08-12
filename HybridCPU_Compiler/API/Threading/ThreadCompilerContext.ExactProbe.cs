using HybridCPU.Compiler.Core.IR;
using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU.Compiler.Core.Threading
{
    public partial class HybridCpuThreadCompilerContext
    {
        private readonly CompilerExactProbeEmissionPlan?[] _exactProbeEmissionPlans =
            new CompilerExactProbeEmissionPlan?[MAX_INSTRUCTIONS_PER_THREAD];

        public CompilerExactProbeEmissionResult CompileProbeNoStateV1WithDecision(
            CompilerExactProbeEmissionRequest request,
            StealabilityPolicy stealabilityPolicy = StealabilityPolicy.NotStealable)
        {
            if (stealabilityPolicy != StealabilityPolicy.NotStealable)
            {
                return new CompilerExactProbeEmissionResult(
                    CompilerExactProbeEmissionDecisionKind.DeniedSchedulingContract,
                    Plan: null,
                    "Exact probe is a SystemSingleton exclusive-cycle carrier and cannot carry a stealable scheduling hint.");
            }

            CompilerExactProbeEmissionResult result = CompilerExactProbeEmissionLowerer.Lower(request);
            if (!result.Emitted)
            {
                return result;
            }

            AppendExactProbeInstruction(
                result.Plan ?? throw new InvalidOperationException("Accepted exact probe emission requires a carrier plan."),
                stealabilityPolicy);
            return result;
        }

        private void AppendExactProbeInstruction(
            CompilerExactProbeEmissionPlan plan,
            StealabilityPolicy stealabilityPolicy)
        {
            EnsureInstructionCapacity();
            VLIW_Instruction instruction = plan.EncodedInstruction;
            instruction.VirtualThreadId = _virtualThreadId.Value;

            _instructions[_instructionCount] = instruction;
            _exactProbeEmissionPlans[_instructionCount] = plan;
            _instructionSlotMetadata[_instructionCount] = new InstructionSlotMetadata(
                _virtualThreadId,
                BuildSlotMetadata((uint)InstructionsEnum.VMCALL, stealabilityPolicy, _domainTag));
            _instructionCount++;
            InvalidateCanonicalCompileCache();
        }

        private IReadOnlyList<CompilerVirtualizationIntentBinding> GetVirtualizationIntentBindings()
        {
            var bindings = new List<CompilerVirtualizationIntentBinding>();
            for (int instructionIndex = 0; instructionIndex < _instructionCount; instructionIndex++)
            {
                if (_exactProbeEmissionPlans[instructionIndex] is { } plan)
                {
                    bindings.Add(new CompilerVirtualizationIntentBinding(instructionIndex, plan));
                }
            }

            return bindings;
        }

        private static void ValidateNoDirectExactProbeEmission(uint opCode)
        {
            if (opCode == (uint)InstructionsEnum.VMCALL)
            {
                throw new InvalidOperationException(
                    "Raw VMCALL compiler emission is denied; use the exact PROBE_NO_STATE_V1 CompilerEmissionDecisionV1 gate.");
            }
        }
    }
}
