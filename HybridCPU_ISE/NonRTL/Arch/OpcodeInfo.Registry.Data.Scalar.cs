namespace YAKSys_Hybrid_CPU.Arch
{
    public static partial class OpcodeRegistry
    {
        private static OpcodeInfo[] CreateScalarOpcodes() =>
        [
            // ========== Scalar Integer Arithmetic ==========
            new OpcodeInfo(
                opCode: (uint)Processor.CPU_Core.InstructionsEnum.ADD,
                mnemonic: "ADD",
                category: OpcodeCategory.Scalar,
                operandCount: 2,
                flags: InstructionFlags.TwoOperand | InstructionFlags.ModifiesFlags,
                executionLatency: 1,
                memoryBandwidth: 0
            ),

            new OpcodeInfo(
                opCode: (uint)Processor.CPU_Core.InstructionsEnum.SUB,
                mnemonic: "SUB",
                category: OpcodeCategory.Scalar,
                operandCount: 2,
                flags: InstructionFlags.TwoOperand | InstructionFlags.ModifiesFlags,
                executionLatency: 1,
                memoryBandwidth: 0
            ),

            new OpcodeInfo(
                opCode: (uint)Processor.CPU_Core.InstructionsEnum.ADDW,
                mnemonic: "ADDW",
                category: OpcodeCategory.Scalar,
                operandCount: 2,
                flags: InstructionFlags.TwoOperand | InstructionFlags.ModifiesFlags,
                executionLatency: 1,
                memoryBandwidth: 0
            ),

            new OpcodeInfo(
                opCode: (uint)Processor.CPU_Core.InstructionsEnum.SUBW,
                mnemonic: "SUBW",
                category: OpcodeCategory.Scalar,
                operandCount: 2,
                flags: InstructionFlags.TwoOperand | InstructionFlags.ModifiesFlags,
                executionLatency: 1,
                memoryBandwidth: 0
            ),

            new OpcodeInfo(
                opCode: (uint)Processor.CPU_Core.InstructionsEnum.SLLW,
                mnemonic: "SLLW",
                category: OpcodeCategory.BitManip,
                operandCount: 2,
                flags: InstructionFlags.TwoOperand | InstructionFlags.ModifiesFlags,
                executionLatency: 1,
                memoryBandwidth: 0
            ),

            new OpcodeInfo(
                opCode: (uint)Processor.CPU_Core.InstructionsEnum.SRLW,
                mnemonic: "SRLW",
                category: OpcodeCategory.BitManip,
                operandCount: 2,
                flags: InstructionFlags.TwoOperand | InstructionFlags.ModifiesFlags,
                executionLatency: 1,
                memoryBandwidth: 0
            ),

            new OpcodeInfo(
                opCode: (uint)Processor.CPU_Core.InstructionsEnum.SRAW,
                mnemonic: "SRAW",
                category: OpcodeCategory.BitManip,
                operandCount: 2,
                flags: InstructionFlags.TwoOperand | InstructionFlags.ModifiesFlags,
                executionLatency: 1,
                memoryBandwidth: 0
            ),

            new OpcodeInfo(
                opCode: (uint)Processor.CPU_Core.InstructionsEnum.MUL,
                mnemonic: "MUL",
                category: OpcodeCategory.Scalar,
                operandCount: 2,
                flags: InstructionFlags.TwoOperand | InstructionFlags.ModifiesFlags,
                executionLatency: 3,
                memoryBandwidth: 0
            ),

            new OpcodeInfo(
                opCode: (uint)Processor.CPU_Core.InstructionsEnum.MULW,
                mnemonic: "MULW",
                category: OpcodeCategory.Scalar,
                operandCount: 2,
                flags: InstructionFlags.TwoOperand | InstructionFlags.ModifiesFlags,
                executionLatency: 3,
                memoryBandwidth: 0
            ),

            new OpcodeInfo(
                opCode: (uint)Processor.CPU_Core.InstructionsEnum.DIVW,
                mnemonic: "DIVW",
                category: OpcodeCategory.Scalar,
                operandCount: 2,
                flags: InstructionFlags.TwoOperand | InstructionFlags.ModifiesFlags,
                executionLatency: 16,
                memoryBandwidth: 0
            ),

            new OpcodeInfo(
                opCode: (uint)Processor.CPU_Core.InstructionsEnum.DIVUW,
                mnemonic: "DIVUW",
                category: OpcodeCategory.Scalar,
                operandCount: 2,
                flags: InstructionFlags.TwoOperand | InstructionFlags.ModifiesFlags,
                executionLatency: 16,
                memoryBandwidth: 0
            ),

            new OpcodeInfo(
                opCode: (uint)Processor.CPU_Core.InstructionsEnum.REMW,
                mnemonic: "REMW",
                category: OpcodeCategory.Scalar,
                operandCount: 2,
                flags: InstructionFlags.TwoOperand | InstructionFlags.ModifiesFlags,
                executionLatency: 16,
                memoryBandwidth: 0
            ),

            new OpcodeInfo(
                opCode: (uint)Processor.CPU_Core.InstructionsEnum.REMUW,
                mnemonic: "REMUW",
                category: OpcodeCategory.Scalar,
                operandCount: 2,
                flags: InstructionFlags.TwoOperand | InstructionFlags.ModifiesFlags,
                executionLatency: 16,
                memoryBandwidth: 0
            ),

            new OpcodeInfo(
                opCode: (uint)Processor.CPU_Core.InstructionsEnum.SEXT_W,
                mnemonic: "SEXT.W",
                category: OpcodeCategory.Scalar,
                operandCount: 2,
                flags: InstructionFlags.TwoOperand | InstructionFlags.ModifiesFlags,
                executionLatency: 1,
                memoryBandwidth: 0
            ),

            new OpcodeInfo(
                opCode: (uint)Processor.CPU_Core.InstructionsEnum.ZEXT_W,
                mnemonic: "ZEXT.W",
                category: OpcodeCategory.Scalar,
                operandCount: 2,
                flags: InstructionFlags.TwoOperand | InstructionFlags.ModifiesFlags,
                executionLatency: 1,
                memoryBandwidth: 0
            ),

            new OpcodeInfo(
                opCode: (uint)Processor.CPU_Core.InstructionsEnum.DIV,
                mnemonic: "DIV",
                category: OpcodeCategory.Scalar,
                operandCount: 2,
                flags: InstructionFlags.TwoOperand | InstructionFlags.ModifiesFlags,
                executionLatency: 16,
                memoryBandwidth: 0
            ),



            new OpcodeInfo(
                opCode: (uint)Processor.CPU_Core.InstructionsEnum.SLL,
                mnemonic: "SLL",
                category: OpcodeCategory.BitManip,
                operandCount: 2,
                flags: InstructionFlags.TwoOperand | InstructionFlags.ModifiesFlags,
                executionLatency: 1,
                memoryBandwidth: 0
            ),

            new OpcodeInfo(
                opCode: (uint)Processor.CPU_Core.InstructionsEnum.SRL,
                mnemonic: "SRL",
                category: OpcodeCategory.BitManip,
                operandCount: 2,
                flags: InstructionFlags.TwoOperand | InstructionFlags.ModifiesFlags,
                executionLatency: 1,
                memoryBandwidth: 0
            ),

            new OpcodeInfo(
                opCode: (uint)Processor.CPU_Core.InstructionsEnum.SRA,
                mnemonic: "SRA",
                category: OpcodeCategory.BitManip,
                operandCount: 2,
                flags: InstructionFlags.TwoOperand | InstructionFlags.ModifiesFlags,
                executionLatency: 1,
                memoryBandwidth: 0
            ),

            new OpcodeInfo(
                opCode: (uint)Processor.CPU_Core.InstructionsEnum.XOR,
                mnemonic: "XOR",
                category: OpcodeCategory.Scalar,
                operandCount: 2,
                flags: InstructionFlags.TwoOperand | InstructionFlags.ModifiesFlags,
                executionLatency: 1,
                memoryBandwidth: 0
            ),

            new OpcodeInfo(
                opCode: (uint)Processor.CPU_Core.InstructionsEnum.OR,
                mnemonic: "OR",
                category: OpcodeCategory.Scalar,
                operandCount: 2,
                flags: InstructionFlags.TwoOperand | InstructionFlags.ModifiesFlags,
                executionLatency: 1,
                memoryBandwidth: 0
            ),

            new OpcodeInfo(
                opCode: (uint)Processor.CPU_Core.InstructionsEnum.AND,
                mnemonic: "AND",
                category: OpcodeCategory.Scalar,
                operandCount: 2,
                flags: InstructionFlags.TwoOperand | InstructionFlags.ModifiesFlags,
                executionLatency: 1,
                memoryBandwidth: 0
            ),

            // ========== Phase 03A: Optional scalar branchless zero-select ==========
            new OpcodeInfo(
                opCode: (uint)Processor.CPU_Core.InstructionsEnum.CZERO_EQZ,
                mnemonic: "CZERO.EQZ",
                category: OpcodeCategory.Scalar,
                operandCount: 2,
                flags: InstructionFlags.TwoOperand,
                executionLatency: 1,
                memoryBandwidth: 0
            ),

            // ========== Phase 03B: Optional scalar bitmanip first slice ==========
            new OpcodeInfo(
                opCode: (uint)Processor.CPU_Core.InstructionsEnum.CLZ,
                mnemonic: "CLZ",
                category: OpcodeCategory.BitManip,
                operandCount: 1,
                flags: InstructionFlags.None,
                executionLatency: 1,
                memoryBandwidth: 0
            ),
            new OpcodeInfo(
                opCode: (uint)Processor.CPU_Core.InstructionsEnum.CTZ,
                mnemonic: "CTZ",
                category: OpcodeCategory.BitManip,
                operandCount: 1,
                flags: InstructionFlags.None,
                executionLatency: 1,
                memoryBandwidth: 0
            ),
            new OpcodeInfo(
                opCode: (uint)Processor.CPU_Core.InstructionsEnum.SEXT_B,
                mnemonic: "SEXT.B",
                category: OpcodeCategory.Scalar,
                operandCount: 1,
                flags: InstructionFlags.None,
                executionLatency: 1,
                memoryBandwidth: 0
            ),
            new OpcodeInfo(
                opCode: (uint)Processor.CPU_Core.InstructionsEnum.SEXT_H,
                mnemonic: "SEXT.H",
                category: OpcodeCategory.Scalar,
                operandCount: 1,
                flags: InstructionFlags.None,
                executionLatency: 1,
                memoryBandwidth: 0
            ),
            new OpcodeInfo(
                opCode: (uint)Processor.CPU_Core.InstructionsEnum.ZEXT_H,
                mnemonic: "ZEXT.H",
                category: OpcodeCategory.Scalar,
                operandCount: 1,
                flags: InstructionFlags.None,
                executionLatency: 1,
                memoryBandwidth: 0
            ),

            // ========== Non-VMX CloseToRTL Iteration 03C: Optional scalar rotates ==========
            new OpcodeInfo(
                opCode: (uint)Processor.CPU_Core.InstructionsEnum.ROL,
                mnemonic: "ROL",
                category: OpcodeCategory.BitManip,
                operandCount: 2,
                flags: InstructionFlags.TwoOperand,
                executionLatency: 1,
                memoryBandwidth: 0
            ),
            new OpcodeInfo(
                opCode: (uint)Processor.CPU_Core.InstructionsEnum.ROR,
                mnemonic: "ROR",
                category: OpcodeCategory.BitManip,
                operandCount: 2,
                flags: InstructionFlags.TwoOperand,
                executionLatency: 1,
                memoryBandwidth: 0
            ),
            new OpcodeInfo(
                opCode: (uint)Processor.CPU_Core.InstructionsEnum.ROLI,
                mnemonic: "ROLI",
                category: OpcodeCategory.BitManip,
                operandCount: 2,
                flags: InstructionFlags.UsesImmediate,
                executionLatency: 1,
                memoryBandwidth: 0
            ),
            new OpcodeInfo(
                opCode: (uint)Processor.CPU_Core.InstructionsEnum.RORI,
                mnemonic: "RORI",
                category: OpcodeCategory.BitManip,
                operandCount: 2,
                flags: InstructionFlags.UsesImmediate,
                executionLatency: 1,
                memoryBandwidth: 0
            ),
            new OpcodeInfo(
                opCode: (uint)Processor.CPU_Core.InstructionsEnum.BSET,
                mnemonic: "BSET",
                category: OpcodeCategory.BitManip,
                operandCount: 2,
                flags: InstructionFlags.TwoOperand,
                executionLatency: 1,
                memoryBandwidth: 0
            ),
            new OpcodeInfo(
                opCode: (uint)Processor.CPU_Core.InstructionsEnum.BCLR,
                mnemonic: "BCLR",
                category: OpcodeCategory.BitManip,
                operandCount: 2,
                flags: InstructionFlags.TwoOperand,
                executionLatency: 1,
                memoryBandwidth: 0
            ),
            new OpcodeInfo(
                opCode: (uint)Processor.CPU_Core.InstructionsEnum.BINV,
                mnemonic: "BINV",
                category: OpcodeCategory.BitManip,
                operandCount: 2,
                flags: InstructionFlags.TwoOperand,
                executionLatency: 1,
                memoryBandwidth: 0
            ),
            new OpcodeInfo(
                opCode: (uint)Processor.CPU_Core.InstructionsEnum.BEXT,
                mnemonic: "BEXT",
                category: OpcodeCategory.BitManip,
                operandCount: 2,
                flags: InstructionFlags.TwoOperand,
                executionLatency: 1,
                memoryBandwidth: 0
            ),
            new OpcodeInfo(
                opCode: (uint)Processor.CPU_Core.InstructionsEnum.BSETI,
                mnemonic: "BSETI",
                category: OpcodeCategory.BitManip,
                operandCount: 2,
                flags: InstructionFlags.UsesImmediate,
                executionLatency: 1,
                memoryBandwidth: 0
            ),
            new OpcodeInfo(
                opCode: (uint)Processor.CPU_Core.InstructionsEnum.BCLRI,
                mnemonic: "BCLRI",
                category: OpcodeCategory.BitManip,
                operandCount: 2,
                flags: InstructionFlags.UsesImmediate,
                executionLatency: 1,
                memoryBandwidth: 0
            ),
            new OpcodeInfo(
                opCode: (uint)Processor.CPU_Core.InstructionsEnum.BINVI,
                mnemonic: "BINVI",
                category: OpcodeCategory.BitManip,
                operandCount: 2,
                flags: InstructionFlags.UsesImmediate,
                executionLatency: 1,
                memoryBandwidth: 0
            ),
            new OpcodeInfo(
                opCode: (uint)Processor.CPU_Core.InstructionsEnum.BEXTI,
                mnemonic: "BEXTI",
                category: OpcodeCategory.BitManip,
                operandCount: 2,
                flags: InstructionFlags.UsesImmediate,
                executionLatency: 1,
                memoryBandwidth: 0
            ),
            new OpcodeInfo(
                opCode: (uint)Processor.CPU_Core.InstructionsEnum.ANDN,
                mnemonic: "ANDN",
                category: OpcodeCategory.BitManip,
                operandCount: 2,
                flags: InstructionFlags.TwoOperand,
                executionLatency: 1,
                memoryBandwidth: 0
            ),
            new OpcodeInfo(
                opCode: (uint)Processor.CPU_Core.InstructionsEnum.ORN,
                mnemonic: "ORN",
                category: OpcodeCategory.BitManip,
                operandCount: 2,
                flags: InstructionFlags.TwoOperand,
                executionLatency: 1,
                memoryBandwidth: 0
            ),
            new OpcodeInfo(
                opCode: (uint)Processor.CPU_Core.InstructionsEnum.XNOR,
                mnemonic: "XNOR",
                category: OpcodeCategory.BitManip,
                operandCount: 2,
                flags: InstructionFlags.TwoOperand,
                executionLatency: 1,
                memoryBandwidth: 0
            ),
            new OpcodeInfo(
                opCode: (uint)Processor.CPU_Core.InstructionsEnum.MIN,
                mnemonic: "MIN",
                category: OpcodeCategory.BitManip,
                operandCount: 2,
                flags: InstructionFlags.TwoOperand,
                executionLatency: 1,
                memoryBandwidth: 0
            ),
            new OpcodeInfo(
                opCode: (uint)Processor.CPU_Core.InstructionsEnum.MAX,
                mnemonic: "MAX",
                category: OpcodeCategory.BitManip,
                operandCount: 2,
                flags: InstructionFlags.TwoOperand,
                executionLatency: 1,
                memoryBandwidth: 0
            ),
            new OpcodeInfo(
                opCode: (uint)Processor.CPU_Core.InstructionsEnum.MINU,
                mnemonic: "MINU",
                category: OpcodeCategory.BitManip,
                operandCount: 2,
                flags: InstructionFlags.TwoOperand,
                executionLatency: 1,
                memoryBandwidth: 0
            ),
            new OpcodeInfo(
                opCode: (uint)Processor.CPU_Core.InstructionsEnum.MAXU,
                mnemonic: "MAXU",
                category: OpcodeCategory.BitManip,
                operandCount: 2,
                flags: InstructionFlags.TwoOperand,
                executionLatency: 1,
                memoryBandwidth: 0
            ),
            new OpcodeInfo(
                opCode: (uint)Processor.CPU_Core.InstructionsEnum.REV8,
                mnemonic: "REV8",
                category: OpcodeCategory.BitManip,
                operandCount: 1,
                flags: InstructionFlags.None,
                executionLatency: 1,
                memoryBandwidth: 0
            ),
            new OpcodeInfo(
                opCode: (uint)Processor.CPU_Core.InstructionsEnum.BREV8,
                mnemonic: "BREV8",
                category: OpcodeCategory.BitManip,
                operandCount: 1,
                flags: InstructionFlags.None,
                executionLatency: 1,
                memoryBandwidth: 0
            ),
            new OpcodeInfo(
                opCode: (uint)Processor.CPU_Core.InstructionsEnum.CZERO_NEZ,
                mnemonic: "CZERO.NEZ",
                category: OpcodeCategory.Scalar,
                operandCount: 2,
                flags: InstructionFlags.TwoOperand,
                executionLatency: 1,
                memoryBandwidth: 0
            ),
            new OpcodeInfo(
                opCode: (uint)Processor.CPU_Core.InstructionsEnum.CPOP,
                mnemonic: "CPOP",
                category: OpcodeCategory.BitManip,
                operandCount: 1,
                flags: InstructionFlags.None,
                executionLatency: 1,
                memoryBandwidth: 0
            ),

            // ========== Phase 03C: Optional scalar address-generation first slice ==========
            new OpcodeInfo(
                opCode: (uint)Processor.CPU_Core.InstructionsEnum.SH1ADD,
                mnemonic: "SH1ADD",
                category: OpcodeCategory.Scalar,
                operandCount: 2,
                flags: InstructionFlags.TwoOperand,
                executionLatency: 1,
                memoryBandwidth: 0
            ),
            new OpcodeInfo(
                opCode: (uint)Processor.CPU_Core.InstructionsEnum.SH2ADD,
                mnemonic: "SH2ADD",
                category: OpcodeCategory.Scalar,
                operandCount: 2,
                flags: InstructionFlags.TwoOperand,
                executionLatency: 1,
                memoryBandwidth: 0
            ),
            new OpcodeInfo(
                opCode: (uint)Processor.CPU_Core.InstructionsEnum.SH3ADD,
                mnemonic: "SH3ADD",
                category: OpcodeCategory.Scalar,
                operandCount: 2,
                flags: InstructionFlags.TwoOperand,
                executionLatency: 1,
                memoryBandwidth: 0
            ),
            new OpcodeInfo(
                opCode: (uint)Processor.CPU_Core.InstructionsEnum.ADD_UW,
                mnemonic: "ADD.UW",
                category: OpcodeCategory.Scalar,
                operandCount: 2,
                flags: InstructionFlags.TwoOperand,
                executionLatency: 1,
                memoryBandwidth: 0
            ),
            new OpcodeInfo(
                opCode: (uint)Processor.CPU_Core.InstructionsEnum.SH1ADD_UW,
                mnemonic: "SH1ADD.UW",
                category: OpcodeCategory.Scalar,
                operandCount: 2,
                flags: InstructionFlags.TwoOperand,
                executionLatency: 1,
                memoryBandwidth: 0
            ),
            new OpcodeInfo(
                opCode: (uint)Processor.CPU_Core.InstructionsEnum.SH2ADD_UW,
                mnemonic: "SH2ADD.UW",
                category: OpcodeCategory.Scalar,
                operandCount: 2,
                flags: InstructionFlags.TwoOperand,
                executionLatency: 1,
                memoryBandwidth: 0
            ),
            new OpcodeInfo(
                opCode: (uint)Processor.CPU_Core.InstructionsEnum.SH3ADD_UW,
                mnemonic: "SH3ADD.UW",
                category: OpcodeCategory.Scalar,
                operandCount: 2,
                flags: InstructionFlags.TwoOperand,
                executionLatency: 1,
                memoryBandwidth: 0
            ),
            new OpcodeInfo(
                opCode: (uint)Processor.CPU_Core.InstructionsEnum.SLLI_UW,
                mnemonic: "SLLI.UW",
                category: OpcodeCategory.Scalar,
                operandCount: 2,
                flags: InstructionFlags.UsesImmediate,
                executionLatency: 1,
                memoryBandwidth: 0
            ),

            // ========== Phase 03E: Optional scalar carry-less first slice ==========
            new OpcodeInfo(
                opCode: (uint)Processor.CPU_Core.InstructionsEnum.CLMUL,
                mnemonic: "CLMUL",
                category: OpcodeCategory.BitManip,
                operandCount: 2,
                flags: InstructionFlags.TwoOperand,
                executionLatency: 1,
                memoryBandwidth: 0
            ),
            new OpcodeInfo(
                opCode: (uint)Processor.CPU_Core.InstructionsEnum.CLMULH,
                mnemonic: "CLMULH",
                category: OpcodeCategory.BitManip,
                operandCount: 2,
                flags: InstructionFlags.TwoOperand,
                executionLatency: 1,
                memoryBandwidth: 0
            ),
            new OpcodeInfo(
                opCode: (uint)Processor.CPU_Core.InstructionsEnum.CLMULR,
                mnemonic: "CLMULR",
                category: OpcodeCategory.BitManip,
                operandCount: 2,
                flags: InstructionFlags.TwoOperand,
                executionLatency: 1,
                memoryBandwidth: 0
            ),

            // ========== ISA v2: RISC-VV Scalar Immediate ALU ==========
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.ADDI, "ADDI", OpcodeCategory.Scalar, 2, InstructionFlags.UsesImmediate, 1, 0),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.ADDIW, "ADDIW", OpcodeCategory.Scalar, 2, InstructionFlags.UsesImmediate, 1, 0),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.SLLIW, "SLLIW", OpcodeCategory.Scalar, 2, InstructionFlags.UsesImmediate, 1, 0),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.SRLIW, "SRLIW", OpcodeCategory.Scalar, 2, InstructionFlags.UsesImmediate, 1, 0),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.SRAIW, "SRAIW", OpcodeCategory.Scalar, 2, InstructionFlags.UsesImmediate, 1, 0),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.ANDI, "ANDI", OpcodeCategory.Scalar, 2, InstructionFlags.UsesImmediate, 1, 0),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.ORI, "ORI", OpcodeCategory.Scalar, 2, InstructionFlags.UsesImmediate, 1, 0),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.XORI, "XORI", OpcodeCategory.Scalar, 2, InstructionFlags.UsesImmediate, 1, 0),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.SLTI, "SLTI", OpcodeCategory.Scalar, 2, InstructionFlags.UsesImmediate, 1, 0),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.SLTIU, "SLTIU", OpcodeCategory.Scalar, 2, InstructionFlags.UsesImmediate, 1, 0),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.SLLI, "SLLI", OpcodeCategory.Scalar, 2, InstructionFlags.UsesImmediate, 1, 0),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.SRLI, "SRLI", OpcodeCategory.Scalar, 2, InstructionFlags.UsesImmediate, 1, 0),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.SRAI, "SRAI", OpcodeCategory.Scalar, 2, InstructionFlags.UsesImmediate, 1, 0),

            // ========== ISA v2: Compare/Set ==========
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.SLT, "SLT", OpcodeCategory.Comparison, 2, InstructionFlags.TwoOperand, 1, 0),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.SLTU, "SLTU", OpcodeCategory.Comparison, 2, InstructionFlags.TwoOperand, 1, 0),

            // ========== ISA v2: Upper Immediate / PC-relative ==========
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.LUI, "LUI", OpcodeCategory.Scalar, 1, InstructionFlags.UsesImmediate, 1, 0),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.AUIPC, "AUIPC", OpcodeCategory.Scalar, 1, InstructionFlags.UsesImmediate, 1, 0),

            // ========== ISA v4: Scalar M-Extension Additions ==========
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.MULH, "MULH", OpcodeCategory.Scalar, 2, InstructionFlags.None, 3, 0),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.MULHU, "MULHU", OpcodeCategory.Scalar, 2, InstructionFlags.None, 3, 0),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.MULHSU, "MULHSU", OpcodeCategory.Scalar, 2, InstructionFlags.None, 3, 0),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.DIVU, "DIVU", OpcodeCategory.Scalar, 2, InstructionFlags.None, 4, 0),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.REM, "REM", OpcodeCategory.Scalar, 2, InstructionFlags.None, 4, 0),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.REMU, "REMU", OpcodeCategory.Scalar, 2, InstructionFlags.None, 4, 0),
        ];
    }
}
