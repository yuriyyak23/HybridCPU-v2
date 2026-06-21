using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Registers.Retire;

namespace YAKSys_Hybrid_CPU
{
    public partial struct Processor
    {
        public partial struct CPU_Core
        {
            /// <summary>
            /// Applies a runtime VT-scoped FSM transition through the canonical guard.
            /// This is the only production helper that may advance live per-VT pipeline
            /// state from a trigger.
            /// </summary>
            internal void ApplyVirtualThreadPipelineTransition(
                int vtId,
                PipelineTransitionTrigger trigger)
            {
                int normalizedVtId = ResolveLiveStateVtIdOrThrow(vtId, nameof(ApplyVirtualThreadPipelineTransition));
                PipelineState currentState = ReadVirtualThreadPipelineState(normalizedVtId);
                WriteVirtualThreadPipelineState(
                    normalizedVtId,
                    PipelineFsmGuard.Transition(currentState, trigger));
            }

            /// <summary>
            /// Publishes a VT-scoped pipeline-state snapshot that has already been
            /// derived through <see cref="PipelineFsmGuard"/> on a scoped adapter.
            /// Raw runtime transitions must not call <see cref="WriteVirtualThreadPipelineState(int, PipelineState)"/>
            /// directly outside the core state helpers.
            /// </summary>
            internal void PublishGuardedVirtualThreadPipelineState(
                int vtId,
                PipelineState guardedState)
            {
                int normalizedVtId = ResolveLiveStateVtIdOrThrow(vtId, nameof(PublishGuardedVirtualThreadPipelineState));
                WriteVirtualThreadPipelineState(normalizedVtId, guardedState);
            }

            /// <summary>
            /// Core-owned PC publication seam. Active frontend PC remains frontend-owned,
            /// while committed PC publication remains retire-owned. Scoped adapters and
            /// helper paths must flow through this helper instead of patching both
            /// contours independently.
            /// </summary>
            internal void PublishVirtualThreadPcOwnership(
                int vtId,
                ulong pc,
                bool retireWhenCommittedPcMatches)
            {
                int normalizedVtId = ResolveLiveStateVtIdOrThrow(vtId, nameof(PublishVirtualThreadPcOwnership));
                if (retireWhenCommittedPcMatches ||
                    ReadCommittedPc(normalizedVtId) != pc)
                {
                    RetireCoordinator.Retire(RetireRecord.PcWrite(normalizedVtId, pc));
                }

                if (ReadActiveVirtualThreadId() == normalizedVtId)
                {
                    WriteActiveLivePc(pc);
                }
            }

            /// <summary>
            /// Scoped live-adapter writeback publishes an explicit PC update only when
            /// the adapter staged one. This prevents passive adapter snapshots from
            /// auto-committing the current frontend PC.
            /// </summary>
            internal void PublishLiveAdapterPcWriteback(
                int vtId,
                ulong pc)
            {
                PublishVirtualThreadPcOwnership(
                    vtId,
                    pc,
                    retireWhenCommittedPcMatches: false);
            }

            /// <summary>
            /// Applies the runtime-visible VT FSM follow-through for a retired VMX effect
            /// via the canonical guard instead of publishing a raw final-state snapshot.
            /// </summary>
            internal void ApplyRetiredVmxPipelineStateOwnership(
                int vtId,
                in Core.VmxRetireEffect effect,
                in Core.VmxRetireOutcome outcome)
            {
                if (!effect.IsValid || effect.IsFaulted)
                {
                    return;
                }

                int normalizedVtId = ResolveLiveStateVtIdOrThrow(vtId, nameof(ApplyRetiredVmxPipelineStateOwnership));

                switch (effect.Operation)
                {
                    case Core.VmxOperationKind.VmLaunch:
                        ApplyVirtualThreadPipelineTransition(
                            normalizedVtId,
                            PipelineTransitionTrigger.VmLaunch);
                        ApplyVirtualThreadPipelineTransition(
                            normalizedVtId,
                            outcome.Faulted
                                ? PipelineTransitionTrigger.EntryFail
                                : PipelineTransitionTrigger.EntryOk);
                        break;

                    case Core.VmxOperationKind.VmResume:
                        ApplyVirtualThreadPipelineTransition(
                            normalizedVtId,
                            PipelineTransitionTrigger.VmResume);
                        ApplyVirtualThreadPipelineTransition(
                            normalizedVtId,
                            outcome.Faulted
                                ? PipelineTransitionTrigger.EntryFail
                                : PipelineTransitionTrigger.EntryOk);
                        break;

                    case Core.VmxOperationKind.VmxOff:
                        if (!effect.ExitGuestContextOnRetire)
                        {
                            return;
                        }

                        ApplyVirtualThreadPipelineTransition(
                            normalizedVtId,
                            PipelineTransitionTrigger.VmxOff);
                        ApplyVirtualThreadPipelineTransition(
                            normalizedVtId,
                            PipelineTransitionTrigger.ExitComplete);
                        break;
                }
            }
        }
    }
}
