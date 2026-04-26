using System;
using HybridCPU_ISE.Arch;

using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core.Registers;

namespace HybridCPU_ISE.Tests;

public sealed class Phase03LegacyMoveCompilerPublicationTests
{
    [Fact]
    public void MoveNum_ArchRegisterOverload_PublishesCanonicalSingleRegisterTriplet()
    {
        _ = new Processor(ProcessorMode.Compiler);
        Processor.Compiler.ResetInstructionBuffer();

        Processor.CPU_Cores[0].Move_Num(ArchRegId.Create(6), 0x1234UL);

        Assert.Equal(1, Processor.Compiler.InstructionCount);
        var recordedInstructions = Processor.Compiler.GetRecordedInstructions();
        Assert.Equal(1, recordedInstructions.Length);
        Assert.Equal((uint)Processor.CPU_Core.InstructionsEnum.Move, recordedInstructions[0].OpCode);
        Assert.True(VLIW_Instruction.TryUnpackArchRegs(
            recordedInstructions[0].DestSrc1Pointer,
            out byte rd,
            out byte rs1,
            out byte rs2));
        Assert.Equal((byte)6, rd);
        Assert.Equal(VLIW_Instruction.NoArchReg, rs1);
        Assert.Equal(VLIW_Instruction.NoArchReg, rs2);
    }

    [Fact]
    public void Move_IntRegisterOverload_PublishesCanonicalSourceDestinationTriplet()
    {
        _ = new Processor(ProcessorMode.Compiler);
        Processor.Compiler.ResetInstructionBuffer();

        Processor.CPU_Cores[0].Move(
            new Processor.CPU_Core.IntRegister(5, 0),
            new Processor.CPU_Core.IntRegister(7, 0));

        Assert.Equal(1, Processor.Compiler.InstructionCount);
        var recordedInstructions = Processor.Compiler.GetRecordedInstructions();
        Assert.Equal(1, recordedInstructions.Length);
        Assert.Equal((uint)Processor.CPU_Core.InstructionsEnum.Move, recordedInstructions[0].OpCode);
        Assert.True(VLIW_Instruction.TryUnpackArchRegs(
            recordedInstructions[0].DestSrc1Pointer,
            out byte rd,
            out byte rs1,
            out byte rs2));
        Assert.Equal((byte)5, rd);
        Assert.Equal((byte)7, rs1);
        Assert.Equal(VLIW_Instruction.NoArchReg, rs2);
    }

    [Fact]
    public void Nope_PublishesSingleRecordedInstructionThroughCompilerEmissionSeam()
    {
        _ = new Processor(ProcessorMode.Compiler);
        Processor.Compiler.ResetInstructionBuffer();

        byte result = Processor.CPU_Cores[0].Nope();

        Assert.Equal((byte)Processor.CPU_Core.InstructionsEnum.Nope, result);
        Assert.Equal(1, Processor.Compiler.InstructionCount);
        Assert.Equal((uint)Processor.CPU_Core.InstructionsEnum.Nope, Processor.Compiler.GetRecordedInstructions()[0].OpCode);
    }

    [Fact]
    public void Store_ArchRegisterOverload_PublishesRetainedRegisterToMemoryMoveShape()
    {
        _ = new Processor(ProcessorMode.Compiler);
        Processor.Compiler.ResetInstructionBuffer();

        Processor.CPU_Cores[0].Store(ArchRegId.Create(9), 0x4400UL);

        ReadOnlySpan<VLIW_Instruction> recordedInstructions = Processor.Compiler.GetRecordedInstructions();
        Assert.Equal(1, recordedInstructions.Length);
        Assert.Equal((uint)Processor.CPU_Core.InstructionsEnum.Move, recordedInstructions[0].OpCode);
        Assert.Equal((byte)2, recordedInstructions[0].DataType);
        Assert.Equal(0x4400UL, recordedInstructions[0].Src2Pointer);
        Assert.True(VLIW_Instruction.TryUnpackArchRegs(
            recordedInstructions[0].DestSrc1Pointer,
            out byte rd,
            out byte rs1,
            out byte rs2));
        Assert.Equal((byte)9, rd);
        Assert.Equal(VLIW_Instruction.NoArchReg, rs1);
        Assert.Equal(VLIW_Instruction.NoArchReg, rs2);
    }

    [Fact]
    public void Load_ArchRegisterOverload_PublishesRetainedMemoryToRegisterMoveShape()
    {
        _ = new Processor(ProcessorMode.Compiler);
        Processor.Compiler.ResetInstructionBuffer();

        Processor.CPU_Cores[0].Load(0x5500UL, ArchRegId.Create(11));

        ReadOnlySpan<VLIW_Instruction> recordedInstructions = Processor.Compiler.GetRecordedInstructions();
        Assert.Equal(1, recordedInstructions.Length);
        Assert.Equal((uint)Processor.CPU_Core.InstructionsEnum.Move, recordedInstructions[0].OpCode);
        Assert.Equal((byte)3, recordedInstructions[0].DataType);
        Assert.Equal(0x5500UL, recordedInstructions[0].Src2Pointer);
        Assert.True(VLIW_Instruction.TryUnpackArchRegs(
            recordedInstructions[0].DestSrc1Pointer,
            out byte rd,
            out byte rs1,
            out byte rs2));
        Assert.Equal((byte)11, rd);
        Assert.Equal(VLIW_Instruction.NoArchReg, rs1);
        Assert.Equal(VLIW_Instruction.NoArchReg, rs2);
    }
}

