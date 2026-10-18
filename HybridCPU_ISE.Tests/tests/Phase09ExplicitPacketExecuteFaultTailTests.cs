using System;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Memory;
using YAKSys_Hybrid_CPU.Core.Pipeline;

namespace HybridCPU_ISE.Tests;

public sealed class ExplicitPacketExecuteFaultTailTests
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
        core.InitializePipeline();
        var microOp = new ThrowingPageFaultMicroOp();
        ulong retiredBefore = core.GetPipelineControl().InstructionsRetired;

        PageFaultException ex = Assert.Throws<PageFaultException>(
            () => core.TestExecuteExplicitPacketLaneMicroOp(
                laneIndex: 0,
                microOp,
                pc: 0x2200));

        Assert.Equal(0xCAFEUL, ex.FaultAddress);
        Assert.False(ex.IsWrite);
        Assert.False(core.GetExecuteStage().Valid);
        Assert.False(core.TestGetExecuteForwardingPath().Valid);
        Assert.Equal(retiredBefore, core.GetPipelineControl().InstructionsRetired);
        Assert.Equal(0UL, core.TestGetReferenceRawFallbackCount());
    }

    [Fact]
    public void ExplicitPacketGenericMicroOp_WhenExecuteThrowsAlignmentFault_ThenRethrowsTranslatedPageFault()
    {
        var core = new Processor.CPU_Core(0);
        core.InitializePipeline();
        var microOp = new ThrowingAlignmentFaultMicroOp();
        ulong retiredBefore = core.GetPipelineControl().InstructionsRetired;

        PageFaultException ex = Assert.Throws<PageFaultException>(
            () => core.TestExecuteExplicitPacketLaneMicroOp(
                laneIndex: 0,
                microOp,
                pc: 0x2300));

        Assert.Equal(0x1003UL, ex.FaultAddress);
        Assert.True(ex.IsWrite);
        Assert.IsType<MemoryAlignmentException>(ex.InnerException);
        Assert.False(core.GetExecuteStage().Valid);
        Assert.False(core.TestGetExecuteForwardingPath().Valid);
        Assert.Equal(retiredBefore, core.GetPipelineControl().InstructionsRetired);
        Assert.Equal(0UL, core.TestGetReferenceRawFallbackCount());
    }

    [Fact]
    public void ExplicitPacketGenericMicroOp_WhenExecuteThrowsUnknownException_ThenFailsClosedInsteadOfStallingLane()
    {
        var core = new Processor.CPU_Core(0);
        var microOp = new ThrowingNonFaultMicroOp();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => core.TestExecuteExplicitPacketLaneMicroOp(
                laneIndex: 0,
                microOp,
                pc: 0x2400));

        Processor.CPU_Core.ExecuteStage executeStage = core.GetExecuteStage();
        Processor.CPU_Core.ScalarExecuteLaneState lane = executeStage.Lane0;

        Assert.Equal(
            ExecutionFaultCategory.InvalidInternalOp,
            ExecutionFaultContract.GetCategory(exception));
        Assert.IsType<InvalidOperationException>(exception.InnerException);
        Assert.True(executeStage.Valid);
        Assert.False(lane.IsOccupied);
        Assert.Null(lane.MicroOp);
        Assert.False(lane.ResultReady);

        Processor.CPU_Core.PipelineControl control = core.GetPipelineControl();
        Assert.Equal(0UL, control.InstructionsRetired);
        Assert.Equal(0UL, control.MemoryStalls);
        Assert.Equal(0UL, core.TestGetReferenceRawFallbackCount());
    }
}
