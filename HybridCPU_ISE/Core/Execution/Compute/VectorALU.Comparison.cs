using HybridCPU_ISE.Arch;

using System;
using System.Runtime.CompilerServices;
using YAKSys_Hybrid_CPU.Arch;

namespace YAKSys_Hybrid_CPU.Execution
{
    internal static partial class VectorALU
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong ApplyComparison(
            uint op,
            DataTypeEnum dt,
            ReadOnlySpan<byte> a,
            ReadOnlySpan<byte> b,
            int elemSize,
            ulong vl,
            byte predIndex,
            ref Processor.CPU_Core core)
        {
            // Mark vector unit as dirty (for context switching)
            MarkVectorDirty(ref core);

            ulong resultMask = 0;

            // Limit comparison to 64 lanes (predicate register width)
            ulong effectiveVL = Math.Min(vl, 64);

            for (ulong lane = 0; lane < effectiveVL; lane++)
            {
                // Check input predicate mask: skip inactive lanes
                bool laneActive = core.LaneActive(predIndex, (int)lane);
                if (!laneActive)
                {
                    // Masked-off lane: result bit = 0
                    continue;
                }

                int off = (int)(lane * (ulong)elemSize);
                bool compResult = false;

                // Perform type-specific comparison
                if (DataTypeUtils.IsFloatingPoint(dt))
                {
                    double x = ElementCodec.LoadF(a, off, dt);
                    double y = ElementCodec.LoadF(b, off, dt);
                    compResult = ExecuteComparisonFloat(op, x, y);
                }
                else if (DataTypeUtils.IsSignedInteger(dt))
                {
                    long x = ElementCodec.LoadI(a, off, dt);
                    long y = ElementCodec.LoadI(b, off, dt);
                    compResult = ExecuteComparisonSignedInt(op, x, y);
                }
                else // Unsigned integer
                {
                    ulong x = ElementCodec.LoadU(a, off, dt);
                    ulong y = ElementCodec.LoadU(b, off, dt);
                    compResult = ExecuteComparisonUnsignedInt(op, x, y);
                }

                // Set result bit if comparison is true
                if (compResult)
                {
                    resultMask |= (1UL << (int)lane);
                }
            }

            return resultMask;
        }

        /// <summary>
        /// Execute comparison for floating-point values.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool ExecuteComparisonFloat(uint op, double x, double y)
        {
            return op switch
            {
                (uint)Processor.CPU_Core.InstructionsEnum.VCMPEQ => x == y,
                (uint)Processor.CPU_Core.InstructionsEnum.VCMPNE => x != y,
                (uint)Processor.CPU_Core.InstructionsEnum.VCMPLT => x < y,
                (uint)Processor.CPU_Core.InstructionsEnum.VCMPLE => x <= y,
                (uint)Processor.CPU_Core.InstructionsEnum.VCMPGT => x > y,
                (uint)Processor.CPU_Core.InstructionsEnum.VCMPGE => x >= y,
                _ => false
            };
        }

        /// <summary>
        /// Execute comparison for signed integer values.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool ExecuteComparisonSignedInt(uint op, long x, long y)
        {
            return op switch
            {
                (uint)Processor.CPU_Core.InstructionsEnum.VCMPEQ => x == y,
                (uint)Processor.CPU_Core.InstructionsEnum.VCMPNE => x != y,
                (uint)Processor.CPU_Core.InstructionsEnum.VCMPLT => x < y,
                (uint)Processor.CPU_Core.InstructionsEnum.VCMPLE => x <= y,
                (uint)Processor.CPU_Core.InstructionsEnum.VCMPGT => x > y,
                (uint)Processor.CPU_Core.InstructionsEnum.VCMPGE => x >= y,
                _ => false
            };
        }

        /// <summary>
        /// Execute comparison for unsigned integer values.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool ExecuteComparisonUnsignedInt(uint op, ulong x, ulong y)
        {
            return op switch
            {
                (uint)Processor.CPU_Core.InstructionsEnum.VCMPEQ => x == y,
                (uint)Processor.CPU_Core.InstructionsEnum.VCMPNE => x != y,
                (uint)Processor.CPU_Core.InstructionsEnum.VCMPLT => x < y,
                (uint)Processor.CPU_Core.InstructionsEnum.VCMPLE => x <= y,
                (uint)Processor.CPU_Core.InstructionsEnum.VCMPGT => x > y,
                (uint)Processor.CPU_Core.InstructionsEnum.VCMPGE => x >= y,
                _ => false
            };
        }

        // ========================================================================
        // Predicate Mask Manipulation Instructions
        // ========================================================================

        /// <summary>
        /// Apply mask-to-mask logical operation.
        /// Operates on predicate registers directly (bit-level operations).
        ///
        /// RVV-style mask operations:
        /// - VMAND: md = ms1 & ms2 (mask AND)
        /// - VMOR: md = ms1 | ms2 (mask OR)
        /// - VMXOR: md = ms1 ^ ms2 (mask XOR)
        ///
        /// Use cases:
        /// - Combining multiple comparison results
        /// - Building complex predicates
        /// - Mask register manipulation
        /// </summary>
        /// <param name="op">Mask operation code</param>
        /// <param name="mask1">First source mask (64-bit)</param>
        /// <param name="mask2">Second source mask (64-bit)</param>
        /// <returns>Result mask after operation</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong ApplyMaskBinary(uint op, ulong mask1, ulong mask2)
        {
            return op switch
            {
                (uint)Processor.CPU_Core.InstructionsEnum.VMAND => mask1 & mask2,
                (uint)Processor.CPU_Core.InstructionsEnum.VMOR => mask1 | mask2,
                (uint)Processor.CPU_Core.InstructionsEnum.VMXOR => mask1 ^ mask2,
                _ => 0
            };
        }

        /// <summary>
        /// Apply unary mask operation.
        /// Operates on a single predicate register.
        ///
        /// RVV-style unary mask operations:
        /// - VMNOT: md = ~ms (mask NOT / invert)
        /// </summary>
        /// <param name="op">Mask operation code</param>
        /// <param name="mask">Source mask (64-bit)</param>
        /// <returns>Result mask after operation</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong ApplyMaskUnary(uint op, ulong mask)
        {
            return op switch
            {
                (uint)Processor.CPU_Core.InstructionsEnum.VMNOT => ~mask,
                _ => 0
            };
        }

        /// <summary>
        /// Count number of set bits in mask (population count).
        /// VPOPC instruction: counts active lanes in predicate register.
        ///
        /// Use cases:
        /// - Determine how many elements passed a filter
        /// - Dynamic workload calculation
        /// - Conditional reduction operations
        /// </summary>
        /// <param name="mask">64-bit predicate mask</param>
        /// <param name="vl">Vector length (limits count to first VL bits)</param>
        /// <returns>Number of set bits in mask[0:vl-1]</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong MaskPopCount(ulong mask, ulong vl)
        {
            // Limit to VL bits (mask off bits beyond VL)
            ulong effectiveVL = Math.Min(vl, 64);
            ulong maskedValue = mask & ((1UL << (int)effectiveVL) - 1);

            // Use hardware popcount if available, fallback to software
            return (ulong)System.Numerics.BitOperations.PopCount(maskedValue);
        }

        // ========================================================================
        // FMA (Fused Multiply-Add) Helper Functions
        // ========================================================================

        /// <summary>
        /// Execute FMA operation for floating-point values.
        /// Uses Math.FusedMultiplyAdd when available for better accuracy.
    }
}
