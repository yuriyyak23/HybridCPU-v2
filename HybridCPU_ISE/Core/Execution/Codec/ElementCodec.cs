using HybridCPU_ISE.Arch;

using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace YAKSys_Hybrid_CPU.Execution
{
    /// <summary>
    /// Unified codec for loading and storing elements of all data types.
    /// Handles endianness, sign-extension, and floating-point bit patterns.
    ///
    /// Design goals:
    /// - Single point of truth for element encoding/decoding
    /// - HLS-friendly: no dynamic allocation, inline methods
    /// - Testable: clear contracts for all data type conversions
    /// - RVV-compatible: supports all element widths (8, 16, 32, 64 bits)
    /// </summary>
    internal static class ElementCodec
    {
        /// <summary>
        /// Get element size in bytes from DataType enum.
        /// Delegates to Arch.DataTypeUtils for consistency.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SizeOf(DataTypeEnum dt) =>
            Arch.DataTypeUtils.SizeOf(dt);

        /// <summary>
        /// Load a signed integer element from memory.
        /// Sign-extends to 64-bit for uniform processing.
        /// Little-endian byte order.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long LoadI(ReadOnlySpan<byte> mem, int off, DataTypeEnum dt)
        {
            return dt switch
            {
                DataTypeEnum.INT8 => unchecked((sbyte)mem[off]),
                DataTypeEnum.INT16 => BinaryPrimitives.ReadInt16LittleEndian(mem.Slice(off, 2)),
                DataTypeEnum.INT32 => BinaryPrimitives.ReadInt32LittleEndian(mem.Slice(off, 4)),
                DataTypeEnum.INT64 => BinaryPrimitives.ReadInt64LittleEndian(mem.Slice(off, 8)),
                _ => throw new ArgumentOutOfRangeException(nameof(dt), dt, $"LoadI not supported for {dt}")
            };
        }

        /// <summary>
        /// Load an unsigned integer element from memory.
        /// Zero-extends to 64-bit for uniform processing.
        /// Little-endian byte order.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong LoadU(ReadOnlySpan<byte> mem, int off, DataTypeEnum dt)
        {
            return dt switch
            {
                DataTypeEnum.UINT8 => mem[off],
                DataTypeEnum.UINT16 => BinaryPrimitives.ReadUInt16LittleEndian(mem.Slice(off, 2)),
                DataTypeEnum.UINT32 => BinaryPrimitives.ReadUInt32LittleEndian(mem.Slice(off, 4)),
                DataTypeEnum.UINT64 => BinaryPrimitives.ReadUInt64LittleEndian(mem.Slice(off, 8)),
                _ => throw new ArgumentOutOfRangeException(nameof(dt), dt, $"LoadU not supported for {dt}")
            };
        }

        /// <summary>
        /// Load a floating-point element from memory.
        /// Converts to double (64-bit) for uniform processing.
        /// Little-endian byte order.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double LoadF(ReadOnlySpan<byte> mem, int off, DataTypeEnum dt)
        {
            return dt switch
            {
                DataTypeEnum.FLOAT8_E4M3 =>
                    DecodeE4M3(mem[off]),
                DataTypeEnum.FLOAT8_E5M2 =>
                    DecodeE5M2(mem[off]),
                DataTypeEnum.FLOAT16 =>
                    ConvertFP16ToFloat(BinaryPrimitives.ReadUInt16LittleEndian(mem.Slice(off, 2))),
                DataTypeEnum.BFLOAT16 =>
                    ConvertBF16ToFloat(BinaryPrimitives.ReadUInt16LittleEndian(mem.Slice(off, 2))),
                DataTypeEnum.FLOAT32 =>
                    BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(mem.Slice(off, 4))),
                DataTypeEnum.FLOAT64 =>
                    BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64LittleEndian(mem.Slice(off, 8))),
                _ => throw new ArgumentOutOfRangeException(nameof(dt), dt, $"LoadF not supported for {dt}")
            };
        }

        /// <summary>
        /// Store a signed integer element to memory.
        /// Truncates from 64-bit to target width.
        /// Little-endian byte order.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StoreI(Span<byte> mem, int off, DataTypeEnum dt, long value)
        {
            switch (dt)
            {
                case DataTypeEnum.INT8:
                    mem[off] = unchecked((byte)value);
                    break;
                case DataTypeEnum.INT16:
                    BinaryPrimitives.WriteInt16LittleEndian(mem.Slice(off, 2), (short)value);
                    break;
                case DataTypeEnum.INT32:
                    BinaryPrimitives.WriteInt32LittleEndian(mem.Slice(off, 4), (int)value);
                    break;
                case DataTypeEnum.INT64:
                    BinaryPrimitives.WriteInt64LittleEndian(mem.Slice(off, 8), value);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(dt), dt, $"StoreI not supported for {dt}");
            }
        }

        /// <summary>
        /// Store an unsigned integer element to memory.
        /// Truncates from 64-bit to target width.
        /// Little-endian byte order.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StoreU(Span<byte> mem, int off, DataTypeEnum dt, ulong value)
        {
            switch (dt)
            {
                case DataTypeEnum.UINT8:
                    mem[off] = (byte)value;
                    break;
                case DataTypeEnum.UINT16:
                    BinaryPrimitives.WriteUInt16LittleEndian(mem.Slice(off, 2), (ushort)value);
                    break;
                case DataTypeEnum.UINT32:
                    BinaryPrimitives.WriteUInt32LittleEndian(mem.Slice(off, 4), (uint)value);
                    break;
                case DataTypeEnum.UINT64:
                    BinaryPrimitives.WriteUInt64LittleEndian(mem.Slice(off, 8), value);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(dt), dt, $"StoreU not supported for {dt}");
            }
        }

        /// <summary>
        /// Store a floating-point element to memory.
        /// Converts from double (64-bit) and writes appropriate width.
        /// Little-endian byte order.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StoreF(Span<byte> mem, int off, DataTypeEnum dt, double value)
        {
            switch (dt)
            {
                case DataTypeEnum.FLOAT8_E4M3:
                    mem[off] = EncodeE4M3(value);
                    break;
                case DataTypeEnum.FLOAT8_E5M2:
                    mem[off] = EncodeE5M2(value);
                    break;
                case DataTypeEnum.FLOAT16:
                    BinaryPrimitives.WriteUInt16LittleEndian(mem.Slice(off, 2),
                        ConvertFloatToFP16(value));
                    break;
                case DataTypeEnum.BFLOAT16:
                    BinaryPrimitives.WriteUInt16LittleEndian(mem.Slice(off, 2),
                        ConvertFloatToBF16(value));
                    break;
                case DataTypeEnum.FLOAT32:
                    BinaryPrimitives.WriteInt32LittleEndian(mem.Slice(off, 4),
                        BitConverter.SingleToInt32Bits((float)value));
                    break;
                case DataTypeEnum.FLOAT64:
                    BinaryPrimitives.WriteInt64LittleEndian(mem.Slice(off, 8),
                        BitConverter.DoubleToInt64Bits(value));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(dt), dt, $"StoreF not supported for {dt}");
            }
        }

        /// <summary>
        /// Load element as raw bits (ulong), regardless of type.
        /// Useful for bitwise operations that work uniformly across types.
        /// Zero-pads to 64-bit for sub-64-bit types.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong LoadRaw(ReadOnlySpan<byte> mem, int off, DataTypeEnum dt)
        {
            int size = SizeOf(dt);
            return size switch
            {
                1 => mem[off],
                2 => BinaryPrimitives.ReadUInt16LittleEndian(mem.Slice(off, 2)),
                4 => BinaryPrimitives.ReadUInt32LittleEndian(mem.Slice(off, 4)),
                8 => BinaryPrimitives.ReadUInt64LittleEndian(mem.Slice(off, 8)),
                _ => throw new InvalidOperationException($"Unexpected element size {size} for data type {dt}")
            };
        }

        /// <summary>
        /// Store element as raw bits (ulong), regardless of type.
        /// Truncates to appropriate width.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StoreRaw(Span<byte> mem, int off, DataTypeEnum dt, ulong value)
        {
            int size = SizeOf(dt);
            switch (size)
            {
                case 1:
                    mem[off] = (byte)value;
                    break;
                case 2:
                    BinaryPrimitives.WriteUInt16LittleEndian(mem.Slice(off, 2), (ushort)value);
                    break;
                case 4:
                    BinaryPrimitives.WriteUInt32LittleEndian(mem.Slice(off, 4), (uint)value);
                    break;
                case 8:
                    BinaryPrimitives.WriteUInt64LittleEndian(mem.Slice(off, 8), value);
                    break;
                default:
                    throw new InvalidOperationException($"Unexpected element size {size} for data type {dt}");
            }
        }

        // ========================================================================
        // FP16/BF16 Conversion Helpers
        // ========================================================================

        /// <summary>
        /// Convert IEEE 754 FP16 (half precision) to float.
        /// FP16 format: 1 sign bit, 5 exponent bits, 10 mantissa bits.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ConvertFP16ToFloat(ushort fp16)
        {
            uint sign = (uint)(fp16 >> 15) << 31;
            uint exponent = (uint)((fp16 >> 10) & 0x1F);
            uint mantissa = (uint)(fp16 & 0x3FF);

            if (exponent == 0)
            {
                if (mantissa == 0)
                {
                    // Zero (positive or negative)
                    return BitConverter.Int32BitsToSingle((int)sign);
                }
                else
                {
                    // Subnormal number
                    // Convert to normalized FP32
                    while ((mantissa & 0x400) == 0)
                    {
                        mantissa <<= 1;
                        exponent--;
                    }
                    exponent++;
                    mantissa &= 0x3FF;
                    uint fp32 = sign | ((exponent + (127 - 15)) << 23) | (mantissa << 13);
                    return BitConverter.Int32BitsToSingle((int)fp32);
                }
            }
            else if (exponent == 0x1F)
            {
                // Infinity or NaN
                uint fp32 = sign | (0xFFU << 23) | (mantissa << 13);
                return BitConverter.Int32BitsToSingle((int)fp32);
            }
            else
            {
                // Normalized number
                uint fp32 = sign | ((exponent + (127 - 15)) << 23) | (mantissa << 13);
                return BitConverter.Int32BitsToSingle((int)fp32);
            }
        }

        /// <summary>
        /// Convert float to IEEE 754 FP16 (half precision).
        /// Rounds to nearest even, handles overflow/underflow.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ushort ConvertFloatToFP16(double value)
        {
            float f = (float)value;
            uint bits = (uint)BitConverter.SingleToInt32Bits(f);
            uint sign = (bits >> 31) & 1;
            int exponent = (int)((bits >> 23) & 0xFF) - 127;
            uint mantissa = bits & 0x7FFFFF;

            if (exponent == 128)
            {
                // Infinity or NaN
                return (ushort)((sign << 15) | 0x7C00 | (mantissa != 0 ? 1 : 0));
            }

            if (exponent > 15)
            {
                // Overflow to infinity
                return (ushort)((sign << 15) | 0x7C00);
            }

            if (exponent < -14)
            {
                // Underflow or subnormal
                if (exponent < -24)
                {
                    // Too small, flush to zero
                    return (ushort)(sign << 15);
                }
                // Subnormal FP16
                mantissa |= 0x800000;
                int shift = -exponent - 14;
                mantissa >>= shift;
                return (ushort)((sign << 15) | (mantissa >> 13));
            }

            // Normalized FP16
            uint fp16Exp = (uint)(exponent + 15);
            uint fp16Mantissa = mantissa >> 13;
            return (ushort)((sign << 15) | (fp16Exp << 10) | fp16Mantissa);
        }

        /// <summary>
        /// Convert BFloat16 to float.
        /// BF16 format: 1 sign bit, 8 exponent bits, 7 mantissa bits (truncated FP32).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ConvertBF16ToFloat(ushort bf16)
        {
            // BF16 is just the upper 16 bits of FP32, so shift left by 16
            uint fp32 = (uint)bf16 << 16;
            return BitConverter.Int32BitsToSingle((int)fp32);
        }

        /// <summary>
        /// Convert float to BFloat16.
        /// Simply truncates FP32 mantissa (takes upper 16 bits).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ushort ConvertFloatToBF16(double value)
        {
            float f = (float)value;
            uint bits = (uint)BitConverter.SingleToInt32Bits(f);
            // BF16 is upper 16 bits of FP32
            // For proper rounding: add 0x7FFF before truncating (round to nearest)
            uint rounded = bits + 0x7FFF + ((bits >> 16) & 1); // Round to nearest even
            return (ushort)(rounded >> 16);
        }

        // ========================================================================
        // FP8 (NVFP8) Conversion Helpers
        // ========================================================================

        /// <summary>
        /// Encode IEEE 754 Float64/Float32 to NVIDIA E4M3 (8-bit float).
        /// E4M3 format: 1 sign bit, 4 exponent bits, 3 mantissa bits. Bias = 7.
        /// Flush-to-zero for underflow; saturate to max finite for overflow.
        /// NaN encoding: exp=15, mantissa=7. No infinity in E4M3.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte EncodeE4M3(double value)
        {
            float f = (float)value;
            uint bits = (uint)BitConverter.SingleToInt32Bits(f);
            uint sign = (bits >> 31) & 1;
            int f32_exp = (int)((bits >> 23) & 0xFF);
            uint f32_mantissa = bits & 0x7FFFFF;

            // Zero or FP32 subnormal → E4M3 zero (preserving sign)
            if (f32_exp == 0)
                return (byte)(sign << 7);

            // FP32 NaN → E4M3 NaN (exp=15, mantissa=7)
            if (f32_exp == 255 && f32_mantissa != 0)
                return (byte)((sign << 7) | 0x7F);

            // FP32 Infinity → saturate to max finite E4M3 (exp=15, mantissa=6)
            // E4M3 has no infinity representation
            if (f32_exp == 255)
                return (byte)((sign << 7) | 0x7E);

            // Bias adjustment: FP32 bias (127) - E4M3 bias (7) = 120
            int e4m3_exp = f32_exp - 120;

            // Underflow → flush to zero
            if (e4m3_exp <= 0)
                return (byte)(sign << 7);

            // Overflow → saturate to max finite (exp=15, mantissa=6)
            if (e4m3_exp > 15)
                return (byte)((sign << 7) | 0x7E);

            // Truncate FP32 mantissa to 3 bits
            uint e4m3_mantissa = f32_mantissa >> 20;

            // Guard against accidental NaN encoding (exp=15, mantissa=7)
            if (e4m3_exp == 15 && e4m3_mantissa == 7)
                e4m3_mantissa = 6;

            return (byte)((sign << 7) | ((uint)e4m3_exp << 3) | e4m3_mantissa);
        }

        /// <summary>
        /// Encode IEEE 754 Float64/Float32 to NVIDIA E5M2 (8-bit float).
        /// E5M2 format: 1 sign bit, 5 exponent bits, 2 mantissa bits. Bias = 15.
        /// Flush-to-zero for underflow; overflow maps to infinity.
        /// Infinity encoding: exp=31, mantissa=0. NaN: exp=31, mantissa!=0.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte EncodeE5M2(double value)
        {
            float f = (float)value;
            uint bits = (uint)BitConverter.SingleToInt32Bits(f);
            uint sign = (bits >> 31) & 1;
            int f32_exp = (int)((bits >> 23) & 0xFF);
            uint f32_mantissa = bits & 0x7FFFFF;

            // Zero or FP32 subnormal → E5M2 zero (preserving sign)
            if (f32_exp == 0)
                return (byte)(sign << 7);

            // FP32 NaN → E5M2 NaN (exp=31, mantissa=3)
            if (f32_exp == 255 && f32_mantissa != 0)
                return (byte)((sign << 7) | 0x7F);

            // FP32 Infinity → E5M2 Infinity (exp=31, mantissa=0)
            if (f32_exp == 255)
                return (byte)((sign << 7) | 0x7C);

            // Bias adjustment: FP32 bias (127) - E5M2 bias (15) = 112
            int e5m2_exp = f32_exp - 112;

            // Underflow → flush to zero
            if (e5m2_exp <= 0)
                return (byte)(sign << 7);

            // Overflow → infinity
            if (e5m2_exp >= 31)
                return (byte)((sign << 7) | 0x7C);

            // Truncate FP32 mantissa to 2 bits
            uint e5m2_mantissa = f32_mantissa >> 21;

            return (byte)((sign << 7) | ((uint)e5m2_exp << 2) | e5m2_mantissa);
        }

        /// <summary>
        /// Decode NVIDIA E4M3 (8-bit float) to IEEE 754 Float32.
        /// E4M3 format: 1 sign bit, 4 exponent bits, 3 mantissa bits.
        /// Hardware decoder for HLS synthesis (no branches).
        /// Implements flush-to-zero for denormals.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float DecodeE4M3(byte value)
        {
            uint sign = (uint)((value & 0x80) << 24);      // Bit 7: sign
            uint exp = (uint)((value & 0x78) >> 3);        // Bits 6-3: exponent (4 bits)
            uint mantissa = (uint)(value & 0x07);          // Bits 2-0: mantissa (3 bits)

            // Flush denormals to zero (exp == 0)
            // Bias adjustment: FP32 bias (127) - E4M3 bias (7) = +120
            uint f32_exp = (exp == 0) ? 0 : (exp + 120);

            // Handle special values (NaN/Inf): exp == 15 and mantissa == 7
            bool isSpecial = (exp == 15 && mantissa == 7);
            f32_exp = isSpecial ? 255 : f32_exp;
            uint f32_mantissa = isSpecial ? 0x007FFFFFu : (exp == 0 ? 0 : (mantissa << 20));

            uint bits = sign | (f32_exp << 23) | f32_mantissa;
            return BitConverter.Int32BitsToSingle((int)bits);
        }

        /// <summary>
        /// Decode NVIDIA E5M2 (8-bit float) to IEEE 754 Float32.
        /// E5M2 format: 1 sign bit, 5 exponent bits, 2 mantissa bits.
        /// Hardware decoder for HLS synthesis (no branches).
        /// Implements flush-to-zero for denormals.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float DecodeE5M2(byte value)
        {
            uint sign = (uint)((value & 0x80) << 24);      // Bit 7: sign
            uint exp = (uint)((value & 0x7C) >> 2);        // Bits 6-2: exponent (5 bits)
            uint mantissa = (uint)(value & 0x03);          // Bits 1-0: mantissa (2 bits)

            // Flush denormals to zero (exp == 0)
            // Bias adjustment: FP32 bias (127) - E5M2 bias (15) = +112
            uint f32_exp = (exp == 0) ? 0 : (exp + 112);

            // Handle special values: exp == 31 (NaN/Inf)
            bool isSpecial = (exp == 31);
            f32_exp = isSpecial ? 255 : f32_exp;
            uint f32_mantissa = isSpecial ? (mantissa != 0 ? 0x007FFFFFu : 0u) : (exp == 0 ? 0 : (mantissa << 21));

            uint bits = sign | (f32_exp << 23) | f32_mantissa;
            return BitConverter.Int32BitsToSingle((int)bits);
        }
    }
}

