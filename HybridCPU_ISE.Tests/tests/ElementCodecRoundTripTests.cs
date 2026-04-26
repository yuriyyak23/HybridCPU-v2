// REF-02: ElementCodec round-trip and symmetry tests.
// Ensures StoreF→LoadF round-trip for every FP DataType,
// StoreRaw→LoadRaw round-trip for every DataType,
// and that LoadI/LoadU throw for non-matching type categories.

using System;
using HybridCPU_ISE.Arch;
using Xunit;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Execution;
using DT = HybridCPU_ISE.Arch.DataTypeEnum;

namespace HybridCPU_ISE.Tests
{
    public class ElementCodecRoundTripTests
    {
        // ================================================================
        // StoreF → LoadF round-trip for each FP DataType
        // ================================================================

        [Theory]
        [InlineData(1.0)]
        [InlineData(-1.0)]
        [InlineData(0.0)]
        [InlineData(2.0)]
        [InlineData(0.5)]
        public void StoreFLoadF_RoundTrip_FLOAT8_E4M3(double value)
        {
            Span<byte> buf = stackalloc byte[1];
            ElementCodec.StoreF(buf, 0, DT.FLOAT8_E4M3, value);
            double loaded = ElementCodec.LoadF(buf, 0, DT.FLOAT8_E4M3);

            // Verify via direct encode→decode: must match byte-level
            byte encoded = ElementCodec.EncodeE4M3(value);
            float decoded = ElementCodec.DecodeE4M3(encoded);
            Assert.Equal((double)decoded, loaded);
        }

        [Theory]
        [InlineData(1.0)]
        [InlineData(-1.0)]
        [InlineData(0.0)]
        [InlineData(2.0)]
        [InlineData(0.5)]
        public void StoreFLoadF_RoundTrip_FLOAT8_E5M2(double value)
        {
            Span<byte> buf = stackalloc byte[1];
            ElementCodec.StoreF(buf, 0, DT.FLOAT8_E5M2, value);
            double loaded = ElementCodec.LoadF(buf, 0, DT.FLOAT8_E5M2);

            byte encoded = ElementCodec.EncodeE5M2(value);
            float decoded = ElementCodec.DecodeE5M2(encoded);
            Assert.Equal((double)decoded, loaded);
        }

        [Theory]
        [InlineData(1.0)]
        [InlineData(-1.0)]
        [InlineData(0.0)]
        [InlineData(3.14)]
        [InlineData(65504.0)] // max finite FP16
        public void StoreFLoadF_RoundTrip_FLOAT16(double value)
        {
            Span<byte> buf = stackalloc byte[2];
            ElementCodec.StoreF(buf, 0, DT.FLOAT16, value);
            double loaded = ElementCodec.LoadF(buf, 0, DT.FLOAT16);

            // FP16 has limited precision; verify store→load is idempotent
            Span<byte> buf2 = stackalloc byte[2];
            ElementCodec.StoreF(buf2, 0, DT.FLOAT16, loaded);
            double loaded2 = ElementCodec.LoadF(buf2, 0, DT.FLOAT16);
            Assert.Equal(loaded, loaded2);
        }

        [Theory]
        [InlineData(1.0)]
        [InlineData(-1.0)]
        [InlineData(0.0)]
        [InlineData(3.14)]
        public void StoreFLoadF_RoundTrip_BFLOAT16(double value)
        {
            Span<byte> buf = stackalloc byte[2];
            ElementCodec.StoreF(buf, 0, DT.BFLOAT16, value);
            double loaded = ElementCodec.LoadF(buf, 0, DT.BFLOAT16);

            Span<byte> buf2 = stackalloc byte[2];
            ElementCodec.StoreF(buf2, 0, DT.BFLOAT16, loaded);
            double loaded2 = ElementCodec.LoadF(buf2, 0, DT.BFLOAT16);
            Assert.Equal(loaded, loaded2);
        }

        [Theory]
        [InlineData(1.0)]
        [InlineData(-1.0)]
        [InlineData(0.0)]
        [InlineData(3.14159265)]
        [InlineData(float.MaxValue)]
        public void StoreFLoadF_RoundTrip_FLOAT32(double value)
        {
            Span<byte> buf = stackalloc byte[4];
            ElementCodec.StoreF(buf, 0, DT.FLOAT32, value);
            double loaded = ElementCodec.LoadF(buf, 0, DT.FLOAT32);

            Assert.Equal((double)(float)value, loaded);
        }

        [Theory]
        [InlineData(1.0)]
        [InlineData(-1.0)]
        [InlineData(0.0)]
        [InlineData(3.141592653589793)]
        [InlineData(double.MaxValue)]
        public void StoreFLoadF_RoundTrip_FLOAT64(double value)
        {
            Span<byte> buf = stackalloc byte[8];
            ElementCodec.StoreF(buf, 0, DT.FLOAT64, value);
            double loaded = ElementCodec.LoadF(buf, 0, DT.FLOAT64);

            Assert.Equal(value, loaded);
        }

        // ================================================================
        // StoreRaw → LoadRaw round-trip for each DataType
        // ================================================================

        [Theory]
        [MemberData(nameof(AllDataTypesWithRawValues))]
        public void StoreRawLoadRaw_RoundTrip(DT dt, ulong rawValue)
        {
            int size = ElementCodec.SizeOf(dt);
            Span<byte> buf = stackalloc byte[8];
            buf.Clear();

            // Mask to element width so we compare only relevant bits
            ulong mask = size < 8 ? (1UL << (size * 8)) - 1 : ulong.MaxValue;
            ulong expected = rawValue & mask;

            ElementCodec.StoreRaw(buf, 0, dt, rawValue);
            ulong loaded = ElementCodec.LoadRaw(buf, 0, dt);

            Assert.Equal(expected, loaded);
        }

        // ================================================================
        // LoadI throws for non-signed-integer types
        // ================================================================

        [Theory]
        [InlineData(DT.UINT8)]
        [InlineData(DT.UINT16)]
        [InlineData(DT.UINT32)]
        [InlineData(DT.UINT64)]
        [InlineData(DT.FLOAT32)]
        [InlineData(DT.FLOAT64)]
        [InlineData(DT.FLOAT16)]
        [InlineData(DT.BFLOAT16)]
        [InlineData(DT.FLOAT8_E4M3)]
        [InlineData(DT.FLOAT8_E5M2)]
        public void LoadI_ThrowsForNonSignedIntegerType(DT dt)
        {
            byte[] buf = new byte[8];
            Assert.Throws<ArgumentOutOfRangeException>(() => ElementCodec.LoadI(buf, 0, dt));
        }

        // ================================================================
        // LoadU throws for non-unsigned-integer types
        // ================================================================

        [Theory]
        [InlineData(DT.INT8)]
        [InlineData(DT.INT16)]
        [InlineData(DT.INT32)]
        [InlineData(DT.INT64)]
        [InlineData(DT.FLOAT32)]
        [InlineData(DT.FLOAT64)]
        [InlineData(DT.FLOAT16)]
        [InlineData(DT.BFLOAT16)]
        [InlineData(DT.FLOAT8_E4M3)]
        [InlineData(DT.FLOAT8_E5M2)]
        public void LoadU_ThrowsForNonUnsignedIntegerType(DT dt)
        {
            byte[] buf = new byte[8];
            Assert.Throws<ArgumentOutOfRangeException>(() => ElementCodec.LoadU(buf, 0, dt));
        }

        // ================================================================
        // LoadF throws for non-floating-point types
        // ================================================================

        [Theory]
        [InlineData(DT.INT8)]
        [InlineData(DT.INT16)]
        [InlineData(DT.INT32)]
        [InlineData(DT.INT64)]
        [InlineData(DT.UINT8)]
        [InlineData(DT.UINT16)]
        [InlineData(DT.UINT32)]
        [InlineData(DT.UINT64)]
        public void LoadF_ThrowsForNonFloatingPointType(DT dt)
        {
            byte[] buf = new byte[8];
            Assert.Throws<ArgumentOutOfRangeException>(() => ElementCodec.LoadF(buf, 0, dt));
        }

        // ================================================================
        // StoreI throws for non-signed-integer types
        // ================================================================

        [Theory]
        [InlineData(DT.UINT8)]
        [InlineData(DT.UINT16)]
        [InlineData(DT.UINT32)]
        [InlineData(DT.UINT64)]
        [InlineData(DT.FLOAT32)]
        [InlineData(DT.FLOAT64)]
        [InlineData(DT.FLOAT16)]
        [InlineData(DT.BFLOAT16)]
        [InlineData(DT.FLOAT8_E4M3)]
        [InlineData(DT.FLOAT8_E5M2)]
        public void StoreI_ThrowsForNonSignedIntegerType(DT dt)
        {
            byte[] buf = new byte[8];
            Assert.Throws<ArgumentOutOfRangeException>(() => ElementCodec.StoreI(buf, 0, dt, 42));
        }

        // ================================================================
        // StoreU throws for non-unsigned-integer types
        // ================================================================

        [Theory]
        [InlineData(DT.INT8)]
        [InlineData(DT.INT16)]
        [InlineData(DT.INT32)]
        [InlineData(DT.INT64)]
        [InlineData(DT.FLOAT32)]
        [InlineData(DT.FLOAT64)]
        [InlineData(DT.FLOAT16)]
        [InlineData(DT.BFLOAT16)]
        [InlineData(DT.FLOAT8_E4M3)]
        [InlineData(DT.FLOAT8_E5M2)]
        public void StoreU_ThrowsForNonUnsignedIntegerType(DT dt)
        {
            byte[] buf = new byte[8];
            Assert.Throws<ArgumentOutOfRangeException>(() => ElementCodec.StoreU(buf, 0, dt, 42));
        }

        // ================================================================
        // StoreF throws for non-floating-point types
        // ================================================================

        [Theory]
        [InlineData(DT.INT8)]
        [InlineData(DT.INT16)]
        [InlineData(DT.INT32)]
        [InlineData(DT.INT64)]
        [InlineData(DT.UINT8)]
        [InlineData(DT.UINT16)]
        [InlineData(DT.UINT32)]
        [InlineData(DT.UINT64)]
        public void StoreF_ThrowsForNonFloatingPointType(DT dt)
        {
            byte[] buf = new byte[8];
            Assert.Throws<ArgumentOutOfRangeException>(() => ElementCodec.StoreF(buf, 0, dt, 1.0));
        }

        // ================================================================
        // FLOAT8 SizeOf correctness (REF-01 prerequisite verified here)
        // ================================================================

        [Fact]
        public void SizeOf_FLOAT8_E4M3_Returns1()
        {
            Assert.Equal(1, ElementCodec.SizeOf(DT.FLOAT8_E4M3));
        }

        [Fact]
        public void SizeOf_FLOAT8_E5M2_Returns1()
        {
            Assert.Equal(1, ElementCodec.SizeOf(DT.FLOAT8_E5M2));
        }

        // ================================================================
        // E4M3 encode/decode specific edge cases
        // ================================================================

        [Fact]
        public void EncodeE4M3_NaN_ProducesE4M3NaN()
        {
            byte encoded = ElementCodec.EncodeE4M3(double.NaN);
            // E4M3 NaN: exp=15, mantissa=7 → 0x7F (positive NaN)
            Assert.Equal(0x7F, encoded & 0x7F);
        }

        [Fact]
        public void EncodeE4M3_Infinity_SaturatesToMaxFinite()
        {
            byte encoded = ElementCodec.EncodeE4M3(double.PositiveInfinity);
            // E4M3 has no infinity; saturate to exp=15, mantissa=6 → 0x7E
            Assert.Equal(0x7E, encoded);
        }

        [Fact]
        public void EncodeE4M3_NegativeInfinity_SaturatesToNegMaxFinite()
        {
            byte encoded = ElementCodec.EncodeE4M3(double.NegativeInfinity);
            // Sign bit set + max finite → 0xFE
            Assert.Equal(0xFE, encoded);
        }

        // ================================================================
        // E5M2 encode/decode specific edge cases
        // ================================================================

        [Fact]
        public void EncodeE5M2_NaN_ProducesE5M2NaN()
        {
            byte encoded = ElementCodec.EncodeE5M2(double.NaN);
            // E5M2 NaN: exp=31, mantissa!=0 → 0x7F (exp=31, mantissa=3)
            Assert.Equal(0x7F, encoded & 0x7F);
        }

        [Fact]
        public void EncodeE5M2_Infinity_ProducesE5M2Infinity()
        {
            byte encoded = ElementCodec.EncodeE5M2(double.PositiveInfinity);
            // E5M2 Infinity: exp=31, mantissa=0 → 0x7C
            Assert.Equal(0x7C, encoded);
        }

        [Fact]
        public void EncodeE5M2_NegativeInfinity_ProducesNegE5M2Infinity()
        {
            byte encoded = ElementCodec.EncodeE5M2(double.NegativeInfinity);
            // Sign bit set + Inf → 0xFC
            Assert.Equal(0xFC, encoded);
        }

        // ================================================================
        // Encode→Decode round-trip for all normal E4M3 byte values
        // ================================================================

        [Fact]
        public void EncodeDecodeE4M3_RoundTrip_AllNormalBytes()
        {
            for (int b = 0; b < 256; b++)
            {
                byte original = (byte)b;
                float decoded = ElementCodec.DecodeE4M3(original);

                // Skip NaN — NaN != NaN by IEEE rules
                if (float.IsNaN(decoded))
                    continue;

                byte reencoded = ElementCodec.EncodeE4M3(decoded);
                float redecoded = ElementCodec.DecodeE4M3(reencoded);

                Assert.Equal(decoded, redecoded);
            }
        }

        // ================================================================
        // Encode→Decode round-trip for all normal E5M2 byte values
        // ================================================================

        [Fact]
        public void EncodeDecodeE5M2_RoundTrip_AllNormalBytes()
        {
            for (int b = 0; b < 256; b++)
            {
                byte original = (byte)b;
                float decoded = ElementCodec.DecodeE5M2(original);

                if (float.IsNaN(decoded) || float.IsInfinity(decoded))
                    continue;

                byte reencoded = ElementCodec.EncodeE5M2(decoded);
                float redecoded = ElementCodec.DecodeE5M2(reencoded);

                Assert.Equal(decoded, redecoded);
            }
        }

        // ================================================================
        // MemberData providers
        // ================================================================

        public static TheoryData<DT, ulong> AllDataTypesWithRawValues()
        {
            var data = new TheoryData<DT, ulong>();
            foreach (var dt in Enum.GetValues<DT>())
            {
                data.Add(dt, 0x00);
                data.Add(dt, 0x42);
                data.Add(dt, 0xFF);
            }
            return data;
        }
    }
}

