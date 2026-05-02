using System.Runtime.CompilerServices;

namespace YAKSys_Hybrid_CPU.Arch
{
    /// <summary>
    /// Maps <see cref="Processor.CPU_Core.InstructionsEnum"/> opcodes to their canonical ISA v4
    /// <see cref="InstructionClass"/> and <see cref="SerializationClass"/> values.
    /// <para>
    /// This is the single authoritative source for opcode-to-class mapping in the
    /// ISA v4 pipeline. All decoders, IR builders, and MicroOp constructors must use
    /// this classifier rather than maintaining independent mapping tables.
    /// </para>
    /// </summary>
    public static class InstructionClassifier
    {
        /// <summary>
        /// Returns the canonical <see cref="InstructionClass"/> for the given opcode.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static InstructionClass GetClass(Processor.CPU_Core.IsaOpcode opcode) =>
            GetClass(opcode.Value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static InstructionClass GetClass(ushort opcode)
        {
            if (OpcodeRegistry.TryGetPublishedSemantics(
                opcode,
                out InstructionClass publishedClass,
                out _))
            {
                return publishedClass;
            }

            return GetClass((Processor.CPU_Core.InstructionsEnum)opcode);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static InstructionClass GetClass(Processor.CPU_Core.InstructionsEnum opcode)
        {
            if (OpcodeRegistry.TryGetPublishedSemantics(
                opcode,
                out InstructionClass publishedClass,
                out _))
            {
                return publishedClass;
            }

            return opcode switch
            {
                // ── Scalar ALU (register-register and immediate integer) ──────────────
                Processor.CPU_Core.InstructionsEnum.Addition         => InstructionClass.ScalarAlu,
                Processor.CPU_Core.InstructionsEnum.Subtraction      => InstructionClass.ScalarAlu,
                Processor.CPU_Core.InstructionsEnum.Multiplication   => InstructionClass.ScalarAlu,
                Processor.CPU_Core.InstructionsEnum.Division         => InstructionClass.ScalarAlu,
                Processor.CPU_Core.InstructionsEnum.Modulus          => InstructionClass.ScalarAlu,
                Processor.CPU_Core.InstructionsEnum.ShiftLeft        => InstructionClass.ScalarAlu,
                Processor.CPU_Core.InstructionsEnum.ShiftRight       => InstructionClass.ScalarAlu,
                Processor.CPU_Core.InstructionsEnum.XOR              => InstructionClass.ScalarAlu,
                Processor.CPU_Core.InstructionsEnum.OR               => InstructionClass.ScalarAlu,
                Processor.CPU_Core.InstructionsEnum.AND              => InstructionClass.ScalarAlu,
                // ISA v2 scalar immediate ALU (152–160)
                Processor.CPU_Core.InstructionsEnum.ADDI             => InstructionClass.ScalarAlu,
                Processor.CPU_Core.InstructionsEnum.ANDI             => InstructionClass.ScalarAlu,
                Processor.CPU_Core.InstructionsEnum.ORI              => InstructionClass.ScalarAlu,
                Processor.CPU_Core.InstructionsEnum.XORI             => InstructionClass.ScalarAlu,
                Processor.CPU_Core.InstructionsEnum.SLTI             => InstructionClass.ScalarAlu,
                Processor.CPU_Core.InstructionsEnum.SLTIU            => InstructionClass.ScalarAlu,
                Processor.CPU_Core.InstructionsEnum.SLLI             => InstructionClass.ScalarAlu,
                Processor.CPU_Core.InstructionsEnum.SRLI             => InstructionClass.ScalarAlu,
                Processor.CPU_Core.InstructionsEnum.SRAI             => InstructionClass.ScalarAlu,
                // ISA v2 compare/set
                Processor.CPU_Core.InstructionsEnum.SLT              => InstructionClass.ScalarAlu,
                Processor.CPU_Core.InstructionsEnum.SLTU             => InstructionClass.ScalarAlu,
                // Upper immediate
                Processor.CPU_Core.InstructionsEnum.LUI              => InstructionClass.ScalarAlu,
                Processor.CPU_Core.InstructionsEnum.AUIPC            => InstructionClass.ScalarAlu,
                // ISA v4 M-extension additions: high-half multiply and unsigned divide/remainder
                Processor.CPU_Core.InstructionsEnum.MULH             => InstructionClass.ScalarAlu,
                Processor.CPU_Core.InstructionsEnum.MULHU            => InstructionClass.ScalarAlu,
                Processor.CPU_Core.InstructionsEnum.MULHSU           => InstructionClass.ScalarAlu,
                Processor.CPU_Core.InstructionsEnum.DIVU             => InstructionClass.ScalarAlu,
                Processor.CPU_Core.InstructionsEnum.REM              => InstructionClass.ScalarAlu,
                Processor.CPU_Core.InstructionsEnum.REMU             => InstructionClass.ScalarAlu,

                // ── Memory (typed scalar loads/stores plus retained absolute legacy Load/Store) ──
                Processor.CPU_Core.InstructionsEnum.Load             => InstructionClass.Memory,
                Processor.CPU_Core.InstructionsEnum.Store            => InstructionClass.Memory,
                Processor.CPU_Core.InstructionsEnum.LB               => InstructionClass.Memory,
                Processor.CPU_Core.InstructionsEnum.LBU              => InstructionClass.Memory,
                Processor.CPU_Core.InstructionsEnum.LH               => InstructionClass.Memory,
                Processor.CPU_Core.InstructionsEnum.LHU              => InstructionClass.Memory,
                Processor.CPU_Core.InstructionsEnum.LW               => InstructionClass.Memory,
                Processor.CPU_Core.InstructionsEnum.LWU              => InstructionClass.Memory,
                Processor.CPU_Core.InstructionsEnum.LD               => InstructionClass.Memory,
                Processor.CPU_Core.InstructionsEnum.SB               => InstructionClass.Memory,
                Processor.CPU_Core.InstructionsEnum.SH               => InstructionClass.Memory,
                Processor.CPU_Core.InstructionsEnum.SW               => InstructionClass.Memory,
                Processor.CPU_Core.InstructionsEnum.SD               => InstructionClass.Memory,
                Processor.CPU_Core.InstructionsEnum.DmaStreamCompute => InstructionClass.Memory,

                // ── Control Flow (jumps and conditional branches) ─────────────────────
                Processor.CPU_Core.InstructionsEnum.JAL              => InstructionClass.ControlFlow,
                Processor.CPU_Core.InstructionsEnum.JALR             => InstructionClass.ControlFlow,
                Processor.CPU_Core.InstructionsEnum.BEQ              => InstructionClass.ControlFlow,
                Processor.CPU_Core.InstructionsEnum.BNE              => InstructionClass.ControlFlow,
                Processor.CPU_Core.InstructionsEnum.BLT              => InstructionClass.ControlFlow,
                Processor.CPU_Core.InstructionsEnum.BGE              => InstructionClass.ControlFlow,
                Processor.CPU_Core.InstructionsEnum.BLTU             => InstructionClass.ControlFlow,
                Processor.CPU_Core.InstructionsEnum.BGEU             => InstructionClass.ControlFlow,

                // ── Atomic (LR/SC and AMO word and doubleword) ────────────────────────
                Processor.CPU_Core.InstructionsEnum.LR_W             => InstructionClass.Atomic,
                Processor.CPU_Core.InstructionsEnum.SC_W             => InstructionClass.Atomic,
                Processor.CPU_Core.InstructionsEnum.LR_D             => InstructionClass.Atomic,
                Processor.CPU_Core.InstructionsEnum.SC_D             => InstructionClass.Atomic,
                Processor.CPU_Core.InstructionsEnum.AMOADD_W         => InstructionClass.Atomic,
                Processor.CPU_Core.InstructionsEnum.AMOSWAP_W        => InstructionClass.Atomic,
                Processor.CPU_Core.InstructionsEnum.AMOOR_W          => InstructionClass.Atomic,
                Processor.CPU_Core.InstructionsEnum.AMOAND_W         => InstructionClass.Atomic,
                Processor.CPU_Core.InstructionsEnum.AMOXOR_W         => InstructionClass.Atomic,
                Processor.CPU_Core.InstructionsEnum.AMOMIN_W         => InstructionClass.Atomic,
                Processor.CPU_Core.InstructionsEnum.AMOMAX_W         => InstructionClass.Atomic,
                Processor.CPU_Core.InstructionsEnum.AMOMINU_W        => InstructionClass.Atomic,
                Processor.CPU_Core.InstructionsEnum.AMOMAXU_W        => InstructionClass.Atomic,

                // ── Atomic doubleword (AMO*_D — ISA v4 addition) ──────────────────────
                Processor.CPU_Core.InstructionsEnum.AMOADD_D         => InstructionClass.Atomic,
                Processor.CPU_Core.InstructionsEnum.AMOSWAP_D        => InstructionClass.Atomic,
                Processor.CPU_Core.InstructionsEnum.AMOOR_D          => InstructionClass.Atomic,
                Processor.CPU_Core.InstructionsEnum.AMOAND_D         => InstructionClass.Atomic,
                Processor.CPU_Core.InstructionsEnum.AMOXOR_D         => InstructionClass.Atomic,
                Processor.CPU_Core.InstructionsEnum.AMOMIN_D         => InstructionClass.Atomic,
                Processor.CPU_Core.InstructionsEnum.AMOMAX_D         => InstructionClass.Atomic,
                Processor.CPU_Core.InstructionsEnum.AMOMINU_D        => InstructionClass.Atomic,
                Processor.CPU_Core.InstructionsEnum.AMOMAXU_D        => InstructionClass.Atomic,

                // ── System / privilege ────────────────────────────────────────────────
                Processor.CPU_Core.InstructionsEnum.FENCE            => InstructionClass.System,
                Processor.CPU_Core.InstructionsEnum.FENCE_I          => InstructionClass.System,
                Processor.CPU_Core.InstructionsEnum.ECALL            => InstructionClass.System,
                Processor.CPU_Core.InstructionsEnum.EBREAK           => InstructionClass.System,
                Processor.CPU_Core.InstructionsEnum.MRET             => InstructionClass.System,
                Processor.CPU_Core.InstructionsEnum.SRET             => InstructionClass.System,
                Processor.CPU_Core.InstructionsEnum.WFI              => InstructionClass.System,
                Processor.CPU_Core.InstructionsEnum.Interrupt        => InstructionClass.System,
                Processor.CPU_Core.InstructionsEnum.InterruptReturn  => InstructionClass.System,
                Processor.CPU_Core.InstructionsEnum.VSETVL           => InstructionClass.System,
                Processor.CPU_Core.InstructionsEnum.VSETVLI          => InstructionClass.System,
                Processor.CPU_Core.InstructionsEnum.VSETIVLI         => InstructionClass.System,
                Processor.CPU_Core.InstructionsEnum.ACCEL_QUERY_CAPS => InstructionClass.System,
                Processor.CPU_Core.InstructionsEnum.ACCEL_SUBMIT     => InstructionClass.System,
                Processor.CPU_Core.InstructionsEnum.ACCEL_POLL       => InstructionClass.System,
                Processor.CPU_Core.InstructionsEnum.ACCEL_WAIT       => InstructionClass.System,
                Processor.CPU_Core.InstructionsEnum.ACCEL_CANCEL     => InstructionClass.System,
                Processor.CPU_Core.InstructionsEnum.ACCEL_FENCE      => InstructionClass.System,

                // ── CSR ───────────────────────────────────────────────────────────────
                Processor.CPU_Core.InstructionsEnum.CSRRW            => InstructionClass.Csr,
                Processor.CPU_Core.InstructionsEnum.CSRRS            => InstructionClass.Csr,
                Processor.CPU_Core.InstructionsEnum.CSRRC            => InstructionClass.Csr,
                Processor.CPU_Core.InstructionsEnum.CSRRWI           => InstructionClass.Csr,
                Processor.CPU_Core.InstructionsEnum.CSRRSI           => InstructionClass.Csr,
                Processor.CPU_Core.InstructionsEnum.CSRRCI           => InstructionClass.Csr,
                Processor.CPU_Core.InstructionsEnum.CSR_CLEAR        => InstructionClass.Csr,
                Processor.CPU_Core.InstructionsEnum.VSETVEXCPMASK    => InstructionClass.Csr,
                Processor.CPU_Core.InstructionsEnum.VSETVEXCPPRI     => InstructionClass.Csr,

                // ── SMT/VT synchronisation (ISA v4 mandatory) ────────────────────────
                Processor.CPU_Core.InstructionsEnum.STREAM_WAIT      => InstructionClass.SmtVt,
                Processor.CPU_Core.InstructionsEnum.STREAM_SETUP     => InstructionClass.System,
                Processor.CPU_Core.InstructionsEnum.STREAM_START     => InstructionClass.System,
                Processor.CPU_Core.InstructionsEnum.YIELD            => InstructionClass.SmtVt,
                Processor.CPU_Core.InstructionsEnum.WFE              => InstructionClass.SmtVt,
                Processor.CPU_Core.InstructionsEnum.SEV              => InstructionClass.SmtVt,
                Processor.CPU_Core.InstructionsEnum.POD_BARRIER      => InstructionClass.SmtVt,
                Processor.CPU_Core.InstructionsEnum.VT_BARRIER       => InstructionClass.SmtVt,

                // ── VMX instruction plane (ISA v4 mandatory) ─────────────────────────
                Processor.CPU_Core.InstructionsEnum.VMXON            => InstructionClass.Vmx,
                Processor.CPU_Core.InstructionsEnum.VMXOFF           => InstructionClass.Vmx,
                Processor.CPU_Core.InstructionsEnum.VMLAUNCH         => InstructionClass.Vmx,
                Processor.CPU_Core.InstructionsEnum.VMRESUME         => InstructionClass.Vmx,
                Processor.CPU_Core.InstructionsEnum.VMREAD           => InstructionClass.Vmx,
                Processor.CPU_Core.InstructionsEnum.VMWRITE          => InstructionClass.Vmx,
                Processor.CPU_Core.InstructionsEnum.VMCLEAR          => InstructionClass.Vmx,
                Processor.CPU_Core.InstructionsEnum.VMPTRLD          => InstructionClass.Vmx,

                // ── Vector / Matrix ──────────────────────────────────────────────────
                // NOTE: Vector and matrix instructions are not part of the mandatory ISA v4
                // core (they are handled as extension slots). They are classified as ScalarAlu
                // for pipeline routing until the optional extension registration system is
                // introduced in Phase 12.
                Processor.CPU_Core.InstructionsEnum.VGATHER          => InstructionClass.Memory,
                Processor.CPU_Core.InstructionsEnum.VSCATTER         => InstructionClass.Memory,
                Processor.CPU_Core.InstructionsEnum.VDOT_FP8         => InstructionClass.ScalarAlu,
                Processor.CPU_Core.InstructionsEnum.MTILE_LOAD       => InstructionClass.Memory,
                Processor.CPU_Core.InstructionsEnum.MTILE_STORE      => InstructionClass.Memory,
                Processor.CPU_Core.InstructionsEnum.MTILE_MACC       => InstructionClass.ScalarAlu,
                Processor.CPU_Core.InstructionsEnum.MTRANSPOSE       => InstructionClass.ScalarAlu,

                // ── Default: all remaining opcodes (vector core, stream, etc.) ─────────
                _ => InstructionClass.ScalarAlu,
            };
        }

        /// <summary>
        /// Returns the canonical <see cref="SerializationClass"/> for the given opcode.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SerializationClass GetSerializationClass(Processor.CPU_Core.IsaOpcode opcode) =>
            GetSerializationClass(opcode.Value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SerializationClass GetSerializationClass(ushort opcode)
        {
            if (OpcodeRegistry.TryGetPublishedSemantics(
                opcode,
                out _,
                out SerializationClass publishedSerialization))
            {
                return publishedSerialization;
            }

            return GetSerializationClass((Processor.CPU_Core.InstructionsEnum)opcode);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SerializationClass GetSerializationClass(Processor.CPU_Core.InstructionsEnum opcode)
        {
            if (OpcodeRegistry.TryGetPublishedSemantics(
                opcode,
                out _,
                out SerializationClass publishedSerialization))
            {
                return publishedSerialization;
            }

            return opcode switch
            {
                // ── Full serialization: privilege-level traps and returns ──────────────
                Processor.CPU_Core.InstructionsEnum.ECALL            => SerializationClass.FullSerial,
                Processor.CPU_Core.InstructionsEnum.EBREAK           => SerializationClass.FullSerial,
                Processor.CPU_Core.InstructionsEnum.MRET             => SerializationClass.FullSerial,
                Processor.CPU_Core.InstructionsEnum.SRET             => SerializationClass.FullSerial,
                Processor.CPU_Core.InstructionsEnum.WFI              => SerializationClass.FullSerial,
                Processor.CPU_Core.InstructionsEnum.Interrupt        => SerializationClass.FullSerial,
                Processor.CPU_Core.InstructionsEnum.InterruptReturn  => SerializationClass.FullSerial,
                Processor.CPU_Core.InstructionsEnum.VSETVL           => SerializationClass.FullSerial,
                Processor.CPU_Core.InstructionsEnum.VSETVLI          => SerializationClass.FullSerial,
                Processor.CPU_Core.InstructionsEnum.VSETIVLI         => SerializationClass.FullSerial,
                // Barriers: FENCE.I is a full pipeline flush
                Processor.CPU_Core.InstructionsEnum.FENCE_I          => SerializationClass.FullSerial,
                // Stream operations require full synchronization at wait point
                Processor.CPU_Core.InstructionsEnum.STREAM_WAIT      => SerializationClass.FullSerial,
                Processor.CPU_Core.InstructionsEnum.STREAM_SETUP     => SerializationClass.FullSerial,
                Processor.CPU_Core.InstructionsEnum.STREAM_START     => SerializationClass.FullSerial,
                // Vector exception control: serializes pipeline
                Processor.CPU_Core.InstructionsEnum.VSETVEXCPMASK    => SerializationClass.FullSerial,
                Processor.CPU_Core.InstructionsEnum.VSETVEXCPPRI     => SerializationClass.FullSerial,
                Processor.CPU_Core.InstructionsEnum.ACCEL_WAIT        => SerializationClass.FullSerial,
                Processor.CPU_Core.InstructionsEnum.ACCEL_CANCEL      => SerializationClass.FullSerial,
                Processor.CPU_Core.InstructionsEnum.ACCEL_FENCE       => SerializationClass.FullSerial,

                // ── Memory ordering: FENCE (store-load ordering) ──────────────────────
                Processor.CPU_Core.InstructionsEnum.FENCE            => SerializationClass.MemoryOrdered,
                Processor.CPU_Core.InstructionsEnum.Store            => SerializationClass.MemoryOrdered,
                // Typed stores observe memory ordering
                Processor.CPU_Core.InstructionsEnum.SB               => SerializationClass.MemoryOrdered,
                Processor.CPU_Core.InstructionsEnum.SH               => SerializationClass.MemoryOrdered,
                Processor.CPU_Core.InstructionsEnum.SW               => SerializationClass.MemoryOrdered,
                Processor.CPU_Core.InstructionsEnum.SD               => SerializationClass.MemoryOrdered,
                Processor.CPU_Core.InstructionsEnum.DmaStreamCompute => SerializationClass.MemoryOrdered,
                Processor.CPU_Core.InstructionsEnum.ACCEL_SUBMIT     => SerializationClass.MemoryOrdered,
                Processor.CPU_Core.InstructionsEnum.VSCATTER         => SerializationClass.MemoryOrdered,
                Processor.CPU_Core.InstructionsEnum.MTILE_STORE      => SerializationClass.MemoryOrdered,

                // ── Atomic: full bus ownership ────────────────────────────────────────
                Processor.CPU_Core.InstructionsEnum.LR_W             => SerializationClass.AtomicSerial,
                Processor.CPU_Core.InstructionsEnum.SC_W             => SerializationClass.AtomicSerial,
                Processor.CPU_Core.InstructionsEnum.LR_D             => SerializationClass.AtomicSerial,
                Processor.CPU_Core.InstructionsEnum.SC_D             => SerializationClass.AtomicSerial,
                Processor.CPU_Core.InstructionsEnum.AMOADD_W         => SerializationClass.AtomicSerial,
                Processor.CPU_Core.InstructionsEnum.AMOSWAP_W        => SerializationClass.AtomicSerial,
                Processor.CPU_Core.InstructionsEnum.AMOOR_W          => SerializationClass.AtomicSerial,
                Processor.CPU_Core.InstructionsEnum.AMOAND_W         => SerializationClass.AtomicSerial,
                Processor.CPU_Core.InstructionsEnum.AMOXOR_W         => SerializationClass.AtomicSerial,
                Processor.CPU_Core.InstructionsEnum.AMOMIN_W         => SerializationClass.AtomicSerial,
                Processor.CPU_Core.InstructionsEnum.AMOMAX_W         => SerializationClass.AtomicSerial,
                Processor.CPU_Core.InstructionsEnum.AMOMINU_W        => SerializationClass.AtomicSerial,
                Processor.CPU_Core.InstructionsEnum.AMOMAXU_W        => SerializationClass.AtomicSerial,

                // ── Atomic doubleword (AMO*_D — ISA v4 addition) ──────────────────────
                Processor.CPU_Core.InstructionsEnum.AMOADD_D         => SerializationClass.AtomicSerial,
                Processor.CPU_Core.InstructionsEnum.AMOSWAP_D        => SerializationClass.AtomicSerial,
                Processor.CPU_Core.InstructionsEnum.AMOOR_D          => SerializationClass.AtomicSerial,
                Processor.CPU_Core.InstructionsEnum.AMOAND_D         => SerializationClass.AtomicSerial,
                Processor.CPU_Core.InstructionsEnum.AMOXOR_D         => SerializationClass.AtomicSerial,
                Processor.CPU_Core.InstructionsEnum.AMOMIN_D         => SerializationClass.AtomicSerial,
                Processor.CPU_Core.InstructionsEnum.AMOMAX_D         => SerializationClass.AtomicSerial,
                Processor.CPU_Core.InstructionsEnum.AMOMINU_D        => SerializationClass.AtomicSerial,
                Processor.CPU_Core.InstructionsEnum.AMOMAXU_D        => SerializationClass.AtomicSerial,

                // ── SMT/VT sync: VtSerialized ordering (pod-scoped barrier) ──────────
                Processor.CPU_Core.InstructionsEnum.YIELD            => SerializationClass.Free,
                Processor.CPU_Core.InstructionsEnum.WFE              => SerializationClass.FullSerial,
                Processor.CPU_Core.InstructionsEnum.SEV              => SerializationClass.FullSerial,
                Processor.CPU_Core.InstructionsEnum.POD_BARRIER      => SerializationClass.FullSerial,
                Processor.CPU_Core.InstructionsEnum.VT_BARRIER       => SerializationClass.FullSerial,

                // ── VMX: full pipeline serialization ─────────────────────────────────
                Processor.CPU_Core.InstructionsEnum.VMXON            => SerializationClass.VmxSerial,
                Processor.CPU_Core.InstructionsEnum.VMXOFF           => SerializationClass.VmxSerial,
                Processor.CPU_Core.InstructionsEnum.VMLAUNCH         => SerializationClass.VmxSerial,
                Processor.CPU_Core.InstructionsEnum.VMRESUME         => SerializationClass.VmxSerial,
                Processor.CPU_Core.InstructionsEnum.VMREAD           => SerializationClass.VmxSerial,
                Processor.CPU_Core.InstructionsEnum.VMWRITE          => SerializationClass.VmxSerial,
                Processor.CPU_Core.InstructionsEnum.VMCLEAR          => SerializationClass.VmxSerial,
                Processor.CPU_Core.InstructionsEnum.VMPTRLD          => SerializationClass.VmxSerial,

                // ── CSR-ordered: CSR hazard must be checked ───────────────────────────
                Processor.CPU_Core.InstructionsEnum.CSRRW            => SerializationClass.CsrOrdered,
                Processor.CPU_Core.InstructionsEnum.CSRRS            => SerializationClass.CsrOrdered,
                Processor.CPU_Core.InstructionsEnum.CSRRC            => SerializationClass.CsrOrdered,
                Processor.CPU_Core.InstructionsEnum.CSRRWI           => SerializationClass.CsrOrdered,
                Processor.CPU_Core.InstructionsEnum.CSRRSI           => SerializationClass.CsrOrdered,
                Processor.CPU_Core.InstructionsEnum.CSRRCI           => SerializationClass.CsrOrdered,
                Processor.CPU_Core.InstructionsEnum.CSR_CLEAR        => SerializationClass.CsrOrdered,
                Processor.CPU_Core.InstructionsEnum.ACCEL_QUERY_CAPS => SerializationClass.CsrOrdered,
                Processor.CPU_Core.InstructionsEnum.ACCEL_POLL       => SerializationClass.CsrOrdered,

                // ── Default: free — no ordering constraints ───────────────────────────
                _ => SerializationClass.Free,
            };
        }

        /// <summary>
        /// Returns both the <see cref="InstructionClass"/> and <see cref="SerializationClass"/>
        /// for the given opcode in a single call.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (InstructionClass Class, SerializationClass Serialization)
            Classify(Processor.CPU_Core.IsaOpcode opcode)
            => (GetClass(opcode), GetSerializationClass(opcode));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (InstructionClass Class, SerializationClass Serialization)
            Classify(ushort opcode)
            => (GetClass(opcode), GetSerializationClass(opcode));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (InstructionClass Class, SerializationClass Serialization)
            Classify(Processor.CPU_Core.InstructionsEnum opcode)
            => (GetClass(opcode), GetSerializationClass(opcode));
    }
}
