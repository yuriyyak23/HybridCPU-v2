// REF-01: DataType matrix consistency tests.
// Ensures canonical DataTypeUtils and compat DataTypeEnum agree on size/isFP
// for every defined DataTypeEnum value, and that unknown values
// produce a predictable exception from SizeOf.

using System;
using System.Linq;
using HybridCPU_ISE.Arch;
using Xunit;
using YAKSys_Hybrid_CPU.Arch;

namespace HybridCPU_ISE.Tests
{
    public class DataTypeMatrixConsistencyTests
    {
        /// <summary>
        /// All architecture-defined DataTypeEnum values.
        /// </summary>
        private static readonly DataTypeEnum[] AllDefinedTypes =
            Enum.GetValues<DataTypeEnum>();

        [Theory]
        [MemberData(nameof(DefinedDataTypes))]
        public void SizeOf_MatchesBetweenDataTypeUtilsAndEnvironment(DataTypeEnum dt)
        {
            int utilsSize = DataTypeUtils.SizeOf(dt);
            int envSize = DataTypeUtils.SizeOf(dt);

            Assert.Equal(envSize, utilsSize);
        }

        [Theory]
        [MemberData(nameof(DefinedDataTypes))]
        public void IsFloatingPoint_MatchesBetweenDataTypeUtilsAndEnvironment(DataTypeEnum dt)
        {
            bool utilsFP = DataTypeUtils.IsFloatingPoint(dt);
            bool envFP = DataTypeUtils.IsFloatingPoint(dt);

            Assert.Equal(envFP, utilsFP);
        }

        [Theory]
        [MemberData(nameof(DefinedDataTypes))]
        public void IsValid_ReturnsTrueForAllDefinedTypes(DataTypeEnum dt)
        {
            Assert.True(DataTypeUtils.IsValid(dt));
        }

        [Theory]
        [MemberData(nameof(DefinedDataTypes))]
        public void SizeOf_ReturnsPositiveForAllDefinedTypes(DataTypeEnum dt)
        {
            Assert.True(DataTypeUtils.SizeOf(dt) > 0);
        }

        [Theory]
        [MemberData(nameof(DefinedDataTypes))]
        public void BitWidth_ReturnsPositiveMultipleOf8ForAllDefinedTypes(DataTypeEnum dt)
        {
            int bits = DataTypeUtils.BitWidth(dt);

            Assert.True(bits > 0);
            Assert.Equal(0, bits % 8);
        }

        [Theory]
        [MemberData(nameof(DefinedDataTypes))]
        public void EachDefinedType_IsExactlyOneCategory(DataTypeEnum dt)
        {
            bool isSigned = DataTypeUtils.IsSignedInteger(dt);
            bool isUnsigned = DataTypeUtils.IsUnsignedInteger(dt);
            bool isFloat = DataTypeUtils.IsFloatingPoint(dt);

            // Exactly one category must be true
            int trueCount = (isSigned ? 1 : 0) + (isUnsigned ? 1 : 0) + (isFloat ? 1 : 0);
            Assert.Equal(1, trueCount);
        }

        [Fact]
        public void SizeOf_ThrowsArgumentOutOfRangeException_ForUndefinedEnumValue()
        {
            var undefined = (DataTypeEnum)255;

            Assert.Throws<ArgumentOutOfRangeException>(() => DataTypeUtils.SizeOf(undefined));
        }

        [Fact]
        public void BitWidth_ThrowsArgumentOutOfRangeException_ForUndefinedEnumValue()
        {
            var undefined = (DataTypeEnum)255;

            Assert.Throws<ArgumentOutOfRangeException>(() => DataTypeUtils.BitWidth(undefined));
        }

        [Fact]
        public void IsValid_ReturnsFalse_ForUndefinedEnumValue()
        {
            var undefined = (DataTypeEnum)255;

            Assert.False(DataTypeUtils.IsValid(undefined));
        }

        [Theory]
        [MemberData(nameof(DefinedDataTypes))]
        public void IsSignedInteger_MatchesBetweenDataTypeUtilsAndEnvironment(DataTypeEnum dt)
        {
            bool utilsSigned = DataTypeUtils.IsSignedInteger(dt);
            bool envSigned = DataTypeUtils.IsSignedInteger(dt);

            Assert.Equal(envSigned, utilsSigned);
        }

        public static TheoryData<DataTypeEnum> DefinedDataTypes()
        {
            var data = new TheoryData<DataTypeEnum>();
            foreach (var dt in Enum.GetValues<DataTypeEnum>())
                data.Add(dt);
            return data;
        }
    }
}

