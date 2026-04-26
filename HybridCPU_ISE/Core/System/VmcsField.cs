// ─────────────────────────────────────────────────────────────────────────────
// HybridCPU ISA v4 — VMCS Field Identifiers
// Phase 09: VMX Subsystem
// ─────────────────────────────────────────────────────────────────────────────

namespace YAKSys_Hybrid_CPU.Core
{
    /// <summary>
    /// Identifies fields within a VMCS (VM Control Structure).
    /// Field values are used as indices by <c>VMREAD</c> and <c>VMWRITE</c>
    /// instructions to access specific VMCS state.
    /// </summary>
    public enum VmcsField : ushort
    {
        // ═══════════════════════════════════════════════════════════
        // Guest state area (0x00–0x1F)
        // ═══════════════════════════════════════════════════════════

        /// <summary>Guest program counter.</summary>
        GuestPc = 0,
        /// <summary>Guest stack pointer.</summary>
        GuestSp = 1,
        /// <summary>Guest flags / status register.</summary>
        GuestFlags = 2,
        /// <summary>Guest control register 0.</summary>
        GuestCr0 = 3,
        /// <summary>Guest page-table base register.</summary>
        GuestCr3 = 4,
        /// <summary>Guest control register 4.</summary>
        GuestCr4 = 5,

        // ═══════════════════════════════════════════════════════════
        // Host state area (0x20–0x3F)
        // ═══════════════════════════════════════════════════════════

        /// <summary>Host program counter (resume address after VM-Exit).</summary>
        HostPc = 32,
        /// <summary>Host stack pointer.</summary>
        HostSp = 33,
        /// <summary>Host flags / status register.</summary>
        HostFlags = 34,
        /// <summary>Host control register 0.</summary>
        HostCr0 = 35,
        /// <summary>Host page-table base register.</summary>
        HostCr3 = 36,

        // ═══════════════════════════════════════════════════════════
        // VM execution controls (0x40–0x4F)
        // ═══════════════════════════════════════════════════════════

        /// <summary>Pin-based VM execution controls.</summary>
        PinBasedControls = 64,
        /// <summary>Processor-based VM execution controls.</summary>
        ProcBasedControls = 65,
        /// <summary>VM-Exit controls.</summary>
        ExitControls = 66,
        /// <summary>VM-Entry controls.</summary>
        EntryControls = 67,

        // ═══════════════════════════════════════════════════════════
        // J53 (Checklist): Nested translation / MMU fields (0x50–0x5F)
        // Links VMX transitions to nested page-table (EPT) state.
        // The hypervisor sets EptPointer before VMLAUNCH / VMRESUME.
        // The hardware page-table walker uses it for second-level
        // (guest-physical → host-physical) address translation.
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// Extended Page Tables pointer (EPTP).
        /// Physical address of the EPT PML4 table used for nested
        /// (guest-physical → host-physical) address translation.
        ///
        /// J53: Links VMX entry/exit to nested-MMU page-table state.
        /// </summary>
        EptPointer = 80,

        /// <summary>
        /// Virtual Processor Identifier (VPID).
        /// Used to tag TLB entries for guest virtual addresses so that
        /// TLB invalidation on VM-exit/entry is scoped to this VPID.
        ///
        /// J53: VPID is the nested-MMU companion to EptPointer.
        /// </summary>
        Vpid = 81,

        /// <summary>
        /// Secondary processor-based VM execution controls.
        /// Bit 1 (EnableEpt) must be set for EPT to take effect.
        /// Bit 5 (EnableVpid) must be set for VPID tagging to take effect.
        ///
        /// J53: Nested-MMU enablement control in the execution-control area.
        /// </summary>
        SecondaryProcControls = 82,

        /// <summary>
        /// Guest CR3 target count.
        /// When non-zero, specifies a list of guest-CR3 values that do NOT
        /// trigger a VM-exit on MOV-to-CR3 (used with EPT-based guests).
        ///
        /// J53: Nested-MMU optimisation for guest page-table updates.
        /// </summary>
        Cr3TargetCount = 83,

        // ═══════════════════════════════════════════════════════════
        // VM exit information (0x60–0x6F)
        // ═══════════════════════════════════════════════════════════

        /// <summary>VM-Exit reason code.</summary>
        ExitReason = 96,
        /// <summary>VM-Exit qualification (additional exit info).</summary>
        ExitQualification = 97,

        // ═══════════════════════════════════════════════════════════
        // J53 (Checklist): Nested-MMU exit information (0x70–0x7F)
        // Populated by hardware on EPT violations so the VMM can
        // handle guest-physical faults without a full MMU emulation.
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// Guest-physical address that triggered an EPT violation.
        /// Populated by hardware on a VM-exit with reason EptViolation.
        ///
        /// J53: Nested-MMU fault address reported to the VMM.
        /// </summary>
        GuestPhysicalAddress = 112,

        /// <summary>
        /// EPT violation qualification flags.
        /// Bit 0 = read access, bit 1 = write access, bit 2 = instruction fetch.
        ///
        /// J53: Nested-MMU fault qualifier (maps to Intel Vol. 3C §28.2).
        /// </summary>
        EptViolationQualification = 113,
    }
}
