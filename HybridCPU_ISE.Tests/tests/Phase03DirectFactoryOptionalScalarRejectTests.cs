using HybridCPU_ISE.Arch;
using System;
using Xunit;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests;

public sealed class Phase03DirectFactoryOptionalScalarRejectTests
{
    [Theory]
    [InlineData(45u)]
    [InlineData(52u)]
    public void DirectFactory_WhenOptionalScalarRawOpcodeHasNoAuthoritativeCarrier_ThenRejectsBeforeScalarPublication(
        uint rawOpcode)
    {
        DecoderContext context = CreateScalarContext(rawOpcode, rd: 1, rs1: 2, rs2: 3);

        Assert.False(InstructionRegistry.IsRegistered(rawOpcode));

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => InstructionRegistry.CreateMicroOp(rawOpcode, context));

        Assert.Contains("Unsupported instruction opcode", exception.Message, StringComparison.Ordinal);
        Assert.Contains($"0x{rawOpcode:X}", exception.Message, StringComparison.Ordinal);
    }

    private static DecoderContext CreateScalarContext(
        uint rawOpcode,
        byte rd,
        byte rs1,
        byte rs2)
    {
        var instruction = new VLIW_Instruction
        {
            OpCode = rawOpcode,
            DataTypeValue = DataTypeEnum.INT32,
            PredicateMask = 0xFF,
            DestSrc1Pointer = VLIW_Instruction.PackArchRegs(rd, rs1, rs2),
            StreamLength = 1,
            Stride = 0
        };

        return new DecoderContext
        {
            OpCode = rawOpcode,
            Reg1ID = rd,
            Reg2ID = rs1,
            Reg3ID = rs2
        };
    }
}

