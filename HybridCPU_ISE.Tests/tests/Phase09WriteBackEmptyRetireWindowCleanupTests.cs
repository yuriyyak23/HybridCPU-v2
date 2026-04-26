using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Pipeline.Metadata;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Phase09;

public sealed class Phase09WriteBackEmptyRetireWindowCleanupTests
{
    private sealed class InvisibleWriteBackMicroOp : MicroOp
    {
        public InvisibleWriteBackMicroOp(int virtualThreadId)
        {
            OpCode = (uint)InstructionsEnum.Nope;
            OwnerThreadId = virtualThreadId;
            VirtualThreadId = virtualThreadId;
            OwnerContextId = virtualThreadId;
            Class = MicroOpClass.Lsu;
            InstructionClass = YAKSys_Hybrid_CPU.Arch.InstructionClass.Memory;
            SerializationClass = SerializationClass.Free;
            Placement = Placement with { DomainTag = 0x31UL };
            RefreshAdmissionMetadata();
        }

        public override bool IsRetireVisible => false;

        public override bool Execute(ref Processor.CPU_Core core) => true;

        public override string GetDescription() => "Synthetic invisible write-back carrier";
    }

    [Fact]
    public void EmptyRetireWindow_WhenOnlyDeferredAndInvisibleLanesRemain_ThenWriteBackStageFailClosesAndClears()
    {
        var core = new Processor.CPU_Core(0);
        core.InitializePipeline();

        WriteBackStage writeBack = CreateWriteBackStageWithOnlyCleanupLanes();

        core.TestSetWriteBackStage(writeBack);
        core.TestHandleEmptyWriteBackRetireWindow();

        WriteBackStage clearedWriteBack = core.GetWriteBackStage();
        ScalarWriteBackLaneState deferredLane = core.GetWriteBackStageLane(6);
        ScalarWriteBackLaneState invisibleLane = core.GetWriteBackStageLane(7);
        PipelineControl control = core.GetPipelineControl();

        Assert.False(clearedWriteBack.Valid);
        Assert.False(clearedWriteBack.UsesExplicitPacketLanes);
        Assert.False(deferredLane.IsOccupied);
        Assert.False(invisibleLane.IsOccupied);
        Assert.Equal(0UL, control.InstructionsRetired);
        Assert.Equal(0UL, control.NonScalarLanesRetired);
        Assert.Equal(0UL, control.ExceptionYoungerSuppressCount);
    }

    private static WriteBackStage CreateWriteBackStageWithOnlyCleanupLanes()
    {
        WriteBackStage writeBack = new();
        writeBack.Clear();
        writeBack.Valid = true;
        writeBack.ActiveLaneIndex = 6;
        writeBack.UsesExplicitPacketLanes = true;
        writeBack.MaterializedPhysicalLaneCount = 2;

        ScalarWriteBackLaneState deferredLane = new();
        deferredLane.Clear(6);
        deferredLane.IsOccupied = true;
        deferredLane.PC = 0x7000UL;
        deferredLane.OpCode = (uint)InstructionsEnum.Nope;
        deferredLane.ResultValue = 0x55UL;
        deferredLane.OwnerThreadId = 0;
        deferredLane.VirtualThreadId = 0;
        deferredLane.OwnerContextId = 0;
        deferredLane.MshrScoreboardSlot = -1;
        writeBack.Lane6 = deferredLane;

        ScalarWriteBackLaneState invisibleLane = new();
        invisibleLane.Clear(7);
        invisibleLane.IsOccupied = true;
        invisibleLane.PC = 0x7010UL;
        invisibleLane.OpCode = (uint)InstructionsEnum.Nope;
        invisibleLane.ResultValue = 0x99UL;
        invisibleLane.OwnerThreadId = 0;
        invisibleLane.VirtualThreadId = 0;
        invisibleLane.OwnerContextId = 0;
        invisibleLane.MicroOp = new InvisibleWriteBackMicroOp(0);
        invisibleLane.MshrScoreboardSlot = -1;
        writeBack.Lane7 = invisibleLane;

        return writeBack;
    }
}
