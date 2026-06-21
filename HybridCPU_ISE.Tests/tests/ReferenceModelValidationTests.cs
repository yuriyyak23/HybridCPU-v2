// REF-03: ReferenceModel validation tests.
// Ensures ValidateResult produces correct comparison for every FP DataType,
// and that every ReferenceModel unknown opcode path fails closed.

using System;
using HybridCPU_ISE.Arch;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Execution;
using DT = HybridCPU_ISE.Arch.DataTypeEnum;

namespace HybridCPU_ISE.Tests
{
    public class ReferenceModelValidationTests
    {
        // ================================================================
        // ValidateResult: each FP DataType has actual comparison logic
        // ================================================================

        [Theory]
        [InlineData(DT.FLOAT32)]
        [InlineData(DT.FLOAT64)]
        [InlineData(DT.FLOAT16)]
        [InlineData(DT.BFLOAT16)]
        [InlineData(DT.FLOAT8_E4M3)]
        [InlineData(DT.FLOAT8_E5M2)]
        public void ValidateResult_IdenticalFPValues_ReturnsTrue(DT dt)
        {
            int size = DataTypeUtils.SizeOf(dt);
            byte[] buf = new byte[size];
            Span<byte> span = buf;

            // Store a known value via ElementCodec
            ElementCodec.StoreF(span, 0, dt, 1.0);

            bool result = ReferenceModel.ValidateResult(
                opCode: (uint)Processor.CPU_Core.InstructionsEnum.VADD,
                dataType: dt,
                actual: buf,
                expected: buf,
                elementIndex: 0);

            Assert.True(result);
        }

        [Theory]
        [InlineData(DT.FLOAT32)]
        [InlineData(DT.FLOAT64)]
        [InlineData(DT.FLOAT16)]
        [InlineData(DT.BFLOAT16)]
        [InlineData(DT.FLOAT8_E4M3)]
        [InlineData(DT.FLOAT8_E5M2)]
        public void ValidateResult_DifferentFPValues_ReturnsFalse(DT dt)
        {
            int size = DataTypeUtils.SizeOf(dt);
            byte[] actual = new byte[size];
            byte[] expected = new byte[size];

            // Store clearly different values
            ElementCodec.StoreF(actual, 0, dt, 1.0);
            ElementCodec.StoreF(expected, 0, dt, -1.0);

            bool result = ReferenceModel.ValidateResult(
                opCode: (uint)Processor.CPU_Core.InstructionsEnum.VADD,
                dataType: dt,
                actual: actual,
                expected: expected,
                elementIndex: 0);

            Assert.False(result);
        }

        [Theory]
        [InlineData(DT.FLOAT32)]
        [InlineData(DT.FLOAT64)]
        [InlineData(DT.FLOAT16)]
        [InlineData(DT.BFLOAT16)]
        [InlineData(DT.FLOAT8_E4M3)]
        [InlineData(DT.FLOAT8_E5M2)]
        public void ValidateResult_ZeroFP_ReturnsTrue(DT dt)
        {
            int size = DataTypeUtils.SizeOf(dt);
            byte[] buf = new byte[size];
            ElementCodec.StoreF(buf, 0, dt, 0.0);

            byte[] buf2 = new byte[size];
            ElementCodec.StoreF(buf2, 0, dt, 0.0);

            bool result = ReferenceModel.ValidateResult(
                opCode: (uint)Processor.CPU_Core.InstructionsEnum.VADD,
                dataType: dt,
                actual: buf,
                expected: buf2,
                elementIndex: 0);

            Assert.True(result);
        }

        // ================================================================
        // ValidateResult: integer types still use exact match
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
        public void ValidateResult_IdenticalIntValues_ReturnsTrue(DT dt)
        {
            int size = DataTypeUtils.SizeOf(dt);
            byte[] buf = new byte[size];
            buf.AsSpan().Fill(0x42);

            bool result = ReferenceModel.ValidateResult(
                opCode: (uint)Processor.CPU_Core.InstructionsEnum.VADD,
                dataType: dt,
                actual: buf,
                expected: buf,
                elementIndex: 0);

            Assert.True(result);
        }

        [Theory]
        [InlineData(DT.INT8)]
        [InlineData(DT.INT16)]
        [InlineData(DT.INT32)]
        [InlineData(DT.INT64)]
        [InlineData(DT.UINT8)]
        [InlineData(DT.UINT16)]
        [InlineData(DT.UINT32)]
        [InlineData(DT.UINT64)]
        public void ValidateResult_DifferentIntValues_ReturnsFalse(DT dt)
        {
            int size = DataTypeUtils.SizeOf(dt);
            byte[] actual = new byte[size];
            byte[] expected = new byte[size];
            actual.AsSpan().Fill(0x01);
            expected.AsSpan().Fill(0x02);

            bool result = ReferenceModel.ValidateResult(
                opCode: (uint)Processor.CPU_Core.InstructionsEnum.VADD,
                dataType: dt,
                actual: actual,
                expected: expected,
                elementIndex: 0);

            Assert.False(result);
        }

        // ================================================================
        // AreCloseNarrowFloat: format-specific tolerance
        // ================================================================

        [Fact]
        public void AreCloseNarrowFloat_FLOAT16_ExactMatch_ReturnsTrue()
        {
            Assert.True(ReferenceModel.AreCloseNarrowFloat(1.0, 1.0, DT.FLOAT16));
        }

        [Fact]
        public void AreCloseNarrowFloat_FLOAT16_WithinTolerance_ReturnsTrue()
        {
            // 1e-3 relative tolerance for FP16; values within that range should match
            Assert.True(ReferenceModel.AreCloseNarrowFloat(1.0, 1.0005, DT.FLOAT16));
        }

        [Fact]
        public void AreCloseNarrowFloat_FLOAT16_BeyondTolerance_ReturnsFalse()
        {
            Assert.False(ReferenceModel.AreCloseNarrowFloat(1.0, 1.01, DT.FLOAT16));
        }

        [Fact]
        public void AreCloseNarrowFloat_FLOAT8_E4M3_LargeTolerance_ReturnsTrue()
        {
            // E4M3 has 0.125 relative tolerance
            Assert.True(ReferenceModel.AreCloseNarrowFloat(1.0, 1.1, DT.FLOAT8_E4M3));
        }

        [Fact]
        public void AreCloseNarrowFloat_FLOAT8_E4M3_BeyondTolerance_ReturnsFalse()
        {
            Assert.False(ReferenceModel.AreCloseNarrowFloat(1.0, 1.5, DT.FLOAT8_E4M3));
        }

        [Fact]
        public void AreCloseNarrowFloat_BothNaN_ReturnsTrue()
        {
            Assert.True(ReferenceModel.AreCloseNarrowFloat(double.NaN, double.NaN, DT.BFLOAT16));
        }

        [Fact]
        public void AreCloseNarrowFloat_OneNaN_ReturnsFalse()
        {
            Assert.False(ReferenceModel.AreCloseNarrowFloat(1.0, double.NaN, DT.BFLOAT16));
        }

        [Fact]
        public void AreCloseNarrowFloat_ThrowsForNonNarrowType()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => ReferenceModel.AreCloseNarrowFloat(1.0, 1.0, DT.FLOAT32));
        }

        // ================================================================
        // BinaryFloat: throws on unsupported opcode
        // ================================================================

        [Fact]
        public void BinaryFloat_UnsupportedOpcode_Throws()
        {
            const uint opCode = 9999;

            InvalidOpcodeException exception = Assert.Throws<InvalidOpcodeException>(
                () => ReferenceModel.BinaryFloat(opCode, 1.0, 2.0));

            AssertUnknownReferenceOpcode(exception, nameof(ReferenceModel.BinaryFloat), opCode);
        }

        [Fact]
        public void BinaryFloat_VADD_ReturnsSum()
        {
            double result = ReferenceModel.BinaryFloat(
                (uint)Processor.CPU_Core.InstructionsEnum.VADD, 3.0, 4.0);
            Assert.Equal(7.0, result);
        }

        // ================================================================
        // BinarySignedInt: throws on unsupported opcode
        // ================================================================

        [Fact]
        public void BinarySignedInt_UnsupportedOpcode_Throws()
        {
            const uint opCode = 9999;

            InvalidOpcodeException exception = Assert.Throws<InvalidOpcodeException>(
                () => ReferenceModel.BinarySignedInt(opCode, 1, 2));

            AssertUnknownReferenceOpcode(exception, nameof(ReferenceModel.BinarySignedInt), opCode);
        }

        [Fact]
        public void BinarySignedInt_VADD_ReturnsSum()
        {
            long result = ReferenceModel.BinarySignedInt(
                (uint)Processor.CPU_Core.InstructionsEnum.VADD, 3, 4);
            Assert.Equal(7, result);
        }

        // ================================================================
        // BinaryUnsignedInt: throws on unsupported opcode
        // ================================================================

        [Fact]
        public void BinaryUnsignedInt_UnsupportedOpcode_Throws()
        {
            const uint opCode = 9999;

            InvalidOpcodeException exception = Assert.Throws<InvalidOpcodeException>(
                () => ReferenceModel.BinaryUnsignedInt(opCode, 1, 2));

            AssertUnknownReferenceOpcode(exception, nameof(ReferenceModel.BinaryUnsignedInt), opCode);
        }

        [Fact]
        public void BinaryUnsignedInt_VADD_ReturnsSum()
        {
            ulong result = ReferenceModel.BinaryUnsignedInt(
                (uint)Processor.CPU_Core.InstructionsEnum.VADD, 3, 4);
            Assert.Equal(7UL, result);
        }

        // ================================================================
        // FMAFloat32/FMAFloat64/UnaryFloat: unknown opcodes fail closed
        // ================================================================

        [Fact]
        public void FMAFloat32_UnsupportedOpcode_Throws()
        {
            const uint opCode = 9999;

            InvalidOpcodeException exception = Assert.Throws<InvalidOpcodeException>(
                () => ReferenceModel.FMAFloat32(opCode, 1.0f, 2.0f, 3.0f));

            AssertUnknownReferenceOpcode(exception, nameof(ReferenceModel.FMAFloat32), opCode);
        }

        [Fact]
        public void FMAFloat64_UnsupportedOpcode_Throws()
        {
            const uint opCode = 9999;

            InvalidOpcodeException exception = Assert.Throws<InvalidOpcodeException>(
                () => ReferenceModel.FMAFloat64(opCode, 1.0, 2.0, 3.0));

            AssertUnknownReferenceOpcode(exception, nameof(ReferenceModel.FMAFloat64), opCode);
        }

        [Fact]
        public void UnaryFloat_UnsupportedOpcode_Throws()
        {
            const uint opCode = 9999;

            InvalidOpcodeException exception = Assert.Throws<InvalidOpcodeException>(
                () => ReferenceModel.UnaryFloat(opCode, 1.0));

            AssertUnknownReferenceOpcode(exception, nameof(ReferenceModel.UnaryFloat), opCode);
        }

        private static void AssertUnknownReferenceOpcode(
            InvalidOpcodeException exception,
            string operationClass,
            uint opCode)
        {
            string opcodeIdentifier = $"0x{opCode:X}";

            Assert.Equal(opcodeIdentifier, exception.OpcodeIdentifier);
            Assert.Equal(-1, exception.SlotIndex);
            Assert.False(exception.IsProhibited);
            Assert.Contains($"ReferenceModel {operationClass}", exception.Message);
            Assert.Contains(opcodeIdentifier, exception.Message);
        }
    }
}

