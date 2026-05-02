using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core.Execution.DmaStreamCompute;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;

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

        /// <summary>
        /// Typed decoded sideband for descriptor-backed lane6 memory-memory compute.
        /// The raw VLIW slot layout is not extended to carry this ABI. Phase 10 native
        /// decode requires the guard-accepted descriptor payload before projector
        /// admission can produce the lane6 DmaStreamComputeMicroOp carrier.
        /// </summary>
        public DmaStreamComputeDescriptorReference? DmaStreamComputeDescriptorReference { get; init; }

        /// <summary>
        /// Guard-accepted descriptor payload that survived compiler annotation,
        /// VLIW decode, InstructionIR, and projector transport without using raw
        /// reserved VLIW fields as ABI storage.
        /// </summary>
        public DmaStreamComputeDescriptor? DmaStreamComputeDescriptor { get; init; }

        /// <summary>
        /// Typed descriptor reference for lane7 L7-SDC external accelerator commands.
        /// This is sideband evidence only; raw reserved VLIW fields and raw VT hints are
        /// never descriptor ABI or owner/domain authority.
        /// </summary>
        public AcceleratorDescriptorReference? AcceleratorCommandDescriptorReference { get; init; }

        /// <summary>
        /// Parsed L7-SDC command descriptor payload that survived native carrier
        /// cleanliness validation at decode. The L7-SDC transport uses this only to
        /// materialize a guarded lane7 carrier; it does not grant backend execution,
        /// queue admission, token lifecycle, staged writes, or commit authority.
        /// </summary>
        public AcceleratorCommandDescriptor? AcceleratorCommandDescriptor { get; init; }

        public InstructionIR SetAcceleratorCommandDescriptor(
            AcceleratorCommandDescriptor descriptor)
        {
            System.ArgumentNullException.ThrowIfNull(descriptor);
            return this with
            {
                AcceleratorCommandDescriptor = descriptor,
                AcceleratorCommandDescriptorReference = descriptor.DescriptorReference
            };
        }

        // NOTE: No hint fields here.
        // Scheduling hints (branch prediction, stealability, locality, thermal) belong
        // in SlotMetadata attached to the instruction's slot position in the bundle.
    }
}
