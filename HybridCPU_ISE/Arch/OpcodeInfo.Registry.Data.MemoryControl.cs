namespace YAKSys_Hybrid_CPU.Arch
{
    public static partial class OpcodeRegistry
    {
        private static OpcodeInfo[] CreateMemoryAndControlOpcodes() =>
        [
            // ========== Control Flow ==========
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.JumpIfEqual, "JEQ", OpcodeCategory.ControlFlow, 3, InstructionFlags.UsesImmediate, 1, 0),

            // ========== ISA v2: Typed Loads ==========
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.LB, "LB", OpcodeCategory.Memory, 2, InstructionFlags.MemoryRead | InstructionFlags.UsesImmediate, 4, 1),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.LBU, "LBU", OpcodeCategory.Memory, 2, InstructionFlags.MemoryRead | InstructionFlags.UsesImmediate, 4, 1),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.LH, "LH", OpcodeCategory.Memory, 2, InstructionFlags.MemoryRead | InstructionFlags.UsesImmediate, 4, 1),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.LHU, "LHU", OpcodeCategory.Memory, 2, InstructionFlags.MemoryRead | InstructionFlags.UsesImmediate, 4, 1),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.LW, "LW", OpcodeCategory.Memory, 2, InstructionFlags.MemoryRead | InstructionFlags.UsesImmediate, 4, 1),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.LWU, "LWU", OpcodeCategory.Memory, 2, InstructionFlags.MemoryRead | InstructionFlags.UsesImmediate, 4, 1),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.LD, "LD", OpcodeCategory.Memory, 2, InstructionFlags.MemoryRead | InstructionFlags.UsesImmediate, 4, 1),

            // ========== ISA v2: Typed Stores ==========
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.SB, "SB", OpcodeCategory.Memory, 2, InstructionFlags.MemoryWrite | InstructionFlags.UsesImmediate, 4, 1),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.SH, "SH", OpcodeCategory.Memory, 2, InstructionFlags.MemoryWrite | InstructionFlags.UsesImmediate, 4, 1),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.SW, "SW", OpcodeCategory.Memory, 2, InstructionFlags.MemoryWrite | InstructionFlags.UsesImmediate, 4, 1),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.SD, "SD", OpcodeCategory.Memory, 2, InstructionFlags.MemoryWrite | InstructionFlags.UsesImmediate, 4, 1),

            // ========== ISA v4: Lane6 DMA/Stream Compute ==========
            new OpcodeInfo(
                (uint)Processor.CPU_Core.InstructionsEnum.DmaStreamCompute,
                "DmaStreamCompute",
                OpcodeCategory.Memory,
                0,
                InstructionFlags.MemoryRead | InstructionFlags.MemoryWrite,
                8,
                3,
                InstructionClass.Memory,
                SerializationClass.MemoryOrdered),

            // ========== ISA v2: Control Flow — RISC-V style ==========
            // Assembler-level JMP remains prohibited and lowers through canonical JAL/JALR forms only.
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.JAL, "JAL", OpcodeCategory.ControlFlow, 1, InstructionFlags.UsesImmediate, 1, 0),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.JALR, "JALR", OpcodeCategory.ControlFlow, 2, InstructionFlags.UsesImmediate, 1, 0),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.BEQ, "BEQ", OpcodeCategory.ControlFlow, 2, InstructionFlags.UsesImmediate, 1, 0),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.BNE, "BNE", OpcodeCategory.ControlFlow, 2, InstructionFlags.UsesImmediate, 1, 0),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.BLT, "BLT", OpcodeCategory.ControlFlow, 2, InstructionFlags.UsesImmediate, 1, 0),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.BGE, "BGE", OpcodeCategory.ControlFlow, 2, InstructionFlags.UsesImmediate, 1, 0),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.BLTU, "BLTU", OpcodeCategory.ControlFlow, 2, InstructionFlags.UsesImmediate, 1, 0),
            new OpcodeInfo((uint)Processor.CPU_Core.InstructionsEnum.BGEU, "BGEU", OpcodeCategory.ControlFlow, 2, InstructionFlags.UsesImmediate, 1, 0),
        ];
    }
}
