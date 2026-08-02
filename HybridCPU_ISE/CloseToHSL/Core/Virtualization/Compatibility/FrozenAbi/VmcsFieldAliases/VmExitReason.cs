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

        /// <summary>Nested page-table permission or presence violation.</summary>
        EptViolation = 48,

        /// <summary>Nested page-table entry was architecturally malformed.</summary>
        EptMisconfiguration = 49,

        /// <summary>Guest executed VMFUNC and the selected leaf required root fallback.</summary>
        VmFunc = 59,

        /// <summary>Guest instruction opcode matched a descriptor-owned intercept bitmap.</summary>
        InstructionIntercept = 60,

        /// <summary>Guest CSR access matched a descriptor-owned intercept bitmap.</summary>
        CsrIntercept = 61,

        /// <summary>Guest memory access matched a descriptor-owned intercept range.</summary>
        MemoryIntercept = 62,

        /// <summary>Guest VMX operation matched a descriptor-owned intercept bitmap.</summary>
        VmxOperationIntercept = 63,

        /// <summary>Guest lane operation matched a descriptor-owned intercept bitmap.</summary>
        LaneOperationIntercept = 64,

        /// <summary>Virtual timer produced a guest-directed event injection.</summary>
        VirtualTimer = 65,

        /// <summary>Root-owned scheduling timer expired and forced a compatibility exit.</summary>
        VmxPreemptionTimerExpired = 66,

        /// <summary>Virtual interrupt fabric delivered or routed an event requiring root handling.</summary>
        VirtualInterrupt = 67,

        /// <summary>Guest Lane6 DMA descriptor failed validation before host evidence materialization.</summary>
        DmaDescriptorFault = 68,

        /// <summary>Guest Lane6 DMA access failed VMID/domain/device IOMMU translation.</summary>
        IommuFault = 69,

        /// <summary>Guest Lane6 DMA access failed read/write permission policy.</summary>
        DmaPermissionFault = 70,

        /// <summary>Guest Lane6 DMA operation was aborted by a non-replayable isolation fault.</summary>
        DmaAbort = 71,

        /// <summary>Guest Lane6 DMA operation hit a transient replay condition before commit.</summary>
        DmaReplay = 72,

        /// <summary>Guest vector execution produced a descriptor-owned vector exception.</summary>
        VectorException = 73,

        /// <summary>Guest stream descriptor failed descriptor-owned vector/stream validation.</summary>
        StreamDescriptorFault = 74,

        /// <summary>Guest stream execution must replay because stream validation evidence is stale.</summary>
        StreamReplayRequired = 75,

        /// <summary>Guest Lane7 accelerator virtual token or handle validation failed.</summary>
        Lane7TokenFault = 76,

        /// <summary>Guest Lane7 accelerator backend binding is unavailable or must be rebuilt.</summary>
        Lane7BackendUnavailable = 77,

        /// <summary>Guest Lane7 operation hit quota or transient pressure and must fall back or replay.</summary>
        Lane7QuotaExceeded = 78,

        /// <summary>Guest operation violated root-owned security policy.</summary>
        SecurityPolicyViolation = 79,
    }
}
