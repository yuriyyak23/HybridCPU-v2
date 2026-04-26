using System;
using HybridCPU_ISE.Arch;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Phase09;

public sealed class Phase09ExtendedVectorNonRepresentableAddressingClosureTests
{
    [Theory]
    [MemberData(
        nameof(DeferredVectorBatchTestHelper.ExtendedNonRepresentableContours),
        MemberType = typeof(DeferredVectorBatchTestHelper))]
    public void ExecuteStage_WhenExtendedVectorFamilyUsesNonRepresentableIndexedOr2DContour_ThenRejectsCompatSuccessShell(
        DeferredVectorAddressingFamily family,
        InstructionsEnum opcode,
        bool is2D,
        string addressingContour)
    {
        var core = new Processor.CPU_Core(0);
        VLIW_Instruction instruction =
            DeferredVectorBatchTestHelper.CreateAddressingInstruction(
                family,
                opcode,
                is2D,
                virtualThreadId: 2);

        InvalidOperationException? publicationEx = Record.Exception(
            () => DeferredVectorBatchTestHelper.CreateAddressingCarrier(
                family,
                opcode,
                is2D,
                ownerThreadId: 2)) as InvalidOperationException;

        if (publicationEx != null)
        {
            Assert.Contains(addressingContour, publicationEx.Message, StringComparison.Ordinal);
            string expectedContourToken = family == DeferredVectorAddressingFamily.Fma
                ? "VectorFmaMicroOp.InitializeMetadata()"
                : DeferredVectorBatchTestHelper.GetExecuteContourLabel(family);
            Assert.Contains(expectedContourToken, publicationEx.Message, StringComparison.Ordinal);
            Assert.Contains("fail closed", publicationEx.Message, StringComparison.Ordinal);
            return;
        }

        MicroOp microOp = DeferredVectorBatchTestHelper.CreateAddressingCarrier(
            family,
            opcode,
            is2D,
            ownerThreadId: 2);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => core.TestRunExecuteStageWithDecodedInstruction(
                instruction,
                microOp,
                isVectorOp: true,
                isMemoryOp: false));

        Assert.Contains("reference raw execute fallback", ex.Message, StringComparison.Ordinal);
        Assert.NotNull(ex.InnerException);
        Assert.Contains(addressingContour, ex.InnerException!.Message, StringComparison.Ordinal);
        Assert.Contains(
            DeferredVectorBatchTestHelper.GetExecuteContourLabel(family),
            ex.InnerException.Message,
            StringComparison.Ordinal);
        Assert.Contains("fail closed", ex.InnerException.Message, StringComparison.Ordinal);

        var executeStage = core.TestReadExecuteStageStatus();
        Assert.False(executeStage.ResultReady);
        Assert.False(executeStage.VectorComplete);
    }
}

