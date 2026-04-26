
using System;
using System.Runtime.CompilerServices;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Memory;
using YAKSys_Hybrid_CPU.Core.Registers.Retire;

namespace YAKSys_Hybrid_CPU.Execution
{
    internal static partial class StreamEngine
    {
        private static bool IsBinaryOp(uint opCode)
        {
            return opCode switch
            {
                // Binary operations
                (uint)Processor.CPU_Core.InstructionsEnum.VADD => true,
                (uint)Processor.CPU_Core.InstructionsEnum.VSUB => true,
                (uint)Processor.CPU_Core.InstructionsEnum.VMUL => true,
                (uint)Processor.CPU_Core.InstructionsEnum.VDIV => true,
                (uint)Processor.CPU_Core.InstructionsEnum.VMOD => true,
                (uint)Processor.CPU_Core.InstructionsEnum.VXOR => true,
                (uint)Processor.CPU_Core.InstructionsEnum.VOR => true,
                (uint)Processor.CPU_Core.InstructionsEnum.VAND => true,

                // Shift operations (binary)
                (uint)Processor.CPU_Core.InstructionsEnum.VSLL => true,
                (uint)Processor.CPU_Core.InstructionsEnum.VSRL => true,
                (uint)Processor.CPU_Core.InstructionsEnum.VSRA => true,

                // Min/Max operations (binary)
                (uint)Processor.CPU_Core.InstructionsEnum.VMIN => true,
                (uint)Processor.CPU_Core.InstructionsEnum.VMAX => true,
                (uint)Processor.CPU_Core.InstructionsEnum.VMINU => true,
                (uint)Processor.CPU_Core.InstructionsEnum.VMAXU => true,

                // Unary operations
                (uint)Processor.CPU_Core.InstructionsEnum.VSQRT => false,
                (uint)Processor.CPU_Core.InstructionsEnum.VNOT => false,
                (uint)Processor.CPU_Core.InstructionsEnum.VREVERSE => false,
                (uint)Processor.CPU_Core.InstructionsEnum.VPOPCNT => false,
                (uint)Processor.CPU_Core.InstructionsEnum.VCLZ => false,
                (uint)Processor.CPU_Core.InstructionsEnum.VCTZ => false,
                (uint)Processor.CPU_Core.InstructionsEnum.VBREV8 => false,

                // Fail-safe default: only explicitly listed in-place two-source contours
                // are allowed to reuse the generic binary execution path.
                _ => false
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsGenericInPlaceVectorComputeOp(uint opCode)
        {
            OpcodeInfo? info = OpcodeRegistry.GetInfo(opCode);
            if (info is null || !info.Value.IsVector)
            {
                return false;
            }

            if (info.Value.Category is not (OpcodeCategory.Vector or OpcodeCategory.BitManip))
            {
                return false;
            }

            if (opCode is
                (uint)Processor.CPU_Core.InstructionsEnum.VLOAD or
                (uint)Processor.CPU_Core.InstructionsEnum.VSTORE or
                (uint)Processor.CPU_Core.InstructionsEnum.VGATHER or
                (uint)Processor.CPU_Core.InstructionsEnum.VSCATTER)
            {
                return false;
            }

            return !IsComparisonOp(opCode) &&
                   !IsMaskManipOp(opCode) &&
                   !IsFMAOp(opCode) &&
                   !IsReductionOp(opCode) &&
                   !IsDotProductOp(opCode) &&
                   !IsPredicativeMovementOp(opCode) &&
                   !IsPermutationOp(opCode) &&
                   !IsSlideOp(opCode);
        }

        /// <summary>
        /// Check if operation is a comparison instruction that generates a predicate mask.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsComparisonOp(uint opCode)
        {
            // Table-driven — replaces VCMPEQ–VCMPGE range check (B9).
            return OpcodeRegistry.IsComparisonOp(opCode);
        }

        /// <summary>
        /// Check if operation is a mask manipulation instruction.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsMaskManipOp(uint opCode)
        {
            // Table-driven — replaces VMAND–VPOPC range check (B9).
            return OpcodeRegistry.IsMaskManipOp(opCode);
        }

        /// <summary>
        /// Read a TriOpDesc descriptor from memory at the given address.
        /// TriOpDesc provides base addresses and strides for FMA tri-operand operations.
        /// Layout (20 bytes): SrcA(8) + SrcB(8) + StrideA(2) + StrideB(2)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static TriOpDesc ReadTriOpDesc(ulong descriptorAddr)
        {
            Span<byte> descBuf = stackalloc byte[20];
            BurstIO.BurstRead(descriptorAddr, descBuf, 1, 20, 20);

            TriOpDesc desc;
            desc.SrcA = BitConverter.ToUInt64(descBuf.Slice(0, 8));
            desc.SrcB = BitConverter.ToUInt64(descBuf.Slice(8, 8));
            desc.StrideA = BitConverter.ToUInt16(descBuf.Slice(16, 2));
            desc.StrideB = BitConverter.ToUInt16(descBuf.Slice(18, 2));
            return desc;
        }

        /// <summary>
        /// Check if operation is a FMA (Fused Multiply-Add) instruction.
        /// FMA operations require 3 source operands (ternary operation).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsFMAOp(uint opCode)
        {
            // Table-driven — replaces VFMADD–VFNMSUB range check (B9).
            return OpcodeRegistry.IsFmaOp(opCode);
        }

        /// <summary>
        /// Check if operation is a reduction instruction.
        /// Reduction operations collapse a vector to a scalar result.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsReductionOp(uint opCode)
        {
            // Table-driven — replaces VREDSUM–VREDXOR range check (B9).
            return OpcodeRegistry.IsReductionOp(opCode);
        }

        /// <summary>
        /// Check if operation is a dot-product instruction.
        /// Dot-product operations: VDOT_VV (signed), VDOTU_VV (unsigned), VDOTF_VV (float)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsDotProductOp(uint opCode)
        {
            return opCode == (uint)Processor.CPU_Core.InstructionsEnum.VDOT ||
                   opCode == (uint)Processor.CPU_Core.InstructionsEnum.VDOTU ||
                   opCode == (uint)Processor.CPU_Core.InstructionsEnum.VDOTF ||
                   opCode == (uint)Processor.CPU_Core.InstructionsEnum.VDOT_FP8;
        }

        /// <summary>
        /// Check if operation is a predicative movement instruction.
        /// Predicative movement operations: VCOMPRESS, VEXPAND (ARM SVE-style)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsPredicativeMovementOp(uint opCode)
        {
            return opCode == (uint)Processor.CPU_Core.InstructionsEnum.VCOMPRESS ||
                   opCode == (uint)Processor.CPU_Core.InstructionsEnum.VEXPAND;
        }

        /// <summary>
        /// Check if operation is a permutation/gather instruction.
        /// Permutation operations: VPERMUTE, VRGATHER (indexed reordering)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsPermutationOp(uint opCode)
        {
            return opCode == (uint)Processor.CPU_Core.InstructionsEnum.VPERMUTE ||
                   opCode == (uint)Processor.CPU_Core.InstructionsEnum.VRGATHER;
        }

        /// <summary>
        /// Check if operation is a slide instruction.
        /// Slide operations: VSLIDEUP, VSLIDEDOWN (element shifting)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsSlideOp(uint opCode)
        {
            return opCode == (uint)Processor.CPU_Core.InstructionsEnum.VSLIDEUP ||
                   opCode == (uint)Processor.CPU_Core.InstructionsEnum.VSLIDEDOWN;
        }
    }
}
