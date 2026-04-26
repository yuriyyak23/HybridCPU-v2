using HybridCPU_ISE.Arch;
using System;
using System.Collections.Generic;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;

namespace HybridCPU.Compiler.Core.IR
{
    /// <summary>
    /// Normalized instruction-level IR node derived from an encoded VLIW instruction.
    /// </summary>
    public sealed record IrInstruction(
        int Index,
        byte VirtualThreadId,
        ulong EncodedAddress,
        Processor.CPU_Core.InstructionsEnum Opcode,
        DataTypeEnum DataType,
        byte PredicateMask,
        ushort Immediate,
        uint StreamLength,
        ushort Stride,
        ushort RowStride,
        bool Indexed,
        bool Is2D,
        bool Reduction,
        bool TailAgnostic,
        bool MaskAgnostic,
        IReadOnlyList<IrOperand> Operands,
    IrInstructionAnnotation Annotation,
    IrSourceSpan? SourceSpan = null)
    {
        // ── ISA v4 Phase 02: canonical instruction classification ─────────────────────
        // These properties are computed from the opcode by the IR builder via
        // InstructionClassifier.Classify(Opcode). Stealability policy is carried separately
        // through Annotation/slot metadata rather than as an instruction field.

        /// <summary>
        /// Canonical ISA v4 instruction class.
        /// Determines pipeline routing and slot class assignment.
        /// </summary>
        public InstructionClass InstructionClass { get; init; } = InstructionClass.ScalarAlu;

        /// <summary>
        /// Canonical ISA v4 serialization class.
        /// Determines ordering and side-effect isolation requirements.
        /// </summary>
        public SerializationClass SerializationClass { get; init; } = SerializationClass.Free;
    }
}

