using System;

namespace YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps
{
    // ─────────────────────────────────────────────────────────────────────────
    // InternalOpDataType — scalar operand width
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Operand width for a scalar <see cref="InternalOp"/>.
    /// Determines how many bytes the operation reads/writes.
    /// </summary>
    public enum InternalOpDataType : byte
    {
        Byte  = 0,
        Half  = 1,
        Word  = 2,
        DWord = 3,
    }

    // ─────────────────────────────────────────────────────────────────────────
    // InternalOpFlags — atomic ordering, signed/unsigned, shift type
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Modifier flags that qualify an <see cref="InternalOp"/> without changing
    /// which architectural state it reads or writes.
    /// </summary>
    [Flags]
    public enum InternalOpFlags : byte
    {
        None             = 0,
        /// <summary>Arithmetic operation treats operands as signed integers.</summary>
        Signed           = 1 << 0,
        /// <summary>Atomic operation carries acquire ordering (A-flag in RISC-V AMO).</summary>
        AcquireOrdering  = 1 << 1,
        /// <summary>Atomic operation carries release ordering (R-flag in RISC-V AMO).</summary>
        ReleaseOrdering  = 1 << 2,
        /// <summary>Shift is arithmetic (sign-extending) rather than logical.</summary>
        ArithmeticShift  = 1 << 3,
    }

    // ─────────────────────────────────────────────────────────────────────────
    // InternalOpKind — the operation taxonomy
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Classifies an <see cref="InternalOp"/> into an abstract operation kind,
    /// independent of the originating ISA opcode.
    /// </summary>
    public enum InternalOpKind : byte
    {
        // ── ALU reg-reg ───────────────────────────────────────────────────────
        Add    =  0,
        Sub    =  1,
        And    =  2,
        Or     =  3,
        Xor    =  4,
        Sll    =  5,
        Srl    =  6,
        Sra    =  7,
        Slt    =  8,
        Sltu   =  9,

        // ── ALU M-extension ───────────────────────────────────────────────────
        Mul    = 10,
        MulH   = 11,
        MulHu  = 12,
        MulHsu = 13,
        Div    = 14,
        Divu   = 15,
        Rem    = 16,
        Remu   = 17,

        // ── ALU immediate ─────────────────────────────────────────────────────
        AddI   = 20,
        AndI   = 21,
        OrI    = 22,
        XorI   = 23,
        SllI   = 24,
        SrlI   = 25,
        SraI   = 26,
        SltI   = 27,
        SltiU  = 28,
        Lui    = 29,
        Auipc  = 30,

        // ── Memory ────────────────────────────────────────────────────────────
        Load   = 40,
        Store  = 41,

        // ── Control flow ──────────────────────────────────────────────────────
        Jal    = 50,
        Jalr   = 51,
        Branch = 52,

        // ── Atomics ───────────────────────────────────────────────────────────
        LrW      = 60,
        ScW      = 61,
        LrD      = 62,
        ScD      = 63,
        AmoWord  = 64,
        AmoDword = 65,

        // ── CSR ───────────────────────────────────────────────────────────────
        CsrReadWrite = 70,
        CsrReadSet   = 71,
        CsrReadClear = 72,
        CsrClear     = 73,

        // ── SMT/VT ────────────────────────────────────────────────────────────
        Yield      = 80,
        Wfe        = 81,
        Sev        = 82,
        PodBarrier = 83,
        VtBarrier  = 84,
        Fence      = 85,
        FenceI     = 86,
        Ecall      = 87,
        Ebreak     = 88,
        Mret       = 89,
        Sret       = 90,
        Wfi        = 91,
        Interrupt  = 92,
        InterruptReturn = 93,

        // ── VMX compute (explicit VMX instruction surface) ───────────────────
        VmxOn    = 100,
        VmxOff   = 101,
        VmLaunch = 102,
        VmResume = 103,
        VmRead   = 104,
        VmWrite  = 105,
        VmClear  = 106,
        VmPtrLd  = 107,
    }

    // ─────────────────────────────────────────────────────────────────────────
    // E25/E26 (Checklist): InternalOpCategory — operation group taxonomy
    // Separates computation / memory / control-flow / atomics / CSR from
    // the architectural event categories (SysEvent / TrapEvent / VmxEvent).
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// High-level category of an <see cref="InternalOp"/>.
    ///
    /// E25: Canonical InternalOp model independent of <c>InstructionEnum</c>.
    /// E26: Explicit split between computation ops (ALU / Memory / Branch /
    ///      Atomic / CSR) and architectural event categories (SysEvent /
    ///      TrapEvent / VmxEvent).
    /// </summary>
    public enum InternalOpCategory : byte
    {
        /// <summary>Integer ALU computation (reg-reg, reg-imm, M-extension).</summary>
        Computation  = 0,

        /// <summary>Memory load or store (scalar or atomic).</summary>
        MemoryAccess = 1,

        /// <summary>Control-flow transfer (branch, JAL, JALR).</summary>
        ControlFlow  = 2,

        /// <summary>Atomic read-modify-write operation (LR/SC, AMO).</summary>
        Atomic       = 3,

        /// <summary>Control/status-register access.</summary>
        Csr          = 4,

        /// <summary>
        /// SMT/VT scheduling event (YIELD, WFE, SEV, POD_BARRIER, VT_BARRIER).
        /// E26: Explicitly separate from computation categories.
        /// </summary>
        SysEvent     = 5,

        /// <summary>
        /// VMX control / VMCS / VM-entry / VM-exit operation.
        /// E26: Explicitly separate from computation categories.
        /// </summary>
        VmxEvent     = 6,
    }

    // ─────────────────────────────────────────────────────────────────────────
    // InternalOp — canonical contract (V5 Phase 1 / V6 E25-E28)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Canonical InternalOp contract — Phase 1 / V6 audit (E25–E28).
    ///
    /// Represents a single unit of computation for the execution engine.
    /// No scheduling policy. No hints. No ISA-origin information.
    ///
    /// CONTRACT RULE: execution result is 100% determined by <see cref="InternalOp"/>
    /// fields plus architectural state.  No metadata field may affect the result.
    ///
    /// DOES NOT carry:
    /// <list type="bullet">
    ///   <item>Scheduling hints (branch prediction, stealability, locality, thermal)</item>
    ///   <item>FSP stealability</item>
    ///   <item>Preferred VT</item>
    ///   <item>Any policy field</item>
    /// </list>
    ///
    /// Hint/policy information belongs exclusively in <c>SlotMetadata</c> /
    /// <c>BundleMetadata</c>.
    ///
    /// V6 additions (E25–E28):
    /// <list type="bullet">
    ///   <item>E25/E26: <see cref="Category"/> — group taxonomy independent of ISA opcode.</item>
    ///   <item>E27: <see cref="IsMemoryRead"/>, <see cref="IsMemoryWrite"/>,
    ///         <see cref="IsControlTransfer"/>, <see cref="IsExceptionPotential"/> — explicit effect descriptors.</item>
    ///   <item>E28: <see cref="IsSerializing"/>, <see cref="RequiresPipelineFlush"/>,
    ///         <see cref="ForbidsFsp"/>, <see cref="ForbidsSmtInjection"/> — serialisation semantics.</item>
    /// </list>
    /// </summary>
    public sealed record InternalOp
    {
        /// <summary>The abstract operation class — determines which execution unit handles this op.</summary>
        public required InternalOpKind Kind { get; init; }

        /// <summary>Source register 1 index. -1 = unused.</summary>
        public int Rs1 { get; init; } = -1;

        /// <summary>Source register 2 index. -1 = unused.</summary>
        public int Rs2 { get; init; } = -1;

        /// <summary>Destination register index. -1 = no destination.</summary>
        public int Rd  { get; init; } = -1;

        /// <summary>Immediate value (sign-extended). 0 when not applicable.</summary>
        public long Immediate { get; init; }

        /// <summary>Operand width. Defaults to <see cref="InternalOpDataType.DWord"/>.</summary>
        public InternalOpDataType DataType { get; init; } = InternalOpDataType.DWord;

        /// <summary>
        /// CSR address for CSR operations.
        /// <see langword="null"/> for all non-CSR operations.
        /// </summary>
        public ushort? CsrTarget { get; init; }

        /// <summary>Atomic ordering, signed/unsigned, shift-type qualifiers.</summary>
        public InternalOpFlags Flags { get; init; }

        // ─────────────────────────────────────────────────────────────────────
        // E25/E26: Category — computed from Kind (no ISA opcode dependency)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// High-level operation category derived solely from <see cref="Kind"/>.
        /// Never depends on the originating ISA opcode.
        ///
        /// E25/E26: Canonical category independent of <c>InstructionEnum</c>;
        /// explicitly separates computation from architectural-event categories.
        /// </summary>
        public InternalOpCategory Category => Kind switch
        {
            // ── Computation (ALU, M-ext, immediate) ──────────────────────────
            InternalOpKind.Add  or InternalOpKind.Sub  or InternalOpKind.And  or
            InternalOpKind.Or   or InternalOpKind.Xor  or InternalOpKind.Sll  or
            InternalOpKind.Srl  or InternalOpKind.Sra  or InternalOpKind.Slt  or
            InternalOpKind.Sltu or InternalOpKind.Mul  or InternalOpKind.MulH or
            InternalOpKind.MulHu or InternalOpKind.MulHsu or InternalOpKind.Div or
            InternalOpKind.Divu or InternalOpKind.Rem  or InternalOpKind.Remu or
            InternalOpKind.AddI or InternalOpKind.AndI or InternalOpKind.OrI  or
            InternalOpKind.XorI or InternalOpKind.SllI or InternalOpKind.SrlI or
            InternalOpKind.SraI or InternalOpKind.SltI or InternalOpKind.SltiU or
            InternalOpKind.Lui  or InternalOpKind.Auipc
                => InternalOpCategory.Computation,

            // ── Memory access ────────────────────────────────────────────────
            InternalOpKind.Load or InternalOpKind.Store
                => InternalOpCategory.MemoryAccess,

            // ── Control flow ─────────────────────────────────────────────────
            InternalOpKind.Jal or InternalOpKind.Jalr or InternalOpKind.Branch
                => InternalOpCategory.ControlFlow,

            // ── Atomics ──────────────────────────────────────────────────────
            InternalOpKind.LrW  or InternalOpKind.ScW  or
            InternalOpKind.LrD  or InternalOpKind.ScD  or
            InternalOpKind.AmoWord or InternalOpKind.AmoDword
                => InternalOpCategory.Atomic,

            // ── CSR ──────────────────────────────────────────────────────────
            InternalOpKind.CsrReadWrite or InternalOpKind.CsrReadSet or InternalOpKind.CsrReadClear or
            InternalOpKind.CsrClear
                => InternalOpCategory.Csr,

            // ── SMT/VT system events ─────────────────────────────────────────
            InternalOpKind.Yield or InternalOpKind.Wfe or InternalOpKind.Sev or
            InternalOpKind.PodBarrier or InternalOpKind.VtBarrier or
            InternalOpKind.Fence or InternalOpKind.FenceI or InternalOpKind.Ecall or
            InternalOpKind.Ebreak or InternalOpKind.Mret or InternalOpKind.Sret or
            InternalOpKind.Wfi or InternalOpKind.Interrupt or InternalOpKind.InterruptReturn
                => InternalOpCategory.SysEvent,

            // ── VMX data-plane events ────────────────────────────────────────
            InternalOpKind.VmxOn or InternalOpKind.VmxOff or
            InternalOpKind.VmLaunch or InternalOpKind.VmResume or
            InternalOpKind.VmRead or InternalOpKind.VmWrite or
            InternalOpKind.VmClear or InternalOpKind.VmPtrLd
                => InternalOpCategory.VmxEvent,

            _ => InternalOpCategory.Computation,
        };

        // ─────────────────────────────────────────────────────────────────────
        // E27: Explicit effect descriptors
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// <see langword="true"/> if this operation reads from memory.
        ///
        /// E27: Explicit effect descriptor; does not require opcode-range inspection.
        /// </summary>
        public bool IsMemoryRead => Kind is InternalOpKind.Load
            or InternalOpKind.LrW or InternalOpKind.LrD
            or InternalOpKind.AmoWord or InternalOpKind.AmoDword;

        /// <summary>
        /// <see langword="true"/> if this operation writes to memory.
        ///
        /// E27: Explicit effect descriptor; does not require opcode-range inspection.
        /// </summary>
        public bool IsMemoryWrite => Kind is InternalOpKind.Store
            or InternalOpKind.ScW or InternalOpKind.ScD
            or InternalOpKind.AmoWord or InternalOpKind.AmoDword;

        /// <summary>
        /// <see langword="true"/> if this operation may change the program counter
        /// to a non-sequential address (branch, jump, call, return).
        ///
        /// E27: Explicit ControlTransfer effect descriptor.
        /// </summary>
        public bool IsControlTransfer => Category == InternalOpCategory.ControlFlow;

        /// <summary>
        /// <see langword="true"/> if this operation may raise an exception (e.g. divide-by-zero,
        /// misaligned access, page fault) or generates a trap boundary event.
        ///
        /// E27: Explicit ExceptionPotential effect descriptor.
        /// </summary>
        public bool IsExceptionPotential => Kind is
            InternalOpKind.Div or InternalOpKind.Divu or
            InternalOpKind.Rem or InternalOpKind.Remu or
            InternalOpKind.Load or InternalOpKind.Store or
            InternalOpKind.LrW or InternalOpKind.LrD or
            InternalOpKind.ScW or InternalOpKind.ScD or
            InternalOpKind.AmoWord or InternalOpKind.AmoDword or
            InternalOpKind.Jal or InternalOpKind.Jalr or
            InternalOpKind.CsrReadWrite or InternalOpKind.CsrReadSet or InternalOpKind.CsrReadClear or
            InternalOpKind.CsrClear or
            InternalOpKind.Ecall or InternalOpKind.Ebreak or InternalOpKind.Mret or
            InternalOpKind.Sret or InternalOpKind.Interrupt or InternalOpKind.InterruptReturn;

        /// <summary>
        /// <see langword="true"/> if this operation has memory ordering semantics.
        /// Atomics with Acquire/Release flags always have ordering.
        ///
        /// E27: Explicit HasOrdering effect descriptor.
        /// </summary>
        public bool HasOrdering =>
            Category == InternalOpCategory.Atomic &&
            (Flags & (InternalOpFlags.AcquireOrdering | InternalOpFlags.ReleaseOrdering)) != 0;

        // ─────────────────────────────────────────────────────────────────────
        // E28: Serialisation semantics (pipeline scheduling contract)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// <see langword="true"/> if this operation is fully serialising:
        /// all preceding ops must retire and all following ops must wait
        /// until this op completes.
        ///
        /// E28: Serialisation is part of InternalOp semantics, not implicit.
        /// </summary>
        public bool IsSerializing => Kind is
            InternalOpKind.CsrReadWrite or InternalOpKind.CsrReadSet or InternalOpKind.CsrReadClear or
            InternalOpKind.CsrClear or
            InternalOpKind.Wfe or InternalOpKind.Sev or
            InternalOpKind.PodBarrier or InternalOpKind.VtBarrier or
            InternalOpKind.Fence or InternalOpKind.FenceI or InternalOpKind.Ecall or
            InternalOpKind.Ebreak or InternalOpKind.Mret or InternalOpKind.Sret or
            InternalOpKind.Wfi or InternalOpKind.Interrupt or InternalOpKind.InterruptReturn or
            InternalOpKind.VmxOn or InternalOpKind.VmxOff or
            InternalOpKind.VmLaunch or InternalOpKind.VmResume or
            InternalOpKind.VmRead or InternalOpKind.VmWrite or
            InternalOpKind.VmClear or InternalOpKind.VmPtrLd;

        /// <summary>
        /// <see langword="true"/> if pipeline must be fully flushed before
        /// this operation can retire (instruction cache included for I-fence class).
        ///
        /// E28: RequiresPipelineFlush is part of InternalOp semantics.
        /// </summary>
        public bool RequiresPipelineFlush => Kind is
            InternalOpKind.PodBarrier or InternalOpKind.VtBarrier or
            InternalOpKind.FenceI or InternalOpKind.Ecall or InternalOpKind.Ebreak or
            InternalOpKind.Mret or InternalOpKind.Sret or InternalOpKind.Interrupt or
            InternalOpKind.InterruptReturn or
            InternalOpKind.VmxOn or InternalOpKind.VmxOff or
            InternalOpKind.VmLaunch or InternalOpKind.VmResume or
            InternalOpKind.VmRead or InternalOpKind.VmWrite or
            InternalOpKind.VmClear or InternalOpKind.VmPtrLd;

        /// <summary>
        /// <see langword="true"/> if the retained compatibility alias for
        /// bundle densification / cross-thread slot injection is forbidden
        /// for any slot in the same bundle as this operation.
        ///
        /// E28: <see cref="ForbidsFsp"/> is a retained compatibility alias
        /// over the canonical serialization boundary.
        /// </summary>
        public bool ForbidsFsp => IsSerializing;

        /// <summary>
        /// <see langword="true"/> if SMT cross-thread injection into empty slots
        /// in the same bundle is forbidden.
        ///
        /// E28: ForbidsSmtInjection is part of InternalOp semantics.
        /// </summary>
        public bool ForbidsSmtInjection => IsSerializing;
    }
}
