using HybridCPU_ISE.CloseToHSL.Memory.MMU;
using HybridCPU_ISE.CloseToHSL.Memory.Timing;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf102ExplicitPacketScalarLoadMemoryCycleTests
{
    [Fact]
    public void Admission_IsFiniteAndAllocatesIdentityOnlyForAcceptedRequests()
    {
        WithMappedMemory((unusedMainMemory, memory) =>
        {
            _ = unusedMainMemory;
            for (int index = 0; index < MemoryCycleController.ExplicitPacketScalarLoadCapacity; index++)
            {
                MemoryAdmissionResult accepted =
                    memory.CycleController.TryAcceptExplicitPacketScalarLoad(0, (ulong)(index * 8), 8);
                Assert.Equal(MemoryAdmissionStatus.Accepted, accepted.Status);
                Assert.True(accepted.RequestId.IsValid);
            }

            MemoryAdmissionResult backpressured =
                memory.CycleController.TryAcceptExplicitPacketScalarLoad(0, 0x100, 8);
            Assert.Equal(MemoryAdmissionStatus.Backpressured, backpressured.Status);
            Assert.False(backpressured.RequestId.IsValid);

            MemoryAdmissionResult rejected =
                memory.CycleController.TryAcceptExplicitPacketScalarLoad(0, 0x100, 3);
            Assert.Equal(MemoryAdmissionStatus.Rejected, rejected.Status);
            Assert.False(rejected.RequestId.IsValid);
            Assert.Contains("1/2/4/8-byte envelope", rejected.Reason, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Completion_IsInvisibleUntilNextLatchPublicationAndConsumableExactlyOnce()
    {
        WithMappedMemory((mainMemory, memory) =>
        {
            const ulong address = 0x180;
            byte[] expected = BitConverter.GetBytes(0x1122_3344_5566_7788UL);
            Assert.True(mainMemory.TryWritePhysicalRange(address, expected));

            MemoryAdmissionResult admission =
                memory.CycleController.TryAcceptExplicitPacketScalarLoad(0, address, expected.Length);
            Assert.Equal(MemoryAdmissionStatus.Accepted, admission.Status);

            memory.AdvanceCycles(1);
            Assert.False(memory.CycleController.TryTakeCompletion(admission.RequestId, out _));

            memory.AdvanceCycles(1);
            Assert.True(memory.CycleController.TryTakeCompletion(
                admission.RequestId,
                out MemoryCompletion? completion));
            Assert.NotNull(completion);
            Assert.True(completion.Succeeded);
            Assert.Equal(expected, completion.Data.ToArray());
            Assert.Equal(2UL, completion.PublishedCycle);
            Assert.False(memory.CycleController.TryTakeCompletion(admission.RequestId, out _));
            Assert.Equal(0, memory.CycleController.OutstandingExplicitPacketScalarLoads);
        });
    }

    [Fact]
    public void Cancellation_IsTerminalAndReleasesCapacityWithoutPublishingCompletion()
    {
        WithMappedMemory((unusedMainMemory, memory) =>
        {
            _ = unusedMainMemory;
            MemoryAdmissionResult admission =
                memory.CycleController.TryAcceptExplicitPacketScalarLoad(0, 0x200, 8);
            Assert.Equal(MemoryAdmissionStatus.Accepted, admission.Status);
            Assert.True(memory.CycleController.TryCancel(admission.RequestId));
            Assert.False(memory.CycleController.TryCancel(admission.RequestId));

            memory.AdvanceCycles(2);
            Assert.False(memory.CycleController.TryTakeCompletion(admission.RequestId, out _));
            Assert.Equal(0, memory.CycleController.OutstandingExplicitPacketScalarLoads);
        });
    }

    [Fact]
    public void SharedController_ConsumesAtMostOneEdgeForSamePlatformCycle()
    {
        WithMappedMemory((unusedMainMemory, memory) =>
        {
            _ = unusedMainMemory;
            Assert.True(MemoryCyclePlatformOrchestrator.AdvanceCoreObservedPlatformEdge(
                memory.CycleController,
                coreId: 0,
                platformCycle: 1));
            Assert.False(MemoryCyclePlatformOrchestrator.AdvanceCoreObservedPlatformEdge(
                memory.CycleController,
                coreId: 1,
                platformCycle: 1));
            Assert.Equal(1UL, memory.CycleController.MemoryCycle);

            Assert.True(MemoryCyclePlatformOrchestrator.AdvanceCoreObservedPlatformEdge(
                memory.CycleController,
                coreId: 0,
                platformCycle: 2));
            Assert.Equal(2UL, memory.CycleController.MemoryCycle);
        });
    }

    [Fact]
    public void RecreatedCoreLocalCycle_RebasesWithoutFreezingSharedControllerProgress()
    {
        WithMappedMemory((unusedMainMemory, memory) =>
        {
            _ = unusedMainMemory;
            Assert.True(MemoryCyclePlatformOrchestrator.AdvanceCoreObservedPlatformEdge(
                memory.CycleController,
                coreId: 0,
                platformCycle: 1));
            Assert.True(MemoryCyclePlatformOrchestrator.AdvanceCoreObservedPlatformEdge(
                memory.CycleController,
                coreId: 0,
                platformCycle: 2));

            // Same CoreID, new compatibility-driver epoch.
            Assert.True(MemoryCyclePlatformOrchestrator.AdvanceCoreObservedPlatformEdge(
                memory.CycleController,
                coreId: 0,
                platformCycle: 1));
            Assert.Equal(3UL, memory.CycleController.MemoryCycle);
        });
    }

    [Fact]
    public void ProductionCutover_IsRestrictedToExplicitPacketScalarLoad()
    {
        string root = FindRepositoryRoot();
        string explicitMemory = File.ReadAllText(Path.Combine(
            root,
            "HybridCPU_ISE",
            "CloseToHSL",
            "Core",
            "Pipeline",
            "Stages",
            "Memory",
            "CPU_Core.PipelineExecution.Memory.cs"));
        string controller = File.ReadAllText(Path.Combine(
            root,
            "HybridCPU_ISE",
            "CloseToHSL",
            "Memory",
            "Timing",
            "MemoryCycleController.cs"));

        Assert.Contains("TryAcceptExplicitPacketScalarLoad", explicitMemory, StringComparison.Ordinal);
        Assert.Contains("PendingMemoryControllerRequestId", explicitMemory, StringComparison.Ordinal);
        Assert.DoesNotContain("memSub.EnqueueRead(", explicitMemory, StringComparison.Ordinal);
        Assert.DoesNotContain("memSub.EnqueueWrite(", explicitMemory, StringComparison.Ordinal);
        Assert.Contains("TryAcceptExplicitPacketScalarStore", explicitMemory, StringComparison.Ordinal);
        Assert.Contains("ExplicitPacketScalarStoreCapacity", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("DMAController", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("AdvancePTW", controller, StringComparison.Ordinal);
    }

    private static void WithMappedMemory(Action<Processor.MainMemoryArea, MemorySubsystem> body)
    {
        Processor.MainMemoryArea originalMainMemory = Processor.MainMemory;
        MemorySubsystem? originalMemorySubsystem = Processor.Memory;
        try
        {
            var mainMemory = new Processor.MultiBankMemoryArea(4, 0x1000UL);
            Processor.MainMemory = mainMemory;
            IOMMU.Initialize();
            IOMMU.RegisterDevice(0);
            Assert.True(IOMMU.Map(
                deviceID: 0,
                ioVirtualAddress: 0,
                physicalAddress: 0,
                size: 0x1000UL,
                permissions: IOMMUAccessPermissions.ReadWrite));
            Processor processor = default;
            var memory = new MemorySubsystem(ref processor);
            Processor.Memory = memory;

            body(mainMemory, memory);
        }
        finally
        {
            Processor.MainMemory = originalMainMemory;
            Processor.Memory = originalMemorySubsystem;
        }
    }

    private static string FindRepositoryRoot()
    {
        string? current = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(current))
        {
            if (File.Exists(Path.Combine(current, "HybridCPU v2.slnx")))
            {
                return current;
            }

            current = Directory.GetParent(current)?.FullName;
        }

        throw new DirectoryNotFoundException("HybridCPU repository root was not found.");
    }
}
