using HybridCPU_ISE.Arch;
using System;
using Xunit;
using YAKSys_Hybrid_CPU.Core;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests;

public sealed class Phase03DecoderContextVectorAddressingAbiTailTests
{
    [Fact]
    public void DirectFactoryVectorBinaryReject_PrefersDecoderContextIndexedContourOverRawInstructionPayload()
    {
        var rawInstruction = CreateRawOneDimensionalVectorInstruction(InstructionsEnum.VADD);

        DecoderContext context = ProjectedVectorDecoderContextBuilder.Create(in rawInstruction);
        context.IndexedAddressing = true;
        context.Is2DAddressing = false;
        context.HasVectorAddressingContour = true;

        InvalidOperationException exception = Assert.ThrowsAny<InvalidOperationException>(
            () => InstructionRegistry.CreateMicroOp((uint)InstructionsEnum.VADD, context));

        Assert.Contains("indexed", exception.Message, StringComparison.Ordinal);
        Assert.Contains("vector-binary addressing", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DirectFactoryVectorTransferReject_PrefersDecoderContextTwoDimensionalContourOverRawInstructionPayload()
    {
        var rawInstruction = CreateRawOneDimensionalVectorInstruction(InstructionsEnum.VLOAD);

        DecoderContext context = ProjectedVectorDecoderContextBuilder.Create(in rawInstruction);
        context.IndexedAddressing = false;
        context.Is2DAddressing = true;
        context.HasVectorAddressingContour = true;

        InvalidOperationException exception = Assert.ThrowsAny<InvalidOperationException>(
            () => InstructionRegistry.CreateMicroOp((uint)InstructionsEnum.VLOAD, context));

        Assert.Contains("2D", exception.Message, StringComparison.Ordinal);
        Assert.Contains("vector-transfer addressing", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DirectFactoryVectorBinaryReject_WithoutProjectedContourFailsClosedInsteadOfUsingRawIndexedPayload()
    {
        var rawInstruction = CreateRawOneDimensionalVectorInstruction(InstructionsEnum.VADD);
        rawInstruction.Indexed = true;

        DecoderContext context = ProjectedVectorDecoderContextBuilder.Create(in rawInstruction);
        context.HasVectorAddressingContour = false;
        context.IndexedAddressing = false;
        context.Is2DAddressing = false;

        InvalidOperationException exception = Assert.ThrowsAny<InvalidOperationException>(
            () => InstructionRegistry.CreateMicroOp((uint)InstructionsEnum.VADD, context));

        Assert.Contains("projected DecoderContext vector-addressing contour handoff", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Indexed/Is2D fallback is retired", exception.Message, StringComparison.Ordinal);
        Assert.Contains("fail closed", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DirectFactoryVectorTransferReject_WithoutProjectedContourFailsClosedInsteadOfUsingRawTwoDimensionalPayload()
    {
        var rawInstruction = CreateRawOneDimensionalVectorInstruction(InstructionsEnum.VLOAD);
        rawInstruction.Is2D = true;

        DecoderContext context = ProjectedVectorDecoderContextBuilder.Create(in rawInstruction);
        context.HasVectorAddressingContour = false;
        context.IndexedAddressing = false;
        context.Is2DAddressing = false;

        InvalidOperationException exception = Assert.ThrowsAny<InvalidOperationException>(
            () => InstructionRegistry.CreateMicroOp((uint)InstructionsEnum.VLOAD, context));

        Assert.Contains("projected DecoderContext vector-addressing contour handoff", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Indexed/Is2D fallback is retired", exception.Message, StringComparison.Ordinal);
        Assert.Contains("fail closed", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DirectFactoryVectorBinary_PrefersProjectedVectorPayloadOverRawInstructionPayload()
    {
        var rawInstruction = CreateRawOneDimensionalVectorInstruction(InstructionsEnum.VADD);

        DecoderContext context = ProjectedVectorDecoderContextBuilder.Create(in rawInstruction);
        context.DataType = (byte)DataTypeEnum.UINT16;
        context.HasDataType = true;
        context.VectorPrimaryPointer = 0x500;
        context.VectorSecondaryPointer = 0x700;
        context.VectorStreamLength = 2;
        context.VectorStride = 8;

        VectorBinaryOpMicroOp microOp = Assert.IsType<VectorBinaryOpMicroOp>(
            InstructionRegistry.CreateMicroOp((uint)InstructionsEnum.VADD, context));

        Assert.Equal(0x500UL, microOp.Instruction.DestSrc1Pointer);
        Assert.Equal(0x700UL, microOp.Instruction.Src2Pointer);
        Assert.Equal(2U, microOp.Instruction.StreamLength);
        Assert.Equal((ushort)8, microOp.Instruction.Stride);
        Assert.Equal(DataTypeEnum.UINT16, microOp.Instruction.DataTypeValue);
    }

    [Fact]
    public void DirectFactoryVectorBinary_WithoutProjectedVectorPayload_FailsClosedInsteadOfUsingRawInstructionPayload()
    {
        var rawInstruction = CreateRawOneDimensionalVectorInstruction(InstructionsEnum.VADD);

        var context = new DecoderContext
        {
            OpCode = (uint)InstructionsEnum.VADD,
            DataType = rawInstruction.DataType,
            HasDataType = true,
            IndexedAddressing = rawInstruction.Indexed,
            Is2DAddressing = rawInstruction.Is2D,
            HasVectorAddressingContour = true,
            PredicateMask = rawInstruction.PredicateMask
        };

        InvalidOperationException exception = Assert.ThrowsAny<InvalidOperationException>(
            () => InstructionRegistry.CreateMicroOp((uint)InstructionsEnum.VADD, context));

        Assert.Contains("projected DecoderContext vector payload handoff", exception.Message, StringComparison.Ordinal);
        Assert.Contains("vector payload fallback is retired", exception.Message, StringComparison.Ordinal);
    }

    private static VLIW_Instruction CreateRawOneDimensionalVectorInstruction(
        InstructionsEnum opcode)
    {
        return new VLIW_Instruction
        {
            OpCode = (uint)opcode,
            DataTypeValue = DataTypeEnum.INT32,
            PredicateMask = 0xFF,
            DestSrc1Pointer = 0x220,
            Src2Pointer = 0x320,
            StreamLength = 4,
            Stride = 4,
            Indexed = false,
            Is2D = false
        };
    }
}

