// ─────────────────────────────────────────────────────────────────────────────
// HybridCPU ISA v4 — Diagnostics / Tracing
// Phase 11: Deterministic Replay and Trace Integration
// ─────────────────────────────────────────────────────────────────────────────

namespace YAKSys_Hybrid_CPU.Core
{
    /// <summary>
    /// Trace event kinds for HybridCPU ISA v4.
    /// Covers all architecturally observable instruction classes.
    /// <para>
    /// All events are stamped with <see cref="V4TraceEvent.BundleSerial"/>,
    /// <see cref="V4TraceEvent.VtId"/>, and <see cref="V4TraceEvent.FsmState"/>
    /// at the time of the event.
    /// </para>
    /// </summary>
    public enum TraceEventKind : byte
    {
        // ─── Bundle Events ──────────────────────────────────────────────────
        /// <summary>Bundle entered the pipeline dispatch stage.</summary>
        BundleDispatched    = 0x01,

        /// <summary>Bundle completed execution and retired.</summary>
        BundleRetired       = 0x02,

        /// <summary>Bundle replayed from a snapshot anchor.</summary>
        BundleReplayed      = 0x03,

        // ─── ALU Events ─────────────────────────────────────────────────────
        /// <summary>ALU instruction completed execution.</summary>
        AluExecuted         = 0x10,

        // ─── Memory Events ──────────────────────────────────────────────────
        /// <summary>Typed load instruction completed.</summary>
        LoadExecuted        = 0x20,

        /// <summary>Typed store instruction completed.</summary>
        StoreExecuted       = 0x21,

        /// <summary>FENCE or FENCE.I memory barrier completed.</summary>
        FenceExecuted       = 0x22,

        // ─── Control Flow Events ────────────────────────────────────────────
        /// <summary>Branch instruction executed and taken.</summary>
        BranchTaken         = 0x30,

        /// <summary>Branch instruction executed and not taken.</summary>
        BranchNotTaken      = 0x31,

        /// <summary>JAL or JALR executed.</summary>
        JumpExecuted        = 0x32,

        // ─── Atomic Events ──────────────────────────────────────────────────
        /// <summary>LR_W or LR_D: load-reserved acquired.</summary>
        LrExecuted          = 0x40,

        /// <summary>SC_W or SC_D: store-conditional succeeded (reservation held).</summary>
        ScSucceeded         = 0x41,

        /// <summary>SC_W or SC_D: store-conditional failed (reservation lost).</summary>
        ScFailed            = 0x42,

        /// <summary>AMO*_W: 32-bit atomic RMW completed.</summary>
        AmoWordExecuted     = 0x43,

        /// <summary>AMO*_D: 64-bit atomic RMW completed (new in v4).</summary>
        AmoDwordExecuted    = 0x44,

        // ─── System Events ──────────────────────────────────────────────────
        /// <summary>ECALL or EBREAK: synchronous trap taken.</summary>
        TrapTaken           = 0x50,

        /// <summary>MRET or SRET: privileged return from trap handler.</summary>
        PrivilegeReturn     = 0x51,

        /// <summary>WFI: virtual thread suspended waiting for interrupt.</summary>
        WfiEntered          = 0x52,

        // ─── CSR Events ─────────────────────────────────────────────────────
        /// <summary>CSRR* read operation completed.</summary>
        CsrRead             = 0x60,

        /// <summary>CSRR* write operation completed.</summary>
        CsrWrite            = 0x61,

        // ─── SMT/VT Events ──────────────────────────────────────────────────
        /// <summary>YIELD executed: voluntarily yielded VT slot.</summary>
        VtYield             = 0x70,

        /// <summary>WFE: VT waiting for event from another VT.</summary>
        VtWfe               = 0x71,

        /// <summary>SEV: event sent to all waiting VTs in the pod.</summary>
        VtSev               = 0x72,

        /// <summary>POD_BARRIER: all VTs in the pod reached the barrier (entered).</summary>
        PodBarrierEntered   = 0x73,

        /// <summary>POD_BARRIER: all VTs in the pod released from the barrier.</summary>
        PodBarrierExited    = 0x74,

        /// <summary>VT_BARRIER: all VTs in the core reached the barrier (entered).</summary>
        VtBarrierEntered    = 0x75,

        /// <summary>VT_BARRIER: all VTs in the core released from the barrier.</summary>
        VtBarrierExited     = 0x76,

        // ─── VMX Events ─────────────────────────────────────────────────────
        /// <summary>VMXON executed: VMX operation enabled.</summary>
        VmxOn               = 0x80,

        /// <summary>VMXOFF executed: VMX operation disabled.</summary>
        VmxOff              = 0x81,

        /// <summary>VMLAUNCH or VMRESUME: VM entry initiated.</summary>
        VmEntry             = 0x82,

        /// <summary>VM entry failed (invalid VMCS, privilege violation, etc.).</summary>
        VmEntryFailed       = 0x83,

        /// <summary>VM exit: guest returned control to host.</summary>
        VmExit              = 0x84,

        /// <summary>VMREAD executed: VMCS field read.</summary>
        VmcsRead            = 0x85,

        /// <summary>VMWRITE executed: VMCS field written.</summary>
        VmcsWrite           = 0x86,

        // ─── FSP Events ─────────────────────────────────────────────────────
        /// <summary>FSP pilfered a slot from a donor VT.</summary>
        FspPilfer           = 0x90,

        /// <summary>FSP boundary enforced: no slot pilfering across this bundle boundary.</summary>
        FspBoundary         = 0x91,

        // ─── Pipeline FSM Events ────────────────────────────────────────────
        /// <summary>Pipeline FSM state transition occurred.</summary>
        FsmTransition       = 0xA0,
    }
}
