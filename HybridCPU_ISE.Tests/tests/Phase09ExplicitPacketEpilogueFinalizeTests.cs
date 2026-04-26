using Xunit;
using YAKSys_Hybrid_CPU;

namespace HybridCPU_ISE.Tests.Phase09;

public sealed class Phase09ExplicitPacketEpilogueFinalizeTests
{
    [Fact]
    public void ExplicitPacketEpilogue_WhenExecutePacketIsEmpty_ThenClearsExecuteStageAndConsumesDecode()
    {
        var core = new Processor.CPU_Core(0);
        core.InitializePipeline();

        bool consumedEmptyPacket = core.TestTryConsumeEmptyExplicitPacketAfterExecution();

        Processor.CPU_Core.ExecuteStage executeStage = core.GetExecuteStage();
        Processor.CPU_Core.DecodeStage decodeStage = core.GetDecodeStage();

        Assert.True(consumedEmptyPacket);
        Assert.False(executeStage.Valid);
        Assert.Equal(0, executeStage.MaterializedPhysicalLaneCount);
        Assert.False(decodeStage.Valid);
    }

    [Fact]
    public void ExplicitPacketEpilogue_WhenExecutePacketStillHasLane_ThenRestoresActiveLaneAndConsumesDecode()
    {
        var core = new Processor.CPU_Core(0);
        core.InitializePipeline();

        core.TestCompleteExplicitPacketExecuteDispatch(
            occupiedLaneIndex: 2,
            originalActiveLaneIndex: 5);

        Processor.CPU_Core.ExecuteStage executeStage = core.GetExecuteStage();
        Processor.CPU_Core.DecodeStage decodeStage = core.GetDecodeStage();
        Processor.CPU_Core.ScalarExecuteLaneState lane2 = core.GetExecuteStageLane(2);

        Assert.True(executeStage.Valid);
        Assert.Equal((byte)5, executeStage.ActiveLaneIndex);
        Assert.True(lane2.IsOccupied);
        Assert.False(decodeStage.Valid);
    }
}
