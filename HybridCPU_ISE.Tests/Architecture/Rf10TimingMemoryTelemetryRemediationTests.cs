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
            Assert.Equal(2UL, accepted.OutstandingRequests);

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
            MemoryCycleTelemetrySnapshot consumed = memory.CycleController.GetTelemetrySnapshot();
            Assert.Equal(2UL, consumed.ConsumedCompletions);
            Assert.Equal(0UL, consumed.CanceledRequests);
            Assert.Equal(0UL, consumed.OutstandingRequests);
            Assert.Equal(
                consumed.TelemetryBaselineOutstandingRequests + consumed.AcceptedRequests,
                consumed.CanceledRequests + consumed.ConsumedCompletions + consumed.OutstandingRequests);
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
            Assert.Equal(1UL, snapshot.CanceledRequests);
            Assert.Equal(0UL, snapshot.ConsumedCompletions);
            Assert.Equal(0UL, snapshot.OutstandingRequests);
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
    public void TelemetryResetCapturesLiveIdentityBaselineForExactLifecycleBalance()
    {
        WithMappedMemory((unusedMainMemory, memory) =>
        {
            _ = unusedMainMemory;
            MemoryAdmissionResult accepted =
                memory.CycleController.TryAcceptSingleLaneScalarLoad(0, 0x200, 8);
            Assert.Equal(MemoryAdmissionStatus.Accepted, accepted.Status);

            memory.ResetStatistics();
            MemoryCycleTelemetrySnapshot reset = memory.CycleController.GetTelemetrySnapshot();
            Assert.Equal(1UL, reset.TelemetryBaselineOutstandingRequests);
            Assert.Equal(0UL, reset.AcceptedRequests);
            Assert.Equal(1UL, reset.OutstandingRequests);

            Assert.True(memory.CycleController.TryCancel(accepted.RequestId));
            MemoryCycleTelemetrySnapshot canceled = memory.CycleController.GetTelemetrySnapshot();
            Assert.Equal(1UL, canceled.CanceledRequests);
            Assert.Equal(0UL, canceled.OutstandingRequests);
            Assert.Equal(
                canceled.TelemetryBaselineOutstandingRequests + canceled.AcceptedRequests,
                canceled.CanceledRequests + canceled.ConsumedCompletions + canceled.OutstandingRequests);
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
                Assert.Equal(0L, zeroReport.MemoryTelemetryBaselineOutstandingRequests);
                Assert.Equal(0L, zeroReport.MemoryCanceledRequests);
                Assert.Equal(0L, zeroReport.MemoryConsumedCompletions);
                Assert.Equal(0L, zeroReport.MemoryOutstandingRequests);

                Assert.Equal(
                    MemoryAdmissionStatus.Accepted,
                    memory.CycleController.TryAcceptSingleLaneScalarLoad(0, 0x200, 8).Status);

                PerformanceReport acceptedReport = Processor.GetPerformanceStats();
                Assert.Equal(1L, acceptedReport.MemoryAcceptedRequests);
                Assert.Equal(1L, acceptedReport.DataReadAcceptedRequests);
                Assert.Equal(0L, acceptedReport.MemoryCompletedRequests);
                Assert.Equal(1L, acceptedReport.MemoryOutstandingRequests);
            });
        }
        finally
        {
            Processor.CurrentProfilingOptions = originalOptions;
        }
    }

    [Fact]
    public void PerformanceReportFormsExactMemoryTelemetryIntervalsWithoutMutatingSnapshots()
    {
        var baseline = new PerformanceReport
        {
            MemoryCycleTelemetrySchemaVersion = MemoryCycleTelemetrySnapshot.SchemaVersion,
            MemoryCycleTelemetryAvailable = true,
            MemoryControllerCycles = 10,
            MemoryAcceptedRequests = 3,
            MemoryCompletedRequests = 2,
            DataReadAcceptedRequests = 2,
            DataReadCompletedRequests = 1,
            DataWriteAcceptedRequests = 1,
            DataWriteCompletedRequests = 1,
            DataReadBytes = 8,
            CommittedDataWriteBytes = 4,
            InstructionFetchReadBytesTelemetryAvailable = true,
            InstructionFetchReadBytes = 32,
            MemoryTelemetryBaselineOutstandingRequests = 1,
            MemoryCanceledRequests = 1,
            MemoryConsumedCompletions = 2,
            MemoryOutstandingRequests = 1
        };
        var current = new PerformanceReport
        {
            MemoryCycleTelemetrySchemaVersion = MemoryCycleTelemetrySnapshot.SchemaVersion,
            MemoryCycleTelemetryAvailable = true,
            MemoryControllerCycles = 14,
            MemoryAcceptedRequests = 5,
            MemoryCompletedRequests = 4,
            DataReadAcceptedRequests = 3,
            DataReadCompletedRequests = 2,
            DataWriteAcceptedRequests = 2,
            DataWriteCompletedRequests = 2,
            DataReadBytes = 16,
            CommittedDataWriteBytes = 8,
            InstructionFetchReadBytesTelemetryAvailable = true,
            InstructionFetchReadBytes = 56,
            MemoryTelemetryBaselineOutstandingRequests = 1,
            MemoryCanceledRequests = 2,
            MemoryConsumedCompletions = 4,
            MemoryOutstandingRequests = 2
        };

        PerformanceReport interval = current.CreateMemoryCycleTelemetryIntervalSince(baseline);

        Assert.True(interval.MemoryCycleTelemetryAvailable);
        Assert.Equal(4, interval.MemoryControllerCycles);
        Assert.Equal(2, interval.MemoryAcceptedRequests);
        Assert.Equal(2, interval.MemoryCompletedRequests);
        Assert.Equal(1, interval.DataReadAcceptedRequests);
        Assert.Equal(1, interval.DataReadCompletedRequests);
        Assert.Equal(1, interval.DataWriteAcceptedRequests);
        Assert.Equal(1, interval.DataWriteCompletedRequests);
        Assert.Equal(8, interval.DataReadBytes);
        Assert.Equal(4, interval.CommittedDataWriteBytes);
        Assert.True(interval.InstructionFetchReadBytesTelemetryAvailable);
        Assert.Equal(24, interval.InstructionFetchReadBytes);
        Assert.Equal(1, interval.MemoryTelemetryBaselineOutstandingRequests);
        Assert.Equal(1, interval.MemoryCanceledRequests);
        Assert.Equal(2, interval.MemoryConsumedCompletions);
        Assert.Equal(2, interval.MemoryOutstandingRequests);
        Assert.Equal(14, current.MemoryControllerCycles);
        Assert.Equal(10, baseline.MemoryControllerCycles);
    }

    [Fact]
    public void PerformanceReportMarksCounterRegressionAndSchemaChangeUnavailable()
    {
        var baseline = new PerformanceReport
        {
            MemoryCycleTelemetrySchemaVersion = MemoryCycleTelemetrySnapshot.SchemaVersion,
            MemoryCycleTelemetryAvailable = true,
            MemoryControllerCycles = 10,
            MemoryAcceptedRequests = 4,
            InstructionFetchReadBytesTelemetryAvailable = true,
            InstructionFetchReadBytes = 40
        };
        var resetCurrent = new PerformanceReport
        {
            MemoryCycleTelemetrySchemaVersion = MemoryCycleTelemetrySnapshot.SchemaVersion,
            MemoryCycleTelemetryAvailable = true,
            MemoryControllerCycles = 1,
            MemoryAcceptedRequests = 1,
            InstructionFetchReadBytesTelemetryAvailable = true,
            InstructionFetchReadBytes = 4
        };
        var schemaChanged = new PerformanceReport
        {
            MemoryCycleTelemetrySchemaVersion = "memory-cycle-telemetry-vNext",
            MemoryCycleTelemetryAvailable = true,
            MemoryControllerCycles = 11,
            MemoryAcceptedRequests = 5
        };

        PerformanceReport resetInterval = resetCurrent.CreateMemoryCycleTelemetryIntervalSince(baseline);
        PerformanceReport schemaInterval = schemaChanged.CreateMemoryCycleTelemetryIntervalSince(baseline);

        Assert.False(resetInterval.MemoryCycleTelemetryAvailable);
        Assert.Equal(0, resetInterval.MemoryControllerCycles);
        Assert.Equal(0, resetInterval.MemoryAcceptedRequests);
        Assert.False(resetInterval.InstructionFetchReadBytesTelemetryAvailable);
        Assert.Equal(0, resetInterval.InstructionFetchReadBytes);
        Assert.False(schemaInterval.MemoryCycleTelemetryAvailable);
        Assert.Equal(0, schemaInterval.MemoryControllerCycles);
    }

    [Fact]
    public void AdditiveDiagnosticIntervalRebasesEveryConsumedSharedCumulativeProducer()
    {
        string[] cumulativeProperties =
        [
            nameof(PerformanceReport.TotalBursts),
            nameof(PerformanceReport.TotalBytesTransferred),
            nameof(PerformanceReport.NopAvoided),
            nameof(PerformanceReport.NopDueToNoClassCapacity),
            nameof(PerformanceReport.NopDueToPinnedConstraint),
            nameof(PerformanceReport.NopDueToResourceConflict),
            nameof(PerformanceReport.NopDueToDynamicState),
            nameof(PerformanceReport.ClassFlexibleInjects),
            nameof(PerformanceReport.HardPinnedInjects),
            nameof(PerformanceReport.EligibilityMaskedCycles),
            nameof(PerformanceReport.EligibilityMaskedReadyCandidates),
            nameof(PerformanceReport.PhaseCertificateReadyHits),
            nameof(PerformanceReport.PhaseCertificateReadyMisses),
            nameof(PerformanceReport.EstimatedPhaseCertificateChecksSaved),
            nameof(PerformanceReport.PhaseCertificateInvalidations),
            nameof(PerformanceReport.PhaseCertificateMutationInvalidations),
            nameof(PerformanceReport.PhaseCertificatePhaseMismatchInvalidations),
            nameof(PerformanceReport.SmtOwnerContextGuardRejects),
            nameof(PerformanceReport.SmtDomainGuardRejects),
            nameof(PerformanceReport.SmtBoundaryGuardRejects),
            nameof(PerformanceReport.SmtSharedResourceCertificateRejects),
            nameof(PerformanceReport.SmtRegisterGroupCertificateRejects),
            nameof(PerformanceReport.SmtLegalityRejectByAluClass),
            nameof(PerformanceReport.SmtLegalityRejectByLsuClass),
            nameof(PerformanceReport.SmtLegalityRejectByDmaStreamClass),
            nameof(PerformanceReport.SmtLegalityRejectByBranchControl),
            nameof(PerformanceReport.SmtLegalityRejectBySystemSingleton),
            nameof(PerformanceReport.L1BypassHits),
            nameof(PerformanceReport.ForegroundWarmAttempts),
            nameof(PerformanceReport.ForegroundWarmSuccesses),
            nameof(PerformanceReport.ForegroundWarmReuseHits),
            nameof(PerformanceReport.ForegroundBypassHits),
            nameof(PerformanceReport.AssistWarmAttempts),
            nameof(PerformanceReport.AssistWarmSuccesses),
            nameof(PerformanceReport.AssistWarmReuseHits),
            nameof(PerformanceReport.AssistBypassHits),
            nameof(PerformanceReport.StreamWarmTranslationRejects),
            nameof(PerformanceReport.StreamWarmBackendRejects),
            nameof(PerformanceReport.AssistWarmResidentBudgetRejects),
            nameof(PerformanceReport.AssistWarmLoadingBudgetRejects),
            nameof(PerformanceReport.AssistWarmNoVictimRejects)
        ];

        foreach (string propertyName in cumulativeProperties)
        {
            var baseline = new PerformanceReport();
            var current = new PerformanceReport();
            baseline.GetType().GetProperty(propertyName)!.SetValue(baseline, 10L);
            current.GetType().GetProperty(propertyName)!.SetValue(current, 15L);

            PerformanceReport interval = current.CreateAdditiveDiagnosticIntervalSince(baseline);

            Assert.Equal(5L, interval.GetType().GetProperty(propertyName)!.GetValue(interval));
            Assert.Equal(15L, current.GetType().GetProperty(propertyName)!.GetValue(current));
        }
    }

    [Fact]
    public void AdditiveDiagnosticIntervalFailsClosedForNumericOnlyCounterRegression()
    {
        var baseline = new PerformanceReport { ForegroundWarmAttempts = 7 };
        var current = new PerformanceReport { ForegroundWarmAttempts = 2 };

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => current.CreateAdditiveDiagnosticIntervalSince(baseline));

        Assert.Contains(nameof(PerformanceReport.ForegroundWarmAttempts), exception.Message, StringComparison.Ordinal);
        Assert.Contains("no availability carrier", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TestAssemblerPerformanceConsumersHaveClosedWorldAggregationPolicies()
    {
        string root = FindRepositoryRoot();
        string consumerSource = Read(root, "TestAssemblerConsoleApps/SimpleAsmApp.Metrics.cs");
        string aggregationSource = Read(root, "TestAssemblerConsoleApps/SimpleAsmApp.cs");
        string intervalSource = Read(root,
            "HybridCPU_ISE/NonRTL/Processor/Performance/PerformanceReport.DiagnosticIntervals.cs");

        string[] additive = SplitNames("""
            TotalBursts TotalBytesTransferred NopAvoided NopDueToNoClassCapacity
            NopDueToPinnedConstraint NopDueToResourceConflict NopDueToDynamicState
            ClassFlexibleInjects HardPinnedInjects EligibilityMaskedCycles
            EligibilityMaskedReadyCandidates PhaseCertificateReadyHits
            PhaseCertificateReadyMisses EstimatedPhaseCertificateChecksSaved
            PhaseCertificateInvalidations PhaseCertificateMutationInvalidations
            PhaseCertificatePhaseMismatchInvalidations SmtOwnerContextGuardRejects
            SmtDomainGuardRejects SmtBoundaryGuardRejects
            SmtSharedResourceCertificateRejects SmtRegisterGroupCertificateRejects
            SmtLegalityRejectByAluClass SmtLegalityRejectByLsuClass
            SmtLegalityRejectByDmaStreamClass SmtLegalityRejectByBranchControl
            SmtLegalityRejectBySystemSingleton L1BypassHits ForegroundWarmAttempts
            ForegroundWarmSuccesses ForegroundWarmReuseHits ForegroundBypassHits
            AssistWarmAttempts AssistWarmSuccesses AssistWarmReuseHits AssistBypassHits
            StreamWarmTranslationRejects StreamWarmBackendRejects
            AssistWarmResidentBudgetRejects AssistWarmLoadingBudgetRejects
            AssistWarmNoVictimRejects
            """);
        string[] lastValue = SplitNames("""
            LastEligibilityRequestedMask LastEligibilityNormalizedMask
            LastEligibilityReadyPortMask LastEligibilityVisibleReadyMask
            LastEligibilityMaskedReadyMask LastSmtLegalityRejectKind
            LastSmtLegalityAuthoritySource MemoryTelemetryBaselineOutstandingRequests
            MemoryOutstandingRequests
            """);
        string[] memoryProjection = SplitNames("""
            MemoryCycleTelemetrySchemaVersion MemoryCycleTelemetryAvailable
            MemoryControllerCycles MemoryReadServiceCycles
            MemoryStoreReadinessServiceCycles MemoryCompletionPublicationCycles
            MemoryAcceptedRequests MemoryCompletedRequests DataReadAcceptedRequests
            DataReadCompletedRequests DataWriteAcceptedRequests DataWriteCompletedRequests
            DataReadBytes CommittedDataWriteBytes InstructionFetchReadBytesTelemetryAvailable
            InstructionFetchReadBytes InstructionFetchRequestTelemetryAvailable
            MemoryQueueFullRejects MemoryBankConflictRejectTelemetryAvailable
            MemoryBankConflictRejects MemoryTelemetryBaselineOutstandingRequests
            MemoryCanceledRequests MemoryConsumedCompletions MemoryOutstandingRequests
            """);

        string[] actual = System.Text.RegularExpressions.Regex
            .Matches(consumerSource, @"\bperformance\.([A-Za-z_][A-Za-z0-9_]*)")
            .Select(match => match.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        string[] classified = additive
            .Concat(lastValue)
            .Concat(memoryProjection)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(72, actual.Length);
        Assert.Equal(actual, classified);
        foreach (string propertyName in additive)
        {
            Assert.Contains(
                $"interval.{propertyName} = SubtractMonotonicOrThrow",
                intervalSource,
                StringComparison.Ordinal);
        }

        foreach (string propertyName in lastValue)
        {
            Assert.Contains(
                $"nameof(PerformanceReport.{propertyName})",
                aggregationSource,
                StringComparison.Ordinal);
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
            "TestAssemblerConsoleApps/TimingMemoryReport.cs");
        string program = Read(root, "TestAssemblerConsoleApps/Program.cs");
        string controller = Read(root,
            "TestAssemblerConsoleApps/DiagnosticRunController.cs");
        string manifest = Read(root,
            "TestAssemblerConsoleApps/DiagnosticArtifactManifest.cs");
        string pipelineStageFlow = Read(root,
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/ExecutionFlow/StageFlow/CPU_Core.PipelineExecution.StageFlow.cs");

        Assert.Contains("MemoryCycleTelemetryAvailable", performanceReport, StringComparison.Ordinal);
        Assert.Contains("CreateMemoryCycleTelemetryIntervalSince", performanceReport, StringComparison.Ordinal);
        Assert.Contains("GetTelemetrySnapshot()", performanceProducer, StringComparison.Ordinal);
        Assert.Contains("timing-memory-report/v3", consoleReport, StringComparison.Ordinal);
        Assert.Contains("post-ref1-timing-memory-v2", consoleReport, StringComparison.Ordinal);
        Assert.Contains("new(\"Available\", value", consoleReport, StringComparison.Ordinal);
        Assert.Contains("new(\"Unavailable\", null", consoleReport, StringComparison.Ordinal);
        Assert.Contains("metrics.MemoryStalls <= metrics.StallCycles", consoleReport, StringComparison.Ordinal);
        Assert.Contains("metrics.StallCycles - metrics.MemoryStalls", consoleReport, StringComparison.Ordinal);
        Assert.Contains("NonMemoryStallCycles: nonMemoryStallCycles", consoleReport, StringComparison.Ordinal);
        Assert.Contains("WAW is an event counter, not a general cycle bucket", consoleReport, StringComparison.Ordinal);
        Assert.Contains("pipeCtrl.StallCycles++;", pipelineStageFlow, StringComparison.Ordinal);
        Assert.Contains("if (stallDecision.CountMemoryStall)", pipelineStageFlow, StringComparison.Ordinal);
        Assert.Contains("pipeCtrl.MemoryStalls++;", pipelineStageFlow, StringComparison.Ordinal);
        Assert.Contains("BankConflictRejects", consoleReport, StringComparison.Ordinal);
        Assert.Contains("InstructionFetchAcceptedRequests", consoleReport, StringComparison.Ordinal);
        Assert.Contains("CanceledMemoryRequests", consoleReport, StringComparison.Ordinal);
        Assert.Contains("ConsumedMemoryCompletions", consoleReport, StringComparison.Ordinal);
        Assert.Contains("OutstandingMemoryRequests", consoleReport, StringComparison.Ordinal);
        Assert.Contains("RequestIdentityBalanceDisposition", consoleReport, StringComparison.Ordinal);
        Assert.Contains("timing_memory_report.json", consoleReport, StringComparison.Ordinal);
        Assert.Contains("post_ref1_timing_memory_report.json", consoleReport, StringComparison.Ordinal);
        Assert.Contains("TimingMemoryReport.ManifestKey", program, StringComparison.Ordinal);
        Assert.Contains("TimingMemoryReport.LegacyManifestKey", program, StringComparison.Ordinal);
        Assert.Contains("RenderTelemetryMetric(report.NonMemoryStallCyclesMetric)", program, StringComparison.Ordinal);
        Assert.Contains("TimingMemoryReport.ManifestKey", controller, StringComparison.Ordinal);
        Assert.Contains("TimingMemoryReport.LegacyManifestKey", controller, StringComparison.Ordinal);
        Assert.Contains("diagnostic-run-manifest/v1", manifest, StringComparison.Ordinal);
        Assert.Contains("CreateAdditiveDiagnosticIntervalSince(previousPerformanceSnapshot)",
            Read(root, "TestAssemblerConsoleApps/SimpleAsmApp.cs"), StringComparison.Ordinal);
        Assert.Contains("IsMemoryTelemetryAvailability(property.Name)",
            Read(root, "TestAssemblerConsoleApps/SimpleAsmApp.cs"), StringComparison.Ordinal);
        Assert.Contains("IsLastValueDiagnostic(property.Name)",
            Read(root, "TestAssemblerConsoleApps/SimpleAsmApp.cs"), StringComparison.Ordinal);
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

    private static string[] SplitNames(string value) =>
        value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

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
