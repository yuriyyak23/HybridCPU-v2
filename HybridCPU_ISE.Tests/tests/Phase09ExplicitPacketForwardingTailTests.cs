using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
namespace HybridCPU_ISE.Tests.Phase09;

public sealed class Phase09ExplicitPacketForwardingTailTests
{
    private sealed class ConstantWriteBackMicroOp : MicroOp
    {
        private readonly ulong _resultValue;

        public ConstantWriteBackMicroOp(ushort destRegId, ulong resultValue, bool writesRegister)
        {
            _resultValue = resultValue;
            OpCode = 0;
            DestRegID = destRegId;
            WritesRegister = writesRegister;
            Class = MicroOpClass.Alu;
            InstructionClass = InstructionClass.ScalarAlu;
            SerializationClass = SerializationClass.Free;
            SetClassFlexiblePlacement(SlotClass.AluClass);
        }

        public override bool Execute(ref Processor.CPU_Core core) => true;

        public override bool TryGetPrimaryWriteBackResult(out ulong value)
        {
            value = _resultValue;
            return true;
        }

        public override string GetDescription() => "Synthetic explicit-packet forwarding carrier";
    }

    [Fact]
    public void ExplicitPacketForwarding_WhenEarlierScalarLaneDoesNotWrite_ThenLowestReadyWriterPublishesForwardEx()
    {
        var core = new Processor.CPU_Core(0);
        core.InitializePipeline();

        var lane0 = new ConstantWriteBackMicroOp(destRegId: 5, resultValue: 0x11UL, writesRegister: false);
        var lane2 = new ConstantWriteBackMicroOp(destRegId: 7, resultValue: 0x22UL, writesRegister: true);
        var lane3 = new ConstantWriteBackMicroOp(destRegId: 8, resultValue: 0x33UL, writesRegister: true);

        core.TestExecuteExplicitPacketLanes(
            (LaneIndex: (byte)0, MicroOp: lane0, Pc: 0x3000UL, VtId: 0),
            (LaneIndex: (byte)2, MicroOp: lane2, Pc: 0x3040UL, VtId: 0),
            (LaneIndex: (byte)3, MicroOp: lane3, Pc: 0x3060UL, VtId: 0));

        Processor.CPU_Core.PipelineControl control = core.GetPipelineControl();
        Processor.CPU_Core.ForwardingPath forwardEx = core.TestGetExecuteForwardingPath();

        Assert.True(forwardEx.Valid);
        Assert.Equal((ushort)7, forwardEx.DestRegID);
        Assert.Equal(0x22UL, forwardEx.ForwardedValue);
        Assert.Equal((long)control.CycleCount + 1, forwardEx.AvailableCycle);
        Assert.Equal(Processor.CPU_Core.PipelineStage.Execute, forwardEx.SourceStage);
    }
}
