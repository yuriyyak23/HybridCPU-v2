using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Registers.Retire;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Phase09;

public sealed class Phase09WriteBackRetireLaneTailTests
{
    private sealed class BudgetReleaseScalarMicroOp : MicroOp
    {
        private ulong _writeBackValue;

        public BudgetReleaseScalarMicroOp(
            ushort destinationRegister,
            ulong writeBackValue,
            int ownerThreadId,
            int virtualThreadId,
            int ownerContextId)
        {
            _writeBackValue = writeBackValue;
            OpCode = (uint)InstructionsEnum.ADDI;
            DestRegID = destinationRegister;
            WritesRegister = true;
            OwnerThreadId = ownerThreadId;
            VirtualThreadId = virtualThreadId;
            OwnerContextId = ownerContextId;
            IsFspInjected = true;
            Class = MicroOpClass.Alu;
            InstructionClass = InstructionClass.ScalarAlu;
            SerializationClass = SerializationClass.Free;
            SetClassFlexiblePlacement(SlotClass.AluClass);
            RefreshAdmissionMetadata();
        }

        public override bool Execute(ref Processor.CPU_Core core) => true;

        public override void EmitWriteBackRetireRecords(
            ref Processor.CPU_Core core,
            Span<RetireRecord> retireRecords,
            ref int retireRecordCount)
        {
            AppendWriteBackRetireRecord(
                retireRecords,
                ref retireRecordCount,
                RetireRecord.RegisterWrite(
                    VirtualThreadId,
                    DestRegID,
                    _writeBackValue));
        }

        public override bool TryGetPrimaryWriteBackResult(out ulong value)
        {
            value = _writeBackValue;
            return true;
        }

        public override void CapturePrimaryWriteBackResult(ulong value)
        {
            _writeBackValue = value;
        }

        public override string GetDescription() => "Synthetic WB retire tail scalar carrier";
    }

    [Fact]
    public void WriteBackRetireLaneTail_WhenSpeculativeScalarLaneRetires_ThenReleasesBudgetAndUpdatesCounters()
    {
        const int ownerThreadId = 1;
        const int originalThreadId = 0;
        const int virtualThreadId = 1;
        const ushort destinationRegister = 7;
        const ulong retiredValue = 0xABCDUL;
        const ulong retiredPc = 0x4A00UL;

        var core = new Processor.CPU_Core(0);
        core.InitializePipeline();
        core.TestInitializeFSPScheduler();

        MicroOpScheduler scheduler = Assert.IsType<MicroOpScheduler>(core.TestGetFSPScheduler());
        scheduler.TestSetSpeculationBudget(0);

        WriteBackStage writeBack = new();
        writeBack.Clear();
        writeBack.Valid = true;
        writeBack.ActiveLaneIndex = 0;
        writeBack.MaterializedScalarLaneCount = 1;
        writeBack.MaterializedPhysicalLaneCount = 1;

        var microOp = new BudgetReleaseScalarMicroOp(
            destinationRegister,
            retiredValue,
            ownerThreadId,
            virtualThreadId,
            ownerContextId: ownerThreadId);

        ScalarWriteBackLaneState lane0 = new();
        lane0.Clear(0);
        lane0.IsOccupied = true;
        lane0.PC = retiredPc;
        lane0.OpCode = microOp.OpCode;
        lane0.ResultValue = retiredValue;
        lane0.WritesRegister = true;
        lane0.DestRegID = destinationRegister;
        lane0.MicroOp = microOp;
        lane0.OwnerThreadId = ownerThreadId;
        lane0.VirtualThreadId = virtualThreadId;
        lane0.OwnerContextId = ownerThreadId;
        lane0.WasFspInjected = true;
        lane0.OriginalThreadId = originalThreadId;
        lane0.MshrScoreboardSlot = -1;
        writeBack.Lane0 = lane0;

        core.TestSetWriteBackStage(writeBack);
        core.TestRunWriteBackStage();

        PipelineControl control = core.GetPipelineControl();
        WriteBackStage clearedWriteBack = core.GetWriteBackStage();

        Assert.Equal(1, scheduler.TestGetSpeculationBudget());
        Assert.Equal(retiredValue, core.ReadArch(virtualThreadId, destinationRegister));
        Assert.Equal(1UL, control.InstructionsRetired);
        Assert.Equal(1UL, control.ScalarLanesRetired);
        Assert.Equal(0UL, control.NonScalarLanesRetired);
        Assert.Equal(1UL, control.RetireCycleCount);
        Assert.False(clearedWriteBack.Valid);
    }
}
