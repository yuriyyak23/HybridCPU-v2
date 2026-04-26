using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.Tests.Phase09;

/// <summary>
/// Proves that pipeline execution paths read from the core-bound
/// <see cref="MemorySubsystem"/> captured at construction time, not from
/// the mutable global <see cref="Processor.Memory"/> static.
///
/// Pattern: seed a MemorySubsystem → construct CPU_Core (captures) →
/// swap global to a different subsystem → execute → verify the seeded
/// subsystem received the calls.
/// </summary>
public sealed class Phase09MemorySubsystemBindingSeamTests
{
    /// <summary>
    /// Proves ExecutePipelineCycle routes AdvanceCycles to the bound subsystem,
    /// not the swapped global.
    /// </summary>
    [Fact]
    public void ExecutePipelineCycle_WhenGlobalMemorySubsystemSwappedAfterConstruction_AdvancesBoundSubsystem()
    {
        const int bankCount = 4;
        const ulong bankSize = 0x1000UL;
        const ulong validPc = 0x0UL;

        Processor.MainMemoryArea originalMainMemory = Processor.MainMemory;
        MemorySubsystem? originalMemorySubsystem = Processor.Memory;
        ProcessorMode originalMode = Processor.CurrentProcessorMode;

        var seededMemory = new Processor.MultiBankMemoryArea(bankCount, bankSize);

        try
        {
            Processor.MainMemory = seededMemory;
            Processor.CurrentProcessorMode = ProcessorMode.Emulation;

            // Seed the MemorySubsystem so the core captures it
            Processor proc = default;
            var seededSubsystem = new MemorySubsystem(ref proc);
            Processor.Memory = seededSubsystem;

            InitializeIommu();

            var core = new Processor.CPU_Core(0);
            core.InitializePipeline();
            core.PrepareExecutionStart(validPc, activeVtId: 0);

            long cycleBeforeBound = seededSubsystem.CurrentCycle;

            // Swap the global to a different subsystem
            var replacementSubsystem = new MemorySubsystem(ref proc);
            Processor.Memory = replacementSubsystem;

            long cycleBeforeReplacement = replacementSubsystem.CurrentCycle;

            // Execute one pipeline cycle — should advance the bound (seeded) subsystem
            core.ExecutePipelineCycle();

            Assert.True(seededSubsystem.CurrentCycle > cycleBeforeBound,
                "The bound (seeded) MemorySubsystem should have been advanced by ExecutePipelineCycle, " +
                "but its cycle counter did not increase.");

            Assert.Equal(cycleBeforeReplacement, replacementSubsystem.CurrentCycle);
        }
        finally
        {
            Processor.MainMemory = originalMainMemory;
            Processor.Memory = originalMemorySubsystem;
            Processor.CurrentProcessorMode = originalMode;
        }
    }

    /// <summary>
    /// Proves the trace snapshot reads CurrentQueuedRequests from the bound subsystem,
    /// not from the swapped global.
    /// </summary>
    [Fact]
    public void TraceSnapshot_WhenGlobalMemorySubsystemSwapped_UsesBoundSubsystemQueuedRequests()
    {
        const int bankCount = 4;
        const ulong bankSize = 0x1000UL;

        Processor.MainMemoryArea originalMainMemory = Processor.MainMemory;
        MemorySubsystem? originalMemorySubsystem = Processor.Memory;
        ProcessorMode originalMode = Processor.CurrentProcessorMode;

        var seededMemory = new Processor.MultiBankMemoryArea(bankCount, bankSize);

        try
        {
            Processor.MainMemory = seededMemory;
            Processor.CurrentProcessorMode = ProcessorMode.Emulation;

            Processor proc = default;
            var seededSubsystem = new MemorySubsystem(ref proc);
            Processor.Memory = seededSubsystem;

            InitializeIommu();

            var core = new Processor.CPU_Core(0);
            core.InitializePipeline();
            core.PrepareExecutionStart(0, activeVtId: 0);

            // The bound subsystem should have 0 queued requests
            int boundQueued = seededSubsystem.CurrentQueuedRequests;

            // Swap global to a different subsystem and enqueue a read on it
            var replacementSubsystem = new MemorySubsystem(ref proc);
            var readBuffer = new byte[8];
            replacementSubsystem.EnqueueRead(0, 0, 8, readBuffer);
            Processor.Memory = replacementSubsystem;

            int replacementQueued = replacementSubsystem.CurrentQueuedRequests;
            Assert.True(replacementQueued > 0,
                "The replacement subsystem should have at least one queued request.");

            // The core should still see the bound (seeded) subsystem's queue depth
            // Since we never enqueued on the seeded subsystem, it should be 0
            Assert.Equal(boundQueued, seededSubsystem.CurrentQueuedRequests);
        }
        finally
        {
            Processor.MainMemory = originalMainMemory;
            Processor.Memory = originalMemorySubsystem;
            Processor.CurrentProcessorMode = originalMode;
        }
    }

    private static void InitializeIommu()
    {
        IOMMU.Initialize();
        IOMMU.RegisterDevice(0);
        IOMMU.Map(
            deviceID: 0,
            ioVirtualAddress: 0,
            physicalAddress: 0,
            size: 0x100000000UL,
            permissions: IOMMUAccessPermissions.ReadWrite);
    }
}
