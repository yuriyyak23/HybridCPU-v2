
using System;
using System.Runtime.CompilerServices;
using YAKSys_Hybrid_CPU.Arch;

namespace YAKSys_Hybrid_CPU.Execution
{
    internal static partial class VectorALU
    {
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double ExecuteFMAFloat(uint op, double d, double s1, double s2)
        {
            return op switch
            {
                (uint)Processor.CPU_Core.InstructionsEnum.VFMADD => Math.FusedMultiplyAdd(d, s1, s2),   // d * s1 + s2
                (uint)Processor.CPU_Core.InstructionsEnum.VFMSUB => Math.FusedMultiplyAdd(d, s1, -s2),  // d * s1 - s2
                (uint)Processor.CPU_Core.InstructionsEnum.VFNMADD => Math.FusedMultiplyAdd(-d, s1, s2), // -(d * s1) + s2
                (uint)Processor.CPU_Core.InstructionsEnum.VFNMSUB => Math.FusedMultiplyAdd(-d, s1, -s2),// -(d * s1) - s2
                _ => 0.0
            };
        }

        /// <summary>
        /// Execute FMA operation for 32-bit floating-point values (FP32).
        /// Uses MathF.FusedMultiplyAdd for single-precision FMA with one rounding step.
        /// Ensures IEEE 754 compliance for binary32 format (24-bit mantissa).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ExecuteFMAFloat32(uint op, float d, float s1, float s2)
        {
            return op switch
            {
                (uint)Processor.CPU_Core.InstructionsEnum.VFMADD => MathF.FusedMultiplyAdd(d, s1, s2),   // d * s1 + s2
                (uint)Processor.CPU_Core.InstructionsEnum.VFMSUB => MathF.FusedMultiplyAdd(d, s1, -s2),  // d * s1 - s2
                (uint)Processor.CPU_Core.InstructionsEnum.VFNMADD => MathF.FusedMultiplyAdd(-d, s1, s2), // -(d * s1) + s2
                (uint)Processor.CPU_Core.InstructionsEnum.VFNMSUB => MathF.FusedMultiplyAdd(-d, s1, -s2),// -(d * s1) - s2
                _ => 0.0f
            };
        }

        /// <summary>
        /// Execute FMA operation for 64-bit floating-point values (FP64).
        /// Uses Math.FusedMultiplyAdd for double-precision FMA with one rounding step.
        /// Ensures IEEE 754 compliance for binary64 format (53-bit mantissa).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double ExecuteFMAFloat64(uint op, double d, double s1, double s2)
        {
            return op switch
            {
                (uint)Processor.CPU_Core.InstructionsEnum.VFMADD => Math.FusedMultiplyAdd(d, s1, s2),   // d * s1 + s2
                (uint)Processor.CPU_Core.InstructionsEnum.VFMSUB => Math.FusedMultiplyAdd(d, s1, -s2),  // d * s1 - s2
                (uint)Processor.CPU_Core.InstructionsEnum.VFNMADD => Math.FusedMultiplyAdd(-d, s1, s2), // -(d * s1) + s2
                (uint)Processor.CPU_Core.InstructionsEnum.VFNMSUB => Math.FusedMultiplyAdd(-d, s1, -s2),// -(d * s1) - s2
                _ => 0.0
            };
        }

        /// <summary>
        /// Apply rounding mode to a 32-bit floating-point value.
        /// Implements IEEE 754 rounding modes for binary32 format (24-bit mantissa).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ApplyRoundingFloat32(float value, byte roundingMode, ref Processor.CPU_Core core)
        {
            // Store original value to detect inexact results
            float original = value;

            // RoundingMode:
            // 0 = RNE (Round to Nearest, ties to Even) - IEEE 754 default
            // 1 = RTZ (Round Towards Zero) - truncate
            // 2 = RDN (Round Down) - towards -infinity
            // 3 = RUP (Round Up) - towards +infinity
            // 4 = RMM (Round to Nearest, ties to Max Magnitude)

            float rounded = roundingMode switch
            {
                0 => MathF.Round(value, MidpointRounding.ToEven),      // RNE
                1 => MathF.Truncate(value),                            // RTZ
                2 => MathF.Floor(value),                               // RDN
                3 => MathF.Ceiling(value),                             // RUP
                4 => MathF.Round(value, MidpointRounding.AwayFromZero), // RMM
                _ => value // Default: no rounding
            };

            // Track inexact result if rounding changed the value
            if (rounded != original && !float.IsNaN(rounded) && !float.IsInfinity(rounded))
            {
                TrackInexact(ref core);
            }

            return rounded;
        }

        /// <summary>
        /// Execute FMA operation for signed integer values.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static long ExecuteFMASignedInt(uint op, long d, long s1, long s2)
        {
            return op switch
            {
                (uint)Processor.CPU_Core.InstructionsEnum.VFMADD => (d * s1) + s2,
                (uint)Processor.CPU_Core.InstructionsEnum.VFMSUB => (d * s1) - s2,
                (uint)Processor.CPU_Core.InstructionsEnum.VFNMADD => -(d * s1) + s2,
                (uint)Processor.CPU_Core.InstructionsEnum.VFNMSUB => -(d * s1) - s2,
                _ => 0
            };
        }

        /// <summary>
        /// Execute FMA operation for unsigned integer values.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong ExecuteFMAUnsignedInt(uint op, ulong d, ulong s1, ulong s2)
        {
            return op switch
            {
                (uint)Processor.CPU_Core.InstructionsEnum.VFMADD => (d * s1) + s2,
                (uint)Processor.CPU_Core.InstructionsEnum.VFMSUB => (d * s1) - s2,
                // Note: Negation doesn't make sense for unsigned, treat as wrapping arithmetic
                (uint)Processor.CPU_Core.InstructionsEnum.VFNMADD => unchecked((0UL - (d * s1)) + s2),
                (uint)Processor.CPU_Core.InstructionsEnum.VFNMSUB => unchecked((0UL - (d * s1)) - s2),
                _ => 0
            };
        }

        // ========================================================================
        // Reduction Helper Functions
        // ========================================================================

        /// <summary>
        /// Get identity value for reduction operations (floating-point).
        /// Identity is the neutral element for the operation.
        /// </summary>
    }
}