namespace YAKSys_Hybrid_CPU.Arch
{
    public static partial class OpcodeRegistry
    {
        private static OpcodeInfo[] CreateScalarOpcodes() =>
        [
            // ========== Scalar Integer Arithmetic ==========
            new OpcodeInfo(
                opCode: (uint)Processor.CPU_Core.InstructionsEnum.Addition,
                mnemonic: "ADD",
                category: OpcodeCategory.Scalar,
                operandCount: 2,
                flags: InstructionFlags.TwoOperand | InstructionFlags.ModifiesFlags,
                executionLatency: 1,
                memoryBandwidth: 0
            ),

            new OpcodeInfo(
                opCode: (uint)Processor.CPU_Core.InstructionsEnum.Subtraction,
                mnemonic: "SUB",
                category: OpcodeCategory.Scalar,
                operandCount: 2,
                flags: InstructionFlags.TwoOperand | InstructionFlags.ModifiesFlags,
                executionLatency: 1,
                memoryBandwidth: 0
            ),

            new OpcodeInfo(
                opCode: (uint)Processor.CPU_Core.InstructionsEnum.Multiplication,
                mnemonic: "MUL",
                category: OpcodeCategory.Scalar,
                operandCount: 2,
                flags: InstructionFlags.TwoOperand | InstructionFlags.ModifiesFlags,
                executionLatency: 3,
                memoryBandwidth: 0
            ),

            new OpcodeInfo(
                opCode: (uint)Processor.CPU_Core.InstructionsEnum.Division,
                mnemonic: "DIV",
                category: OpcodeCategory.Scalar,
                operandCount: 2,
                flags: InstructionFlags.TwoOperand | InstructionFlags.ModifiesFlags,
                executionLatency: 16,
                memoryBandwidth: 0
            ),

            new OpcodeInfo(
                opCode: (uint)Processor.CPU_Core.InstructionsEnum.Modulus,
                mnemonic: "REM",
                category: OpcodeCategory.Scalar,
                operandCount: 2,
                flags: InstructionFlags.TwoOperand | InstructionFlags.ModifiesFlags,
                executionLatency: 16,
                memoryBandwidth: 0
            ),

            new OpcodeInfo(
                opCode: (uint)Processor.CPU_Core.InstructionsEnum.ShiftLeft,
                mnemonic: "SLL",
                category: OpcodeCategory.BitManip,
                operandCount: 2,
                flags: InstructionFlags.TwoOperand | InstructionFlags.ModifiesFlags,
                executionLatency: 1,
                memoryBandwidth: 0
            ),

            new OpcodeInfo(
                opCode: (uint)Processor.CPU_Core.InstructionsEnum.ShiftRight,
                mnemonic: "SRL",
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

            // ========== ISA v2: RISC-VV Scalar Immediate ALU ==========
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.ADDI, "ADDI", OpcodeCategory.Scalar, 2, InstructionFlags.UsesImmediate, 1, 0),
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
