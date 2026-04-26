using HybridCPU_ISE.Arch;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Phase09;

public sealed class DirectFactoryUnconditionalBranchFollowThroughTests
{
    [Fact]
    public void DirectFactoryJalWithoutRawTargetPointer_OnActiveNonZeroVt_RetiresLinkAndRedirectThroughWriteBack()
    {
        const int vtId = 2;
        const ulong startPc = 0x3200;
        const ulong untouchedVt0Pc = 0x1100;
        const ushort immediate = 0x0040;
        const ushort rd = 5;
        ulong targetPc = startPc + immediate;
        ulong linkValue = startPc + 4;
        const ulong originalDestinationValue = 0xDEAD_BEEF_CAFE_BABEUL;

        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(startPc, activeVtId: vtId);
        core.WriteCommittedPc(0, untouchedVt0Pc);
        core.WriteCommittedPc(vtId, startPc);
        core.WriteCommittedArch(vtId, rd, originalDestinationValue);

        VLIW_Instruction instruction =
            CreateControlInstruction(InstructionsEnum.JAL, rd: (byte)rd, immediate: immediate);
        BranchMicroOp microOp = CreateDirectFactoryUnconditionalBranchMicroOp(InstructionsEnum.JAL, instruction, vtId);

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
        Assert.Equal(originalDestinationValue, core.ReadArch(vtId, rd));

        core.TestRunMemoryAndWriteBackStagesFromCurrentExecuteState();

        Assert.Equal(targetPc, core.ReadActiveLivePc());
        Assert.Equal(targetPc, core.ReadCommittedPc(vtId));
        Assert.Equal(untouchedVt0Pc, core.ReadCommittedPc(0));
        Assert.Equal(linkValue, core.ReadArch(vtId, rd));
        Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(vtId));
        Assert.Equal((ushort)rd, microOp.DestRegID);
        Assert.True(microOp.WritesRegister);
        Assert.Equal(new[] { (int)rd }, microOp.AdmissionMetadata.WriteRegisters);
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
    public void DirectFactoryJalrWithoutRawTargetPointer_OnActiveNonZeroVt_RetiresCanonicalBaseAndLinkThroughWriteBack()
    {
        const int vtId = 3;
        const ulong startPc = 0x2800;
        const ulong untouchedVt0Pc = 0x1000;
        const ushort immediate = 0x0024;
        const ushort rd = 7;
        const ushort rs1 = 6;
        const ulong baseValue = 0x5003;
        ulong targetPc = (baseValue + immediate) & ~1UL;
        ulong linkValue = startPc + 4;
        const ulong originalDestinationValue = 0x9999_8888_7777_6666UL;

        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(startPc, activeVtId: vtId);
        core.WriteCommittedPc(0, untouchedVt0Pc);
        core.WriteCommittedPc(vtId, startPc);
        core.WriteCommittedArch(vtId, rs1, baseValue);
        core.WriteCommittedArch(vtId, rd, originalDestinationValue);

        VLIW_Instruction instruction =
            CreateControlInstruction(InstructionsEnum.JALR, rd: (byte)rd, rs1: (byte)rs1, immediate: immediate);
        BranchMicroOp microOp = CreateDirectFactoryUnconditionalBranchMicroOp(InstructionsEnum.JALR, instruction, vtId);

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
        Assert.Equal(originalDestinationValue, core.ReadArch(vtId, rd));

        core.TestRunMemoryAndWriteBackStagesFromCurrentExecuteState();

        Assert.Equal(targetPc, core.ReadActiveLivePc());
        Assert.Equal(targetPc, core.ReadCommittedPc(vtId));
        Assert.Equal(untouchedVt0Pc, core.ReadCommittedPc(0));
        Assert.Equal(linkValue, core.ReadArch(vtId, rd));
        Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(vtId));
        Assert.Equal((ushort)rd, microOp.DestRegID);
        Assert.Equal((ushort)rs1, microOp.Reg1ID);
        Assert.Equal(new[] { (int)rs1 }, microOp.AdmissionMetadata.ReadRegisters);
        Assert.Equal(new[] { (int)rd }, microOp.AdmissionMetadata.WriteRegisters);
        Assert.Equal(InstructionClass.ControlFlow, microOp.InstructionClass);
        Assert.Equal(SerializationClass.Free, microOp.SerializationClass);

        var control = core.GetPipelineControl();
        Assert.Equal(1UL, control.InstructionsRetired);
        Assert.Equal(1UL, control.ScalarLanesRetired);
        Assert.Equal(0UL, control.NonScalarLanesRetired);
        Assert.Equal(0UL, control.Lane7ConditionalBranchExecuteCompletionCount);
        Assert.Equal(0UL, control.Lane7ConditionalBranchRedirectCount);
    }

    private static BranchMicroOp CreateDirectFactoryUnconditionalBranchMicroOp(
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
