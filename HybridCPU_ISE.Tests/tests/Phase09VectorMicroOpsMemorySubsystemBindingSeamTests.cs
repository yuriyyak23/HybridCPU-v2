using Xunit;
using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.Tests.Phase09;

/// <summary>
/// Proves that <see cref="LoadSegmentMicroOp.Execute"/> and <see cref="StoreSegmentMicroOp.Execute"/>
/// read the <see cref="MemorySubsystem"/> from the core-bound seam (D3-C)
/// instead of the mutable global <see cref="Processor.Memory"/> static.
///
/// Pattern: seed MemorySubsystem → construct CPU_Core (captures via
/// PrepareExecutionStart→FlushPipeline→CancelInFlightExplicitMemoryRequests→
/// GetBoundMemorySubsystem()) → swap global → Execute vector micro-op →
/// verify the seeded subsystem received the enqueue.
/// </summary>
public sealed class Phase09VectorMicroOpsMemorySubsystemBindingSeamTests
{
    [Fact]
    public void LoadSegmentMicroOp_WhenGlobalMemorySubsystemSwappedAfterConstruction_EnqueuesToBoundSubsystem()
    {
        const ulong address = 0x400UL;

        Processor.MainMemoryArea originalMainMemory = Processor.MainMemory;
        MemorySubsystem? originalMemorySubsystem = Processor.Memory;
        ProcessorMode originalMode = Processor.CurrentProcessorMode;

        var seededMemory = new Processor.MultiBankMemoryArea(4, 0x1000UL);

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

            // Swap global to a replacement subsystem
            var replacementSubsystem = new MemorySubsystem(ref proc);
            Processor.Memory = replacementSubsystem;

            int seededQueuedBefore = seededSubsystem.CurrentQueuedRequests;
            int replacementQueuedBefore = replacementSubsystem.CurrentQueuedRequests;

            var instruction = new VLIW_Instruction();
            instruction.DestSrc1Pointer = address;
            instruction.StreamLength = 4;
            instruction.DataTypeValue = DataTypeEnum.UINT32;
            instruction.Stride = 4;

            var load = new LoadSegmentMicroOp { Instruction = instruction };
            load.InitializeMetadata();

            // First Execute should enqueue an async read and return false
            bool completed = load.Execute(ref core);
            Assert.False(completed, "LoadSegmentMicroOp.Execute should return false on first call (async enqueue).");

            // The seeded (bound) subsystem should have received the enqueue
            Assert.True(seededSubsystem.CurrentQueuedRequests > seededQueuedBefore,
                "The bound (seeded) MemorySubsystem should have received the EnqueueRead, " +
                "but its queued request count did not increase.");

            // The replacement subsystem should remain untouched
            Assert.Equal(replacementQueuedBefore, replacementSubsystem.CurrentQueuedRequests);
        }
        finally
        {
            Processor.MainMemory = originalMainMemory;
            Processor.Memory = originalMemorySubsystem;
            Processor.CurrentProcessorMode = originalMode;
        }
    }

    [Fact]
    public void StoreSegmentMicroOp_WhenGlobalMemorySubsystemSwappedAfterConstruction_EnqueuesToBoundSubsystem()
    {
        const ulong address = 0x500UL;

        Processor.MainMemoryArea originalMainMemory = Processor.MainMemory;
        MemorySubsystem? originalMemorySubsystem = Processor.Memory;
        ProcessorMode originalMode = Processor.CurrentProcessorMode;

        var seededMemory = new Processor.MultiBankMemoryArea(4, 0x1000UL);

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

            // Swap global to a replacement subsystem
            var replacementSubsystem = new MemorySubsystem(ref proc);
            Processor.Memory = replacementSubsystem;

            int seededQueuedBefore = seededSubsystem.CurrentQueuedRequests;
            int replacementQueuedBefore = replacementSubsystem.CurrentQueuedRequests;

            var instruction = new VLIW_Instruction();
            instruction.DestSrc1Pointer = address;
            instruction.StreamLength = 4;
            instruction.DataTypeValue = DataTypeEnum.UINT32;
            instruction.Stride = 4;

            var store = new StoreSegmentMicroOp { Instruction = instruction };
            store.SetStoreBuffer(new byte[16]); // 4 elements × 4 bytes
            store.InitializeMetadata();

            // First Execute should enqueue an async write and return false
            bool completed = store.Execute(ref core);
            Assert.False(completed, "StoreSegmentMicroOp.Execute should return false on first call (async enqueue).");

            // The seeded (bound) subsystem should have received the enqueue
            Assert.True(seededSubsystem.CurrentQueuedRequests > seededQueuedBefore,
                "The bound (seeded) MemorySubsystem should have received the EnqueueWrite, " +
                "but its queued request count did not increase.");

            // The replacement subsystem should remain untouched
            Assert.Equal(replacementQueuedBefore, replacementSubsystem.CurrentQueuedRequests);
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

