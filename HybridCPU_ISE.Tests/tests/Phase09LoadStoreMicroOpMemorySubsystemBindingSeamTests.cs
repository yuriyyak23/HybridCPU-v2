using HybridCPU_ISE.CloseToHSL.Memory.MMU;
using System;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.Tests;

/// <summary>
/// Proves that <see cref="LoadMicroOp.Execute"/> and <see cref="StoreMicroOp.Execute"/>
/// read the <see cref="MemorySubsystem"/> from the core-bound seam (D3-C)
/// instead of the mutable global <see cref="Processor.Memory"/> static.
///
/// Pattern: seed MemorySubsystem → construct CPU_Core (captures) → swap global →
/// Execute micro-op → verify the seeded subsystem received controller admission.
/// </summary>
public sealed class LoadStoreMicroOpMemorySubsystemBindingSeamTests
{
    [Fact]
    public void LoadMicroOp_WhenGlobalMemorySubsystemSwappedAfterConstruction_EnqueuesToBoundSubsystem()
    {
        const ulong address = 0x280UL;

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

            // Write test data so the read can succeed after AdvanceCycles
            Assert.True(seededMemory.TryWritePhysicalRange(address, BitConverter.GetBytes(0x1122_3344_5566_7788UL)));

            var core = new Processor.CPU_Core(0);
            core.InitializePipeline();
            core.PrepareExecutionStart(0, activeVtId: 0);

            // Swap global to a replacement subsystem
            var replacementSubsystem = new MemorySubsystem(ref proc);
            Processor.Memory = replacementSubsystem;

            int seededQueuedBefore = seededSubsystem.CycleController.OutstandingSingleLaneScalarLoads;
            int replacementQueuedBefore = replacementSubsystem.CycleController.OutstandingSingleLaneScalarLoads;

            var load = new LoadMicroOp
            {
                Address = address,
                Size = 8,
                DestRegID = 9,
                BaseRegID = 1,
                WritesRegister = true
            };
            load.InitializeMetadata();

            // First Execute should enqueue an async read and return false
            bool completed = load.Execute(ref core);
            Assert.False(completed, "LoadMicroOp.Execute should return false on first call (async enqueue).");

            // The seeded (bound) subsystem should have received controller admission.
            Assert.True(
                seededSubsystem.CycleController.OutstandingSingleLaneScalarLoads > seededQueuedBefore,
                "The bound (seeded) MemorySubsystem should have accepted the controller request, " +
                "but its single-lane outstanding count did not increase.");

            // The replacement subsystem should remain untouched
            Assert.Equal(
                replacementQueuedBefore,
                replacementSubsystem.CycleController.OutstandingSingleLaneScalarLoads);
        }
        finally
        {
            Processor.MainMemory = originalMainMemory;
            Processor.Memory = originalMemorySubsystem;
            Processor.CurrentProcessorMode = originalMode;
        }
    }

    [Fact]
    public void StoreMicroOp_WhenGlobalMemorySubsystemSwappedAfterConstruction_EnqueuesToBoundSubsystem()
    {
        const ulong address = 0x180UL;
        const ulong storedValue = 0x00000000_11223344UL;

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

            var store = new StoreMicroOp
            {
                Address = address,
                Value = storedValue,
                Size = 4,
                SrcRegID = 2,
                BaseRegID = 1
            };
            store.InitializeMetadata();

            // First Execute should accept controller readiness and return false.
            bool completed = store.Execute(ref core);
            Assert.False(completed, "StoreMicroOp.Execute should return false on first call (async enqueue).");

            Assert.Equal(seededQueuedBefore, seededSubsystem.CurrentQueuedRequests);
            Assert.Equal(1, seededSubsystem.CycleController.OutstandingSingleLaneScalarStores);

            // The replacement subsystem should remain untouched
            Assert.Equal(replacementQueuedBefore, replacementSubsystem.CurrentQueuedRequests);
            Assert.Equal(0, replacementSubsystem.CycleController.OutstandingSingleLaneScalarStores);
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
