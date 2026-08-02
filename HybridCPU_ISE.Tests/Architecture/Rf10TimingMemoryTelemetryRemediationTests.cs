using HybridCPU_ISE.CloseToHSL.Memory.MMU;
using HybridCPU_ISE.CloseToHSL.Memory.Timing;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf10TimingMemoryTelemetryRemediationTests
{
    [Fact]
    public void ProducerCountsAcceptedPublishedCompletionAndBytesExactlyOnce()
    {
        WithMappedMemory((mainMemory, memory) =>
        {
            byte[] expected = BitConverter.GetBytes(0x1122_3344_5566_7788UL);
            Assert.True(mainMemory.TryWritePhysicalRange(0x180, expected));

            MemoryAdmissionResult read =
                memory.CycleController.TryAcceptExplicitPacketScalarLoad(0, 0x180, 8);
            MemoryAdmissionResult store =
                memory.CycleController.TryAcceptExplicitPacketScalarStore(0, 0x200, 8, expected);

            MemoryCycleTelemetrySnapshot accepted = memory.CycleController.GetTelemetrySnapshot();
            Assert.Equal(2UL, accepted.AcceptedRequests);
            Assert.Equal(1UL, accepted.DataReadAcceptedRequests);
            Assert.Equal(1UL, accepted.DataWriteAcceptedRequests);
            Assert.Equal(0UL, accepted.CompletedRequests);

            memory.AdvanceCycles(1);
            MemoryCycleTelemetrySnapshot serviced = memory.CycleController.GetTelemetrySnapshot();
            Assert.Equal(1UL, serviced.ReadServiceCycles);
            Assert.Equal(1UL, serviced.StoreReadinessServiceCycles);
            Assert.Equal(0UL, serviced.CompletedRequests);

            memory.AdvanceCycles(1);
            MemoryCycleTelemetrySnapshot published = memory.CycleController.GetTelemetrySnapshot();
            Assert.Equal(2UL, published.ControllerCycles);
            Assert.Equal(1UL, published.CompletionPublicationCycles);
            Assert.Equal(2UL, published.CompletedRequests);
            Assert.Equal(1UL, published.DataReadCompletedRequests);
            Assert.Equal(1UL, published.DataWriteCompletedRequests);
            Assert.Equal(8UL, published.DataReadBytes);

            Assert.True(memory.CycleController.TryTakeCompletion(read.RequestId, out _));
            Assert.True(memory.CycleController.TryTakeCompletion(store.RequestId, out _));
            Assert.False(memory.CycleController.TryTakeCompletion(read.RequestId, out _));
            Assert.Equal(published, memory.CycleController.GetTelemetrySnapshot());
        });
    }

    [Fact]
    public void QueueFullIsMeasuredButInvalidAndBankConflictAreNotInvented()
    {
        WithMappedMemory((unusedMainMemory, memory) =>
        {
            _ = unusedMainMemory;
            for (int index = 0;
                 index < MemoryCycleController.ExplicitPacketScalarLoadCapacity;
                 index++)
            {
                Assert.Equal(
                    MemoryAdmissionStatus.Accepted,
                    memory.CycleController
                        .TryAcceptExplicitPacketScalarLoad(0, (ulong)(index * 8), 8)
                        .Status);
            }

            Assert.Equal(
                MemoryAdmissionStatus.Backpressured,
                memory.CycleController.TryAcceptExplicitPacketScalarLoad(0, 0x100, 8).Status);
            Assert.Equal(
                MemoryAdmissionStatus.Rejected,
                memory.CycleController.TryAcceptExplicitPacketScalarLoad(0, 0x100, 3).Status);

            MemoryCycleTelemetrySnapshot snapshot = memory.CycleController.GetTelemetrySnapshot();
            Assert.Equal(8UL, snapshot.AcceptedRequests);
            Assert.Equal(1UL, snapshot.QueueFullRejects);
            Assert.False(MemoryCycleTelemetrySnapshot.BankConflictRejectTelemetryAvailable);
            Assert.False(MemoryCycleTelemetrySnapshot.InstructionFetchRequestTelemetryAvailable);
        });
    }

    [Fact]
    public void CancellationAfterServicePublishesNoCompletionOrReadBytes()
    {
        WithMappedMemory((unusedMainMemory, memory) =>
        {
            _ = unusedMainMemory;
            MemoryAdmissionResult accepted =
                memory.CycleController.TryAcceptSingleLaneScalarLoad(0, 0x200, 8);
            memory.AdvanceCycles(1);
            Assert.True(memory.CycleController.TryCancel(accepted.RequestId));
            memory.AdvanceCycles(1);

            MemoryCycleTelemetrySnapshot snapshot = memory.CycleController.GetTelemetrySnapshot();
            Assert.Equal(1UL, snapshot.AcceptedRequests);
            Assert.Equal(1UL, snapshot.ReadServiceCycles);
            Assert.Equal(0UL, snapshot.CompletedRequests);
            Assert.Equal(0UL, snapshot.DataReadBytes);
            Assert.Equal(0UL, snapshot.CompletionPublicationCycles);
            Assert.False(memory.CycleController.TryTakeCompletion(accepted.RequestId, out _));
        });
    }

    [Fact]
    public void CompatibilityAdvanceUsesSameEdgeAndResetDoesNotChangeTimingState()
    {
        WithMappedMemory((unusedMainMemory, memory) =>
        {
            _ = unusedMainMemory;
            memory.AdvanceCycles(2);
            Assert.Equal(2UL, memory.CycleController.MemoryCycle);
            Assert.Equal(2UL, memory.CycleController.GetTelemetrySnapshot().ControllerCycles);

            memory.ResetStatistics();
            Assert.Equal(2UL, memory.CycleController.MemoryCycle);
            Assert.Equal(default, memory.CycleController.GetTelemetrySnapshot());

            memory.AdvanceCycles(1);
            Assert.Equal(3UL, memory.CycleController.MemoryCycle);
            Assert.Equal(1UL, memory.CycleController.GetTelemetrySnapshot().ControllerCycles);
        });
    }

    [Fact]
    public void SelectedRetireOwnerCountsOnlySuccessfulCommittedStoreBytes()
    {
        WithMappedMemory((mainMemory, memory) =>
        {
            const ulong address = 0x380;
            Assert.True(mainMemory.TryWritePhysicalRange(address, new byte[8]));

            var core = new Processor.CPU_Core(0);
            core.TestPrepareExplicitPacketStoreForWriteBack(
                4,
                0xA400,
                address,
                0xA1B2_C3D4,
                4,
                1);

            Assert.Equal(0UL, memory.CycleController.GetTelemetrySnapshot().CommittedDataWriteBytes);
            core.TestRunWriteBackStage();
            Assert.Equal(4UL, memory.CycleController.GetTelemetrySnapshot().CommittedDataWriteBytes);
        });
    }

    [Fact]
    public void PerformanceReportTransportsAvailableZeroAndExplicitUnavailability()
    {
        ProfilingOptions? originalOptions = Processor.CurrentProfilingOptions;
        try
        {
            WithMappedMemory((unusedMainMemory, memory) =>
            {
                _ = unusedMainMemory;
                Processor.CurrentProfilingOptions = ProfilingOptions.Default();

                PerformanceReport zeroReport = Processor.GetPerformanceStats();
                Assert.True(zeroReport.MemoryCycleTelemetryAvailable);
                Assert.Equal(MemoryCycleTelemetrySnapshot.SchemaVersion,
                    zeroReport.MemoryCycleTelemetrySchemaVersion);
                Assert.Equal(0L, zeroReport.MemoryAcceptedRequests);
                Assert.True(zeroReport.InstructionFetchReadBytesTelemetryAvailable);
                Assert.Equal(0L, zeroReport.InstructionFetchReadBytes);
                Assert.False(zeroReport.InstructionFetchRequestTelemetryAvailable);
                Assert.False(zeroReport.MemoryBankConflictRejectTelemetryAvailable);

                Assert.Equal(
                    MemoryAdmissionStatus.Accepted,
                    memory.CycleController.TryAcceptSingleLaneScalarLoad(0, 0x200, 8).Status);

                PerformanceReport acceptedReport = Processor.GetPerformanceStats();
                Assert.Equal(1L, acceptedReport.MemoryAcceptedRequests);
                Assert.Equal(1L, acceptedReport.DataReadAcceptedRequests);
                Assert.Equal(0L, acceptedReport.MemoryCompletedRequests);
            });
        }
        finally
        {
            Processor.CurrentProfilingOptions = originalOptions;
        }
    }

    [Fact]
    public void PerformanceAndConsoleTransportPreserveZeroVersusUnavailableAndManifestCompatibility()
    {
        string root = FindRepositoryRoot();
        string performanceReport = Read(root,
            "HybridCPU_ISE/NonRTL/Processor/Performance/PerformanceReport.Memory.cs");
        string performanceProducer = Read(root,
            "HybridCPU_ISE/NonRTL/Processor/Performance/Processor.Performance.cs");
        string consoleReport = Read(root,
            "TestAssemblerConsoleApps/PostRef1TimingMemoryReport.cs");
        string program = Read(root, "TestAssemblerConsoleApps/Program.cs");
        string controller = Read(root,
            "TestAssemblerConsoleApps/DiagnosticRunController.cs");

        Assert.Contains("MemoryCycleTelemetryAvailable", performanceReport, StringComparison.Ordinal);
        Assert.Contains("GetTelemetrySnapshot()", performanceProducer, StringComparison.Ordinal);
        Assert.Contains("post-ref1-timing-memory-v2", consoleReport, StringComparison.Ordinal);
        Assert.Contains("new(\"Available\", value", consoleReport, StringComparison.Ordinal);
        Assert.Contains("new(\"Unavailable\", null", consoleReport, StringComparison.Ordinal);
        Assert.Contains("BankConflictRejects", consoleReport, StringComparison.Ordinal);
        Assert.Contains("InstructionFetchAcceptedRequests", consoleReport, StringComparison.Ordinal);
        Assert.Contains("generatedFiles[\"post_ref1_timing_memory\"]", program, StringComparison.Ordinal);
        Assert.Contains("\"post_ref1_timing_memory\"", controller, StringComparison.Ordinal);
        Assert.Contains("post_ref1_timing_memory_report.json", consoleReport, StringComparison.Ordinal);
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
                0,
                0,
                0,
                0x1000UL,
                IOMMUAccessPermissions.ReadWrite));
            Processor processor = default;
            var memory = new MemorySubsystem(ref processor);
            Processor.Memory = memory;
            body(mainMemory, memory);
        }
        finally
        {
            Processor.Memory = originalMemorySubsystem;
            Processor.MainMemory = originalMainMemory;
        }
    }

    private static string Read(string root, string relativePath) =>
        File.ReadAllText(Path.Combine(
            root,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current != null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE")) &&
                Directory.Exists(Path.Combine(current.FullName, "TestAssemblerConsoleApps")))
            {
                return current.FullName;
            }
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
