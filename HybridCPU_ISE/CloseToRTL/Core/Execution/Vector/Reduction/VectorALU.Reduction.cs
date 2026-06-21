
using System;
using System.Runtime.CompilerServices;
using YAKSys_Hybrid_CPU.Arch;

namespace YAKSys_Hybrid_CPU.Execution
{
    internal static partial class VectorALU
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double GetReductionIdentityFloat(uint op)
        {
            return op switch
            {
                (uint)Processor.CPU_Core.InstructionsEnum.VREDSUM => 0.0,
                (uint)Processor.CPU_Core.InstructionsEnum.VREDMAX => double.MinValue,
                (uint)Processor.CPU_Core.InstructionsEnum.VREDMIN => double.MaxValue,
                _ => 0.0
            };
        }

        /// <summary>
        /// Get identity value for reduction operations (signed integer).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static long GetReductionIdentitySignedInt(uint op)
        {
            return op switch
            {
                (uint)Processor.CPU_Core.InstructionsEnum.VREDSUM => 0,
                (uint)Processor.CPU_Core.InstructionsEnum.VREDMAX => long.MinValue,
                (uint)Processor.CPU_Core.InstructionsEnum.VREDMIN => long.MaxValue,
                (uint)Processor.CPU_Core.InstructionsEnum.VREDAND => ~0L, // All bits set
                (uint)Processor.CPU_Core.InstructionsEnum.VREDOR => 0,
                (uint)Processor.CPU_Core.InstructionsEnum.VREDXOR => 0,
                _ => 0
            };
        }

        /// <summary>
        /// Get identity value for reduction operations (unsigned integer).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong GetReductionIdentityUnsignedInt(uint op)
        {
            return op switch
            {
                (uint)Processor.CPU_Core.InstructionsEnum.VREDSUM => 0,
                (uint)Processor.CPU_Core.InstructionsEnum.VREDMAX => ulong.MinValue,
                (uint)Processor.CPU_Core.InstructionsEnum.VREDMAXU => ulong.MinValue,
                (uint)Processor.CPU_Core.InstructionsEnum.VREDMIN => ulong.MaxValue,
                (uint)Processor.CPU_Core.InstructionsEnum.VREDMINU => ulong.MaxValue,
                (uint)Processor.CPU_Core.InstructionsEnum.VREDAND => ~0UL, // All bits set
                (uint)Processor.CPU_Core.InstructionsEnum.VREDOR => 0,
                (uint)Processor.CPU_Core.InstructionsEnum.VREDXOR => 0,
                _ => 0
            };
        }

        /// <summary>
        /// Execute reduction operation for floating-point values.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double ExecuteReductionFloat(uint op, double acc, double value)
        {
            return op switch
            {
                (uint)Processor.CPU_Core.InstructionsEnum.VREDSUM => acc + value,
                (uint)Processor.CPU_Core.InstructionsEnum.VREDMAX => Math.Max(acc, value),
                (uint)Processor.CPU_Core.InstructionsEnum.VREDMIN => Math.Min(acc, value),
                _ => acc
            };
        }

        /// <summary>
        /// Execute reduction operation for signed integer values.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static long ExecuteReductionSignedInt(uint op, long acc, long value)
        {
            return op switch
            {
                (uint)Processor.CPU_Core.InstructionsEnum.VREDSUM => acc + value,
                (uint)Processor.CPU_Core.InstructionsEnum.VREDMAX => Math.Max(acc, value),
                (uint)Processor.CPU_Core.InstructionsEnum.VREDMIN => Math.Min(acc, value),
                (uint)Processor.CPU_Core.InstructionsEnum.VREDAND => acc & value,
                (uint)Processor.CPU_Core.InstructionsEnum.VREDOR => acc | value,
                (uint)Processor.CPU_Core.InstructionsEnum.VREDXOR => acc ^ value,
                _ => acc
            };
        }

        /// <summary>
        /// Execute reduction operation for unsigned integer values.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong ExecuteReductionUnsignedInt(uint op, ulong acc, ulong value)
        {
            return op switch
            {
                (uint)Processor.CPU_Core.InstructionsEnum.VREDSUM => acc + value,
                (uint)Processor.CPU_Core.InstructionsEnum.VREDMAX => Math.Max(acc, value),
                (uint)Processor.CPU_Core.InstructionsEnum.VREDMAXU => Math.Max(acc, value),
                (uint)Processor.CPU_Core.InstructionsEnum.VREDMIN => Math.Min(acc, value),
                (uint)Processor.CPU_Core.InstructionsEnum.VREDMINU => Math.Min(acc, value),
                (uint)Processor.CPU_Core.InstructionsEnum.VREDAND => acc & value,
                (uint)Processor.CPU_Core.InstructionsEnum.VREDOR => acc | value,
                (uint)Processor.CPU_Core.InstructionsEnum.VREDXOR => acc ^ value,
                _ => acc
            };
        }

        // ========================================================================
        // Saturating Arithmetic Operations (DSP/ML optimized)
        // ========================================================================

        /// <summary>
        /// Saturating addition for signed integers - clamps to min/max on overflow
        /// </summary>
    }
}
