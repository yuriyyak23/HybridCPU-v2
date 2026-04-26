using System.Runtime.CompilerServices;

namespace YAKSys_Hybrid_CPU.Arch
{
    /// <summary>
    /// Unified metadata for instruction opcodes.
    /// Provides architectural information about instructions for decoder/encoder/documentation.
    ///
    /// Design goals:
    /// - Single source of truth for opcode properties
    /// - Support for extended instruction attributes (masks, indexed, reduction, etc.)
    /// - HLS-friendly (no dynamic allocation, pure data structure)
    /// - Extensible for future ISA additions without breaking existing code
    /// </summary>
    public readonly struct OpcodeInfo
    {
        /// <summary>
        /// Opcode value (matches InstructionsEnum)
        /// </summary>
        public readonly uint OpCode;

        /// <summary>
        /// Human-readable mnemonic (e.g., "VADD", "VSUB")
        /// </summary>
        public readonly string Mnemonic;

        /// <summary>
        /// Category of instruction
        /// </summary>
        public readonly OpcodeCategory Category;

        /// <summary>
        /// Number of source operands (0, 1, 2, or 3)
        /// </summary>
        public readonly byte OperandCount;

        /// <summary>
        /// Instruction characteristics flags
        /// </summary>
        public readonly InstructionFlags Flags;

        /// <summary>
        /// Execution latency in cycles (minimum, for scalar operations)
        /// Vector operations scale by VL / VLMAX
        /// </summary>
        public readonly byte ExecutionLatency;

        /// <summary>
        /// Memory bandwidth requirement (bytes per element)
        /// 0 = no memory access, 1 = single operand, 2 = dual operand
        /// </summary>
        public readonly byte MemoryBandwidth;

        /// <summary>
        /// Canonical runtime routing class for the published opcode contour.
        /// </summary>
        public readonly InstructionClass InstructionClass;

        /// <summary>
        /// Canonical runtime serialization class for the published opcode contour.
        /// </summary>
        public readonly SerializationClass SerializationClass;

        public OpcodeInfo(
            uint opCode,
            string mnemonic,
            OpcodeCategory category,
            byte operandCount,
            InstructionFlags flags,
            byte executionLatency,
            byte memoryBandwidth,
            InstructionClass? instructionClass = null,
            SerializationClass? serializationClass = null)
        {
            OpCode = opCode;
            Mnemonic = mnemonic;
            Category = category;
            OperandCount = operandCount;
            Flags = flags;
            ExecutionLatency = executionLatency;
            MemoryBandwidth = memoryBandwidth;
            InstructionClass = instructionClass ?? ResolveInstructionClass(category);
            SerializationClass = serializationClass ?? ResolveSerializationClass(category, flags);
        }

        /// <summary>
        /// Check if instruction is a vector/stream operation
        /// </summary>
        public bool IsVector => (Flags & InstructionFlags.Vector) != 0;

        /// <summary>
        /// Check if instruction supports predication (masking)
        /// </summary>
        public bool SupportsMasking => (Flags & InstructionFlags.Maskable) != 0;

        /// <summary>
        /// Check if instruction can perform reduction operations
        /// </summary>
        public bool SupportsReduction => (Flags & InstructionFlags.Reduction) != 0;

        /// <summary>
        /// Check if instruction supports indexed (gather/scatter) addressing
        /// </summary>
        public bool SupportsIndexed => (Flags & InstructionFlags.Indexed) != 0;

        /// <summary>
        /// Check if instruction is a control flow operation
        /// </summary>
        public bool IsControlFlow => Category == OpcodeCategory.ControlFlow;

        /// <summary>
        /// Check if instruction is a math or vector operation.
        /// True for Scalar ALU (integer/FP/shift), Vector, Comparison, and BitManip
        /// categories.  This is the table-driven replacement for the legacy
        /// <c>VLIW_Instruction.IsMathOrVector</c> opcode-range comparison (V6 B8/B9).
        /// </summary>
        public bool IsMathOrVector =>
            Category == OpcodeCategory.Scalar     ||
            Category == OpcodeCategory.Vector     ||
            Category == OpcodeCategory.Comparison ||
            Category == OpcodeCategory.BitManip;

        /// <summary>
        /// Check if instruction modifies flags register
        /// </summary>
        public bool ModifiesFlags => (Flags & InstructionFlags.ModifiesFlags) != 0;

        private static InstructionClass ResolveInstructionClass(OpcodeCategory category)
        {
            return category switch
            {
                OpcodeCategory.Memory => InstructionClass.Memory,
                OpcodeCategory.ControlFlow => InstructionClass.ControlFlow,
                OpcodeCategory.System => InstructionClass.System,
                OpcodeCategory.Atomic => InstructionClass.Atomic,
                OpcodeCategory.Privileged => InstructionClass.System,
                _ => InstructionClass.ScalarAlu,
            };
        }

        private static SerializationClass ResolveSerializationClass(
            OpcodeCategory category,
            InstructionFlags flags)
        {
            return category switch
            {
                OpcodeCategory.Atomic => SerializationClass.AtomicSerial,
                OpcodeCategory.Memory when (flags & InstructionFlags.MemoryWrite) != 0 =>
                    SerializationClass.MemoryOrdered,
                OpcodeCategory.Privileged => SerializationClass.FullSerial,
                _ => SerializationClass.Free,
            };
        }
    }

    /// <summary>
    /// Instruction category classification
    /// </summary>
    public enum OpcodeCategory : byte
    {
        Scalar = 0,         // Scalar integer/FP operations
        Vector = 1,         // Vector/stream operations
        Memory = 2,         // Memory load/store
        ControlFlow = 3,    // Jumps, calls, returns
        System = 4,         // System control, VMX, interrupts
        Comparison = 5,     // Comparison and mask generation
        BitManip = 6,       // Bit manipulation and shifts
        Atomic = 7,         // LR/SC/AMO atomic operations
        Privileged = 8      // ECALL/EBREAK/MRET/SRET/WFI
    }

    /// <summary>
    /// Instruction characteristic flags (bit flags)
    /// </summary>
    [System.Flags]
    public enum InstructionFlags : ushort
    {
        None = 0,
        Vector = 1 << 0,           // Vector/stream instruction
        Maskable = 1 << 1,         // Supports predicate masking
        Reduction = 1 << 2,        // Can perform reduction
        Indexed = 1 << 3,          // Supports indexed addressing
        ModifiesFlags = 1 << 4,    // Modifies CPU flags register
        UsesImmediate = 1 << 5,    // Uses immediate value
        TwoOperand = 1 << 6,       // Two-operand (destructive)
        ThreeOperand = 1 << 7,     // Three-operand (non-destructive)
        MemoryRead = 1 << 8,       // Reads from memory
        MemoryWrite = 1 << 9,      // Writes to memory
        FloatingPoint = 1 << 10,   // Floating-point operation
        Atomic = 1 << 11,          // Atomic operation
        Privileged = 1 << 12,      // Requires privileged mode
        MaskManipulation = 1 << 13 // Predicate-mask manipulation (VMAND/VMOR/VMXOR/VMNOT/VPOPC)
    }
}
