// ─────────────────────────────────────────────────────────────────────────────
// HybridCPU ISA v4 — VMEXIT Reason Codes
// Phase 09: VMX Subsystem
// ─────────────────────────────────────────────────────────────────────────────

namespace YAKSys_Hybrid_CPU.Core
{
    /// <summary>
    /// Reason codes for VM-Exit events.
    /// Written to the <see cref="Arch.CsrAddresses.VmxExitReason"/> CSR
    /// on every VM-Exit so that host software can determine the exit cause.
    /// </summary>
    public enum VmExitReason : uint
    {
        /// <summary>No exit — default / sentinel.</summary>
        None = 0,

        /// <summary>External interrupt received while in guest mode.</summary>
        ExternalInterrupt = 1,

        /// <summary>Triple-fault in guest (unrecoverable).</summary>
        TripleFault = 2,

        /// <summary>INIT signal delivered.</summary>
        InitSignal = 3,

        /// <summary>Guest executed HLT instruction.</summary>
        Hlt = 12,

        /// <summary>Guest executed VMCALL.</summary>
        VmCall = 18,

        /// <summary>Host issued VMXOFF while guest was executing.</summary>
        VmxOff = 26,

        /// <summary>Invalid guest state detected during VM-Entry.</summary>
        InvalidGuestState = 33,

        /// <summary>MSR loading failed during VM-Entry.</summary>
        EntryFailMsrLoading = 34,

        /// <summary>Machine-check event during VM-Entry.</summary>
        EntryFailMachineCheck = 41,
    }
}
