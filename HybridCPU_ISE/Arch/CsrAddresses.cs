// ─────────────────────────────────────────────────────────────────────────────
// HybridCPU ISA v4 — Canonical CSR Address Space
// Phase 08: CSR Layer
// ─────────────────────────────────────────────────────────────────────────────

namespace YAKSys_Hybrid_CPU.Arch
{
    /// <summary>
    /// HybridCPU ISA v4 canonical CSR address assignments.
    /// <para>
    /// CSR addresses are 12-bit (0x000–0xFFF).  This class defines all groups:
    /// <list type="bullet">
    ///   <item>Supervisor-mode system registers (0x100–0x144)</item>
    ///   <item>Machine-mode privilege/system registers (0x300–0x344)</item>
    ///   <item>VT / SMT scheduling-visible registers (0x800–0x803)</item>
    ///   <item>Capability / configuration registers (0x810–0x814)</item>
    ///   <item>VMX virtualisation control registers (0x820–0x824)</item>
    ///   <item>Pod / NoC cluster registers (0xB00–0xB03)</item>
    ///   <item>Performance counters (0xC00–0xC06)</item>
    /// </list>
    /// </para>
    /// </summary>
    public static class CsrAddresses
    {
        // ═══════════════════════════════════════════════════════════
        // Privilege / System — Machine-mode (0x300–0x344)
        // ═══════════════════════════════════════════════════════════

        /// <summary>Machine status register.</summary>
        public const ushort Mstatus  = 0x300;
        /// <summary>ISA and extensions.</summary>
        public const ushort Misa     = 0x301;
        /// <summary>Machine interrupt enable.</summary>
        public const ushort Mie      = 0x304;
        /// <summary>Machine trap-handler base address.</summary>
        public const ushort Mtvec    = 0x305;
        /// <summary>Machine scratch register.</summary>
        public const ushort Mscratch = 0x340;
        /// <summary>Machine exception program counter.</summary>
        public const ushort Mepc     = 0x341;
        /// <summary>Machine trap cause.</summary>
        public const ushort Mcause   = 0x342;
        /// <summary>Machine bad address or instruction.</summary>
        public const ushort Mtval    = 0x343;
        /// <summary>Machine interrupt pending.</summary>
        public const ushort Mip      = 0x344;
        /// <summary>Machine power-state status/control CSR.</summary>
        public const ushort MpowerState = 0x345;
        /// <summary>Machine performance-level status/control CSR.</summary>
        public const ushort MperfLevel  = 0x346;

        // ═══════════════════════════════════════════════════════════
        // Supervisor Mode — S-mode (0x100–0x144)
        // ═══════════════════════════════════════════════════════════

        /// <summary>Supervisor status register.</summary>
        public const ushort Sstatus  = 0x100;
        /// <summary>Supervisor interrupt enable.</summary>
        public const ushort Sie      = 0x104;
        /// <summary>Supervisor trap-handler base address.</summary>
        public const ushort Stvec    = 0x105;
        /// <summary>Supervisor scratch register.</summary>
        public const ushort Sscratch = 0x140;
        /// <summary>Supervisor exception program counter.</summary>
        public const ushort Sepc     = 0x141;
        /// <summary>Supervisor trap cause.</summary>
        public const ushort Scause   = 0x142;
        /// <summary>Supervisor bad address or instruction.</summary>
        public const ushort Stval    = 0x143;
        /// <summary>Supervisor interrupt pending.</summary>
        public const ushort Sip      = 0x144;

        // ═══════════════════════════════════════════════════════════
        // VT / SMT Scheduling-Visible Architectural State (0x800–0x803)
        // ═══════════════════════════════════════════════════════════

        /// <summary>Current virtual thread ID (0–3 for 4-way SMT).</summary>
        public const ushort VtId     = 0x800;
        /// <summary>Virtual thread active mask (bitmask of active VTs).</summary>
        public const ushort VtMask   = 0x801;
        /// <summary>VT execution status (running/waiting/blocked).</summary>
        public const ushort VtStatus = 0x802;
        /// <summary>VT-local trap cause.</summary>
        public const ushort VtCause  = 0x803;

        // ═══════════════════════════════════════════════════════════
        // Capability / Configuration (0x810–0x814)
        // ═══════════════════════════════════════════════════════════

        /// <summary>ISA capability bits (v4 surface indicator).</summary>
        public const ushort IsaCaps    = 0x810;
        /// <summary>Number of lanes and lane capability bits.</summary>
        public const ushort LaneCaps   = 0x811;
        /// <summary>Bundle width and bundle capability bits.</summary>
        public const ushort BundleCaps = 0x812;
        /// <summary>SafetyMask / legality capability discovery.</summary>
        public const ushort SafetyCaps = 0x813;
        /// <summary>Memory model capability bits.</summary>
        public const ushort MemCaps    = 0x814;

        // ═══════════════════════════════════════════════════════════
        // VMX (0x820–0x824)
        // ═══════════════════════════════════════════════════════════

        /// <summary>VMX enable/disable control. Set by VMXON/VMXOFF instruction plane.</summary>
        public const ushort VmxEnable    = 0x820;
        /// <summary>VMX capability discovery register.</summary>
        public const ushort VmxCaps      = 0x821;
        /// <summary>VMX control configuration register.</summary>
        public const ushort VmxControl   = 0x822;
        /// <summary>VMEXIT reason code (last exit).</summary>
        public const ushort VmxExitReason = 0x823;
        /// <summary>VMEXIT qualification (additional exit information).</summary>
        public const ushort VmxExitQual  = 0x824;
        /// <summary>Vector exception mask control CSR.</summary>
        public const ushort VexcpMask = 0x900;
        /// <summary>Vector exception priority control CSR.</summary>
        public const ushort VexcpPri  = 0x901;

        // ═══════════════════════════════════════════════════════════
        // Performance Counters (0xC00–0xC06)
        // ═══════════════════════════════════════════════════════════

        /// <summary>Cycle counter.</summary>
        public const ushort Cycle      = 0xC00;
        /// <summary>Retired instruction bundles.</summary>
        public const ushort BundleRet  = 0xC01;
        /// <summary>Retired individual instructions.</summary>
        public const ushort InstrRet   = 0xC02;
        /// <summary>VMEXIT event counter.</summary>
        public const ushort VmExitCnt  = 0xC03;
        /// <summary>Barrier instruction counter.</summary>
        public const ushort BarrierCnt = 0xC04;
        /// <summary>FSP steal event counter.</summary>
        public const ushort StealCnt   = 0xC05;
        /// <summary>Deterministic replay diagnostics counter.</summary>
        public const ushort ReplayCnt  = 0xC06;
    }
}
