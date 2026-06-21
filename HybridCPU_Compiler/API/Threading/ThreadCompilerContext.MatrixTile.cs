using HybridCPU.Compiler.Core.IR;
using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU.Compiler.Core.Threading
{
    public partial class HybridCpuThreadCompilerContext
    {
        public CompilerMatrixTileEmissionPlan CompileMtileLoad(
            CompilerMatrixTileTileOperand destinationTile,
            CompilerMatrixTileDescriptorAbi descriptor,
            CompilerMatrixTileMemoryFaultAbiInputs memoryFaultAbi,
            StealabilityPolicy stealabilityPolicy = StealabilityPolicy.NotStealable)
        {
            CompilerMatrixTileEmissionPlan plan =
                CompilerMatrixTileEmissionLowerer.Lower(
                    CompilerMatrixTileEmissionRequest.MtileLoad(
                        destinationTile,
                        descriptor,
                        memoryFaultAbi));
            AppendMatrixTileInstruction(plan, stealabilityPolicy);
            return plan;
        }

        public CompilerMatrixTileEmissionPlan CompileMtileStore(
            CompilerMatrixTileTileOperand sourceTile,
            CompilerMatrixTileDescriptorAbi descriptor,
            CompilerMatrixTileMemoryFaultAbiInputs memoryFaultAbi,
            StealabilityPolicy stealabilityPolicy = StealabilityPolicy.NotStealable)
        {
            CompilerMatrixTileEmissionPlan plan =
                CompilerMatrixTileEmissionLowerer.Lower(
                    CompilerMatrixTileEmissionRequest.MtileStore(
                        sourceTile,
                        descriptor,
                        memoryFaultAbi));
            AppendMatrixTileInstruction(plan, stealabilityPolicy);
            return plan;
        }

        public CompilerMatrixTileEmissionPlan CompileMtileMacc(
            CompilerMatrixTileTileOperand leftSourceTile,
            CompilerMatrixTileTileOperand rightSourceTile,
            CompilerMatrixTileTileOperand accumulatorTile,
            CompilerMatrixTileDescriptorAbi leftSourceDescriptor,
            CompilerMatrixTileAccumulatorPolicyAbi accumulatorPolicyAbi,
            StealabilityPolicy stealabilityPolicy = StealabilityPolicy.NotStealable)
        {
            CompilerMatrixTileEmissionPlan plan =
                CompilerMatrixTileEmissionLowerer.Lower(
                    CompilerMatrixTileEmissionRequest.MtileMacc(
                        leftSourceTile,
                        rightSourceTile,
                        accumulatorTile,
                        leftSourceDescriptor,
                        accumulatorPolicyAbi));
            AppendMatrixTileInstruction(plan, stealabilityPolicy);
            return plan;
        }

        public CompilerMatrixTileEmissionPlan CompileMtranspose(
            CompilerMatrixTileTileOperand sourceTile,
            CompilerMatrixTileTileOperand destinationTile,
            CompilerMatrixTileDescriptorAbi sourceDescriptor,
            CompilerMatrixTileTransposePolicyAbi transposePolicyAbi,
            StealabilityPolicy stealabilityPolicy = StealabilityPolicy.NotStealable)
        {
            CompilerMatrixTileEmissionPlan plan =
                CompilerMatrixTileEmissionLowerer.Lower(
                    CompilerMatrixTileEmissionRequest.Mtranspose(
                        sourceTile,
                        destinationTile,
                        sourceDescriptor,
                        transposePolicyAbi));
            AppendMatrixTileInstruction(plan, stealabilityPolicy);
            return plan;
        }

        private void AppendMatrixTileInstruction(
            CompilerMatrixTileEmissionPlan plan,
            StealabilityPolicy stealabilityPolicy)
        {
            EnsureInstructionCapacity();

            VLIW_Instruction instruction = plan.EncodedInstruction;
            instruction.VirtualThreadId = _virtualThreadId.Value;

            _instructions[_instructionCount] = instruction;
            _instructionSlotMetadata[_instructionCount] = new InstructionSlotMetadata(
                _virtualThreadId,
                BuildSlotMetadata((uint)plan.Request.Opcode, stealabilityPolicy, _domainTag))
            {
                MatrixTileNumericPolicy = plan.MatrixTileNumericPolicy,
                MatrixTileLayoutPolicy = plan.MatrixTileLayoutPolicy
            };
            _instructionCount++;
            InvalidateCanonicalCompileCache();
        }

        private static void ValidateNoDirectMatrixTileEmission(uint opCode)
        {
            if (CompilerMatrixTilePositiveEmissionAbiContract.IsMatrixTilePositiveOpcode(opCode))
            {
                throw new InvalidOperationException(
                    "MTILE compiler emission requires the typed matrix/tile helper ABI; raw CompileInstruction/EmitRawInstruction transport is not emission authority.");
            }
        }
    }
}
