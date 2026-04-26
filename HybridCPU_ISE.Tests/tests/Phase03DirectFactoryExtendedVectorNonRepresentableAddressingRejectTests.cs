using System;
using HybridCPU_ISE.Arch;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU.Core;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests;

public sealed class Phase03DirectFactoryExtendedVectorNonRepresentableAddressingRejectTests
{
    [Theory]
    [MemberData(
        nameof(DeferredVectorBatchTestHelper.ExtendedNonRepresentableContours),
        MemberType = typeof(DeferredVectorBatchTestHelper))]
    public void DirectFactory_WhenExtendedVectorFamilyUsesNonRepresentableIndexedOr2DContour_ThenFailsClosedBeforeManualPublication(
        DeferredVectorAddressingFamily family,
        InstructionsEnum opcode,
        bool is2D,
        string addressingContour)
    {
        VLIW_Instruction instruction =
            DeferredVectorBatchTestHelper.CreateAddressingInstruction(family, opcode, is2D);
        DecoderContext context = DeferredVectorBatchTestHelper.CreateDecoderContext(in instruction);

        InvalidOperationException ex = Assert.ThrowsAny<InvalidOperationException>(
            () => InstructionRegistry.CreateMicroOp((uint)opcode, context));

        Assert.Contains(addressingContour, ex.Message, StringComparison.Ordinal);
        Assert.Contains(
            DeferredVectorBatchTestHelper.GetFactoryAddressingLabel(family),
            ex.Message,
            StringComparison.Ordinal);
        Assert.Contains("fail closed", ex.Message, StringComparison.Ordinal);
    }
}

