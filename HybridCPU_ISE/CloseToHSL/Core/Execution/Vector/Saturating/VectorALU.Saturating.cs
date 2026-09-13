
using System;
using System.Runtime.CompilerServices;
using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;

namespace YAKSys_Hybrid_CPU.Execution
{
    internal static partial class VectorALU
    {
        public static void ApplySaturatingAddElement(
            uint op,
            DataTypeEnum dataType,
            ReadOnlySpan<byte> sourceA,
            ReadOnlySpan<byte> sourceB,
            Span<byte> destination,
            ref Processor.CPU_Core core)
        {
            if (op != (uint)Processor.CPU_Core.InstructionsEnum.VADD)
            {
                throw new InvalidOperationException(
                    $"VectorALU.ApplySaturatingAddElement rejected opcode 0x{op:X}; Phase 05B opens only VADD.SAT.");
            }

            MarkVectorDirty(ref core);

            if (DataTypeUtils.IsSignedInteger(dataType))
            {
                long x = ElementCodec.LoadI(sourceA, 0, dataType);
                long y = ElementCodec.LoadI(sourceB, 0, dataType);
                long result = SaturatingAddSignedForElementWidth(x, y, dataType, ref core);
                ElementCodec.StoreI(destination, 0, dataType, result);
                return;
            }

            if (DataTypeUtils.IsUnsignedInteger(dataType))
            {
                ulong x = ElementCodec.LoadU(sourceA, 0, dataType);
                ulong y = ElementCodec.LoadU(sourceB, 0, dataType);
                ulong result = SaturatingAddUnsignedForElementWidth(x, y, dataType, ref core);
                ElementCodec.StoreU(destination, 0, dataType, result);
                return;
            }

            throw new DecodeProjectionFaultException(
                $"VectorALU.ApplySaturatingAddElement rejected unsupported VADD.SAT DataType {dataType}. " +
                "Phase 05B opens saturating add only for integer signed/unsigned element types.");
        }

        private static long SaturatingAddSignedForElementWidth(
            long x,
            long y,
            DataTypeEnum dataType,
            ref Processor.CPU_Core core)
        {
            System.Int128 result = (System.Int128)x + (System.Int128)y;
            System.Int128 min = SignedMin(dataType);
            System.Int128 max = SignedMax(dataType);

            if (result > max)
            {
                TrackOverflow(ref core);
                return (long)max;
            }

            if (result < min)
            {
                TrackOverflow(ref core);
                return (long)min;
            }

            return (long)result;
        }

        private static ulong SaturatingAddUnsignedForElementWidth(
            ulong x,
            ulong y,
            DataTypeEnum dataType,
            ref Processor.CPU_Core core)
        {
            System.UInt128 result = (System.UInt128)x + (System.UInt128)y;
            System.UInt128 max = UnsignedMax(dataType);

            if (result > max)
            {
                TrackOverflow(ref core);
                return (ulong)max;
            }

            return (ulong)result;
        }

        private static System.Int128 SignedMin(DataTypeEnum dataType) =>
            dataType switch
            {
                DataTypeEnum.INT8 => sbyte.MinValue,
                DataTypeEnum.INT16 => short.MinValue,
                DataTypeEnum.INT32 => int.MinValue,
                DataTypeEnum.INT64 => long.MinValue,
                _ => throw new DecodeProjectionFaultException(
                    $"VADD.SAT signed clamp rejected unsupported DataType {dataType}.")
            };

        private static System.Int128 SignedMax(DataTypeEnum dataType) =>
            dataType switch
            {
                DataTypeEnum.INT8 => sbyte.MaxValue,
                DataTypeEnum.INT16 => short.MaxValue,
                DataTypeEnum.INT32 => int.MaxValue,
                DataTypeEnum.INT64 => long.MaxValue,
                _ => throw new DecodeProjectionFaultException(
                    $"VADD.SAT signed clamp rejected unsupported DataType {dataType}.")
            };

        private static System.UInt128 UnsignedMax(DataTypeEnum dataType) =>
            dataType switch
            {
                DataTypeEnum.UINT8 => byte.MaxValue,
                DataTypeEnum.UINT16 => ushort.MaxValue,
                DataTypeEnum.UINT32 => uint.MaxValue,
                DataTypeEnum.UINT64 => ulong.MaxValue,
                _ => throw new DecodeProjectionFaultException(
                    $"VADD.SAT unsigned clamp rejected unsupported DataType {dataType}.")
            };

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
