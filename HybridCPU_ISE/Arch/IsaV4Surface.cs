using System;
using System.Collections.Generic;

namespace YAKSys_Hybrid_CPU.Arch
{
    /// <summary>
    /// Canonical HybridCPU ISA v4 surface declaration.
    /// <para>
    /// This file is the single authoritative source for which instruction classes and
    /// mnemonics are part of the mandatory ISA v4 core, which are assembler pseudo-ops
    /// only, and which are optional extension instructions.
    /// </para>
    /// <para>
    /// All decoders, IR builders, and execution units must conform to this surface.
    /// No instruction listed in <see cref="ProhibitedOpcodes"/> may appear as a
    /// hardware opcode in any ISE subsystem.
    /// </para>
    /// </summary>
    public static class IsaV4Surface
    {
        // ─── Mandatory Core Instruction Classes ───────────────────────────────────

        /// <summary>
        /// All canonical ISA v4 instruction class names that form the mandatory core.
        /// These map to <c>InstructionClass</c> enum values defined in Phase 02.
        /// </summary>
        public static readonly IReadOnlySet<string> MandatoryCoreClasses = new HashSet<string>
        {
            "ScalarAlu",     // Register-register and immediate integer ALU
            "Memory",        // Typed scalar loads and stores
            "ControlFlow",   // JAL, JALR, conditional branches
            "Atomic",        // LR/SC and AMO (word and doubleword)
            "System",        // FENCE, ECALL, EBREAK, MRET, SRET, WFI
            "Csr",           // CSRRW/CSRRS/CSRRC and immediate variants
            "SmtVt",         // YIELD, WFE, SEV, POD_BARRIER, VT_BARRIER
            "Vmx",           // VMXON/VMXOFF/VMLAUNCH/VMRESUME/VMREAD/VMWRITE/VMCLEAR/VMPTRLD
        };

        // ─── Mandatory Core Opcodes ────────────────────────────────────────────────

        /// <summary>
        /// Complete canonical ISA v4 opcode surface — all hardware mnemonics that
        /// must be present in a conforming ISA v4 implementation.
        /// </summary>
        public static readonly IReadOnlySet<string> MandatoryCoreOpcodes = new HashSet<string>
        {
            // Scalar integer ALU (reg-reg)
            "ADD", "SUB", "AND", "OR", "XOR", "SLL", "SRL", "SRA", "SLT", "SLTU",
            // Scalar integer multiply
            "MUL", "MULH", "MULHU", "MULHSU",
            // Scalar integer divide
            "DIV", "DIVU", "REM", "REMU",
            // Scalar immediate
            "ADDI", "ANDI", "ORI", "XORI", "SLLI", "SRLI", "SRAI", "SLTI", "SLTIU",
            // Upper immediate
            "LUI", "AUIPC",
            // Typed loads
            "LB", "LBU", "LH", "LHU", "LW", "LWU", "LD",
            // Typed stores
            "SB", "SH", "SW", "SD",
            // Control flow
            "JAL", "JALR", "BEQ", "BNE", "BLT", "BGE", "BLTU", "BGEU",
            // Atomics — LR/SC word and doubleword
            "LR_W", "SC_W", "LR_D", "SC_D",
            // Atomics — AMO word
            "AMOSWAP_W", "AMOADD_W", "AMOXOR_W", "AMOAND_W", "AMOOR_W",
            "AMOMIN_W", "AMOMAX_W", "AMOMINU_W", "AMOMAXU_W",
            // Atomics — AMO doubleword
            "AMOSWAP_D", "AMOADD_D", "AMOXOR_D", "AMOAND_D", "AMOOR_D",
            "AMOMIN_D", "AMOMAX_D", "AMOMINU_D", "AMOMAXU_D",
            // System / privilege
            "FENCE", "FENCE_I", "ECALL", "EBREAK", "MRET", "SRET", "WFI",
            // CSR instruction plane
            "CSRRW", "CSRRS", "CSRRC", "CSRRWI", "CSRRSI", "CSRRCI",
            // SMT / VT synchronisation
            "YIELD", "WFE", "SEV", "POD_BARRIER", "VT_BARRIER",
            // VMX instruction plane
            "VMXON", "VMXOFF", "VMLAUNCH", "VMRESUME",
            "VMREAD", "VMWRITE", "VMCLEAR", "VMPTRLD",
        };

        // ─── Prohibited Hardware Opcodes ──────────────────────────────────────────

        /// <summary>
        /// Opcodes that must NOT appear in the hardware ISA opcode space.
        /// <list type="bullet">
        ///   <item>Assembler pseudo-ops: expanded by the assembler, never encoded as hardware instructions.</item>
        ///   <item>Hint/policy opcodes: scheduling information belongs in <c>SlotMetadata</c> / <c>BundleMetadata</c>.</item>
        ///   <item>Compiler wrappers: helper abstractions that must be lowered to canonical ISA instructions before execution.</item>
        ///   <item>VT-identity opcodes: thread identity is read via the CSR plane, not dedicated opcodes.</item>
        ///   <item>FSP policy opcodes: FSP scheduling boundaries belong in <c>SlotMetadata</c>, not the opcode space.</item>
        /// </list>
        /// </summary>
        public static readonly IReadOnlySet<string> ProhibitedOpcodes = new HashSet<string>
        {
            // Assembler pseudo-ops (ISA mnemonic forms)
            "NOP", "LI", "MV", "CALL", "RET", "JMP",
            // Removed pseudo-ops: enum values were deleted so ToString() yields numeric strings
            "14", "15", "18",   // formerly Call, Return, Jump
            // Hint / scheduling-policy opcodes
            "HINT_LIKELY", "HINT_UNLIKELY", "HINT_HOT", "HINT_COLD",
            "HINT_STREAM", "HINT_REUSE", "HINT_STEALABLE", "HINT_NOSTEAL",
            // VT identity opcodes (must be accessed via CSR plane)
            "RDVTID", "RDVTMASK",
            // FSP policy opcode (belongs in SlotMetadata)
            "FSP_FENCE",
            // Compiler/emulator wrappers (mnemonic + numeric forms after enum removal)
            "CSR_READ", "CSR_WRITE", "147", "148", // formerly CSR_READ=147, CSR_WRITE=148
        };

        // ─── Optional Extension Opcodes ───────────────────────────────────────────

        /// <summary>
        /// Optional extension opcodes — not part of mandatory ISA v4 core.
        /// These may be present as extension blocks but must not be required by
        /// the base ISE. An extension registration mechanism (Phase 12) is required
        /// before these can be formally enabled.
        /// </summary>
        public static readonly IReadOnlySet<string> OptionalExtensions = new HashSet<string>
        {
            // XBIT extension (bit-manipulation)
            "NOT", "CLZ", "CTZ", "POPCNT", "ROL", "ROR",
            // XSCALAR_MATH extension
            "XSQRT", "XFMAC",
        };

        // ─── Pipeline Class Model ─────────────────────────────────────────────────

        /// <summary>
        /// Canonical pipeline class taxonomy for ISA v4.
        /// Maps instruction class names to their pipeline routing class.
        /// Used by the decoder (Phase 03) and execution engine (Phase 04).
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> PipelineClassMap =
            new Dictionary<string, string>
            {
                // ALU class
                ["ADD"] = "ALU",   ["SUB"] = "ALU",   ["AND"] = "ALU",  ["OR"] = "ALU",
                ["XOR"] = "ALU",   ["SLL"] = "ALU",   ["SRL"] = "ALU",  ["SRA"] = "ALU",
                ["SLT"] = "ALU",   ["SLTU"] = "ALU",  ["MUL"] = "ALU",  ["MULH"] = "ALU",
                ["MULHU"] = "ALU", ["MULHSU"] = "ALU",["DIV"] = "ALU",  ["DIVU"] = "ALU",
                ["REM"] = "ALU",   ["REMU"] = "ALU",
                ["ADDI"] = "ALU",  ["ANDI"] = "ALU",  ["ORI"] = "ALU",  ["XORI"] = "ALU",
                ["SLLI"] = "ALU",  ["SRLI"] = "ALU",  ["SRAI"] = "ALU", ["SLTI"] = "ALU",
                ["SLTIU"] = "ALU", ["LUI"] = "ALU",   ["AUIPC"] = "ALU",
                // LSU class
                ["LB"] = "LSU",  ["LBU"] = "LSU",  ["LH"] = "LSU",  ["LHU"] = "LSU",
                ["LW"] = "LSU",  ["LWU"] = "LSU",  ["LD"] = "LSU",
                ["SB"] = "LSU",  ["SH"] = "LSU",   ["SW"] = "LSU",  ["SD"] = "LSU",
                // BR class
                ["JAL"] = "BR",  ["JALR"] = "BR",
                ["BEQ"] = "BR",  ["BNE"] = "BR",   ["BLT"] = "BR",  ["BGE"] = "BR",
                ["BLTU"] = "BR", ["BGEU"] = "BR",
                // ATOM class
                ["LR_W"] = "ATOM",      ["SC_W"] = "ATOM",
                ["LR_D"] = "ATOM",      ["SC_D"] = "ATOM",
                ["AMOSWAP_W"] = "ATOM", ["AMOADD_W"] = "ATOM",  ["AMOXOR_W"] = "ATOM",
                ["AMOAND_W"] = "ATOM",  ["AMOOR_W"] = "ATOM",   ["AMOMIN_W"] = "ATOM",
                ["AMOMAX_W"] = "ATOM",  ["AMOMINU_W"] = "ATOM", ["AMOMAXU_W"] = "ATOM",
                ["AMOSWAP_D"] = "ATOM", ["AMOADD_D"] = "ATOM",  ["AMOXOR_D"] = "ATOM",
                ["AMOAND_D"] = "ATOM",  ["AMOOR_D"] = "ATOM",   ["AMOMIN_D"] = "ATOM",
                ["AMOMAX_D"] = "ATOM",  ["AMOMINU_D"] = "ATOM", ["AMOMAXU_D"] = "ATOM",
                // SYS_SERIAL class
                ["FENCE"] = "SYS_SERIAL",     ["FENCE_I"] = "SYS_SERIAL",
                ["ECALL"] = "SYS_SERIAL",     ["EBREAK"] = "SYS_SERIAL",
                ["MRET"] = "SYS_SERIAL",      ["SRET"] = "SYS_SERIAL",
                ["WFI"] = "SYS_SERIAL",
                ["YIELD"] = "SYS_SERIAL",     ["WFE"] = "SYS_SERIAL",
                ["SEV"] = "SYS_SERIAL",       ["POD_BARRIER"] = "SYS_SERIAL",
                ["VT_BARRIER"] = "SYS_SERIAL",
                // CSR_SERIAL class
                ["CSRRW"] = "CSR_SERIAL",  ["CSRRS"] = "CSR_SERIAL",  ["CSRRC"] = "CSR_SERIAL",
                ["CSRRWI"] = "CSR_SERIAL", ["CSRRSI"] = "CSR_SERIAL", ["CSRRCI"] = "CSR_SERIAL",
                // VMX_SERIAL class
                ["VMXON"] = "VMX_SERIAL",    ["VMXOFF"] = "VMX_SERIAL",
                ["VMLAUNCH"] = "VMX_SERIAL", ["VMRESUME"] = "VMX_SERIAL",
                ["VMREAD"] = "VMX_SERIAL",   ["VMWRITE"] = "VMX_SERIAL",
                ["VMCLEAR"] = "VMX_SERIAL",  ["VMPTRLD"] = "VMX_SERIAL",
            };

        // ─── ISA v4 Freeze Declaration ────────────────────────────────────────────

        /// <summary>
        /// HybridCPU ISA v4 — FROZEN.
        ///
        /// This ISA surface was formally frozen on completion of Phase 12.
        /// No changes to the mandatory core are permitted without a formal
        /// ISA evolution process that increments the ISA version number.
        ///
        /// Mandatory core: <see cref="IsaMandatoryOpcodeCount"/> instructions
        /// across 8 categories.
        /// New in v4 vs v3: DIVU, REMU, MULH, MULHU, MULHSU, 9× AMO*_D,
        /// full VMX instruction plane formalization.
        /// Removed from v3: RDVTID, RDVTMASK, FSP_FENCE, hint opcode family.
        /// </summary>
        public const int IsaVersion = 4;

        /// <summary>
        /// Total number of mandatory hardware opcodes in ISA v4.
        /// This is the authoritative count derived from the canonical instruction
        /// list in the ISA v4 specification (not the summary table, which
        /// over-counts atomics by 2).
        /// </summary>
        public const int IsaMandatoryOpcodeCount = 96;

        /// <summary>Date on which the ISA v4 surface was formally frozen.</summary>
        public static readonly DateOnly FrozenDate = new(2026, 3, 14);
    }
}
