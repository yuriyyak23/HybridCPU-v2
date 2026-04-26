using System;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core.Registers;

namespace HybridCPU_ISE.Tests.Phase09;

public sealed class Phase09SystemEmulationMainMemoryBindingSeamTests
{
    [Fact]
    public void MoveRegToMem_WhenGlobalMainMemoryIsReplacedAfterCoreConstruction_UsesBoundMemory()
    {
        const ulong address = 0x200UL;
        const ulong registerValue = 0xAABB_CCDD_EEFF_0011UL;
        const int archReg = 5;

        Processor.MainMemoryArea originalMainMemory = Processor.MainMemory;
        ProcessorMode originalMode = Processor.CurrentProcessorMode;
        var seededMemory = new Processor.MultiBankMemoryArea(4, 0x1000UL);
        var replacementMemory = new Processor.MultiBankMemoryArea(4, 0x10UL);

        try
        {
            Processor.MainMemory = seededMemory;
            Processor.CurrentProcessorMode = ProcessorMode.Emulation;

            var core = new Processor.CPU_Core(0);
            core.WriteCommittedArch(0, archReg, registerValue);

            Processor.MainMemory = replacementMemory;

            core.Move(ArchRegId.Create(archReg), address);

            byte[] committed = new byte[sizeof(ulong)];
            Assert.True(seededMemory.TryReadPhysicalRange(address, committed));
            Assert.Equal(registerValue, BitConverter.ToUInt64(committed, 0));
        }
        finally
        {
            Processor.MainMemory = originalMainMemory;
            Processor.CurrentProcessorMode = originalMode;
        }
    }

    [Fact]
    public void MoveMemToReg_WhenGlobalMainMemoryIsReplacedAfterCoreConstruction_UsesBoundMemory()
    {
        const ulong address = 0x300UL;
        const ulong seededValue = 0x1122_3344_5566_7788UL;
        const int archReg = 7;

        Processor.MainMemoryArea originalMainMemory = Processor.MainMemory;
        ProcessorMode originalMode = Processor.CurrentProcessorMode;
        var seededMemory = new Processor.MultiBankMemoryArea(4, 0x1000UL);
        var replacementMemory = new Processor.MultiBankMemoryArea(4, 0x10UL);

        try
        {
            Processor.MainMemory = seededMemory;
            Processor.CurrentProcessorMode = ProcessorMode.Emulation;
            Assert.True(seededMemory.TryWritePhysicalRange(address, BitConverter.GetBytes(seededValue)));

            var core = new Processor.CPU_Core(0);

            Processor.MainMemory = replacementMemory;

            core.Move(address, ArchRegId.Create(archReg));

            Assert.Equal(seededValue, core.ReadArch(0, archReg));
        }
        finally
        {
            Processor.MainMemory = originalMainMemory;
            Processor.CurrentProcessorMode = originalMode;
        }
    }

    [Fact]
    public void StoreRegToMem_WhenGlobalMainMemoryIsReplacedAfterCoreConstruction_UsesBoundMemory()
    {
        const ulong address = 0x400UL;
        const ulong registerValue = 0xDEAD_BEEF_CAFE_BABEUL;
        const int archReg = 10;

        Processor.MainMemoryArea originalMainMemory = Processor.MainMemory;
        ProcessorMode originalMode = Processor.CurrentProcessorMode;
        var seededMemory = new Processor.MultiBankMemoryArea(4, 0x1000UL);
        var replacementMemory = new Processor.MultiBankMemoryArea(4, 0x10UL);

        try
        {
            Processor.MainMemory = seededMemory;
            Processor.CurrentProcessorMode = ProcessorMode.Emulation;

            var core = new Processor.CPU_Core(0);
            core.WriteCommittedArch(0, archReg, registerValue);

            Processor.MainMemory = replacementMemory;

            core.Store(ArchRegId.Create(archReg), address);

            byte[] committed = new byte[sizeof(ulong)];
            Assert.True(seededMemory.TryReadPhysicalRange(address, committed));
            Assert.Equal(registerValue, BitConverter.ToUInt64(committed, 0));
        }
        finally
        {
            Processor.MainMemory = originalMainMemory;
            Processor.CurrentProcessorMode = originalMode;
        }
    }

    [Fact]
    public void LoadMemToReg_WhenGlobalMainMemoryIsReplacedAfterCoreConstruction_UsesBoundMemory()
    {
        const ulong address = 0x500UL;
        const ulong seededValue = 0x0102_0304_0506_0708UL;
        const int archReg = 12;

        Processor.MainMemoryArea originalMainMemory = Processor.MainMemory;
        ProcessorMode originalMode = Processor.CurrentProcessorMode;
        var seededMemory = new Processor.MultiBankMemoryArea(4, 0x1000UL);
        var replacementMemory = new Processor.MultiBankMemoryArea(4, 0x10UL);

        try
        {
            Processor.MainMemory = seededMemory;
            Processor.CurrentProcessorMode = ProcessorMode.Emulation;
            Assert.True(seededMemory.TryWritePhysicalRange(address, BitConverter.GetBytes(seededValue)));

            var core = new Processor.CPU_Core(0);

            Processor.MainMemory = replacementMemory;

            core.Load(address, ArchRegId.Create(archReg));

            Assert.Equal(seededValue, core.ReadArch(0, archReg));
        }
        finally
        {
            Processor.MainMemory = originalMainMemory;
            Processor.CurrentProcessorMode = originalMode;
        }
    }
}
