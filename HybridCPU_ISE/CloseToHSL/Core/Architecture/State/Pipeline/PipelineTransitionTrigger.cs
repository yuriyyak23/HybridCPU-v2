namespace YAKSys_Hybrid_CPU.Core
{
    /// <summary>
    /// Triggers that drive pipeline FSM state transitions.
    /// Each trigger corresponds to a specific architectural event.
    /// </summary>
    public enum PipelineTransitionTrigger
    {
        /// <summary>
        /// Core power-on or hard reset. Transitions Reset → Task.
        /// </summary>
        Init,

        /// <summary>
        /// VMLAUNCH instruction issued. Transitions Task → VmEntry.
        /// </summary>
        VmLaunch,

        /// <summary>
        /// VMRESUME instruction issued. Transitions Task → VmEntry.
        /// </summary>
        VmResume,

        /// <summary>
        /// VM entry completed successfully. Transitions VmEntry → GuestExecution.
        /// </summary>
        EntryOk,

        /// <summary>
        /// VM entry failed (invalid VMCS, privilege violation, etc.).
        /// Transitions VmEntry → Task. VMEXIT reason is set in CSR.
        /// </summary>
        EntryFail,

        /// <summary>
        /// VMEXIT condition triggered (exception, interrupt, explicit VMEXIT).
        /// Transitions GuestExecution → VmExit.
        /// </summary>
        VmExitCond,

        /// <summary>
        /// VMXOFF instruction issued. Transitions GuestExecution → VmExit.
        /// </summary>
        VmxOff,

        /// <summary>
        /// VM exit handling complete; host state fully restored.
        /// Transitions VmExit → Task.
        /// </summary>
        ExitComplete,

        /// <summary>
        /// All virtual threads issued WFI (Wait For Interrupt), or explicit halt.
        /// Transitions Task → Halted.
        /// </summary>
        HaltAll,

        /// <summary>
        /// External interrupt or IPI received while halted.
        /// Transitions Halted → Task.
        /// </summary>
        Interrupt,

        // ─────────────────────────────────────────────────────────────────────
        // Phase 2 (D19/D20): Triggers for cluster sync, PTW stall, and
        // clock-gating states.  Added so that PipelineFsmGuard.Advance()
        // can consume Phase 2 PipelineEvents and drive the new FSM states.
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// POD_BARRIER executed; VT enters cluster synchronisation wait.
        /// Transitions Task → WaitForClusterSync.
        /// </summary>
        EnterClusterSync,

        /// <summary>
        /// All cores in the pod affinity mask reached the barrier.
        /// Transitions WaitForClusterSync → Task.
        /// </summary>
        ExitClusterSync,

        /// <summary>
        /// TLB miss detected; hardware PTW walk initiated; pipeline stalls.
        /// Transitions Task → PtwStall.
        /// </summary>
        PtwStart,

        /// <summary>
        /// Hardware PTW completed successfully; TLB filled; pipeline may replay memory access.
        /// Transitions PtwStall → Task.
        /// </summary>
        PtwComplete,

        /// <summary>
        /// Hardware PTW completed with page fault (page not present or permission denied).
        /// Transitions PtwStall → Task (fault delivery handled by trap mechanism).
        /// </summary>
        PtwFault,

        /// <summary>
        /// FspPowerController gates this VT's Fetch/Decode stages (idle donor detected).
        /// Transitions Task → ClockGatedDonor.
        /// </summary>
        EnterClockGate,

        /// <summary>
        /// FspPowerController ungates this VT (new work arrived or interrupt signalled).
        /// Transitions ClockGatedDonor → Task.
        /// </summary>
        ExitClockGate,

        // ─────────────────────────────────────────────────────────────────────
        // C14/C15 (Checklist): Trap entry/return serialising triggers.
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Synchronous trap (exception / ECALL / EBREAK) or asynchronous interrupt
        /// received; pipeline enters drain mode before transferring to the handler.
        /// Transitions Task → TrapPending.
        ///
        /// C14: TRAP_ENTRY as first-class serialising pipeline trigger.
        /// </summary>
        TrapEnter,

        /// <summary>
        /// MRET or SRET executed; pipeline drains and returns to the interrupted context.
        /// Transitions TrapPending → Task.
        ///
        /// C15: TRAP_RETURN (MRET/SRET) as first-class serialising pipeline trigger.
        /// </summary>
        TrapReturn,

        // ─────────────────────────────────────────────────────────────────────
        // C16 (Checklist): WFE/SEV barrier triggers.
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// WFE executed; VT stalls waiting for an SEV signal or an interrupt.
        /// Transitions Task → WaitForEvent.
        ///
        /// C16: WFE as first-class pipeline FSM trigger.
        /// </summary>
        EnterWaitForEvent,

        /// <summary>
        /// SEV received (or external interrupt); VT resumes normal execution.
        /// Transitions WaitForEvent → Task.
        ///
        /// C16: SEV/interrupt wakeup as first-class pipeline FSM trigger.
        /// </summary>
        ExitWaitForEvent,
    }
}
