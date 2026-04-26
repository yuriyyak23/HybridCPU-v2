using System;
using System.Runtime.CompilerServices;
using YAKSys_Hybrid_CPU.Core.Registers.Retire;

namespace YAKSys_Hybrid_CPU
{
    public partial struct Processor
    {
        public partial struct CPU_Core
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private Core.VmxRetireEffect? MaterializeLaneVmxEffect(Core.MicroOp microOp)
            {
                if (microOp is Core.VmxMicroOp vmxMicroOp)
                {
                    return vmxMicroOp.CreateRetireEffect();
                }

                return null;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private Core.VmxRetireOutcome ApplyRetiredVmxEffect(
                in Core.VmxRetireEffect vmxEffect,
                int virtualThreadId)
            {
                int normalizedVtId = NormalizePipelineStateVtId(virtualThreadId);
                LiveCpuStateAdapter liveState = CreateLiveCpuStateAdapter(normalizedVtId);
                Core.VmxRetireOutcome outcome = VmxUnit.RetireEffect(
                    vmxEffect,
                    liveState,
                    (byte)normalizedVtId);

                RetireVmxOutcomeRecords(outcome, normalizedVtId);
                ApplyRetiredVmxPipelineStateOwnership(normalizedVtId, vmxEffect, outcome);

                if (outcome.RedirectTargetPc.HasValue)
                {
                    // VM-entry / VM-exit control-flow redirects are owned by the issuing VT.
                    if (outcome.FlushesPipeline &&
                        ReadActiveVirtualThreadId() == normalizedVtId)
                    {
                        ApplyPipelineControlFlowRedirect(
                            outcome.RedirectTargetPc.Value,
                            Core.AssistInvalidationReason.VmTransition);
                    }
                }

                if (outcome.FlushesPipeline &&
                    (!outcome.RedirectTargetPc.HasValue ||
                     ReadActiveVirtualThreadId() != normalizedVtId))
                {
                    PublishReplayInvalidationForOffPipeVmxBoundary();
                    InvalidateAssistRuntime(Core.AssistInvalidationReason.VmTransition);
                }

                return outcome;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void PublishReplayInvalidationForOffPipeVmxBoundary()
            {
                if (!_loopBuffer.CurrentReplayPhase.IsActive)
                {
                    return;
                }

                _loopBuffer.Invalidate(Core.ReplayPhaseInvalidationReason.SerializingEvent);
                _fspScheduler?.SetReplayPhaseContext(
                    _loopBuffer.CurrentReplayPhase,
                    invalidateAssistOnDeactivate: false);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void RetireVmxOutcomeRecords(
                in Core.VmxRetireOutcome outcome,
                int virtualThreadId)
            {
                Span<RetireRecord> retireRecords = stackalloc RetireRecord[3];
                int retireRecordCount = 0;

                if (outcome.HasRegisterWriteback)
                {
                    if (outcome.RegisterDestination == 0)
                    {
                        throw new InvalidOperationException(
                            "VMX retire outcome requested an architectural writeback to x0.");
                    }

                    if (outcome.RestoredStackPointer.HasValue &&
                        outcome.RegisterDestination == 2)
                    {
                        throw new InvalidOperationException(
                            "VMX retire outcome published both an explicit writeback and a restored stack pointer for x2.");
                    }

                    retireRecords[retireRecordCount++] = RetireRecord.RegisterWrite(
                        virtualThreadId,
                        outcome.RegisterDestination,
                        outcome.RegisterWritebackValue);
                }

                if (outcome.RestoredStackPointer.HasValue)
                {
                    retireRecords[retireRecordCount++] = RetireRecord.RegisterWrite(
                        virtualThreadId,
                        2,
                        outcome.RestoredStackPointer.Value);
                }

                if (outcome.RedirectTargetPc.HasValue)
                {
                    retireRecords[retireRecordCount++] = RetireRecord.PcWrite(
                        virtualThreadId,
                        outcome.RedirectTargetPc.Value);
                }

                if (retireRecordCount != 0)
                {
                    RetireCoordinator.Retire(retireRecords[..retireRecordCount]);
                }
            }

            internal Core.VmxRetireOutcome ApplyRetiredVmxEffectForTesting(
                in Core.VmxRetireEffect vmxEffect,
                int virtualThreadId)
            {
                return ApplyRetiredVmxEffect(vmxEffect, virtualThreadId);
            }
        }
    }
}
