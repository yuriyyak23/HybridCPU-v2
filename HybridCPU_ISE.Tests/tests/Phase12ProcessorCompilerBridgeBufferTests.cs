using System;
using HybridCPU_ISE.Arch;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core.Contracts;

namespace HybridCPU_ISE.Tests.Phase12;

public sealed class Phase12ProcessorCompilerBridgeBufferTests
{
    [Fact]
    public void AddVliwInstruction_GrowsBeyondLegacy4096Limit()
    {
        const int targetInstructionCount = 5000;
        ProcessorCompilerBridge bridge = CreateHandshakenBridge();

        for (int index = 0; index < targetInstructionCount; index++)
        {
            bridge.Add_VLIW_Instruction(
                opCode: (uint)(0x1000 + index),
                dataType: (byte)(index % 8),
                predicate: (byte)(index % byte.MaxValue),
                immediate: (ushort)index,
                destSrc1: (ulong)index,
                src2: (ulong)(index + 1),
                streamLength: (ulong)(index + 2),
                stride: (ushort)(index + 3));
        }

        ReadOnlySpan<VLIW_Instruction> recordedInstructions = bridge.GetRecordedInstructions();

        Assert.Equal(targetInstructionCount, bridge.InstructionCount);
        Assert.Equal(targetInstructionCount, recordedInstructions.Length);
        Assert.Equal(0x1000U, recordedInstructions[0].OpCode);
        Assert.Equal((uint)(0x1000 + targetInstructionCount - 1), recordedInstructions[^1].OpCode);
        Assert.Equal((ushort)(targetInstructionCount - 1), recordedInstructions[^1].Immediate);
        Assert.Equal((ulong)(targetInstructionCount - 1), recordedInstructions[^1].DestSrc1Pointer);
    }

    [Fact]
    public void ResetInstructionBuffer_AfterGrowth_ClearsVisibleRecordedInstructions()
    {
        ProcessorCompilerBridge bridge = CreateHandshakenBridge();

        for (int index = 0; index < 5000; index++)
        {
            bridge.Add_VLIW_Instruction(
                opCode: (uint)(0x2000 + index),
                dataType: 1,
                predicate: 0xFF,
                immediate: (ushort)index,
                destSrc1: (ulong)index,
                src2: 0,
                streamLength: 0,
                stride: 0);
        }

        bridge.ResetInstructionBuffer();
        bridge.Add_VLIW_Instruction(
            opCode: 0xDEAD,
            dataType: 7,
            predicate: 1,
            immediate: 0xBEEF,
            destSrc1: 0x1234,
            src2: 0x5678,
            streamLength: 0x9ABC,
            stride: 0xDEF0);

        ReadOnlySpan<VLIW_Instruction> recordedInstructions = bridge.GetRecordedInstructions();

        Assert.Equal(1, bridge.InstructionCount);
        Assert.Equal(1, recordedInstructions.Length);
        Assert.Equal(0xDEADU, recordedInstructions[0].OpCode);
        Assert.Equal(0xBEEF, recordedInstructions[0].Immediate);
        Assert.Equal(0x1234UL, recordedInstructions[0].DestSrc1Pointer);
        Assert.Equal(0x5678UL, recordedInstructions[0].Src2Pointer);
    }

    private static ProcessorCompilerBridge CreateHandshakenBridge()
    {
        var bridge = new ProcessorCompilerBridge();
        bridge.DeclareCompilerContractVersion(CompilerContract.Version, "Phase12ProcessorCompilerBridgeBufferTests");
        return bridge;
    }
}
