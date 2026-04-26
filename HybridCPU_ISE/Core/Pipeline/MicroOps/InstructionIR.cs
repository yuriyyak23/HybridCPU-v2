using YAKSys_Hybrid_CPU.Arch;

namespace YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps
{
    /// <summary>
    /// Internal IR record for a single HybridCPU ISA v4 instruction.
    /// This is the canonical IR form used by both the ISE runtime (MicroOp)
    /// and the compiler (IR node).
    /// <para>
    /// INVARIANT: No hint or scheduling policy fields.
    /// All hint/policy information belongs in <c>SlotMetadata</c> / <c>BundleMetadata</c>.
    /// </para>
    /// <para>
    /// Stores canonical opcode identity as <see cref="Processor.CPU_Core.IsaOpcode"/>.
    /// Legacy enum publication is intentionally not part of the IR surface.
    /// </para>
    /// </summary>
    public sealed record InstructionIR
    {
        private Processor.CPU_Core.IsaOpcode _canonicalOpcode;

        /// <summary>Canonical opcode identifier carried by the IR.</summary>
        public Processor.CPU_Core.IsaOpcode CanonicalOpcode
        {
            get => _canonicalOpcode;
            init => _canonicalOpcode = value;
        }

        /// <summary>Instruction class — determines pipeline routing.</summary>
        public required InstructionClass Class { get; init; }

        /// <summary>Serialization class — determines ordering requirements.</summary>
        public required SerializationClass SerializationClass { get; init; }

        /// <summary>Destination register (0 = no destination / x0).</summary>
        public required byte Rd { get; init; }

        /// <summary>Source register 1 (0 = x0 or not used).</summary>
        public required byte Rs1 { get; init; }

        /// <summary>Source register 2 (0 = x0 or not used).</summary>
        public required byte Rs2 { get; init; }

        /// <summary>
        /// Immediate value (sign-extended).
        /// 0 for register-register instructions.
        /// When <see cref="HasAbsoluteAddressing"/> is <see langword="true"/>,
        /// carries the absolute memory address or branch target instead of a
        /// relative offset.
        /// </summary>
        public required long Imm { get; init; }

        /// <summary>
        /// When <see langword="true"/>, <see cref="Imm"/> carries an absolute
        /// address (memory address or branch target) rather than a base+offset
        /// displacement or PC-relative offset. Set by the active decoder boundary
        /// when it projects an absolute memory address or an explicit compat
        /// wrapper target into canonical IR.
        /// </summary>
        public bool HasAbsoluteAddressing { get; init; }

        /// <summary>
        /// Operand data type for typed memory and vector operations.
        /// Defaults to <see cref="InternalOpDataType.DWord"/> for most scalar instructions.
        /// </summary>
        public InternalOpDataType DataType { get; init; } = InternalOpDataType.DWord;

        /// <summary>
        /// CSR address for CSR-class instructions (CSRRW/CSRRS/CSRRC and immediate variants).
        /// <see langword="null"/> for all non-CSR instructions.
        /// </summary>
        public ushort? CsrAddress { get; init; }

        // NOTE: No hint fields here.
        // Scheduling hints (branch prediction, stealability, locality, thermal) belong
        // in SlotMetadata attached to the instruction's slot position in the bundle.
    }
}
