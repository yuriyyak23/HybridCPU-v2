using HybridCPU.Compiler.Core.IR;
using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU.Compiler.Core.Threading
{
    public partial class HybridCpuThreadCompilerContext
    {
        public CompilerVectorTransferEmissionPlan CompileVload(
            CompilerVectorTransferMemoryAddressAbi destination,
            CompilerVectorTransferMemoryAddressAbi source,
            CompilerVectorTransferShapeAbi shape,
            StealabilityPolicy stealabilityPolicy = StealabilityPolicy.NotStealable)
        {
            CompilerVectorTransferEmissionPlan plan =
                CompilerVectorTransferEmissionLowerer.Lower(
                    CompilerVectorTransferEmissionRequest.Vload(
                        destination,
                        source,
                        shape));
            AppendVectorTransferInstruction(plan, stealabilityPolicy);
            return plan;
        }

        public CompilerVectorTransferEmissionPlan CompileVstore(
            CompilerVectorTransferMemoryAddressAbi source,
            CompilerVectorTransferMemoryAddressAbi destination,
            CompilerVectorTransferShapeAbi shape,
            StealabilityPolicy stealabilityPolicy = StealabilityPolicy.NotStealable)
        {
            CompilerVectorTransferEmissionPlan plan =
                CompilerVectorTransferEmissionLowerer.Lower(
                    CompilerVectorTransferEmissionRequest.Vstore(
                        source,
                        destination,
                        shape));
            AppendVectorTransferInstruction(plan, stealabilityPolicy);
            return plan;
        }

        private void AppendVectorTransferInstruction(
            CompilerVectorTransferEmissionPlan plan,
            StealabilityPolicy stealabilityPolicy)
        {
            EnsureInstructionCapacity();

            VLIW_Instruction instruction = plan.EncodedInstruction;
            instruction.VirtualThreadId = _virtualThreadId.Value;

            _instructions[_instructionCount] = instruction;
            _instructionSlotMetadata[_instructionCount] = new InstructionSlotMetadata(
                _virtualThreadId,
                BuildSlotMetadata((uint)plan.Request.Opcode, stealabilityPolicy, _domainTag));
            _instructionCount++;
            InvalidateCanonicalCompileCache();
        }
    }
}
