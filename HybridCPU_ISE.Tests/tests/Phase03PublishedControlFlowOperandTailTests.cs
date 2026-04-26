using HybridCPU_ISE.Arch;
using System;
using System.Reflection;
using Xunit;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests;

public sealed class Phase03PublishedControlFlowOperandTailTests
{
    [Fact]
    public void InstructionRegistry_TryCreatePublishedControlFlowMicroOp_Jal_DropsNonCanonicalOptionalSourceFields()
    {
        BranchMicroOp branchMicroOp = CreatePublishedControlFlowMicroOp(
            new InstructionIR
            {
                CanonicalOpcode = InstructionsEnum.JAL,
                Class = InstructionClass.ControlFlow,
                SerializationClass = SerializationClass.Free,
                Rd = 1,
                Rs1 = 9,
                Rs2 = 10,
                Imm = 0x20,
            });

        Assert.False(branchMicroOp.IsConditional);
        Assert.Equal((ushort)1, branchMicroOp.DestRegID);
        Assert.Equal(VLIW_Instruction.NoReg, branchMicroOp.Reg1ID);
        Assert.Equal(VLIW_Instruction.NoReg, branchMicroOp.Reg2ID);
        Assert.Empty(branchMicroOp.AdmissionMetadata.ReadRegisters);
        Assert.Equal(new[] { 1 }, branchMicroOp.AdmissionMetadata.WriteRegisters);
    }

    [Fact]
    public void InstructionRegistry_TryCreatePublishedControlFlowMicroOp_Jalr_DropsNonCanonicalSecondSourceField()
    {
        BranchMicroOp branchMicroOp = CreatePublishedControlFlowMicroOp(
            new InstructionIR
            {
                CanonicalOpcode = InstructionsEnum.JALR,
                Class = InstructionClass.ControlFlow,
                SerializationClass = SerializationClass.Free,
                Rd = 1,
                Rs1 = 2,
                Rs2 = 9,
                Imm = 0x10,
            });

        Assert.False(branchMicroOp.IsConditional);
        Assert.Equal((ushort)1, branchMicroOp.DestRegID);
        Assert.Equal((ushort)2, branchMicroOp.Reg1ID);
        Assert.Equal(VLIW_Instruction.NoReg, branchMicroOp.Reg2ID);
        Assert.Equal(new[] { 2 }, branchMicroOp.AdmissionMetadata.ReadRegisters);
        Assert.Equal(new[] { 1 }, branchMicroOp.AdmissionMetadata.WriteRegisters);
    }

    [Fact]
    public void InstructionRegistry_TryCreatePublishedControlFlowMicroOp_Beq_DropsNonCanonicalDestinationField()
    {
        BranchMicroOp branchMicroOp = CreatePublishedControlFlowMicroOp(
            new InstructionIR
            {
                CanonicalOpcode = InstructionsEnum.BEQ,
                Class = InstructionClass.ControlFlow,
                SerializationClass = SerializationClass.Free,
                Rd = 7,
                Rs1 = 2,
                Rs2 = 3,
                Imm = 0x10,
            });

        Assert.True(branchMicroOp.IsConditional);
        Assert.Equal(VLIW_Instruction.NoReg, branchMicroOp.DestRegID);
        Assert.Equal((ushort)2, branchMicroOp.Reg1ID);
        Assert.Equal((ushort)3, branchMicroOp.Reg2ID);
        Assert.Equal(new[] { 2, 3 }, branchMicroOp.AdmissionMetadata.ReadRegisters);
        Assert.Empty(branchMicroOp.AdmissionMetadata.WriteRegisters);
    }

    private static BranchMicroOp CreatePublishedControlFlowMicroOp(InstructionIR instruction)
    {
        MethodInfo tryCreatePublishedControlFlowMicroOp = typeof(InstructionRegistry).GetMethod(
            "TryCreatePublishedControlFlowMicroOp",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("TryCreatePublishedControlFlowMicroOp method was not found.");

        object?[] args = { instruction, null };
        object? created = tryCreatePublishedControlFlowMicroOp.Invoke(null, args);

        Assert.True(Assert.IsType<bool>(created));
        return Assert.IsType<BranchMicroOp>(args[1]);
    }
}

