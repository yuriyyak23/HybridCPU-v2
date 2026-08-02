// ─────────────────────────────────────────────────────────────────────────────
// HybridCPU ISA v4 — CSR Access Control Policy
// Phase 08: CSR Layer
// ─────────────────────────────────────────────────────────────────────────────

namespace YAKSys_Hybrid_CPU.Core.Registers
{
    /// <summary>
    /// CSR access control policy for ISA v4.
    /// Determines read/write permissions based on current privilege level.
    /// </summary>
    public enum CsrAccessPolicy
    {
        /// <summary>Read-write in M-mode only.</summary>
        MachineReadWrite,

        /// <summary>Read-only in all privilege modes.</summary>
        ReadOnly,

        /// <summary>Read-write in M-mode and S-mode.</summary>
        SupervisorReadWrite,

        /// <summary>
        /// Read-only from program perspective, updated by hardware.
        /// (e.g., VTID — thread ID set by hardware on VT dispatch)
        /// </summary>
        HardwareUpdated,
    }

    /// <summary>
    /// Privilege levels for CSR access control.
    /// </summary>
    public enum PrivilegeLevel
    {
        /// <summary>User mode — most restricted.</summary>
        User = 0,
        /// <summary>Supervisor mode — OS kernel.</summary>
        Supervisor = 1,
        /// <summary>Machine mode — firmware / full access.</summary>
        Machine = 3,
    }
}
