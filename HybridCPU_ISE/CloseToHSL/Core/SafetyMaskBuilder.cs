using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;

namespace YAKSys_Hybrid_CPU.Core
{
    /// <summary>
    /// Builds a <see cref="SafetyMask128"/> from an instruction's canonical classification.
    /// Used to populate safety certificates attached to pipeline operations.
    ///
    /// G37: All build paths are runtime-computable — legality is determined without
    ///      mandatory compiler hints.  The opcode table and InternalOp category are
    ///      the sole authoritative sources.
    ///
    /// G38: SafetyMask128 is fully decoupled from opcode integer ranges and from
    ///      broken identity fields.  Masks encode only the instruction-class / 
    ///      serialization-class pair, or the InternalOpCategory / serialization pair
    ///      when built from an <see cref="InternalOp"/>.
    /// </summary>
    public static class SafetyMaskBuilder
    {
        // ─── Bit position assignments ─────────────────────────────────────────────
        // Low 64 bits: per-class base bits (one bit per InstructionClass value)
        // High 64 bits: per-serialization-class bits (one bit per SerializationClass value)

        /// <summary>
        /// Builds a <see cref="SafetyMask128"/> that encodes the canonical instruction class
        /// and serialization class in the low and high words respectively.
        /// The mask is a minimal certificate that uniquely identifies the instruction class
        /// pair and is used by the safety verifier during FSP scheduling.
        /// </summary>
        public static SafetyMask128 BuildFromClass(
            InstructionClass instrClass,
            SerializationClass serialClass)
        {
            ulong low  = 1UL << (int)instrClass;
            ulong high = 1UL << (int)serialClass;
            return new SafetyMask128(low, high);
        }

        // ─────────────────────────────────────────────────────────────────────
        // G37: Runtime-computable path — no compiler hints required.
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Builds a <see cref="SafetyMask128"/> from a raw opcode by querying the
        /// canonical <see cref="OpcodeRegistry"/> table at runtime.
        ///
        /// No compiler hint is required.  If the opcode is not registered the
        /// returned mask is <see cref="SafetyMask128.Zero"/> — callers must treat
        /// a zero mask as unclassified / maximally conservative.
        ///
        /// G37: Runtime legality computation without mandatory compiler hints.
        /// G38: Decoupled from opcode integer ranges; uses OpcodeRegistry table.
        /// </summary>
        public static SafetyMask128 BuildFromOpcode(uint opcode)
        {
            OpcodeInfo? info = OpcodeRegistry.GetInfo(opcode);
            if (info is null)
                return SafetyMask128.Zero;

            InstructionClass  instrClass  = ClassifyOpcodeInfo(info.Value);
            SerializationClass serialClass = SerializationClassForInstruction(instrClass);
            return BuildFromClass(instrClass, serialClass);
        }

        // ─────────────────────────────────────────────────────────────────────
        // G38: InternalOp-based path — fully decoupled from opcode ranges.
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Builds a <see cref="SafetyMask128"/> from a canonical <see cref="InternalOp"/>.
        ///
        /// The mask is derived solely from <see cref="InternalOp.Category"/> and the
        /// corresponding serialisation class — no opcode integer ranges are used.
        ///
        /// G38: Fully decoupled from opcode ranges and identity fields.
        /// </summary>
        public static SafetyMask128 BuildFromInternalOp(InternalOp op)
        {
            InstructionClass  instrClass  = InternalOpCategoryToInstructionClass(op.Category);
            SerializationClass serialClass = SerializationClassForInternalOp(op);
            return BuildFromClass(instrClass, serialClass);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Private helpers
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Derives the canonical <see cref="InstructionClass"/> from an
        /// <see cref="OpcodeInfo"/> record.  Category flags are used directly;
        /// no opcode integer comparison is performed.
        /// </summary>
        private static InstructionClass ClassifyOpcodeInfo(OpcodeInfo info)
        {
            if ((info.Flags & InstructionFlags.Privileged) != 0)
                return InstructionClass.System;
            if ((info.Flags & (InstructionFlags.MemoryRead | InstructionFlags.MemoryWrite)) != 0
                && (info.Flags & InstructionFlags.Atomic) != 0)
                return InstructionClass.Atomic;
            if ((info.Flags & (InstructionFlags.MemoryRead | InstructionFlags.MemoryWrite)) != 0)
                return InstructionClass.Memory;
            if (info.IsControlFlow)
                return InstructionClass.ControlFlow;
            if (info.IsVector)
                return InstructionClass.ScalarAlu; // vectors share the ALU slot class
            return InstructionClass.ScalarAlu;
        }

        /// <summary>
        /// Converts an <see cref="InternalOpCategory"/> to its corresponding
        /// <see cref="InstructionClass"/>.
        /// </summary>
        private static InstructionClass InternalOpCategoryToInstructionClass(InternalOpCategory category) =>
            category switch
            {
                InternalOpCategory.Computation  => InstructionClass.ScalarAlu,
                InternalOpCategory.MemoryAccess => InstructionClass.Memory,
                InternalOpCategory.ControlFlow  => InstructionClass.ControlFlow,
                InternalOpCategory.Atomic       => InstructionClass.Atomic,
                InternalOpCategory.Csr          => InstructionClass.Csr,
                InternalOpCategory.SysEvent     => InstructionClass.SmtVt,
                InternalOpCategory.VmxEvent     => InstructionClass.Vmx,
                _                               => InstructionClass.ScalarAlu,
            };

        /// <summary>
        /// Returns the canonical <see cref="SerializationClass"/> for a given
        /// <see cref="InstructionClass"/>.
        /// </summary>
        private static SerializationClass SerializationClassForInstruction(InstructionClass instrClass) =>
            instrClass switch
            {
                InstructionClass.ScalarAlu   => SerializationClass.Free,
                InstructionClass.Memory      => SerializationClass.MemoryOrdered,
                InstructionClass.ControlFlow => SerializationClass.Free,
                InstructionClass.Atomic      => SerializationClass.AtomicSerial,
                InstructionClass.System      => SerializationClass.FullSerial,
                InstructionClass.Csr         => SerializationClass.CsrOrdered,
                InstructionClass.SmtVt       => SerializationClass.FullSerial,
                InstructionClass.Vmx         => SerializationClass.VmxSerial,
                _                            => SerializationClass.Free,
            };

        /// <summary>
        /// Returns the canonical <see cref="SerializationClass"/> for an
        /// <see cref="InternalOp"/>, taking into account ordering flags on atomics.
        ///
        /// G38: Derived from op semantics, not from opcode integer ranges.
        /// </summary>
        private static SerializationClass SerializationClassForInternalOp(InternalOp op)
        {
            // Atomics with aq/rl flags need full serial ordering
            if (op.Category == InternalOpCategory.Atomic && op.HasOrdering)
                return SerializationClass.AtomicSerial;

            return SerializationClassForInstruction(InternalOpCategoryToInstructionClass(op.Category));
        }
    }
}
