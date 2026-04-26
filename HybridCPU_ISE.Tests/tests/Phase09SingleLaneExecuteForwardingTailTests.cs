using HybridCPU_ISE.Arch;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Phase09;

public sealed class Phase09SingleLaneExecuteForwardingTailTests
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
            WriteRegisters = new[] { (int)destRegId };
            Class = MicroOpClass.Alu;
            InstructionClass = YAKSys_Hybrid_CPU.Arch.InstructionClass.ScalarAlu;
            SerializationClass = YAKSys_Hybrid_CPU.Arch.SerializationClass.Free;
            SetClassFlexiblePlacement(SlotClass.AluClass);
        }

        public override bool Execute(ref Processor.CPU_Core core) => true;

        public override bool TryGetPrimaryWriteBackResult(out ulong value)
        {
            value = _resultValue;
            return true;
        }

        public override string GetDescription() => "Synthetic single-lane execute forwarding carrier";
    }

    [Fact]
    public void SingleLaneExecute_WhenMicroOpPathPublishesForwardEx_ThenAddsExecuteTimingMetadata()
    {
        const ushort destinationRegister = 9;
        const ulong expectedValue = 0xABCDUL;

        var core = new Processor.CPU_Core(0);
        core.InitializePipeline();

        VLIW_Instruction instruction =
            CreateScalarInstruction(InstructionsEnum.ADDI, rd: (byte)destinationRegister);
        var microOp = new ConstantWriteBackMicroOp(destinationRegister, expectedValue);

        core.TestRunExecuteStageWithDecodedInstruction(
            instruction,
            microOp,
            writesRegister: true,
            reg1Id: instruction.Reg1ID,
            reg2Id: instruction.Reg2ID,
            reg3Id: instruction.Reg3ID,
            pc: 0x8100UL);

        Processor.CPU_Core.ForwardingPath forwardEx = core.TestGetExecuteForwardingPath();
        Processor.CPU_Core.PipelineControl control = core.GetPipelineControl();

        Assert.True(forwardEx.Valid);
        Assert.Equal(destinationRegister, forwardEx.DestRegID);
        Assert.Equal(expectedValue, forwardEx.ForwardedValue);
        Assert.Equal((long)control.CycleCount + 1, forwardEx.AvailableCycle);
        Assert.Equal(Processor.CPU_Core.PipelineStage.Execute, forwardEx.SourceStage);
    }

    [Fact]
    public void SingleLaneExecute_WhenReferenceRawFallbackPublishesForwardEx_ThenLeavesTimingMetadataAtDefault()
    {
        const ushort sourceRegister1 = 2;
        const ushort sourceRegister2 = 3;
        const ushort destinationRegister = 7;

        var core = new Processor.CPU_Core(0);
        core.InitializePipeline();
        core.WriteCommittedArch(0, sourceRegister1, 9UL);
        core.WriteCommittedArch(0, sourceRegister2, 4UL);

        VLIW_Instruction instruction =
            CreateScalarInstruction(
                InstructionsEnum.Addition,
                rd: (byte)destinationRegister,
                rs1: (byte)sourceRegister1,
                rs2: (byte)sourceRegister2);

        core.TestRunReferenceRawExecuteFallbackWithDecodedInstruction(
            instruction,
            writesRegister: true,
            reg1Id: instruction.Reg1ID,
            reg2Id: instruction.Reg2ID,
            reg3Id: instruction.Reg3ID,
            pc: 0x8200UL);

        Processor.CPU_Core.ForwardingPath forwardEx = core.TestGetExecuteForwardingPath();
        Processor.CPU_Core.ExecuteStage executeStage = core.GetExecuteStage();

        Assert.True(forwardEx.Valid);
        Assert.True(executeStage.ResultReady);
        Assert.Equal(destinationRegister, forwardEx.DestRegID);
        Assert.Equal(executeStage.ResultValue, forwardEx.ForwardedValue);
        Assert.Equal(0L, forwardEx.AvailableCycle);
        Assert.Equal(Processor.CPU_Core.PipelineStage.None, forwardEx.SourceStage);
    }

    [Fact]
    public void SingleLaneExecute_WhenRawFallbackRunsOnNonZeroActiveVt_ThenReadsOperandsFromThatThread()
    {
        const int virtualThreadId = 3;
        const ushort sourceRegister1 = 2;
        const ushort sourceRegister2 = 3;
        const ushort destinationRegister = 8;

        var core = new Processor.CPU_Core(0);
        core.InitializePipeline();
        core.ActiveVirtualThreadId = virtualThreadId;
        core.WriteVirtualThreadPipelineState(virtualThreadId, PipelineState.Task);

        core.WriteCommittedArch(0, sourceRegister1, 1UL);
        core.WriteCommittedArch(0, sourceRegister2, 2UL);
        core.WriteCommittedArch(virtualThreadId, sourceRegister1, 9UL);
        core.WriteCommittedArch(virtualThreadId, sourceRegister2, 4UL);

        VLIW_Instruction instruction =
            CreateScalarInstruction(
                InstructionsEnum.Addition,
                rd: (byte)destinationRegister,
                rs1: (byte)sourceRegister1,
                rs2: (byte)sourceRegister2,
                virtualThreadId: (byte)virtualThreadId);

        core.TestRunReferenceRawExecuteFallbackWithDecodedInstruction(
            instruction,
            writesRegister: true,
            reg1Id: instruction.Reg1ID,
            reg2Id: instruction.Reg2ID,
            reg3Id: instruction.Reg3ID,
            pc: 0x8300UL);

        Processor.CPU_Core.ExecuteStage executeStage = core.GetExecuteStage();

        Assert.True(executeStage.ResultReady);
        Assert.Equal(13UL, executeStage.ResultValue);
        Assert.NotEqual(3UL, executeStage.ResultValue);
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
            Immediate = immediate,
            StreamLength = 0,
            Stride = 0,
            VirtualThreadId = virtualThreadId
        };
    }
}

