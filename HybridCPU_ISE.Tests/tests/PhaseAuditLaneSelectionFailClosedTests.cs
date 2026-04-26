using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Pipeline;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.PhaseAudit;

public sealed class PhaseAuditLaneSelectionFailClosedTests
{
    private const byte InvalidLaneIndex = 8;

    [Fact]
    public void ExecuteStage_WhenLaneIndexIsInvalid_ThenFailsClosedWithoutLane0Alias()
    {
        ExecuteStage stage = new();
        stage.Clear();
        stage.Lane0.PC = 0xE001UL;

        InvalidPipelineLaneException readFault =
            Assert.Throws<InvalidPipelineLaneException>(() => stage.GetLane(InvalidLaneIndex));

        AssertInvalidLaneFault(readFault, nameof(ExecuteStage));

        ScalarExecuteLaneState replacement = new();
        replacement.Clear(0);
        replacement.PC = 0xBAD0UL;

        InvalidPipelineLaneException writeFault =
            Assert.Throws<InvalidPipelineLaneException>(() => stage.SetLane(InvalidLaneIndex, replacement));

        AssertInvalidLaneFault(writeFault, nameof(ExecuteStage));
        Assert.Equal(0xE001UL, stage.Lane0.PC);
    }

    [Fact]
    public void MemoryStage_WhenLaneIndexIsInvalid_ThenFailsClosedWithoutLane0Alias()
    {
        MemoryStage stage = new();
        stage.Clear();
        stage.Lane0.PC = 0xA001UL;

        InvalidPipelineLaneException readFault =
            Assert.Throws<InvalidPipelineLaneException>(() => stage.GetLane(InvalidLaneIndex));

        AssertInvalidLaneFault(readFault, nameof(MemoryStage));

        ScalarMemoryLaneState replacement = new();
        replacement.Clear(0);
        replacement.PC = 0xBAD1UL;

        InvalidPipelineLaneException writeFault =
            Assert.Throws<InvalidPipelineLaneException>(() => stage.SetLane(InvalidLaneIndex, replacement));

        AssertInvalidLaneFault(writeFault, nameof(MemoryStage));
        Assert.Equal(0xA001UL, stage.Lane0.PC);
    }

    [Fact]
    public void WriteBackStage_WhenLaneIndexIsInvalid_ThenFailsClosedWithoutLane0Alias()
    {
        WriteBackStage stage = new();
        stage.Clear();
        stage.Lane0.PC = 0xB001UL;

        InvalidPipelineLaneException readFault =
            Assert.Throws<InvalidPipelineLaneException>(() => stage.GetLane(InvalidLaneIndex));

        AssertInvalidLaneFault(readFault, nameof(WriteBackStage));

        ScalarWriteBackLaneState replacement = new();
        replacement.Clear(0);
        replacement.PC = 0xBAD2UL;

        InvalidPipelineLaneException writeFault =
            Assert.Throws<InvalidPipelineLaneException>(() => stage.SetLane(InvalidLaneIndex, replacement));

        AssertInvalidLaneFault(writeFault, nameof(WriteBackStage));
        Assert.Equal(0xB001UL, stage.Lane0.PC);
    }

    private static void AssertInvalidLaneFault(
        InvalidPipelineLaneException fault,
        string expectedStageName)
    {
        Assert.Equal(expectedStageName, fault.StageName);
        Assert.Equal(InvalidLaneIndex, fault.LaneIndex);
        Assert.Equal(ExecutionFaultCategory.InvalidPipelineLane, fault.Category);
        Assert.Equal(
            ExecutionFaultCategory.InvalidPipelineLane,
            ExecutionFaultContract.GetCategory(fault));
        Assert.Null(fault.BundlePc);
        Assert.Null(fault.VtId);
    }
}
