using HybridCPU_ISE.Arch;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Phase09;

public sealed class Phase09SingleLaneDispatchMshrReservationTests
{
    private sealed class SyntheticLoadMicroOp : LoadMicroOp
    {
        private readonly ulong _resultValue;

        public SyntheticLoadMicroOp(ushort destRegId, ushort baseRegId, ulong address, byte size, int virtualThreadId)
        {
            _resultValue = 0xCAFEUL;
            OpCode = (uint)InstructionsEnum.Load;
            Address = address;
            Size = size;
            BaseRegID = baseRegId;
            DestRegID = destRegId;
            WritesRegister = true;
            OwnerThreadId = virtualThreadId;
            VirtualThreadId = virtualThreadId;
            OwnerContextId = virtualThreadId;
            InitializeMetadata();
        }

        public override bool Execute(ref Processor.CPU_Core core) => true;

        public override bool TryGetPrimaryWriteBackResult(out ulong value)
        {
            value = _resultValue;
            return true;
        }

        public override string GetDescription() => "Synthetic single-lane load carrier";
    }

    private sealed class SyntheticScalarMicroOp : MicroOp
    {
        private readonly ulong _resultValue;

        public SyntheticScalarMicroOp(ushort destRegId, ulong resultValue, int virtualThreadId)
        {
            _resultValue = resultValue;
            OpCode = (uint)InstructionsEnum.ADDI;
            DestRegID = destRegId;
            WritesRegister = true;
            OwnerThreadId = virtualThreadId;
            VirtualThreadId = virtualThreadId;
            OwnerContextId = virtualThreadId;
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

        public override string GetDescription() => "Synthetic single-lane scalar carrier";
    }

    [Fact]
    public void SingleLaneExecute_WhenLoadMicroOpDispatches_ThenReservesTypedMshrScoreboardSlot()
    {
        const int virtualThreadId = 2;
        const ushort destinationRegister = 11;
        const ushort baseRegister = 4;

        ProcessorMemoryScope.WithProcessorMemory(
            ProcessorMemoryScope.CreateMemorySubsystem(numBanks: 16, bankWidthBytes: 4096),
            () =>
        {
            var core = new Processor.CPU_Core(0);
            core.InitializePipeline();
            core.TestInitializeFSPScheduler();

            var instruction = CreateScalarInstruction(
                InstructionsEnum.LW,
                rd: (byte)destinationRegister,
                rs1: (byte)baseRegister,
                virtualThreadId: (byte)virtualThreadId);
            var microOp = new SyntheticLoadMicroOp(
                destinationRegister,
                baseRegister,
                address: 0x240UL,
                size: 8,
                virtualThreadId);

            core.TestRunExecuteStageWithDecodedInstruction(
                instruction,
                microOp,
                isMemoryOp: true,
                writesRegister: true,
                reg1Id: instruction.Reg1ID,
                reg2Id: instruction.Reg2ID,
                reg3Id: instruction.Reg3ID,
                pc: 0x8800UL);

            ExecuteStage executeStage = core.GetExecuteStage();
            MicroOpScheduler scheduler = Assert.IsType<MicroOpScheduler>(core.TestGetFSPScheduler());

            Assert.True(executeStage.Valid);
            Assert.True(executeStage.MshrScoreboardSlot >= 0);
            Assert.Equal(virtualThreadId, executeStage.MshrVirtualThreadId);
            Assert.True(scheduler.IsBankPendingForVT(microOp.MemoryBankId, virtualThreadId));
            Assert.True(scheduler.GetOutstandingMemoryCount(virtualThreadId) > 0);
        });
    }

    [Fact]
    public void SingleLaneExecute_WhenNonMemoryMicroOpDispatches_ThenKeepsMshrScoreboardClear()
    {
        const int virtualThreadId = 3;
        const ushort destinationRegister = 12;

        var core = new Processor.CPU_Core(0);
        core.InitializePipeline();
        core.TestInitializeFSPScheduler();

        var instruction = CreateScalarInstruction(
            InstructionsEnum.ADDI,
            rd: (byte)destinationRegister,
            rs1: 5,
            virtualThreadId: (byte)virtualThreadId);
        var microOp = new SyntheticScalarMicroOp(destinationRegister, 0x1234UL, virtualThreadId);

        core.TestRunExecuteStageWithDecodedInstruction(
            instruction,
            microOp,
            writesRegister: true,
            reg1Id: instruction.Reg1ID,
            reg2Id: instruction.Reg2ID,
            reg3Id: instruction.Reg3ID,
            pc: 0x8900UL);

        ExecuteStage executeStage = core.GetExecuteStage();
        MicroOpScheduler scheduler = Assert.IsType<MicroOpScheduler>(core.TestGetFSPScheduler());

        Assert.True(executeStage.Valid);
        Assert.Equal(-1, executeStage.MshrScoreboardSlot);
        Assert.Equal(virtualThreadId, executeStage.MshrVirtualThreadId);
        Assert.Equal(0, scheduler.GetOutstandingMemoryCount(virtualThreadId));
    }

    private static VLIW_Instruction CreateScalarInstruction(
        InstructionsEnum opcode,
        byte rd = 0,
        byte rs1 = 0,
        byte rs2 = 0,
        ushort immediate = 0,
        byte virtualThreadId = 0)
    {
        return new VLIW_Instruction
        {
            OpCode = (uint)opcode,
            DataTypeValue = DataTypeEnum.INT32,
            PredicateMask = 0xFF,
            DestSrc1Pointer = VLIW_Instruction.PackArchRegs(rd, rs1, rs2),
            Src2Pointer = immediate,
            StreamLength = 0,
            Stride = 0,
            VirtualThreadId = virtualThreadId
        };
    }
}

