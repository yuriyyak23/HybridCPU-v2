using System.Runtime.CompilerServices;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core.Pipeline;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using CpuCore = YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace YAKSys_Hybrid_CPU.Core.Execution
{
    public sealed partial class ExecutionDispatcherV4
    {
        private void CaptureVmxRetireWindowPublications(
            InstructionIR instr,
            ICanonicalCpuState state,
            ref CpuCore.RetireWindowBatch retireBatch,
            byte vtId)
        {
            VmxRetireEffect effect = CreateRemovedFrontendFaultEffect(instr);

            if (effect.IsValid)
            {
                retireBatch.CaptureRetireWindowVmxEffect(effect, vtId);
            }
        }

        private ExecutionResult ExecuteVmx(InstructionIR instr, ICanonicalCpuState state, byte vtId)
        {
            return ExecutionResult.VmxFault();
        }

        private static VmxRetireEffect CreateRemovedFrontendFaultEffect(InstructionIR instr)
        {
            if (!InstructionRegistry.TryResolvePublishedVmxOperationKind(in instr, out VmxOperationKind operation))
            {
                throw new InvalidOperationException(
                    $"Unknown frozen VMX opcode {OpcodeRegistry.GetMnemonicOrHex(instr.CanonicalOpcode.Value)}.");
            }

            return VmxRetireEffect.Fault(operation, VmExitReason.SecurityPolicyViolation);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ExecutionResult EnqueuePipelineEvent(PipelineEvent evt)
        {
            _pipelineEventQueue.Enqueue(evt);
            return ExecutionResult.Ok();
        }
    }
}
