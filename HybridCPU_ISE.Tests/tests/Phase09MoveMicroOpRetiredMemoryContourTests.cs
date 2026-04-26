using HybridCPU_ISE.Arch;
using System;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.Phase09;

public sealed class Phase09MoveMicroOpRetiredMemoryContourTests
{
    [Fact]
    public void MoveMicroOpDt2Execute_WhenReached_ThrowsFailClosedBeforeMemoryMutation()
    {
        const ulong address = 0x180UL;

        Processor.MainMemoryArea originalMainMemory = Processor.MainMemory;
        var seededMemory = new Processor.MultiBankMemoryArea(4, 0x1000UL);
        byte[] baseline = { 0xAA, 0xBB, 0xCC, 0xDD, 0x11, 0x22, 0x33, 0x44 };

        try
        {
            Processor.MainMemory = seededMemory;
            Assert.True(seededMemory.TryWritePhysicalRange(address, baseline));

            var core = new Processor.CPU_Core(0);
            var move = new MoveMicroOp
            {
                OpCode = (uint)Processor.CPU_Core.InstructionsEnum.Move,
                OwnerThreadId = 0
            };
            move.ApplyCanonicalRuntimeMoveShapeProjection(
                dataType: 2,
                reg1Id: 6,
                reg2Id: VLIW_Instruction.NoReg,
                primaryPayload: address);
            move.RefreshWriteMetadata();

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => move.Execute(ref core));

            Assert.Contains("Move DT=2", exception.Message, StringComparison.Ordinal);
            Assert.Contains("canonicalized to Load/Store", exception.Message, StringComparison.Ordinal);
            Assert.Contains("fail closed", exception.Message, StringComparison.Ordinal);

            byte[] committed = new byte[baseline.Length];
            Assert.True(seededMemory.TryReadPhysicalRange(address, committed));
            Assert.Equal(baseline, committed);
        }
        finally
        {
            Processor.MainMemory = originalMainMemory;
        }
    }

    [Fact]
    public void MoveMicroOpDt3Execute_WhenReached_ThrowsFailClosedBeforeMemoryPublication()
    {
        const ulong address = 0x280UL;
        const ulong loadedValue = 0x8877_6655_4433_2211UL;

        Processor.MainMemoryArea originalMainMemory = Processor.MainMemory;
        var seededMemory = new Processor.MultiBankMemoryArea(4, 0x1000UL);

        try
        {
            Processor.MainMemory = seededMemory;
            Assert.True(seededMemory.TryWritePhysicalRange(address, BitConverter.GetBytes(loadedValue)));

            var core = new Processor.CPU_Core(0);
            var move = new MoveMicroOp
            {
                OpCode = (uint)Processor.CPU_Core.InstructionsEnum.Move,
                OwnerThreadId = 0
            };
            move.ApplyCanonicalRuntimeMoveShapeProjection(
                dataType: 3,
                reg1Id: 9,
                reg2Id: VLIW_Instruction.NoReg,
                primaryPayload: address);
            move.RefreshWriteMetadata();

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => move.Execute(ref core));

            Assert.Contains("Move DT=3", exception.Message, StringComparison.Ordinal);
            Assert.Contains("canonicalized to Load/Store", exception.Message, StringComparison.Ordinal);
            Assert.Contains("fail closed", exception.Message, StringComparison.Ordinal);

            byte[] observed = new byte[sizeof(ulong)];
            Assert.True(seededMemory.TryReadPhysicalRange(address, observed));
            Assert.Equal(BitConverter.GetBytes(loadedValue), observed);
        }
        finally
        {
            Processor.MainMemory = originalMainMemory;
        }
    }
}

