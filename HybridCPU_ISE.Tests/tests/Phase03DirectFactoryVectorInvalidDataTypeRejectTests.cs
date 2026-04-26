using System;
using HybridCPU_ISE.Arch;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU.Core;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests;

public sealed class Phase03DirectFactoryVectorInvalidDataTypeRejectTests
{
    [Theory]
    [MemberData(
        nameof(DeferredVectorBatchTestHelper.InvalidDataTypePublicationCases),
        MemberType = typeof(DeferredVectorBatchTestHelper))]
    public void DirectFactory_WhenElementSizedVectorFamilyUsesInvalidDataType_ThenFailsClosedBeforePublication(
        DeferredVectorPublicationFamily family,
        InstructionsEnum opcode,
        DataTypeEnum nominalDataType,
        string metadataSurface)
    {
        VLIW_Instruction instruction =
            DeferredVectorBatchTestHelper.CreateInvalidDataTypeInstruction(
                family,
                opcode,
                nominalDataType);
        DecoderContext context = DeferredVectorBatchTestHelper.CreateDecoderContext(in instruction);

        InvalidOperationException ex = Assert.ThrowsAny<InvalidOperationException>(
            () => InstructionRegistry.CreateMicroOp((uint)opcode, context));

        Assert.Contains(metadataSurface, ex.Message, StringComparison.Ordinal);
        Assert.Contains("unsupported element DataType", ex.Message, StringComparison.Ordinal);
        Assert.Contains("authoritative mainline vector publication contour", ex.Message, StringComparison.Ordinal);
        Assert.Contains("fail closed", ex.Message, StringComparison.Ordinal);
    }
}

