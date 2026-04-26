using System;
using System.Collections.Generic;
using YAKSys_Hybrid_CPU.Core.Pipeline;

namespace YAKSys_Hybrid_CPU.Core
{
    /// <summary>
    /// Thrown when a pipeline FSM transition is attempted that has no legal target state.
    /// This indicates a deterministic execution contract violation: the pipeline must
    /// never be placed in an architecturally undefined state.
    /// </summary>
    public sealed class IllegalFsmTransitionException : Exception
    {
        /// <summary>The pipeline state from which the illegal transition was attempted.</summary>
        public PipelineState FromState { get; }

        /// <summary>The trigger that caused the illegal transition attempt.</summary>
        public PipelineTransitionTrigger Trigger { get; }

        public IllegalFsmTransitionException(
            PipelineState fromState,
            PipelineTransitionTrigger trigger)
            : base(
                $"Illegal FSM transition: {fromState} + {trigger} has no valid target state. " +
                $"Deterministic execution contract violated.")
        {
            FromState = fromState;
            Trigger = trigger;
        }
    }

    /// <summary>
    /// Validates pipeline FSM state transitions and enforces the HybridCPU
    /// deterministic execution contract.
    ///
    /// All transitions not present in the transition table are illegal and
    /// will throw <see cref="IllegalFsmTransitionException"/>.
    ///
    /// The transition table is the single authoritative source of truth for
    /// legal pipeline state transitions.
    /// Non-VMX runtime reaches it through the <see cref="PipelineEvent"/> plane.
    /// VMX runtime reaches it through typed retire triggers rather than a
    /// compatibility event mirror.
    /// </summary>
    public static class PipelineFsmGuard
    {
        private static readonly IReadOnlyDictionary<(PipelineState, PipelineTransitionTrigger), PipelineState>
            TransitionTable = new Dictionary<(PipelineState, PipelineTransitionTrigger), PipelineState>
        {
            // Reset to Task on power-on / hard reset.
            [(PipelineState.Reset, PipelineTransitionTrigger.Init)] = PipelineState.Task,

            // Task to VmEntry on VMLAUNCH / VMRESUME.
            [(PipelineState.Task, PipelineTransitionTrigger.VmLaunch)] = PipelineState.VmEntry,
            [(PipelineState.Task, PipelineTransitionTrigger.VmResume)] = PipelineState.VmEntry,

            // Task to Halted when all VTs issue WFI.
            [(PipelineState.Task, PipelineTransitionTrigger.HaltAll)] = PipelineState.Halted,

            // VmEntry to GuestExecution on successful entry.
            [(PipelineState.VmEntry, PipelineTransitionTrigger.EntryOk)] = PipelineState.GuestExecution,

            // VmEntry to Task on entry failure.
            [(PipelineState.VmEntry, PipelineTransitionTrigger.EntryFail)] = PipelineState.Task,

            // GuestExecution to VmExit on VMEXIT condition or VMXOFF.
            [(PipelineState.GuestExecution, PipelineTransitionTrigger.VmExitCond)] = PipelineState.VmExit,
            [(PipelineState.GuestExecution, PipelineTransitionTrigger.VmxOff)] = PipelineState.VmExit,

            // VmExit to Task when host state is fully restored.
            [(PipelineState.VmExit, PipelineTransitionTrigger.ExitComplete)] = PipelineState.Task,

            // Halted to Task on external interrupt or IPI.
            [(PipelineState.Task, PipelineTransitionTrigger.Interrupt)] = PipelineState.Task,
            [(PipelineState.Halted, PipelineTransitionTrigger.Interrupt)] = PipelineState.Task,

            // Cluster synchronisation transitions.
            [(PipelineState.Task, PipelineTransitionTrigger.EnterClusterSync)] = PipelineState.WaitForClusterSync,
            [(PipelineState.WaitForClusterSync, PipelineTransitionTrigger.ExitClusterSync)] = PipelineState.Task,
            [(PipelineState.WaitForClusterSync, PipelineTransitionTrigger.Interrupt)] = PipelineState.Task,

            // Hardware PTW stall transitions.
            [(PipelineState.Task, PipelineTransitionTrigger.PtwStart)] = PipelineState.PtwStall,
            [(PipelineState.PtwStall, PipelineTransitionTrigger.PtwComplete)] = PipelineState.Task,
            [(PipelineState.PtwStall, PipelineTransitionTrigger.PtwFault)] = PipelineState.Task,
            [(PipelineState.PtwStall, PipelineTransitionTrigger.Interrupt)] = PipelineState.Task,

            // FSP clock-gating transitions.
            [(PipelineState.Task, PipelineTransitionTrigger.EnterClockGate)] = PipelineState.ClockGatedDonor,
            [(PipelineState.ClockGatedDonor, PipelineTransitionTrigger.ExitClockGate)] = PipelineState.Task,
            [(PipelineState.ClockGatedDonor, PipelineTransitionTrigger.Interrupt)] = PipelineState.Task,

            // Trap-entry / trap-return serialising transitions.
            [(PipelineState.Task, PipelineTransitionTrigger.TrapEnter)] = PipelineState.TrapPending,
            [(PipelineState.TrapPending, PipelineTransitionTrigger.TrapReturn)] = PipelineState.Task,
            [(PipelineState.TrapPending, PipelineTransitionTrigger.Interrupt)] = PipelineState.Task,

            // WFE / SEV event-driven stall transitions.
            [(PipelineState.Task, PipelineTransitionTrigger.EnterWaitForEvent)] = PipelineState.WaitForEvent,
            [(PipelineState.WaitForEvent, PipelineTransitionTrigger.ExitWaitForEvent)] = PipelineState.Task,
            [(PipelineState.WaitForEvent, PipelineTransitionTrigger.Interrupt)] = PipelineState.Task,
        };

        /// <summary>
        /// Validates and returns the next pipeline state for a given (current state, trigger) pair.
        /// </summary>
        /// <param name="current">The current pipeline state.</param>
        /// <param name="trigger">The event triggering the transition.</param>
        /// <returns>The legal target state.</returns>
        /// <exception cref="IllegalFsmTransitionException">
        /// Thrown when no legal transition exists for the given (state, trigger) pair.
        /// </exception>
        public static PipelineState Transition(
            PipelineState current,
            PipelineTransitionTrigger trigger)
        {
            if (TransitionTable.TryGetValue((current, trigger), out var next))
            {
                return next;
            }

            throw new IllegalFsmTransitionException(current, trigger);
        }

        /// <summary>
        /// Returns true if the given (current state, trigger) pair has a legal transition.
        /// Does not perform the transition.
        /// </summary>
        public static bool IsLegalTransition(
            PipelineState current,
            PipelineTransitionTrigger trigger)
        {
            return TransitionTable.ContainsKey((current, trigger));
        }

        /// <summary>
        /// Advances the pipeline FSM in response to a <see cref="PipelineEvent"/>.
        /// This is the canonical event-to-transition entry point for the live pipeline.
        /// </summary>
        /// <param name="current">The current pipeline FSM state.</param>
        /// <param name="evt">The pipeline event that was observed.</param>
        /// <returns>
        /// The new pipeline FSM state after consuming <paramref name="evt"/>,
        /// or <paramref name="current"/> if the event has no FSM mapping.
        /// </returns>
        /// <exception cref="IllegalFsmTransitionException">
        /// Thrown when the event maps to a trigger that has no legal transition
        /// from <paramref name="current"/>.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="evt"/> is <see langword="null"/>.
        /// </exception>
        public static PipelineState Advance(PipelineState current, PipelineEvent evt)
        {
            if (evt is null)
            {
                throw new ArgumentNullException(nameof(evt));
            }

            var trigger = TryMapEventToTrigger(evt);
            if (trigger is null)
            {
                return current;
            }

            return Transition(current, trigger.Value);
        }

        /// <summary>
        /// Maps a <see cref="PipelineEvent"/> subtype to its
        /// <see cref="PipelineTransitionTrigger"/>, or returns
        /// <see langword="null"/> if the event does not drive an FSM transition.
        /// </summary>
        private static PipelineTransitionTrigger? TryMapEventToTrigger(PipelineEvent evt) => evt switch
        {
            ResetEvent => PipelineTransitionTrigger.Init,
            HaltEvent => PipelineTransitionTrigger.HaltAll,
            WfiEvent => PipelineTransitionTrigger.HaltAll,
            TrapEntryEvent => PipelineTransitionTrigger.TrapEnter,
            MretEvent => PipelineTransitionTrigger.TrapReturn,
            SretEvent => PipelineTransitionTrigger.TrapReturn,
            WfeEvent => PipelineTransitionTrigger.EnterWaitForEvent,
            SevEvent => PipelineTransitionTrigger.ExitWaitForEvent,
            ClusterSyncEnterEvent => PipelineTransitionTrigger.EnterClusterSync,
            ClusterSyncExitEvent => PipelineTransitionTrigger.ExitClusterSync,
            PtwWalkStartEvent => PipelineTransitionTrigger.PtwStart,
            PtwWalkCompleteEvent => PipelineTransitionTrigger.PtwComplete,
            PtwWalkFaultEvent => PipelineTransitionTrigger.PtwFault,
            ClockGatedDonorEnterEvent => PipelineTransitionTrigger.EnterClockGate,
            ClockGatedDonorExitEvent => PipelineTransitionTrigger.ExitClockGate,
            _ => null,
        };
    }
}
