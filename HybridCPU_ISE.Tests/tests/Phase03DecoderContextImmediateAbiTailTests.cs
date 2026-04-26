using Xunit;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using System.Reflection;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;
using HybridCPU_ISE.Arch;

namespace HybridCPU_ISE.Tests;

public sealed class Phase03DecoderContextImmediateAbiTailTests
{
    [Fact]
    public void DirectFactoryScalarImmediate_PrefersDecoderContextImmediateOverRawInstructionPayload()
    {
        var rawInstruction = new VLIW_Instruction
        {
            OpCode = (uint)InstructionsEnum.ADDI,
            Immediate = 0x1234,
            DestSrc1Pointer = VLIW_Instruction.PackArchRegs(
                7,
                5,
                VLIW_Instruction.NoArchReg)
        };

        var context = new DecoderContext
        {
            OpCode = (uint)InstructionsEnum.ADDI,
            Immediate = 0xFFF0,
            HasImmediate = true,
            Reg1ID = 7,
            Reg2ID = 5,
            Reg3ID = VLIW_Instruction.NoReg
        };

        ScalarALUMicroOp microOp = Assert.IsType<ScalarALUMicroOp>(
            InstructionRegistry.CreateMicroOp((uint)InstructionsEnum.ADDI, context));

        Assert.Equal(unchecked((ulong)(long)(short)0xFFF0), microOp.Immediate);
    }

    [Theory]
    [InlineData(InstructionsEnum.ADDI)]
    [InlineData(InstructionsEnum.AUIPC)]
    public void DirectFactoryScalarImmediateCreation_WithoutDecoderContextImmediate_FailsClosed(
        InstructionsEnum opcode)
    {
        var rawInstruction = new VLIW_Instruction
        {
            OpCode = (uint)opcode,
            Immediate = 0x00AA,
        };

        var context = new DecoderContext
        {
            OpCode = (uint)opcode,
            Reg1ID = 7,
            Reg2ID = 5
        };

        InvalidOperationException exception = Assert.ThrowsAny<InvalidOperationException>(
            () => InstructionRegistry.CreateMicroOp((uint)opcode, context));

        Assert.Contains("projected DecoderContext immediate handoff", exception.Message, StringComparison.Ordinal);
        Assert.Contains("raw VLIW_Instruction.Immediate fallback is retired", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DirectFactoryCsrMicroOp_PrefersDecoderContextImmediateOverRawInstructionPayload()
    {
        var rawInstruction = new VLIW_Instruction
        {
            OpCode = (uint)InstructionsEnum.CSRRW,
            Immediate = 0x123,
        };

        var context = new DecoderContext
        {
            OpCode = (uint)InstructionsEnum.CSRRW,
            Immediate = CsrAddresses.Mstatus,
            HasImmediate = true,
            Reg1ID = 7,
            Reg2ID = 5
        };

        CsrReadWriteMicroOp microOp = Assert.IsType<CsrReadWriteMicroOp>(
            InstructionRegistry.CreateMicroOp((uint)InstructionsEnum.CSRRW, context));

        Assert.Equal(CsrAddresses.Mstatus, microOp.CSRAddress);
    }

    [Theory]
    [InlineData(InstructionsEnum.CSRRW)]
    [InlineData(InstructionsEnum.CSRRS)]
    [InlineData(InstructionsEnum.CSRRC)]
    [InlineData(InstructionsEnum.CSRRWI)]
    [InlineData(InstructionsEnum.CSRRSI)]
    [InlineData(InstructionsEnum.CSRRCI)]
    public void DirectFactoryCsrCreation_WithoutDecoderContextImmediate_FailsClosed(
        InstructionsEnum opcode)
    {
        var rawInstruction = new VLIW_Instruction
        {
            OpCode = (uint)opcode,
            Immediate = CsrAddresses.Mstatus,
        };

        var context = new DecoderContext
        {
            OpCode = (uint)opcode,
            Reg1ID = 7,
            Reg2ID = 5
        };

        InvalidOperationException exception = Assert.ThrowsAny<InvalidOperationException>(
            () => InstructionRegistry.CreateMicroOp((uint)opcode, context));

        Assert.Contains("projected DecoderContext immediate handoff", exception.Message, StringComparison.Ordinal);
        Assert.Contains("raw VLIW_Instruction.Immediate fallback is retired", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DirectFactoryBranchProjection_PrefersDecoderContextPackedRegistersOverRawInstructionPayload()
    {
        var rawInstruction = new VLIW_Instruction
        {
            OpCode = (uint)InstructionsEnum.JALR,
            DestSrc1Pointer = VLIW_Instruction.PackArchRegs(1, 2, 0),
        };

        var context = new DecoderContext
        {
            OpCode = (uint)InstructionsEnum.JALR,
            Immediate = 0x0010,
            HasImmediate = true,
            PackedRegisterTriplet = VLIW_Instruction.PackArchRegs(7, 5, 0),
            HasPackedRegisterTriplet = true,
            Reg1ID = 1,
            Reg2ID = 2,
            Reg3ID = VLIW_Instruction.NoReg
        };

        BranchMicroOp microOp = Assert.IsType<BranchMicroOp>(
            InstructionRegistry.CreateMicroOp((uint)InstructionsEnum.JALR, context));

        Assert.Equal((ushort)7, microOp.DestRegID);
        Assert.Equal((ushort)5, microOp.Reg1ID);
        Assert.Equal(VLIW_Instruction.NoReg, microOp.Reg2ID);
    }

    [Fact]
    public void DirectFactoryBranchProjection_WithoutPackedTriplet_PrefersDecoderContextRegisterFieldsOverRawInstructionPayload()
    {
        var rawInstruction = new VLIW_Instruction
        {
            OpCode = (uint)InstructionsEnum.JALR,
            DestSrc1Pointer = VLIW_Instruction.PackArchRegs(1, 2, 0),
        };

        var context = new DecoderContext
        {
            OpCode = (uint)InstructionsEnum.JALR,
            Immediate = 0x0010,
            HasImmediate = true,
            Reg1ID = 7,
            Reg2ID = 5,
            Reg3ID = VLIW_Instruction.NoReg
        };

        BranchMicroOp microOp = Assert.IsType<BranchMicroOp>(
            InstructionRegistry.CreateMicroOp((uint)InstructionsEnum.JALR, context));

        Assert.Equal((ushort)7, microOp.DestRegID);
        Assert.Equal((ushort)5, microOp.Reg1ID);
        Assert.Equal(VLIW_Instruction.NoReg, microOp.Reg2ID);
    }

    [Fact]
    public void DirectFactoryConditionalBranchResolution_PrefersDecoderContextImmediateOverRawInstructionPayload()
    {
        var rawInstruction = new VLIW_Instruction
        {
            OpCode = (uint)InstructionsEnum.BEQ,
            Immediate = 0x0010,
            DestSrc1Pointer = VLIW_Instruction.PackArchRegs(0, 3, 4),
        };

        var context = new DecoderContext
        {
            OpCode = (uint)InstructionsEnum.BEQ,
            Immediate = 0x0040,
            HasImmediate = true,
            PackedRegisterTriplet = rawInstruction.DestSrc1Pointer,
            HasPackedRegisterTriplet = true,
            Reg1ID = 0,
            Reg2ID = 3,
            Reg3ID = 4
        };

        BranchMicroOp microOp = Assert.IsType<BranchMicroOp>(
            InstructionRegistry.CreateMicroOp((uint)InstructionsEnum.BEQ, context));

        ulong resolvedTarget = InvokeConditionalTargetResolution(microOp, 0x2000);
        Assert.Equal(0x2040UL, resolvedTarget);
    }

    [Fact]
    public void DirectFactoryUnconditionalBranchResolution_PrefersDecoderContextImmediateOverRawInstructionPayload()
    {
        var rawInstruction = new VLIW_Instruction
        {
            OpCode = (uint)InstructionsEnum.JALR,
            Immediate = 0x0008,
            DestSrc1Pointer = VLIW_Instruction.PackArchRegs(7, 5, 0),
        };

        var context = new DecoderContext
        {
            OpCode = (uint)InstructionsEnum.JALR,
            Immediate = 0x0024,
            HasImmediate = true,
            PackedRegisterTriplet = rawInstruction.DestSrc1Pointer,
            HasPackedRegisterTriplet = true,
            Reg1ID = 7,
            Reg2ID = 5,
            Reg3ID = VLIW_Instruction.NoReg
        };

        BranchMicroOp microOp = Assert.IsType<BranchMicroOp>(
            InstructionRegistry.CreateMicroOp((uint)InstructionsEnum.JALR, context));

        ulong resolvedTarget = InvokeUnconditionalTargetResolution(microOp, 0x2800, 0x5003);
        Assert.Equal((0x5003UL + 0x24UL) & ~1UL, resolvedTarget);
    }

    [Theory]
    [InlineData(InstructionsEnum.BEQ, true)]
    [InlineData(InstructionsEnum.JALR, false)]
    public void DirectFactoryBranchCreation_WithoutDecoderContextImmediate_FailsClosed(
        InstructionsEnum opcode,
        bool isConditional)
    {
        var rawInstruction = new VLIW_Instruction
        {
            OpCode = (uint)opcode,
            Immediate = 0x0040,
            DestSrc1Pointer = VLIW_Instruction.PackArchRegs(7, 5, 4),
        };

        var context = new DecoderContext
        {
            OpCode = (uint)opcode,
            PackedRegisterTriplet = rawInstruction.DestSrc1Pointer,
            HasPackedRegisterTriplet = true,
            Reg1ID = 7,
            Reg2ID = 5,
            Reg3ID = isConditional ? (ushort)4 : VLIW_Instruction.NoReg
        };

        InvalidOperationException exception = Assert.ThrowsAny<InvalidOperationException>(
            () => InstructionRegistry.CreateMicroOp((uint)opcode, context));

        Assert.Contains("projected DecoderContext immediate handoff", exception.Message);
        Assert.Contains("fallback is retired", exception.Message);
    }

    [Theory]
    [InlineData(InstructionsEnum.BEQ)]
    [InlineData(InstructionsEnum.JALR)]
    public void BranchMicroOp_TargetResolutionWithoutProjectedRelativeTarget_FailsClosed(
        InstructionsEnum opcode)
    {
        var microOp = new BranchMicroOp
        {
            OpCode = (uint)opcode,
            IsConditional = opcode == InstructionsEnum.BEQ
        };

        InvalidOperationException exception = opcode == InstructionsEnum.BEQ
            ? Assert.ThrowsAny<InvalidOperationException>(() => InvokeConditionalTargetResolution(microOp, 0x2000))
            : Assert.ThrowsAny<InvalidOperationException>(() => InvokeUnconditionalTargetResolution(microOp, 0x2000, 0x3000));

        Assert.Contains("projected DecoderContext immediate", exception.Message);
        Assert.Contains("fallback is retired", exception.Message);
    }

    [Fact]
    public void DirectFactoryTypedLoad_PrefersDecoderContextImmediateOverRawInstructionPayload()
    {
        var rawInstruction = new VLIW_Instruction
        {
            OpCode = (uint)InstructionsEnum.LB,
            Immediate = 0x0123,
        };

        var context = new DecoderContext
        {
            OpCode = (uint)InstructionsEnum.LB,
            Immediate = 0xFFF0,
            HasImmediate = true,
            Reg1ID = 7,
            Reg2ID = 5,
            Reg3ID = VLIW_Instruction.NoReg
        };

        LoadMicroOp microOp = Assert.IsType<LoadMicroOp>(
            InstructionRegistry.CreateMicroOp((uint)InstructionsEnum.LB, context));

        Assert.Equal(unchecked((ulong)(long)(short)0xFFF0), microOp.Address);
    }

    [Theory]
    [InlineData(InstructionsEnum.LB)]
    [InlineData(InstructionsEnum.SD)]
    public void DirectFactoryTypedMemoryCreation_WithoutProjectedAddressHandoff_FailsClosed(
        InstructionsEnum opcode)
    {
        var rawInstruction = new VLIW_Instruction
        {
            OpCode = (uint)opcode,
            Immediate = 0x0123,
        };

        var context = new DecoderContext
        {
            OpCode = (uint)opcode,
            Reg1ID = opcode == InstructionsEnum.SD
                ? VLIW_Instruction.NoReg
                : (ushort)7,
            Reg2ID = 5,
            Reg3ID = opcode == InstructionsEnum.SD
                ? (ushort)9
                : VLIW_Instruction.NoReg
        };

        InvalidOperationException exception = Assert.ThrowsAny<InvalidOperationException>(
            () => InstructionRegistry.CreateMicroOp((uint)opcode, context));

        Assert.Contains("projected DecoderContext memory-address handoff", exception.Message, StringComparison.Ordinal);
        Assert.Contains("raw VLIW_Instruction.Immediate fallback is retired", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DirectFactoryTypedStore_PrefersDecoderContextImmediateOverRawInstructionPayload()
    {
        var rawInstruction = new VLIW_Instruction
        {
            OpCode = (uint)InstructionsEnum.SD,
            Immediate = 0x0123,
        };

        var context = new DecoderContext
        {
            OpCode = (uint)InstructionsEnum.SD,
            Immediate = 0xFFE0,
            HasImmediate = true,
            Reg1ID = VLIW_Instruction.NoReg,
            Reg2ID = 5,
            Reg3ID = 9
        };

        StoreMicroOp microOp = Assert.IsType<StoreMicroOp>(
            InstructionRegistry.CreateMicroOp((uint)InstructionsEnum.SD, context));

        Assert.Equal(unchecked((ulong)(long)(short)0xFFE0), microOp.Address);
    }

    [Fact]
    public void DirectFactoryRetainedAbsoluteLoad_PrefersDecoderContextMemoryAddressOverRawInstructionPayload()
    {
        var rawInstruction = new VLIW_Instruction
        {
            OpCode = (uint)InstructionsEnum.Load,
            Immediate = 0x0123,
        };

        var context = new DecoderContext
        {
            OpCode = (uint)InstructionsEnum.Load,
            MemoryAddress = 0,
            HasMemoryAddress = true,
            Reg1ID = 7,
            Reg2ID = VLIW_Instruction.NoReg,
            Reg3ID = VLIW_Instruction.NoReg
        };

        LoadMicroOp microOp = Assert.IsType<LoadMicroOp>(
            InstructionRegistry.CreateMicroOp((uint)InstructionsEnum.Load, context));

        Assert.Equal(0UL, microOp.Address);
    }

    [Theory]
    [InlineData(InstructionsEnum.Load)]
    [InlineData(InstructionsEnum.Store)]
    public void DirectFactoryRetainedAbsoluteMemoryCreation_WithoutProjectedAddressHandoff_FailsClosed(
        InstructionsEnum opcode)
    {
        var rawInstruction = new VLIW_Instruction
        {
            OpCode = (uint)opcode,
            Immediate = 0x0123,
        };

        var context = new DecoderContext
        {
            OpCode = (uint)opcode,
            Reg1ID = opcode == InstructionsEnum.Load ? (ushort)7 : (ushort)6,
            Reg2ID = VLIW_Instruction.NoReg,
            Reg3ID = VLIW_Instruction.NoReg
        };

        InvalidOperationException exception = Assert.ThrowsAny<InvalidOperationException>(
            () => InstructionRegistry.CreateMicroOp((uint)opcode, context));

        Assert.Contains("projected DecoderContext memory-address handoff", exception.Message, StringComparison.Ordinal);
        Assert.Contains("raw VLIW_Instruction.Immediate fallback is retired", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DirectFactoryRetainedAbsoluteStore_PrefersDecoderContextMemoryAddressOverRawInstructionPayload()
    {
        var rawInstruction = new VLIW_Instruction
        {
            OpCode = (uint)InstructionsEnum.Store,
            Immediate = 0x0123,
        };

        var context = new DecoderContext
        {
            OpCode = (uint)InstructionsEnum.Store,
            MemoryAddress = 0,
            HasMemoryAddress = true,
            Reg1ID = 6,
            Reg2ID = VLIW_Instruction.NoReg,
            Reg3ID = VLIW_Instruction.NoReg
        };

        StoreMicroOp microOp = Assert.IsType<StoreMicroOp>(
            InstructionRegistry.CreateMicroOp((uint)InstructionsEnum.Store, context));

        Assert.Equal(0UL, microOp.Address);
    }

    [Fact]
    public void DirectFactoryVsetvli_PrefersDecoderContextDataTypeOverRawInstructionPayload()
    {
        var rawInstruction = new VLIW_Instruction
        {
            OpCode = (uint)InstructionsEnum.VSETVLI,
            DataType = 0x12,
        };

        var context = new DecoderContext
        {
            OpCode = (uint)InstructionsEnum.VSETVLI,
            DataType = 0xA5,
            HasDataType = true,
            Reg2ID = 5
        };

        VConfigMicroOp microOp = Assert.IsType<VConfigMicroOp>(
            InstructionRegistry.CreateMicroOp((uint)InstructionsEnum.VSETVLI, context));

        Assert.Equal(VectorConfigOperationKind.Vsetvli, microOp.OperationKind);
        Assert.Equal((ushort)5, microOp.SrcReg1ID);
        Assert.Equal(0xA5UL, microOp.EncodedVTypeImmediate);
    }

    [Fact]
    public void DirectFactoryVsetvli_WithoutDecoderContextDataType_FailsClosed()
    {
        var rawInstruction = new VLIW_Instruction
        {
            OpCode = (uint)InstructionsEnum.VSETVLI,
            DataType = 0x12,
        };

        var context = new DecoderContext
        {
            OpCode = (uint)InstructionsEnum.VSETVLI,
            Reg2ID = 5
        };

        InvalidOperationException exception = Assert.ThrowsAny<InvalidOperationException>(
            () => InstructionRegistry.CreateMicroOp((uint)InstructionsEnum.VSETVLI, context));

        Assert.Contains("projected DecoderContext data-type handoff", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Raw VLIW_Instruction.DataType fallback is retired", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DirectFactoryVsetivli_PrefersDecoderContextImmediateAndDataTypeOverRawInstructionPayload()
    {
        var rawInstruction = new VLIW_Instruction
        {
            OpCode = (uint)InstructionsEnum.VSETIVLI,
            Immediate = 0x0012,
            DataType = 0x34,
        };

        var context = new DecoderContext
        {
            OpCode = (uint)InstructionsEnum.VSETIVLI,
            Immediate = 0x00AA,
            HasImmediate = true,
            DataType = 0xBC,
            HasDataType = true
        };

        VConfigMicroOp microOp = Assert.IsType<VConfigMicroOp>(
            InstructionRegistry.CreateMicroOp((uint)InstructionsEnum.VSETIVLI, context));

        Assert.Equal(VectorConfigOperationKind.Vsetivli, microOp.OperationKind);
        Assert.Equal(0xAAUL, microOp.EncodedAvlImmediate);
        Assert.Equal(0xBCUL, microOp.EncodedVTypeImmediate);
    }

    [Fact]
    public void DirectFactoryVsetivli_WithoutDecoderContextImmediate_FailsClosed()
    {
        var rawInstruction = new VLIW_Instruction
        {
            OpCode = (uint)InstructionsEnum.VSETIVLI,
            Immediate = 0x0012,
            DataType = 0x34,
        };

        var context = new DecoderContext
        {
            OpCode = (uint)InstructionsEnum.VSETIVLI,
            DataType = 0xBC,
            HasDataType = true
        };

        InvalidOperationException exception = Assert.ThrowsAny<InvalidOperationException>(
            () => InstructionRegistry.CreateMicroOp((uint)InstructionsEnum.VSETIVLI, context));

        Assert.Contains("projected DecoderContext immediate handoff", exception.Message, StringComparison.Ordinal);
        Assert.Contains("raw VLIW_Instruction.Immediate fallback is retired", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DirectFactoryVsetivli_WithoutDecoderContextDataType_FailsClosed()
    {
        var rawInstruction = new VLIW_Instruction
        {
            OpCode = (uint)InstructionsEnum.VSETIVLI,
            Immediate = 0x0012,
            DataType = 0x34,
        };

        var context = new DecoderContext
        {
            OpCode = (uint)InstructionsEnum.VSETIVLI,
            Immediate = 0x00AA,
            HasImmediate = true
        };

        InvalidOperationException exception = Assert.ThrowsAny<InvalidOperationException>(
            () => InstructionRegistry.CreateMicroOp((uint)InstructionsEnum.VSETIVLI, context));

        Assert.Contains("projected DecoderContext data-type handoff", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Raw VLIW_Instruction.DataType fallback is retired", exception.Message, StringComparison.Ordinal);
    }

    private static ulong InvokeConditionalTargetResolution(BranchMicroOp microOp, ulong executionPc)
    {
        MethodInfo method = typeof(BranchMicroOp).GetMethod(
            "ResolveConditionalTargetAddress",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("ResolveConditionalTargetAddress method was not found.");

        try
        {
            return Assert.IsType<ulong>(method.Invoke(microOp, new object[] { executionPc }));
        }
        catch (TargetInvocationException exception) when (exception.InnerException is InvalidOperationException invalidOperationException)
        {
            throw invalidOperationException;
        }
    }

    private static ulong InvokeUnconditionalTargetResolution(
        BranchMicroOp microOp,
        ulong executionPc,
        ulong baseValue)
    {
        MethodInfo method = typeof(BranchMicroOp).GetMethod(
            "ResolveUnconditionalTargetAddress",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("ResolveUnconditionalTargetAddress method was not found.");

        try
        {
            return Assert.IsType<ulong>(method.Invoke(microOp, new object[] { executionPc, baseValue }));
        }
        catch (TargetInvocationException exception) when (exception.InnerException is InvalidOperationException invalidOperationException)
        {
            throw invalidOperationException;
        }
    }
}

