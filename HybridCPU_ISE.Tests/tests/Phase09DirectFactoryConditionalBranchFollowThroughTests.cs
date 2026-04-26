using HybridCPU_ISE.Arch;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Phase09;

public sealed class DirectFactoryConditionalBranchFollowThroughTests
{
    [Fact]
    public void DirectFactoryBeqWithoutRawTargetPointer_OnActiveNonZeroVt_RetiresThroughWriteBackOwnedRedirect()
    {
        const int vtId = 2;
        const ulong startPc = 0x2500;
        const ulong untouchedVt0Pc = 0x1100;
        const ushort branchImmediate = 0x0040;
        const ulong targetPc = startPc + branchImmediate;
        const ushort rs1 = 3;
        const ushort rs2 = 4;
        const ulong compareValue = 0x5566_7788_99AA_BBCCUL;

        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(startPc, activeVtId: vtId);
        core.WriteCommittedPc(0, untouchedVt0Pc);
        core.WriteCommittedPc(vtId, startPc);
        core.WriteCommittedArch(vtId, rs1, compareValue);
        core.WriteCommittedArch(vtId, rs2, compareValue);

        VLIW_Instruction instruction =
            CreateControlInstruction(InstructionsEnum.BEQ, rs1: (byte)rs1, rs2: (byte)rs2, immediate: branchImmediate);
        BranchMicroOp microOp = CreateDirectFactoryConditionalBranchMicroOp(InstructionsEnum.BEQ, instruction, vtId);

        core.TestRunExecuteStageWithDecodedInstruction(
            instruction,
            microOp,
            isBranchOp: true,
            reg1Id: instruction.Reg1ID,
            reg2Id: instruction.Reg2ID,
            reg3Id: instruction.Reg3ID,
            pc: startPc);

        var executeStatus = core.TestReadExecuteStageStatus();
        Assert.True(executeStatus.Valid);
        Assert.True(executeStatus.ResultReady);
        Assert.Equal(startPc, core.ReadActiveLivePc());
        Assert.Equal(startPc, core.ReadCommittedPc(vtId));
        Assert.Equal(untouchedVt0Pc, core.ReadCommittedPc(0));

        core.TestRunMemoryAndWriteBackStagesFromCurrentExecuteState();

        Assert.Equal(targetPc, core.ReadActiveLivePc());
        Assert.Equal(targetPc, core.ReadCommittedPc(vtId));
        Assert.Equal(untouchedVt0Pc, core.ReadCommittedPc(0));
        Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(vtId));
        Assert.True(microOp.ConditionMet);
        Assert.Equal((ushort)rs1, microOp.Reg1ID);
        Assert.Equal((ushort)rs2, microOp.Reg2ID);
        Assert.Equal(InstructionClass.ControlFlow, microOp.InstructionClass);
        Assert.Equal(SerializationClass.Free, microOp.SerializationClass);

        var control = core.GetPipelineControl();
        Assert.Equal(1UL, control.InstructionsRetired);
        Assert.Equal(1UL, control.ScalarLanesRetired);
        Assert.Equal(0UL, control.NonScalarLanesRetired);
        Assert.Equal(0UL, control.Lane7ConditionalBranchExecuteCompletionCount);
        Assert.Equal(0UL, control.Lane7ConditionalBranchRedirectCount);
    }

    [Fact]
    public void DirectFactoryBneNotTaken_OnActiveNonZeroVt_RetiresWithoutPcWrite()
    {
        const int vtId = 1;
        const ulong startPc = 0x2A00;
        const ulong untouchedVt0Pc = 0x1800;
        const ushort branchImmediate = 0x0020;
        const ushort rs1 = 6;
        const ushort rs2 = 7;
        const ulong compareValue = 0x9988_7766_5544_3322UL;

        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(startPc, activeVtId: vtId);
        core.WriteCommittedPc(0, untouchedVt0Pc);
        core.WriteCommittedPc(vtId, startPc);
        core.WriteCommittedArch(vtId, rs1, compareValue);
        core.WriteCommittedArch(vtId, rs2, compareValue);

        VLIW_Instruction instruction =
            CreateControlInstruction(InstructionsEnum.BNE, rs1: (byte)rs1, rs2: (byte)rs2, immediate: branchImmediate);
        BranchMicroOp microOp = CreateDirectFactoryConditionalBranchMicroOp(InstructionsEnum.BNE, instruction, vtId);

        core.TestRunExecuteStageWithDecodedInstruction(
            instruction,
            microOp,
            isBranchOp: true,
            reg1Id: instruction.Reg1ID,
            reg2Id: instruction.Reg2ID,
            reg3Id: instruction.Reg3ID,
            pc: startPc);

        var executeStatus = core.TestReadExecuteStageStatus();
        Assert.True(executeStatus.Valid);
        Assert.True(executeStatus.ResultReady);
        Assert.Equal(startPc, core.ReadActiveLivePc());
        Assert.Equal(startPc, core.ReadCommittedPc(vtId));

        core.TestRunMemoryAndWriteBackStagesFromCurrentExecuteState();

        Assert.Equal(startPc, core.ReadActiveLivePc());
        Assert.Equal(startPc, core.ReadCommittedPc(vtId));
        Assert.Equal(untouchedVt0Pc, core.ReadCommittedPc(0));
        Assert.False(microOp.ConditionMet);

        var control = core.GetPipelineControl();
        Assert.Equal(1UL, control.InstructionsRetired);
        Assert.Equal(1UL, control.ScalarLanesRetired);
        Assert.Equal(0UL, control.NonScalarLanesRetired);
        Assert.Equal(0UL, control.Lane7ConditionalBranchExecuteCompletionCount);
        Assert.Equal(0UL, control.Lane7ConditionalBranchRedirectCount);
    }

    private static BranchMicroOp CreateDirectFactoryConditionalBranchMicroOp(
        InstructionsEnum opcode,
        VLIW_Instruction instruction,
        int vtId)
    {
        var context = new DecoderContext
        {
            OpCode = (uint)opcode,
            Immediate = instruction.Immediate,
            HasImmediate = true,
            PackedRegisterTriplet = instruction.DestSrc1Pointer,
            HasPackedRegisterTriplet = true,
            Reg1ID = instruction.Reg1ID,
            Reg2ID = instruction.Reg2ID,
            Reg3ID = instruction.Reg3ID,
            AuxData = instruction.Src2Pointer,
            PredicateMask = instruction.PredicateMask
        };

        BranchMicroOp microOp =
            Assert.IsType<BranchMicroOp>(InstructionRegistry.CreateMicroOp((uint)opcode, context));
        microOp.OwnerThreadId = vtId;
        microOp.VirtualThreadId = vtId;
        microOp.OwnerContextId = vtId;
        microOp.RefreshAdmissionMetadata();
        return microOp;
    }

    private static VLIW_Instruction CreateControlInstruction(
        InstructionsEnum opcode,
        byte rd = 0,
        byte rs1 = 0,
        byte rs2 = 0,
        ulong targetPc = 0,
        ushort immediate = 0)
    {
        return new VLIW_Instruction
        {
            OpCode = (uint)opcode,
            DataTypeValue = DataTypeEnum.INT32,
            PredicateMask = 0xFF,
            DestSrc1Pointer = VLIW_Instruction.PackArchRegs(rd, rs1, rs2),
            Src2Pointer = targetPc,
            Immediate = immediate,
            StreamLength = 0,
            Stride = 0,
            VirtualThreadId = 0
        };
    }
}
