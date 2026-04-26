using System;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.Tests.Phase09;

public sealed class Phase09LoadStoreMainMemoryBindingSeamTests
{
    [Fact]
    public void LoadMicroOpFallback_WhenGlobalMainMemoryIsReplacedAfterCoreConstruction_UsesBoundCoreMemory()
    {
        const ulong address = 0x280UL;
        const ulong loadedValue = 0x8877_6655_4433_2211UL;

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
            var load = new LoadMicroOp
            {
                Address = address,
                Size = 8,
                DestRegID = 9,
                BaseRegID = 1,
                WritesRegister = true
            };
            load.InitializeMetadata();

            Processor.MainMemory = replacementMemory;

            Assert.True(load.Execute(ref core));
            Assert.True(load.TryGetPrimaryWriteBackResult(out ulong value));
            Assert.Equal(loadedValue, value);
        }
        finally
        {
            Processor.MainMemory = originalMainMemory;
            Processor.Memory = originalMemorySubsystem;
        }
    }

    [Fact]
    public void StoreMicroOpFallback_WhenGlobalMainMemoryIsReplacedAfterCoreConstruction_UsesBoundCoreMemory()
    {
        const ulong address = 0x180UL;
        const ulong storedValue = 0x00000000_11223344UL;

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
            var store = new StoreMicroOp
            {
                Address = address,
                Value = storedValue,
                Size = 4,
                SrcRegID = 2,
                BaseRegID = 1
            };
            store.InitializeMetadata();

            Processor.MainMemory = replacementMemory;

            Assert.True(store.Execute(ref core));

            byte[] committed = new byte[baseline.Length];
            Assert.True(seededMemory.TryReadPhysicalRange(address, committed));
            Assert.Equal(
                new byte[] { 0x44, 0x33, 0x22, 0x11, 0xAA, 0xAA, 0xAA, 0xAA },
                committed);
        }
        finally
        {
            Processor.MainMemory = originalMainMemory;
            Processor.Memory = originalMemorySubsystem;
        }
    }
}
