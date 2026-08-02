using HybridCPU_ISE.Arch;
using System;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU.Compiler.Core.IR
{
    /// <summary>
    /// Shared compiler-side opcode semantics helpers.
    /// Keeps IR construction, hazard modeling, and structural resource classification
    /// aligned on the same ISA/runtime surface without reopening larger pipeline design.
    /// </summary>
    internal static class HybridCpuOpcodeSemantics
    {
        public static OpcodeInfo? GetOpcodeInfo(InstructionsEnum opcode)
        {
            return OpcodeRegistry.GetInfo((uint)NormalizeSemanticOpcode(opcode));
        }

        public static InstructionsEnum NormalizeSemanticOpcode(InstructionsEnum opcode) => opcode;

        public static bool IsLoadStoreOpcode(InstructionsEnum opcode, OpcodeInfo? opcodeInfo = null)
        {
            if (IsDmaStreamComputeOpcode(opcode))
            {
                return false;
            }

            OpcodeInfo? resolvedOpcodeInfo = ResolveOpcodeInfo(opcode, opcodeInfo);
            if (resolvedOpcodeInfo.HasValue &&
                resolvedOpcodeInfo.Value.InstructionClass is InstructionClass.Memory or InstructionClass.Atomic)
            {
                return true;
            }

            return TryResolveRetainedCompatibilityScalarMemoryDirection(opcode, out _) ||
                TryResolveRetainedCompatibilityVectorTransferDirection(opcode, out _);
        }

        public static bool UsesLoadStoreReadPath(InstructionsEnum opcode, OpcodeInfo? opcodeInfo = null)
        {
            if (IsDmaStreamComputeOpcode(opcode))
            {
                return false;
            }

            OpcodeInfo? resolvedOpcodeInfo = ResolveOpcodeInfo(opcode, opcodeInfo);
            if (!IsLoadStoreOpcode(opcode, resolvedOpcodeInfo))
            {
                return false;
            }

            if (resolvedOpcodeInfo.HasValue)
            {
                OpcodeInfo publishedOpcodeInfo = resolvedOpcodeInfo.Value;
                if (publishedOpcodeInfo.InstructionClass == InstructionClass.Atomic)
                {
                    return (publishedOpcodeInfo.Flags & InstructionFlags.MemoryRead) != 0 ||
                        (publishedOpcodeInfo.Flags & InstructionFlags.MemoryWrite) != 0;
                }

                if (publishedOpcodeInfo.InstructionClass == InstructionClass.Memory &&
                    publishedOpcodeInfo.Category == OpcodeCategory.Vector)
                {
                    return publishedOpcodeInfo.SerializationClass != SerializationClass.MemoryOrdered;
                }

                return (publishedOpcodeInfo.Flags & InstructionFlags.MemoryRead) != 0;
            }

            if (TryResolveRetainedCompatibilityVectorTransferDirection(opcode, out bool isVectorWriteContour))
            {
                return !isVectorWriteContour;
            }

            return TryResolveRetainedCompatibilityScalarMemoryDirection(opcode, out bool isWriteContour) &&
                !isWriteContour;
        }

        public static bool UsesLoadStoreWritePath(InstructionsEnum opcode, OpcodeInfo? opcodeInfo = null)
        {
            if (IsDmaStreamComputeOpcode(opcode))
            {
                return false;
            }

            OpcodeInfo? resolvedOpcodeInfo = ResolveOpcodeInfo(opcode, opcodeInfo);
            if (!IsLoadStoreOpcode(opcode, resolvedOpcodeInfo))
            {
                return false;
            }

            if (resolvedOpcodeInfo.HasValue)
            {
                OpcodeInfo publishedOpcodeInfo = resolvedOpcodeInfo.Value;
                if (publishedOpcodeInfo.InstructionClass == InstructionClass.Memory &&
                    publishedOpcodeInfo.Category == OpcodeCategory.Vector)
                {
                    return publishedOpcodeInfo.SerializationClass == SerializationClass.MemoryOrdered;
                }

                return (publishedOpcodeInfo.Flags & InstructionFlags.MemoryWrite) != 0;
            }

            if (TryResolveRetainedCompatibilityVectorTransferDirection(opcode, out bool isVectorWriteContour))
            {
                return isVectorWriteContour;
            }

            return TryResolveRetainedCompatibilityScalarMemoryDirection(opcode, out bool isWriteContour) &&
                isWriteContour;
        }

        public static bool UsesAddressGeneration(InstructionsEnum opcode, OpcodeInfo? opcodeInfo = null)
        {
            return UsesLoadStoreReadPath(opcode, opcodeInfo) || UsesLoadStoreWritePath(opcode, opcodeInfo);
        }

        public static bool IsDmaStreamComputeOpcode(InstructionsEnum opcode) =>
            opcode == InstructionsEnum.DmaStreamCompute;

        public static bool IsVectorInstruction(InstructionsEnum opcode, OpcodeInfo? opcodeInfo = null)
        {
            OpcodeInfo? resolvedOpcodeInfo = ResolveOpcodeInfo(opcode, opcodeInfo);
            if (resolvedOpcodeInfo.HasValue)
            {
                return resolvedOpcodeInfo.Value.IsVector;
            }

            return TryResolveRetainedCompatibilityVectorTransferDirection(opcode, out _);
        }

        public static bool IsSystemInstruction(InstructionsEnum opcode, OpcodeInfo? opcodeInfo = null)
        {
            OpcodeInfo? resolvedOpcodeInfo = ResolveOpcodeInfo(opcode, opcodeInfo);
            if (resolvedOpcodeInfo.HasValue &&
                resolvedOpcodeInfo.Value.InstructionClass is InstructionClass.System or
                    InstructionClass.Csr or
                    InstructionClass.SmtVt or
                    InstructionClass.Vmx)
            {
                return true;
            }

            return InstructionClassifier.GetClass(opcode) is
                InstructionClass.System or
                InstructionClass.Csr or
                InstructionClass.SmtVt or
                InstructionClass.Vmx;
        }

        public static bool IsBarrierLike(InstructionsEnum opcode)
        {
            OpcodeInfo? resolvedOpcodeInfo = GetOpcodeInfo(opcode);
            if (resolvedOpcodeInfo.HasValue)
            {
                if (resolvedOpcodeInfo.Value.InstructionClass == InstructionClass.System)
                {
                    InstructionFlags flags = resolvedOpcodeInfo.Value.Flags;

                    return !resolvedOpcodeInfo.Value.IsVector &&
                           resolvedOpcodeInfo.Value.OperandCount == 0 &&
                           (flags & InstructionFlags.Privileged) == 0;
                }

                if (resolvedOpcodeInfo.Value.InstructionClass == InstructionClass.Atomic)
                {
                    InstructionFlags flags = resolvedOpcodeInfo.Value.Flags;

                    return (resolvedOpcodeInfo.Value.OperandCount == 1 &&
                            (flags & InstructionFlags.MemoryRead) != 0 &&
                            (flags & InstructionFlags.MemoryWrite) == 0)
                        || (resolvedOpcodeInfo.Value.OperandCount == 2 &&
                            (flags & InstructionFlags.MemoryRead) == 0 &&
                            (flags & InstructionFlags.MemoryWrite) != 0);
                }

                return false;
            }

            return false;
        }

        public static bool IsSignedDivideTrapContour(
            InstructionsEnum opcode,
            OpcodeInfo? opcodeInfo = null)
        {
            OpcodeInfo? resolvedOpcodeInfo = ResolveOpcodeInfo(opcode, opcodeInfo);
            if (!resolvedOpcodeInfo.HasValue ||
                resolvedOpcodeInfo.Value.InstructionClass != InstructionClass.ScalarAlu)
            {
                return false;
            }

            if (resolvedOpcodeInfo.Value.IsVector)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(opcode),
                    opcode,
                    "Vector opcodes do not use the compiler scalar trap contour.");
            }

            return opcode == InstructionsEnum.DIV;
        }


        internal static (InstructionsEnum Opcode, ulong PackedCompareRegisters) NormalizeRetainedConditionalWrapperForEmission(
            InstructionsEnum opcode, ulong packedCompareRegisters)
        {
            return (opcode, packedCompareRegisters);

            //!! to delete

        }

        internal static bool TryResolveRetainedCompatibilityControlFlowKind(
            InstructionsEnum opcode,
            out IrControlFlowKind controlFlowKind)
        {
            controlFlowKind = IrControlFlowKind.None;
            return false;
        }

        internal static bool TryResolveRetainedCompatibilityScalarMemoryDirection(
            InstructionsEnum opcode,
            out bool isWriteContour)
        {
            isWriteContour = false;
            return false;
        }

        internal static bool TryResolveRetainedCompatibilityVectorTransferDirection(
            InstructionsEnum opcode,
            out bool isWriteContour)
        {
            isWriteContour = opcode == InstructionsEnum.VSTORE;

            return opcode is InstructionsEnum.VLOAD or InstructionsEnum.VSTORE;
        }

        private static OpcodeInfo? ResolveOpcodeInfo(InstructionsEnum opcode, OpcodeInfo? opcodeInfo)
        {
            return opcodeInfo ?? GetOpcodeInfo(opcode);
        }

        private static ulong SwapPackedCompareRegisters(ulong packedRegisters)
        {
            if (!VLIW_Instruction.TryUnpackArchRegs(
                    packedRegisters,
                    out byte rd,
                    out byte rs1,
                    out byte rs2))
            {
                return packedRegisters;
            }

            return VLIW_Instruction.PackArchRegs(rd, rs2, rs1);
        }
    }
}
