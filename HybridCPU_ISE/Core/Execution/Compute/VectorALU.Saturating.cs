
using System;
using System.Runtime.CompilerServices;
using YAKSys_Hybrid_CPU.Arch;

namespace YAKSys_Hybrid_CPU.Execution
{
    internal static partial class VectorALU
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static long SaturatingAddSigned(long x, long y, ref Processor.CPU_Core core)
        {
            long result = x + y;
            // Check for overflow: if signs of x and y are same, and result sign differs, overflow occurred
            if (((x ^ result) & (y ^ result)) < 0)
            {
                // Track overflow exception
                TrackOverflow(ref core);
                // Overflow: return max if positive, min if negative
                return x >= 0 ? long.MaxValue : long.MinValue;
            }
            return result;
        }

        /// <summary>
        /// Saturating subtraction for signed integers
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static long SaturatingSubSigned(long x, long y, ref Processor.CPU_Core core)
        {
            long result = x - y;
            // Check for overflow: if signs of x and y differ, and result sign differs from x
            if (((x ^ y) & (x ^ result)) < 0)
            {
                TrackOverflow(ref core);
                return x >= 0 ? long.MaxValue : long.MinValue;
            }
            return result;
        }

        /// <summary>
        /// Saturating multiplication for signed integers
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static long SaturatingMulSigned(long x, long y, ref Processor.CPU_Core core)
        {
            // Use 128-bit intermediate to detect overflow
            System.Int128 result = (System.Int128)x * (System.Int128)y;
            if (result > long.MaxValue || result < long.MinValue)
            {
                TrackOverflow(ref core);
                return result > long.MaxValue ? long.MaxValue : long.MinValue;
            }
            return (long)result;
        }

        /// <summary>
        /// Saturating addition for unsigned integers
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong SaturatingAddUnsigned(ulong x, ulong y, ref Processor.CPU_Core core)
        {
            ulong result = x + y;
            // Overflow if result < x (wraparound occurred)
            if (result < x)
            {
                TrackOverflow(ref core);
                return ulong.MaxValue;
            }
            return result;
        }

        /// <summary>
        /// Saturating subtraction for unsigned integers
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong SaturatingSubUnsigned(ulong x, ulong y, ref Processor.CPU_Core core)
        {
            // Underflow if y > x
            if (y > x)
            {
                TrackOverflow(ref core);
                return 0;
            }
            return x - y;
        }

        /// <summary>
        /// Saturating multiplication for unsigned integers
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong SaturatingMulUnsigned(ulong x, ulong y, ref Processor.CPU_Core core)
        {
            // Use 128-bit intermediate to detect overflow
            System.UInt128 result = (System.UInt128)x * (System.UInt128)y;
            if (result > ulong.MaxValue)
            {
                TrackOverflow(ref core);
                return ulong.MaxValue;
            }
            return (ulong)result;
        }

        // ========================================================================
        // Bit Manipulation Operations
        // ========================================================================

        /// <summary>
        /// Count leading zeros in a 64-bit value
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int CountLeadingZeros(ulong value)
        {
            if (value == 0) return 64;
            return System.Numerics.BitOperations.LeadingZeroCount(value);
        }

        /// <summary>
        /// Count trailing zeros in a 64-bit value
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int CountTrailingZeros(ulong value)
        {
            if (value == 0) return 64;
            return System.Numerics.BitOperations.TrailingZeroCount(value);
        }

        /// <summary>
        /// Count population (number of set bits) in a 64-bit value
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int PopCount(ulong value)
        {
            return System.Numerics.BitOperations.PopCount(value);
        }

        /// <summary>
        /// Reverse bits in a 64-bit value
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong ReverseBits(ulong value)
        {
            // Swap bit pairs, then nibbles, then bytes, etc.
            value = ((value & 0x5555555555555555UL) << 1) | ((value & 0xAAAAAAAAAAAAAAAAUL) >> 1);
            value = ((value & 0x3333333333333333UL) << 2) | ((value & 0xCCCCCCCCCCCCCCCCUL) >> 2);
            value = ((value & 0x0F0F0F0F0F0F0F0FUL) << 4) | ((value & 0xF0F0F0F0F0F0F0F0UL) >> 4);
            value = ((value & 0x00FF00FF00FF00FFUL) << 8) | ((value & 0xFF00FF00FF00FF00UL) >> 8);
            value = ((value & 0x0000FFFF0000FFFFUL) << 16) | ((value & 0xFFFF0000FFFF0000UL) >> 16);
            value = (value << 32) | (value >> 32);
            return value;
        }

        /// <summary>
        /// Reverse byte order (endianness swap) in a 64-bit value
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong ReverseBytes(ulong value)
        {
            return System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(value);
        }
    }
}