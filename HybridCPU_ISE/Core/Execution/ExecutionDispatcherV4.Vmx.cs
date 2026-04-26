using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core.Memory;
using YAKSys_Hybrid_CPU.Core.Pipeline;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using YAKSys_Hybrid_CPU.Core.Registers;
using YAKSys_Hybrid_CPU.Core.Registers.Retire;
using CpuCore = YAKSys_Hybrid_CPU.Processor.CPU_Core;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace YAKSys_Hybrid_CPU.Core.Execution
{
    public sealed partial class ExecutionDispatcherV4
    {
        // ── VMX execution unit ─────────────────────────────────────────────

        private void CaptureVmxRetireWindowPublications(
            InstructionIR instr,
            ICanonicalCpuState state,
            ref CpuCore.RetireWindowBatch retireBatch,
            byte vtId)
        {
            VmxRetireEffect effect = _vmxUnit!.Resolve(
                instr,
                state,
                PrivilegeLevel.Machine,
                vtId);

            if (effect.IsValid)
            {
                retireBatch.CaptureRetireWindowVmxEffect(effect, vtId);
            }
        }

        private ExecutionResult ExecuteVmx(InstructionIR instr, ICanonicalCpuState state, byte vtId)
        {
            return _vmxUnit!.Execute(instr, state, PrivilegeLevel.Machine, vtId);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ExecutionResult EnqueuePipelineEvent(PipelineEvent evt)
        {
            _pipelineEventQueue.Enqueue(evt);
            return ExecutionResult.Ok();
    }
    }
}

