using System;
using HybridCPU_ISE.Arch;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Execution;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using YAKSys_Hybrid_CPU.Execution;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests;

public sealed class Phase09ExecutionFaultContractTests
{
    [Fact]
    public void UnsupportedExecutionSurfaceException_UsesStableCategoryPrefix_AndNoInnerException()
    {
        UnsupportedExecutionSurfaceException ex =
            UnsupportedExecutionSurfaceException.CreateForStreamControl(
                slotIndex: 1,
                opCode: (uint)InstructionsEnum.STREAM_START,
                bundlePc: 0x3700);

        string expectedPrefix =
            ExecutionFaultContract.GetMessagePrefix(ExecutionFaultCategory.UnsupportedExecutionSurface);

        Assert.Equal(ExecutionFaultCategory.UnsupportedExecutionSurface, ex.Category);
        Assert.Equal(ExecutionFaultCategory.UnsupportedExecutionSurface, ExecutionFaultContract.GetCategory(ex));
        Assert.StartsWith(expectedPrefix, ex.Message);
        Assert.Contains("StreamControl", ex.Message, StringComparison.Ordinal);
        Assert.Null(ex.InnerException);
    }

    [Fact]
    public void InvalidInternalOpException_UsesStableCategoryPrefix_AndNoInnerException()
    {
        var ex = new InvalidInternalOpException(InternalOpKind.Load);
        string expectedPrefix =
            ExecutionFaultContract.GetMessagePrefix(ExecutionFaultCategory.InvalidInternalOp);

        Assert.Equal(ExecutionFaultCategory.InvalidInternalOp, ex.Category);
        Assert.Equal(ExecutionFaultCategory.InvalidInternalOp, ExecutionFaultContract.GetCategory(ex));
        Assert.StartsWith(expectedPrefix, ex.Message);
        Assert.Contains("InternalOpKind.Load", ex.Message, StringComparison.Ordinal);
        Assert.Null(ex.InnerException);
    }

    [Fact]
    public void ReferenceModelValidationRejection_UsesStableCategoryPrefix_AndNoInnerException()
    {
        ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => ReferenceModel.AreCloseNarrowFloat(1.0, 1.0, DataTypeEnum.FLOAT32));

        string expectedPrefix =
            ExecutionFaultContract.GetMessagePrefix(ExecutionFaultCategory.ReferenceModelValidation);

        Assert.Equal(ExecutionFaultCategory.ReferenceModelValidation, ExecutionFaultContract.GetCategory(ex));
        Assert.StartsWith(expectedPrefix, ex.Message);
        Assert.Contains("AreCloseNarrowFloat not applicable to FLOAT32", ex.Message, StringComparison.Ordinal);
        Assert.Null(ex.InnerException);
    }

    [Fact]
    public void VectorElementTypePublicationRejection_UsesStableCategoryPrefix_AndNoInnerException()
    {
        VLIW_Instruction instruction =
            DeferredVectorBatchTestHelper.CreateInvalidDataTypeInstruction(
                DeferredVectorPublicationFamily.Transfer,
                InstructionsEnum.VLOAD,
                DataTypeEnum.INT32);
        DecoderContext context = DeferredVectorBatchTestHelper.CreateDecoderContext(in instruction);

        InvalidOperationException ex = Assert.ThrowsAny<InvalidOperationException>(
            () => InstructionRegistry.CreateMicroOp((uint)InstructionsEnum.VLOAD, context));

        string expectedPrefix =
            ExecutionFaultContract.GetMessagePrefix(ExecutionFaultCategory.UnsupportedVectorElementType);

        Assert.Equal(ExecutionFaultCategory.UnsupportedVectorElementType, ExecutionFaultContract.GetCategory(ex));
        Assert.StartsWith(expectedPrefix, ex.Message);
        Assert.Contains("unsupported element DataType", ex.Message, StringComparison.Ordinal);
        Assert.Null(ex.InnerException);
    }

    [Fact]
    public void DotProductElementTypePublicationRejection_UsesStableCategoryPrefix_AndNoInnerException()
    {
        VLIW_Instruction instruction =
            DeferredVectorBatchTestHelper.CreateInvalidDataTypeInstruction(
                DeferredVectorPublicationFamily.DotProduct,
                InstructionsEnum.VDOT,
                DataTypeEnum.INT32);
        DecoderContext context = DeferredVectorBatchTestHelper.CreateDecoderContext(in instruction);

        InvalidOperationException ex = Assert.ThrowsAny<InvalidOperationException>(
            () => InstructionRegistry.CreateMicroOp((uint)InstructionsEnum.VDOT, context));

        string expectedPrefix =
            ExecutionFaultContract.GetMessagePrefix(ExecutionFaultCategory.UnsupportedVectorElementType);

        Assert.Equal(ExecutionFaultCategory.UnsupportedVectorElementType, ExecutionFaultContract.GetCategory(ex));
        Assert.StartsWith(expectedPrefix, ex.Message);
        Assert.Contains("unsupported element DataType", ex.Message, StringComparison.Ordinal);
        Assert.Null(ex.InnerException);
    }

    [Fact]
    public void VectorExecuteFallback_WrapsKnownFaultWithSameStableCategory()
    {
        var core = new Processor.CPU_Core(0);
        var microOp = new VectorALUMicroOp
        {
            OpCode = (uint)InstructionsEnum.VADD,
            OwnerThreadId = 2,
            VirtualThreadId = 2,
            OwnerContextId = 2,
            Instruction = new VLIW_Instruction
            {
                OpCode = (uint)InstructionsEnum.VADD,
                DataType = 0xFE,
                PredicateMask = 0xFF,
                DestSrc1Pointer = 0x200UL,
                Src2Pointer = 0x300UL,
                StreamLength = 2,
                Stride = 4
            }
        };
        microOp.InitializeMetadata();

        InvalidOperationException ex = Assert.ThrowsAny<InvalidOperationException>(
            () => core.TestRunExecuteStageWithDecodedInstruction(
                microOp.Instruction,
                microOp,
                isVectorOp: true));

        string expectedPrefix =
            ExecutionFaultContract.GetMessagePrefix(ExecutionFaultCategory.UnsupportedVectorElementType);

        Assert.Equal(ExecutionFaultCategory.UnsupportedVectorElementType, ExecutionFaultContract.GetCategory(ex));
        Assert.StartsWith(expectedPrefix, ex.Message);
            Assert.Contains("reference raw execute fallback after MicroOp failure", ex.Message, StringComparison.Ordinal);
        Assert.NotNull(ex.InnerException);
        Assert.Equal(
            ExecutionFaultCategory.UnsupportedVectorElementType,
            ExecutionFaultContract.GetCategory(ex.InnerException!));
        Assert.StartsWith(expectedPrefix, ex.InnerException!.Message);
        Assert.Contains("unsupported element DataType", ex.InnerException.Message, StringComparison.Ordinal);
    }
}
