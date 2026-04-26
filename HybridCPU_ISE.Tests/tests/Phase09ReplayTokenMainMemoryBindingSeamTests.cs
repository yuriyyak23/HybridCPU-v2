using System;
using HybridCPU_ISE.Core;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.Tests.Phase09;

public sealed class Phase09ReplayTokenMainMemoryBindingSeamTests
{
    [Fact]
    public void ReplayTokenRollback_WhenGlobalMainMemoryIsReplacedAfterCapture_UsesBoundMemory()
    {
        const ulong address = 0x180UL;

        Processor.MainMemoryArea originalMainMemory = Processor.MainMemory;
        MemorySubsystem? originalMemorySubsystem = Processor.Memory;
        var seededMemory = new Processor.MultiBankMemoryArea(4, 0x1000UL);
        var replacementMemory = new Processor.MultiBankMemoryArea(4, 0x10UL);
        byte[] baseline = { 0x11, 0x22, 0x33, 0x44 };
        byte[] mutated = { 0xAA, 0xBB, 0xCC, 0xDD };

        try
        {
            Processor.MainMemory = seededMemory;
            Processor.Memory = null;
            Assert.True(seededMemory.TryWritePhysicalRange(address, baseline));

            var token = new ReplayToken(mainMemory: seededMemory);
            token.CaptureMemoryState(address, baseline.Length);

            Assert.True(seededMemory.TryWritePhysicalRange(address, mutated));
            Processor.MainMemory = replacementMemory;

            var core = new Processor.CPU_Core(0);
            token.Rollback(ref core);

            byte[] restored = new byte[baseline.Length];
            Assert.True(seededMemory.TryReadPhysicalRange(address, restored));
            Assert.Equal(baseline, restored);
        }
        finally
        {
            Processor.MainMemory = originalMainMemory;
            Processor.Memory = originalMemorySubsystem;
        }
    }

    [Fact]
    public void StoreMicroOpRollbackToken_WhenGlobalMainMemoryIsReplacedAfterCapture_UsesBoundMemory()
    {
        const ulong address = 0x280UL;

        Processor.MainMemoryArea originalMainMemory = Processor.MainMemory;
        MemorySubsystem? originalMemorySubsystem = Processor.Memory;
        var seededMemory = new Processor.MultiBankMemoryArea(4, 0x1000UL);
        var replacementMemory = new Processor.MultiBankMemoryArea(4, 0x10UL);
        byte[] baseline = { 0x10, 0x20, 0x30, 0x40 };
        byte[] mutated = { 0xFE, 0xED, 0xFA, 0xCE };

        try
        {
            Processor.MainMemory = seededMemory;
            Processor.Memory = null;
            Assert.True(seededMemory.TryWritePhysicalRange(address, baseline));

            var store = new StoreMicroOp
            {
                Address = address,
                Size = 4,
                Value = 0x1122_3344U,
                SrcRegID = 2,
                BaseRegID = 1
            };
            store.InitializeMetadata();

            ReplayToken token = store.CreateRollbackToken(ownerThreadId: 0, mainMemory: seededMemory);

            Assert.True(seededMemory.TryWritePhysicalRange(address, mutated));
            Processor.MainMemory = replacementMemory;

            var core = new Processor.CPU_Core(0);
            token.Rollback(ref core);

            byte[] restored = new byte[baseline.Length];
            Assert.True(seededMemory.TryReadPhysicalRange(address, restored));
            Assert.Equal(baseline, restored);
        }
        finally
        {
            Processor.MainMemory = originalMainMemory;
            Processor.Memory = originalMemorySubsystem;
        }
    }

    [Fact]
    public void StoreMicroOpRollbackToken_WhenMainMemoryBindingIsOmitted_ThenFailsClosed()
    {
        const ulong address = 0x280UL;

        Processor.MainMemoryArea originalMainMemory = Processor.MainMemory;
        MemorySubsystem? originalMemorySubsystem = Processor.Memory;
        var seededMemory = new Processor.MultiBankMemoryArea(4, 0x1000UL);

        try
        {
            Processor.MainMemory = seededMemory;
            Processor.Memory = null;

            var store = new StoreMicroOp
            {
                Address = address,
                Size = 4,
                Value = 0x1122_3344U,
                SrcRegID = 2,
                BaseRegID = 1
            };
            store.InitializeMetadata();

            MainMemoryBindingUnavailableException ex = Assert.Throws<MainMemoryBindingUnavailableException>(
                () => store.CreateRollbackToken(ownerThreadId: 0));

            Assert.Equal("ReplayToken", ex.BindingSurface);
            Assert.Contains("ReplayToken.CaptureMemoryState()", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            Processor.MainMemory = originalMainMemory;
            Processor.Memory = originalMemorySubsystem;
        }
    }
}
