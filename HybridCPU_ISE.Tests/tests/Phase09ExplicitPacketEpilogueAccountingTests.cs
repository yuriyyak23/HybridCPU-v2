using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.Phase09;

public sealed class Phase09ExplicitPacketEpilogueAccountingTests
{
    private sealed class ConstantWriteBackMicroOp : MicroOp
    {
        private readonly ulong _resultValue;

        public ConstantWriteBackMicroOp(ushort destRegId, ulong resultValue)
        {
            _resultValue = resultValue;
            OpCode = 0;
            DestRegID = destRegId;
            WritesRegister = true;
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

        public override string GetDescription() => "Synthetic explicit-packet constant writeback carrier";
    }

    [Fact]
    public void ExplicitPacketEpilogue_WhenTwoScalarLanesExecute_ThenUpdatesMultilaneCountersAndHistogram()
    {
        var core = new Processor.CPU_Core(0);
        core.InitializePipeline();
        var lane0 = new ConstantWriteBackMicroOp(destRegId: 5, resultValue: 0x11UL);
        var lane1 = new ConstantWriteBackMicroOp(destRegId: 6, resultValue: 0x22UL);

        core.TestExecuteExplicitPacketLanes(
            (LaneIndex: (byte)0, MicroOp: lane0, Pc: 0x3000UL, VtId: 0),
            (LaneIndex: (byte)1, MicroOp: lane1, Pc: 0x3020UL, VtId: 0));

        Processor.CPU_Core.PipelineControl control = core.GetPipelineControl();

        Assert.Equal(1UL, control.MultiLaneExecuteCount);
        Assert.Equal(1UL, control.PartialWidthIssueCount);
        Assert.NotNull(control.ScalarIssueWidthHistogram);
        Assert.Equal(1UL, control.ScalarIssueWidthHistogram[2]);
        Assert.Equal(0UL, control.ScalarIssueWidthHistogram[0]);
        Assert.Equal(0UL, control.ScalarIssueWidthHistogram[1]);
        Assert.Equal(0UL, control.ScalarIssueWidthHistogram[3]);
        Assert.Equal(0UL, control.ScalarIssueWidthHistogram[4]);
    }
}
