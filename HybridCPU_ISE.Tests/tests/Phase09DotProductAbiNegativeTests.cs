using HybridCPU_ISE.Arch;
using Xunit;
using YAKSys_Hybrid_CPU.Arch;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests;

public sealed class DotProductAbiNegativeTests
{
    [Theory]
    [InlineData(InstructionsEnum.VDOT)]
    [InlineData(InstructionsEnum.VDOTU)]
    [InlineData(InstructionsEnum.VDOTF)]
    [InlineData(InstructionsEnum.VDOT_FP8)]
    public void DotProductLegalityMatrix_DescriptorBackedAbi_RemainsFailClosed(
        InstructionsEnum opcode)
    {
        VectorLegalityMatrixRow row = VectorLegalityMatrix.GetRow(opcode);
        InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus(opcode.ToString());

        Assert.Equal("VectorDotProductScalarFootprint", row.FamilyName);
        Assert.Equal(VectorContourLegalityStatus.Executable, row.OneDimensional);
        Assert.Equal(VectorContourLegalityStatus.FailClosed, row.IndexedAddressing);
        Assert.Equal(VectorContourLegalityStatus.FailClosed, row.TwoDimensionalAddressing);
        Assert.Equal(VectorContourLegalityStatus.FailClosed, row.DescriptorBacked);
        Assert.Equal("VectorDotProductScalarFootprint", status.ExtensionName);
        Assert.True(status.IsExecutableClaim);
    }

    [Theory]
    [InlineData(InstructionsEnum.VDOT, DataTypeEnum.INT32)]
    [InlineData(InstructionsEnum.VDOTU, DataTypeEnum.UINT32)]
    [InlineData(InstructionsEnum.VDOTF, DataTypeEnum.FLOAT32)]
    [InlineData(InstructionsEnum.VDOT_FP8, DataTypeEnum.FLOAT8_E4M3)]
    public void EncodeDotProduct_WhenSeparateDestinationPointerIsRequested_ThenFailsClosed(
        InstructionsEnum opcode,
        DataTypeEnum dataType)
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => InstructionEncoder.EncodeDotProduct(
                (uint)opcode,
                dataType,
                destPtr: 0x1000,
                src1Ptr: 0x2000,
                src2Ptr: 0x3000,
                streamLength: 4,
                stride: dataType == DataTypeEnum.FLOAT8_E4M3 ? (ushort)1 : (ushort)4));

        Assert.Contains("separate destination pointer", exception.Message, System.StringComparison.Ordinal);
        Assert.Contains("1D scalar-footprint contour", exception.Message, System.StringComparison.Ordinal);
        Assert.Contains("fail closed", exception.Message, System.StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(InstructionsEnum.VDOT, DataTypeEnum.INT32, 4)]
    [InlineData(InstructionsEnum.VDOTU, DataTypeEnum.UINT32, 4)]
    [InlineData(InstructionsEnum.VDOTF, DataTypeEnum.FLOAT32, 4)]
    [InlineData(InstructionsEnum.VDOT_FP8, DataTypeEnum.FLOAT8_E4M3, 1)]
    public void EncodeDotProduct_WhenDestinationAliasesSource1_ThenKeepsPublishedScalarFootprintAbi(
        InstructionsEnum opcode,
        DataTypeEnum dataType,
        ushort stride)
    {
        const ulong sourceAndResultAddress = 0x2200;
        const ulong secondSourceAddress = 0x2A00;

        VLIW_Instruction instruction = InstructionEncoder.EncodeDotProduct(
            (uint)opcode,
            dataType,
            destPtr: sourceAndResultAddress,
            src1Ptr: sourceAndResultAddress,
            src2Ptr: secondSourceAddress,
            streamLength: 4,
            stride: stride,
            predicateMask: 0x0F,
            tailAgnostic: true,
            maskAgnostic: true);

        Assert.Equal((uint)opcode, instruction.OpCode);
        Assert.Equal(dataType, instruction.DataTypeValue);
        Assert.Equal(0x0F, instruction.PredicateMask);
        Assert.Equal(sourceAndResultAddress, instruction.DestSrc1Pointer);
        Assert.Equal(secondSourceAddress, instruction.Src2Pointer);
        Assert.Equal(4U, instruction.StreamLength);
        Assert.Equal(stride, instruction.Stride);
        Assert.True(instruction.Reduction);
        Assert.False(instruction.Indexed);
        Assert.False(instruction.Is2D);
        Assert.True(instruction.TailAgnostic);
        Assert.True(instruction.MaskAgnostic);
    }
}
