using HybridCPU_ISE.Core;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests;

public sealed class Phase09InterlockMemoryStallTailTests
{
    [Fact]
    public void CheckPipelineHazards_WhenExplicitExecuteLaneIsNotReady_ThenReportsMemoryStall()
    {
        var core = new Processor.CPU_Core(0);

        bool shouldStall = core.TestCheckPipelineHazardForExplicitExecuteLaneNotReady();
        var pipeState = core.GetPipelineControl();

        Assert.True(shouldStall);
        Assert.Equal(1UL, pipeState.MemoryStalls);
        Assert.Equal(Processor.CPU_Core.PipelineStallKind.MemoryWait, pipeState.StallReason);
    }

    [Fact]
    public void ExecutePipelineCycle_WhenExplicitExecuteLaneIsNotReady_ThenUsesUnifiedStallOwnerAndSkipsFetch()
    {
        var sink = new TraceSink(TraceFormat.JSON, "phase11-interlock-explicit-execute-stall.json");
        sink.SetEnabled(true);
        sink.SetLevel(TraceLevel.Full);

        TraceSink? originalTraceSink = Processor.TraceSink;
        try
        {
            Processor.TraceSink = sink;

            var core = new Processor.CPU_Core(0);
            core.TestStageFullCycleForExplicitExecuteLaneNotReadyHazard();

            core.ExecutePipelineCycle();

            var pipeState = core.GetPipelineControl();
            FullStateTraceEvent evt = Assert.Single(sink.GetThreadTrace(0));

            Assert.True(pipeState.Stalled);
            Assert.Equal(1UL, pipeState.CycleCount);
            Assert.Equal(1UL, pipeState.StallCycles);
            Assert.Equal(1UL, pipeState.MemoryStalls);
            Assert.Equal(Processor.CPU_Core.PipelineStallKind.MemoryWait, pipeState.StallReason);
            Assert.False(core.GetFetchStage().Valid);
            Assert.False(core.GetDecodeStage().Valid);
            Assert.Equal(0x6400UL, core.ReadActiveLivePc());
            Assert.Equal("STALL", evt.PipelineStage);
            Assert.True(evt.Stalled);
            Assert.Equal(
                Processor.CPU_Core.PipelineStallText.Render(
                    Processor.CPU_Core.PipelineStallKind.MemoryWait,
                    Processor.CPU_Core.PipelineStallTextStyle.Trace),
                evt.StallReason);
        }
        finally
        {
            Processor.TraceSink = originalTraceSink;
        }
    }

    [Fact]
    public void CheckPipelineHazards_WhenExplicitMemoryLaneIsNotReady_ThenReportsStructuralMemoryStall()
    {
        var core = new Processor.CPU_Core(0);

        bool shouldStall = core.TestCheckPipelineHazardForExplicitMemoryLaneNotReady();
        var pipeState = core.GetPipelineControl();

        Assert.True(shouldStall);
        Assert.Equal(1UL, pipeState.MemoryStalls);
        Assert.Equal(Processor.CPU_Core.PipelineStallKind.MemoryWait, pipeState.StallReason);
    }

    [Fact]
    public void CheckPipelineHazards_WhenVectorExecuteIsIncomplete_ThenReportsMemoryStall()
    {
        var core = new Processor.CPU_Core(0);

        bool shouldStall = core.TestCheckPipelineHazardForIncompleteVectorExecute();
        var pipeState = core.GetPipelineControl();

        Assert.True(shouldStall);
        Assert.Equal(1UL, pipeState.MemoryStalls);
        Assert.Equal(Processor.CPU_Core.PipelineStallKind.MemoryWait, pipeState.StallReason);
    }

    [Fact]
    public void CheckPipelineHazards_WhenExecuteStageValidButMaterializedLaneCountIsZero_ThenReportsInvariantViolation()
    {
        var core = new Processor.CPU_Core(0);

        bool shouldStall = core.TestCheckPipelineHazardForInvalidExecuteOccupancyState();
        var pipeState = core.GetPipelineControl();

        Assert.True(shouldStall);
        Assert.Equal(0UL, pipeState.MemoryStalls);
        Assert.Equal(1UL, pipeState.InvariantViolationCount);
        Assert.Equal(Processor.CPU_Core.PipelineStallKind.InvariantViolation, pipeState.StallReason);
    }

    [Fact]
    public void CheckPipelineHazards_WhenMemoryStageValidButMaterializedLaneCountIsZero_ThenReportsInvariantViolation()
    {
        var core = new Processor.CPU_Core(0);

        bool shouldStall = core.TestCheckPipelineHazardForInvalidMemoryOccupancyState();
        var pipeState = core.GetPipelineControl();

        Assert.True(shouldStall);
        Assert.Equal(0UL, pipeState.MemoryStalls);
        Assert.Equal(1UL, pipeState.InvariantViolationCount);
        Assert.Equal(Processor.CPU_Core.PipelineStallKind.InvariantViolation, pipeState.StallReason);
    }
}
