using System;
using HybridCPU_ISE.Arch;
using Xunit;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Decoder;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests;

public sealed class VliwBundleAuditClosureTests
{
    [Fact]
    public void DataTypeSetter_UpdatesOnlyDataTypeByte()
    {
        var instruction = new VLIW_Instruction();
        instruction.Reserved = 0xAB;
        instruction.DataType = 0x12;

        Assert.Equal(0xAB, instruction.Reserved);
        Assert.Equal(0x12, instruction.DataType);
        Assert.Equal(0xABUL, (instruction.Word0 >> 40) & 0xFFUL);
        Assert.Equal(0x12UL, (instruction.Word0 >> 32) & 0xFFUL);

        instruction.DataType = 0x34;

        Assert.Equal(0xAB, instruction.Reserved);
        Assert.Equal(0x34, instruction.DataType);
    }

    [Fact]
    public void Serialization_UsesCanonicalLittleEndianWords()
    {
        var instruction = new VLIW_Instruction
        {
            Word0 = 0x0123006789ABCDEFUL,
            Word1 = 0x1020304050607080UL,
            Word2 = 0xFFEEDDCCBBAA9988UL,
            Word3 = 0x0000000089ABCDEFUL
        };
        Span<byte> bytes = stackalloc byte[32];

        Assert.True(instruction.TryWriteBytes(bytes));

        Assert.Equal(
            new byte[] { 0xEF, 0xCD, 0xAB, 0x89, 0x67, 0x00, 0x23, 0x01 },
            bytes.Slice(0, 8).ToArray());
        Assert.Equal(
            new byte[] { 0x80, 0x70, 0x60, 0x50, 0x40, 0x30, 0x20, 0x10 },
            bytes.Slice(8, 8).ToArray());

        var roundTrip = new VLIW_Instruction();
        Assert.True(roundTrip.TryReadBytes(bytes));
        Assert.Equal(instruction.Word0, roundTrip.Word0);
        Assert.Equal(instruction.Word1, roundTrip.Word1);
        Assert.Equal(instruction.Word2, roundTrip.Word2);
        Assert.Equal(instruction.Word3, roundTrip.Word3);
    }

    [Fact]
    public void TryReadBytes_RejectsReservedWord0ProductionIngress()
    {
        var instruction = new VLIW_Instruction
        {
            OpCode = (uint)InstructionsEnum.VADD,
            DataTypeValue = DataTypeEnum.INT32,
            StreamLength = 4
        };
        instruction.Reserved = 1;
        byte[] bytes = new byte[32];
        Assert.True(instruction.TryWriteBytes(bytes));

        var decoded = new VLIW_Instruction();
        InvalidOpcodeException ex = Assert.Throws<InvalidOpcodeException>(
            () => decoded.TryReadBytes(bytes));

        Assert.Contains("word0[47:40]", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DecodeInstructionBundle_RejectsNonCanonicalEmptySlotPayload()
    {
        var bundle = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        bundle[0].Word1 = 1;

        InvalidOpcodeException ex = Assert.Throws<InvalidOpcodeException>(
            () => new VliwDecoderV4().DecodeInstructionBundle(
                bundle,
                bundleAddress: 0x1000));

        Assert.Contains("empty/NOP", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("all-zero", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void VliwDecoderV4_RejectsReservedWord0OnOccupiedSlot()
    {
        var instruction = new VLIW_Instruction
        {
            OpCode = (uint)InstructionsEnum.VADD,
            DataTypeValue = DataTypeEnum.INT32,
            StreamLength = 4
        };
        instruction.Reserved = 1;

        InvalidOpcodeException ex = Assert.Throws<InvalidOpcodeException>(
            () => new VliwDecoderV4().Decode(in instruction, slotIndex: 2));

        Assert.Contains("word0[47:40]", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void VliwDecoderV4_RejectsAcquireReleaseOnNonAtomicOpcode()
    {
        var instruction = new VLIW_Instruction
        {
            OpCode = (uint)InstructionsEnum.VADD,
            DataTypeValue = DataTypeEnum.INT32,
            StreamLength = 4,
            Acquire = true,
            Release = true
        };

        InvalidOpcodeException ex = Assert.Throws<InvalidOpcodeException>(
            () => new VliwDecoderV4().Decode(in instruction, slotIndex: 0));

        Assert.Contains("Acquire/Release", ex.Message, StringComparison.Ordinal);
        Assert.Contains("atomic", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void VliwDecoderV4_PreservesAcquireReleaseOnAtomicOpcode()
    {
        var instruction = new VLIW_Instruction
        {
            OpCode = (uint)InstructionsEnum.AMOADD_W,
            Word1 = VLIW_Instruction.PackArchRegs(5, 6, 7),
            Acquire = true,
            Release = true
        };

        var ir = new VliwDecoderV4().Decode(in instruction, slotIndex: 0);

        Assert.Equal((ushort)InstructionsEnum.AMOADD_W, ir.CanonicalOpcode.Value);
        Assert.True(ir.AcquireOrdering);
        Assert.True(ir.ReleaseOrdering);
    }

    [Fact]
    public void VliwDecoderV4_RejectsAddressingAndReductionFlagsOnUnsupportedOpcodes()
    {
        var indexed = new VLIW_Instruction
        {
            OpCode = (uint)InstructionsEnum.ADD,
            DataTypeValue = DataTypeEnum.INT32,
            Word1 = VLIW_Instruction.PackArchRegs(1, 2, 3),
            StreamLength = 1,
            Indexed = true
        };
        Assert.Throws<InvalidOpcodeException>(
            () => new VliwDecoderV4().Decode(in indexed, slotIndex: 0));

        var reduction = new VLIW_Instruction
        {
            OpCode = (uint)InstructionsEnum.VADD,
            DataTypeValue = DataTypeEnum.INT32,
            StreamLength = 4,
            Reduction = true
        };
        Assert.Throws<InvalidOpcodeException>(
            () => new VliwDecoderV4().Decode(in reduction, slotIndex: 0));
    }

    [Fact]
    public void InstructionEncoder_RejectsSilentTruncation()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            InstructionEncoder.EncodeVector1D(
                (uint)InstructionsEnum.VADD,
                DataTypeEnum.INT32,
                0x1000,
                0x2000,
                (ulong)uint.MaxValue + 1UL));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            InstructionEncoder.EncodeVector2D(
                (uint)InstructionsEnum.VLOAD,
                DataTypeEnum.INT32,
                0x1000,
                0x2000,
                16,
                colStride: 4,
                rowStride: 64,
                rowLength: (uint)ushort.MaxValue + 1U));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            InstructionEncoder.EncodeSystem(
                (uint)InstructionsEnum.STREAM_SETUP,
                reg1: 1,
                param1: 0,
                param2: (ulong)uint.MaxValue + 1UL));
    }
}
