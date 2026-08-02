namespace YAKSys_Hybrid_CPU.Core
{
    /// <summary>
    /// HybridCPU pipeline execution state machine states.
    /// These are the formal architectural states of the pipeline FSM.
    ///
    /// CRITICAL: State transitions must be deterministic and replayable.
    /// All state transitions must be logged in the diagnostic trace.
    /// </summary>
    public enum PipelineState
    {
        /// <summary>
        /// Normal host execution state.
        /// The pipeline is executing host instructions.
        /// This is the default state after reset.
        /// </summary>
        Task            = 0,

        /// <summary>
        /// Transition state: entering guest VM execution.
        /// Entered via VMLAUNCH or VMRESUME.
        /// During VM_ENTRY:
        ///   - Host registers are saved
        ///   - Guest VMCS state is loaded
        ///   - Pipeline transitions to guest execution
        /// Transitions to: Task (if entry fails), GuestExecution (on success)
        /// </summary>
        VmEntry         = 1,

        /// <summary>
        /// Guest VM is executing.
        /// The pipeline is executing guest instructions.
        /// All VT slots execute guest instructions.
        /// </summary>
        GuestExecution  = 2,

        /// <summary>
        /// Transition state: exiting guest VM execution.
        /// Entered via VMEXIT condition (exception, interrupt, HW trigger).
        /// During VM_EXIT:
        ///   - Guest registers are saved to VMCS
        ///   - Host state is restored
        ///   - VMEXIT reason is recorded in CSR
        ///   - Pipeline transitions back to host execution
        /// Transitions to: Task
        /// </summary>
        VmExit          = 3,

        /// <summary>
        /// Core is halted (WFI in all VTs, or explicit halt).
        /// </summary>
        Halted          = 4,

        /// <summary>
        /// Core is in reset state.
        /// </summary>
        Reset           = 5,

        // ─────────────────────────────────────────────────────────────────────
        // C14 (Checklist): TRAP_ENTRY / TRAP_RETURN serializing pipeline states.
        // These make trap entry and privilege-return explicit serializing
        // boundaries — the pipeline drains before entering the handler and
        // before returning to the interrupted context.
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Pipeline is draining in response to a synchronous trap (exception /
        /// ECALL / EBREAK) or an asynchronous interrupt.  No new instructions
        /// may be issued; all in-flight micro-ops retire or are squashed before
        /// the trap handler takes control.
        ///
        /// Entry: <see cref="Pipeline.TrapEntryEvent"/> received while in Task.
        /// Exit (handler PC set): <see cref="PipelineTransitionTrigger.TrapReturn"/> — handler begins.
        /// Exit (interrupt): <see cref="PipelineTransitionTrigger.Interrupt"/> — preempt.
        ///
        /// C14: TRAP_ENTRY modelled as a first-class serialising pipeline transition.
        /// </summary>
        TrapPending     = 9,

        // ─────────────────────────────────────────────────────────────────────
        // C16 (Checklist): WFE barrier stall state.
        // Models the WFE (Wait-For-Event) barrier as a first-class pipeline
        // state rather than an opaque side-effect.
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// VT has executed WFE and is stalled pending an SEV signal from
        /// another VT or an external interrupt.
        ///
        /// Entry: <see cref="Pipeline.WfeEvent"/> received while in Task.
        /// Exit (event): <see cref="PipelineTransitionTrigger.ExitWaitForEvent"/> — SEV received.
        /// Exit (interrupt): <see cref="PipelineTransitionTrigger.Interrupt"/> — immediate wakeup.
        ///
        /// C16: WFE/SEV modelled as first-class pipeline FSM state, not an opaque side effect.
        /// </summary>
        WaitForEvent    = 10,

        // ─────────────────────────────────────────────────────────────────────
        // Phase 2 (D19): Special pipeline stall states.
        // These model fine-grained pipeline hazards that previously lived in
        // a separate legacy FSM layer. They are introduced
        // here so that PipelineEventQueue can drive them as first-class
        // serialising event boundaries (D20).
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// All VTs in the pod affinity mask have executed POD_BARRIER and are
        /// waiting for the last straggler to arrive.
        ///
        /// Entry: <see cref="Pipeline.ClusterSyncEnterEvent"/> received while in Task.
        /// Exit (resume): <see cref="Pipeline.ClusterSyncExitEvent"/> — all cores reached barrier.
        /// Exit (interrupt): <see cref="PipelineTransitionTrigger.Interrupt"/> — deadlock escape.
        ///
        /// This is now the canonical runtime wait-for-cluster-sync state.
        /// </summary>
        WaitForClusterSync = 6,

        /// <summary>
        /// Pipeline stalled waiting for the hardware page-table walker to complete
        /// a TLB fill (2-level page walk, worst-case 8 cycles).
        ///
        /// Entry: <see cref="Pipeline.PtwWalkStartEvent"/> received while in Task.
        /// Exit (success): <see cref="Pipeline.PtwWalkCompleteEvent"/> — TLB filled; replay MEM.
        /// Exit (fault): <see cref="Pipeline.PtwWalkFaultEvent"/> — page fault; deliver trap.
        /// Exit (interrupt): <see cref="PipelineTransitionTrigger.Interrupt"/> — preempt.
        ///
        /// This is now the canonical runtime PTW stall state.
        /// </summary>
        PtwStall           = 7,

        /// <summary>
        /// VT's Fetch/Decode stages are clock-gated by the FSP power controller
        /// (dark silicon donor idle — hysteresis exceeded).  Execute/MEM/WB remain
        /// active for commit of previously stolen ops.
        ///
        /// Entry: <see cref="Pipeline.ClockGatedDonorEnterEvent"/> received while in Task.
        /// Exit (new work / ungate): <see cref="Pipeline.ClockGatedDonorExitEvent"/>.
        /// Exit (interrupt): <see cref="PipelineTransitionTrigger.Interrupt"/> — immediate wakeup.
        ///
        /// This is now the canonical runtime donor clock-gating state.
        /// </summary>
        ClockGatedDonor    = 8,
    }
}
