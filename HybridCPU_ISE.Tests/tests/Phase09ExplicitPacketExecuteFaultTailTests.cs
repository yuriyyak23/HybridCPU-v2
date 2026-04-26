using System;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Memory;
using YAKSys_Hybrid_CPU.Core.Pipeline;

namespace HybridCPU_ISE.Tests.Phase09;

public sealed class Phase09ExplicitPacketExecuteFaultTailTests
{
    private abstract class ThrowingExplicitPacketMicroOp : MicroOp
    {
        protected ThrowingExplicitPacketMicroOp(uint opCode = 0)
        {
            OpCode = opCode;
            Class = MicroOpClass.Alu;
            InstructionClass = InstructionClass.ScalarAlu;
            SerializationClass = SerializationClass.Free;
            SetClassFlexiblePlacement(SlotClass.AluClass);
        }
    }

    private sealed class ThrowingPageFaultMicroOp : ThrowingExplicitPacketMicroOp
    {
        public override bool Execute(ref Processor.CPU_Core core)
        {
            throw new PageFaultException(0xCAFEUL, isWrite: false);
        }

        public override string GetDescription() => "Synthetic explicit-packet page-fault carrier";
    }

    private sealed class ThrowingAlignmentFaultMicroOp : ThrowingExplicitPacketMicroOp
    {
        public override bool Execute(ref Processor.CPU_Core core)
        {
            throw new MemoryAlignmentException(0x1003UL, 4, "SYNTH");
        }

        public override string GetDescription() => "Synthetic explicit-packet alignment-fault carrier";
    }

    private sealed class ThrowingNonFaultMicroOp : ThrowingExplicitPacketMicroOp
    {
        public override bool Execute(ref Processor.CPU_Core core)
        {
            throw new InvalidOperationException("synthetic execute failure");
        }

        public override string GetDescription() => "Synthetic explicit-packet non-fault failure carrier";
    }

    [Fact]
    public void ExplicitPacketGenericMicroOp_WhenExecuteThrowsPageFault_ThenPropagatesStageAwareFault()
    {
        var core = new Processor.CPU_Core(0);
        var microOp = new ThrowingPageFaultMicroOp();

        PageFaultException ex = Assert.Throws<PageFaultException>(
            () => core.TestExecuteExplicitPacketLaneMicroOp(
                laneIndex: 0,
                microOp,
                pc: 0x2200));

        Assert.Equal(0xCAFEUL, ex.FaultAddress);
        Assert.False(ex.IsWrite);
    }

    [Fact]
    public void ExplicitPacketGenericMicroOp_WhenExecuteThrowsAlignmentFault_ThenRethrowsTranslatedPageFault()
    {
        var core = new Processor.CPU_Core(0);
        var microOp = new ThrowingAlignmentFaultMicroOp();

        PageFaultException ex = Assert.Throws<PageFaultException>(
            () => core.TestExecuteExplicitPacketLaneMicroOp(
                laneIndex: 0,
                microOp,
                pc: 0x2300));

        Assert.Equal(0x1003UL, ex.FaultAddress);
        Assert.True(ex.IsWrite);
        Assert.IsType<MemoryAlignmentException>(ex.InnerException);
    }

    [Fact]
    public void ExplicitPacketGenericMicroOp_WhenExecuteThrowsNonFaultException_ThenFailClosesLaneWithoutTrap()
    {
        var core = new Processor.CPU_Core(0);
        var microOp = new ThrowingNonFaultMicroOp();

        core.TestExecuteExplicitPacketLaneMicroOp(
            laneIndex: 0,
            microOp,
            pc: 0x2400);

        Processor.CPU_Core.ExecuteStage executeStage = core.GetExecuteStage();
        Processor.CPU_Core.ScalarExecuteLaneState lane = executeStage.Lane0;

        Assert.True(executeStage.Valid);
        Assert.True(lane.IsOccupied);
        Assert.Null(lane.MicroOp);
        Assert.False(lane.ResultReady);

        Processor.CPU_Core.PipelineControl control = core.GetPipelineControl();
        Assert.Equal(0UL, control.InstructionsRetired);
        Assert.Equal(0UL, control.MemoryStalls);
    }
}
