using System;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Memory;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Phase09;

public sealed class Phase09PipelineMainMemoryBindingSeamTests
{
    [Fact]
    public void SingleLaneMemoryLoad_WhenGlobalMainMemoryIsReplacedAfterCoreConstruction_UsesBoundMemory()
    {
        const ushort destinationRegister = 14;
        const ulong address = 0x280UL;
        const ulong loadedValue = 0x8877_6655_4433_2211UL;
        const ulong pc = 0x8C80UL;

        Processor.MainMemoryArea originalMainMemory = Processor.MainMemory;
        MemorySubsystem? originalMemorySubsystem = Processor.Memory;
        var seededMemory = new Processor.MultiBankMemoryArea(4, 0x1000UL);
        var replacementMemory = new Processor.MultiBankMemoryArea(4, 0x10UL);

        try
        {
            Processor.MainMemory = seededMemory;
            Processor.Memory = null;
            Assert.True(seededMemory.TryWritePhysicalRange(address, BitConverter.GetBytes(loadedValue)));

            var core = new Processor.CPU_Core(0);
            core.InitializePipeline();

            Processor.MainMemory = replacementMemory;

            core.TestSeedSingleLaneExecuteForMemoryFollowThrough(
                isMemoryOp: true,
                isLoad: true,
                writesRegister: true,
                destRegId: destinationRegister,
                memoryAddress: address,
                memoryAccessSize: 8,
                pc: pc,
                opCode: (uint)InstructionsEnum.Load);

            core.TestRunMemoryStageFromCurrentExecuteState();

            MemoryStage memoryStage = core.GetMemoryStage();
            Assert.True(memoryStage.Valid);
            Assert.True(memoryStage.ResultReady);
            Assert.Equal(loadedValue, memoryStage.ResultValue);
        }
        finally
        {
            Processor.MainMemory = originalMainMemory;
            Processor.Memory = originalMemorySubsystem;
        }
    }

    [Fact]
    public void ExplicitPacketLoadFallback_WhenGlobalMainMemoryIsReplacedAfterCoreConstruction_UsesBoundMemory()
    {
        const int vtId = 1;
        const ulong pc = 0x2840UL;
        const ulong address = 0x1880UL;
        const ushort destinationRegister = 9;
        const ulong originalDestinationValue = 0xDEAD_BEEF_CAFE_BABEUL;
        const ulong loadedValue = 0x0102_0304_0506_0708UL;

        Processor.MainMemoryArea originalMainMemory = Processor.MainMemory;
        MemorySubsystem? originalMemorySubsystem = Processor.Memory;
        var seededMemory = new Processor.MultiBankMemoryArea(4, 0x1000UL);
        var replacementMemory = new Processor.MultiBankMemoryArea(4, 0x10UL);

        try
        {
            Processor.MainMemory = seededMemory;
            Processor.Memory = null;
            Assert.True(seededMemory.TryWritePhysicalRange(address, BitConverter.GetBytes(loadedValue)));

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(pc, activeVtId: vtId);
            core.WriteCommittedPc(vtId, pc);
            core.WriteCommittedArch(vtId, destinationRegister, originalDestinationValue);

            Processor.MainMemory = replacementMemory;

            core.TestPrepareExplicitPacketLoadForWriteBack(
                laneIndex: 4,
                pc,
                address,
                destinationRegister,
                accessSize: 8,
                vtId);

            Assert.Equal(originalDestinationValue, core.ReadArch(vtId, destinationRegister));

            core.TestRunWriteBackStage();

            Assert.Equal(loadedValue, core.ReadArch(vtId, destinationRegister));
            Assert.Equal(pc, core.ReadCommittedPc(vtId));
        }
        finally
        {
            Processor.MainMemory = originalMainMemory;
            Processor.Memory = originalMemorySubsystem;
        }
    }

    [Fact]
    public void LegacyScalarStoreRetire_WhenGlobalMainMemoryIsReplacedAfterCoreConstruction_UsesBoundMemory()
    {
        const ulong pc = 0x2000UL;
        const ulong address = 0x180UL;
        const ulong data = 0x00000000_11223344UL;

        Processor.MainMemoryArea originalMainMemory = Processor.MainMemory;
        MemorySubsystem? originalMemorySubsystem = Processor.Memory;
        var seededMemory = new Processor.MultiBankMemoryArea(4, 0x1000UL);
        var replacementMemory = new Processor.MultiBankMemoryArea(4, 0x10UL);

        try
        {
            Processor.MainMemory = seededMemory;
            Processor.Memory = null;
            byte[] baseline = { 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA };
            Assert.True(seededMemory.TryWritePhysicalRange(address, baseline));

            var core = new Processor.CPU_Core(0);

            Processor.MainMemory = replacementMemory;

            core.TestRetireLegacyScalarStoreThroughWriteBack(
                pc,
                address,
                data,
                accessSize: 4,
                vtId: 0);

            byte[] committed = new byte[baseline.Length];
            Assert.True(seededMemory.TryReadPhysicalRange(address, committed));
            Assert.Equal(
                new byte[] { 0x44, 0x33, 0x22, 0x11, 0xAA, 0xAA, 0xAA, 0xAA },
                committed);

            var control = core.GetPipelineControl();
            Assert.Equal(1UL, control.InstructionsRetired);
            Assert.Equal(1UL, control.ScalarLanesRetired);
        }
        finally
        {
            Processor.MainMemory = originalMainMemory;
            Processor.Memory = originalMemorySubsystem;
        }
    }
}
