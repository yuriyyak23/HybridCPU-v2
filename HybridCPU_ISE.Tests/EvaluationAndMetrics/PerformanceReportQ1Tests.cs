using HybridCPU_ISE.Core;
using Xunit;
using YAKSys_Hybrid_CPU;

namespace HybridCPU_ISE.Tests.EvaluationAndMetrics
{
    /// <summary>
    /// PR 4 (Part 2) — PerformanceReport Q1 Counters Tests
    ///
    /// Tests the Q1 Review performance counters in PerformanceReport:
    /// - BankContentionStalls (Q1 Review §2)
    /// - FspPipelineLatencyCycles (Q1 Review §1)
    /// - L1BypassHits (Q1 Review §5)
    /// - GenerateSummary includes new counters
    /// - ExportToCSV includes new counters
    ///
    /// Target file: PerformanceReport.cs
    /// Namespace: HybridCPU_ISE.Tests.EvaluationAndMetrics
    /// </summary>
    public class PerformanceReportQ1Tests
    {
        #region 4.3 PerformanceReport.BankContentionStalls

        [Fact]
        public void WhenReportCreatedThenBankContentionStallsIsZero()
        {
            // Arrange & Act
            var report = new PerformanceReport();

            // Assert
            Assert.Equal(0, report.BankContentionStalls);
        }

        [Fact]
        public void WhenBankContentionStallsSetThenValueReflects()
        {
            // Arrange
            var report = new PerformanceReport();

            // Act
            report.BankContentionStalls = 42;

            // Assert
            Assert.Equal(42, report.BankContentionStalls);
        }

        [Fact]
        public void WhenBankContentionStallsNonZeroThenInSummary()
        {
            // Arrange
            var report = new PerformanceReport
            {
                BankContentionStalls = 1234,
                SuccessfulInjections = 1 // Needed to trigger scheduler section
            };

            // Act
            string summary = report.GenerateSummary();

            // Assert
            Assert.Contains("Bank Contention Stalls", summary);
            Assert.Contains("1,234", summary); // Formatted with comma separator
        }

        [Fact]
        public void WhenBankContentionStallsThenInCSV()
        {
            // Arrange
            var report = new PerformanceReport
            {
                BankContentionStalls = 5678
            };

            // Act
            string csv = report.ExportToCSV();

            // Assert
            Assert.Contains("Scheduler,BankContentionStalls,5678", csv);
        }

        #endregion

        #region 4.4 PerformanceReport.FspPipelineLatencyCycles

        [Fact]
        public void WhenReportCreatedThenFspPipelineLatencyCyclesIsZero()
        {
            // Arrange & Act
            var report = new PerformanceReport();

            // Assert
            Assert.Equal(0, report.FspPipelineLatencyCycles);
        }

        [Fact]
        public void WhenFspPipelineLatencyCyclesSetThenValueReflects()
        {
            // Arrange
            var report = new PerformanceReport();

            // Act
            report.FspPipelineLatencyCycles = 987;

            // Assert
            Assert.Equal(987, report.FspPipelineLatencyCycles);
        }

        [Fact]
        public void WhenFspPipelineLatencyCyclesNonZeroThenInSummary()
        {
            // Arrange
            var report = new PerformanceReport
            {
                FspPipelineLatencyCycles = 4567,
                SuccessfulInjections = 1 // Needed to trigger scheduler section
            };

            // Act
            string summary = report.GenerateSummary();

            // Assert
            Assert.Contains("FSP Pipeline Latency Cycles", summary);
            Assert.Contains("4,567", summary);
        }

        [Fact]
        public void WhenFspPipelineLatencyCyclesThenInCSV()
        {
            // Arrange
            var report = new PerformanceReport
            {
                FspPipelineLatencyCycles = 3456
            };

            // Act
            string csv = report.ExportToCSV();

            // Assert
            Assert.Contains("Scheduler,FspPipelineLatencyCycles,3456", csv);
        }

        #endregion

        #region 4.5 PerformanceReport.L1BypassHits

        [Fact]
        public void WhenReportCreatedThenL1BypassHitsIsZero()
        {
            // Arrange & Act
            var report = new PerformanceReport();

            // Assert
            Assert.Equal(0, report.L1BypassHits);
        }

        [Fact]
        public void WhenL1BypassHitsSetThenValueReflects()
        {
            // Arrange
            var report = new PerformanceReport();

            // Act
            report.L1BypassHits = 1500;

            // Assert
            Assert.Equal(1500, report.L1BypassHits);
        }

        [Fact]
        public void WhenL1BypassHitsZeroThenNotInSummary()
        {
            // Arrange
            var report = new PerformanceReport
            {
                L1BypassHits = 0
            };

            // Act
            string summary = report.GenerateSummary();

            // Assert - Section should not appear when L1BypassHits is 0
            Assert.DoesNotContain("Stream Register File", summary);
        }

        [Fact]
        public void WhenL1BypassHitsNonZeroThenInSummary()
        {
            // Arrange
            var report = new PerformanceReport
            {
                L1BypassHits = 2500
            };

            // Act
            string summary = report.GenerateSummary();

            // Assert
            Assert.Contains("Stream Register File (Q1 §5)", summary);
            Assert.Contains("L1 Bypass Hits", summary);
            Assert.Contains("2,500", summary);
        }

        [Fact]
        public void WhenL1BypassHitsThenInCSV()
        {
            // Arrange
            var report = new PerformanceReport
            {
                L1BypassHits = 9876
            };

            // Act
            string csv = report.ExportToCSV();

            // Assert
            Assert.Contains("StreamRegFile,L1BypassHits,9876", csv);
        }

        [Fact]
        public void WhenStreamIngressWarmTelemetryPresentThenSummaryIncludesDetailedWarmOutcomes()
        {
            // Arrange
            var report = new PerformanceReport
            {
                L1BypassHits = 5,
                ForegroundWarmAttempts = 2,
                ForegroundWarmSuccesses = 2,
                ForegroundWarmReuseHits = 1,
                ForegroundBypassHits = 3,
                AssistWarmAttempts = 1,
                AssistWarmSuccesses = 1,
                AssistBypassHits = 2,
                StreamWarmTranslationRejects = 4,
                AssistWarmResidentBudgetRejects = 1
            };

            // Act
            string summary = report.GenerateSummary();

            // Assert
            Assert.True(report.HasStreamIngressWarmTelemetry);
            Assert.Contains("Foreground Warm: 2/2 success, 1 reuse, 3 bypass", summary);
            Assert.Contains("Assist Warm: 1/1 success, 0 reuse, 2 bypass", summary);
            Assert.Contains("Warm Rejects: translation 4, backend 0, resident-budget 1", summary);
        }

        [Fact]
        public void WhenOnlyWarmRejectTelemetryPresentThenSummaryStillIncludesStreamRegisterSection()
        {
            // Arrange
            var report = new PerformanceReport
            {
                StreamWarmTranslationRejects = 1
            };

            // Act
            string summary = report.GenerateSummary();

            // Assert
            Assert.True(report.HasStreamIngressWarmTelemetry);
            Assert.Contains("Stream Register File", summary);
            Assert.Contains("Warm Rejects: translation 1", summary);
        }

        #endregion

        #region 4.6 GenerateSummary Integration

        [Fact]
        public void WhenAllQ1CountersNonZeroThenAllInSummary()
        {
            // Arrange
            var report = new PerformanceReport
            {
                BankContentionStalls = 100,
                FspPipelineLatencyCycles = 200,
                L1BypassHits = 300,
                SuccessfulInjections = 1 // Trigger scheduler section
            };

            // Act
            string summary = report.GenerateSummary();

            // Assert - All three Q1 counters should appear
            Assert.Contains("Bank Contention Stalls", summary);
            Assert.Contains("FSP Pipeline Latency Cycles", summary);
            Assert.Contains("L1 Bypass Hits", summary);
        }

        [Fact]
        public void WhenPhase1ReplayMetricsNonZeroThenSummaryIncludesPhaseTelemetry()
        {
            // Arrange
            var report = new PerformanceReport
            {
                SuccessfulInjections = 1,
                ReplayEpochCount = 12,
                AverageReplayEpochLength = 3.5,
                StableDonorSlotRatio = 0.5,
                ReplayAwareCycles = 9,
                PhaseCertificateReadyHits = 7,
                PhaseCertificateReadyMisses = 2,
                EstimatedPhaseCertificateChecksSaved = 21,
                PhaseCertificateInvalidations = 5,
                PhaseCertificateMutationInvalidations = 3,
                PhaseCertificatePhaseMismatchInvalidations = 2,
                DeterministicReplayTransitions = 12
            };

            // Act
            string summary = report.GenerateSummary();

            // Assert
            Assert.Contains("Phase 1 Validation Contour", summary);
            Assert.Contains("Replay Evidence", summary);
            Assert.Contains("Dense Timeline", summary);
            Assert.Contains("Usefulness", summary);
            Assert.Contains("Invalidations", summary);
        }

        [Fact]
        public void WhenOnlyPhase1ValidationTelemetryPresentThenSummaryIncludesDedicatedValidationSection()
        {
            // Arrange
            var report = new PerformanceReport
            {
                ReplayEpochCount = 8,
                AverageReplayEpochLength = 4.0,
                StableDonorSlotRatio = 0.75,
                ReplayAwareCycles = 16,
                PhaseCertificateReadyHits = 6,
                PhaseCertificateReadyMisses = 2,
                EstimatedPhaseCertificateChecksSaved = 24,
                PhaseCertificateInvalidations = 4,
                PhaseCertificateMutationInvalidations = 1,
                PhaseCertificatePhaseMismatchInvalidations = 3,
                DeterministicReplayTransitions = 8
            };

            // Act
            string summary = report.GenerateSummary();

            // Assert
            Assert.Contains("Phase 1 Validation Contour", summary);
            Assert.Contains("Replay Evidence", summary);
            Assert.Contains("Usefulness", summary);
            Assert.Contains("Invalidations", summary);
        }

        [Fact]
        public void WhenPhase1ValidationSummaryGeneratedThenUsefulnessRatesAreReadable()
        {
            // Arrange
            var report = new PerformanceReport
            {
                ReplayEpochCount = 5,
                AverageReplayEpochLength = 3.2,
                StableDonorSlotRatio = 0.875,
                ReplayAwareCycles = 10,
                PhaseCertificateReadyHits = 9,
                PhaseCertificateReadyMisses = 3,
                EstimatedPhaseCertificateChecksSaved = 27,
                PhaseCertificateInvalidations = 2,
                PhaseCertificateMutationInvalidations = 1,
                PhaseCertificatePhaseMismatchInvalidations = 1,
                DeterministicReplayTransitions = 5
            };

            // Act
            string validationSummary = report.GeneratePhase1ValidationSummary();

            // Assert
            Assert.Contains("75.00%", validationSummary);
            Assert.Contains("20.00%", validationSummary);
            Assert.Contains("27 checks saved", validationSummary);
        }

        [Fact]
        public void WhenPhase1EvidenceCorrelatesThenRegressionBaselineIsAligned()
        {
            // Arrange
            var report = new PerformanceReport
            {
                ReplayEpochCount = 2,
                AverageReplayEpochLength = 4.5,
                StableDonorSlotRatio = 0.625,
                PhaseCertificateReadyHits = 9,
                PhaseCertificateReadyMisses = 3,
                EstimatedPhaseCertificateChecksSaved = 27,
                PhaseCertificateInvalidations = 2,
                DeterministicReplayTransitions = 2
            };

            var traceSummary = new ReplayTraceEvidenceSummary(
                totalEvents: 8,
                replayPhaseEvents: 8,
                denseTimelineSamples: 6,
                writeBackSamples: 2,
                replayEpochCount: 2,
                totalEpochLength: 9,
                stableDonorSlotSamples: 40,
                totalReplaySlotSamples: 64,
                phaseCertificateReadyHits: 9,
                phaseCertificateReadyMisses: 3,
                estimatedPhaseCertificateChecksSaved: 27,
                phaseCertificateInvalidations: 2,
                invalidationObservations: 4,
                mutationInvalidationObservations: 2,
                phaseMismatchInvalidationObservations: 2,
                eligibilityMaskedCycles: 0,
                eligibilityMaskedReadyCandidates: 0,
                invalidationBursts: 2,
                longestInvalidationBurst: 2);

            var determinismReport = new ReplayDeterminismReport(
                isDeterministic: true,
                comparedEvents: 8,
                comparedReplayEvents: 8,
                comparedTimelineSamples: 6,
                comparedInvalidationEvents: 4,
                comparedEpochs: 2,
                mismatchThreadId: -1,
                mismatchCycle: -1,
                mismatchField: string.Empty,
                expectedValue: string.Empty,
                actualValue: string.Empty);

            // Act
            Phase1EvidenceCorrelationReport correlation = report.CorrelatePhase1Evidence(traceSummary, determinismReport);
            string baselineSummary = report.GeneratePhase1RegressionBaselineSummary(traceSummary, determinismReport);

            // Assert
            Assert.True(correlation.IsAligned);
            Assert.Equal(0, correlation.MismatchCount);
            Assert.Contains("Phase 1 Regression Baseline", baselineSummary);
            Assert.Contains("Trace Evidence", baselineSummary);
            Assert.Contains("Repeated Runs", baselineSummary);
            Assert.Contains("Correlation: Aligned", baselineSummary);
        }

        [Fact]
        public void WhenPhase1EvidenceDivergesThenCorrelationReportFlagsMismatch()
        {
            // Arrange
            var report = new PerformanceReport
            {
                ReplayEpochCount = 2,
                AverageReplayEpochLength = 4.5,
                StableDonorSlotRatio = 0.625,
                PhaseCertificateReadyHits = 9,
                PhaseCertificateReadyMisses = 3,
                EstimatedPhaseCertificateChecksSaved = 27,
                PhaseCertificateInvalidations = 3,
                DeterministicReplayTransitions = 2
            };

            var traceSummary = new ReplayTraceEvidenceSummary(
                totalEvents: 8,
                replayPhaseEvents: 8,
                denseTimelineSamples: 6,
                writeBackSamples: 2,
                replayEpochCount: 2,
                totalEpochLength: 9,
                stableDonorSlotSamples: 40,
                totalReplaySlotSamples: 64,
                phaseCertificateReadyHits: 9,
                phaseCertificateReadyMisses: 3,
                estimatedPhaseCertificateChecksSaved: 27,
                phaseCertificateInvalidations: 2,
                invalidationObservations: 4,
                mutationInvalidationObservations: 2,
                phaseMismatchInvalidationObservations: 2,
                eligibilityMaskedCycles: 0,
                eligibilityMaskedReadyCandidates: 0,
                invalidationBursts: 2,
                longestInvalidationBurst: 2);

            var determinismReport = new ReplayDeterminismReport(
                isDeterministic: true,
                comparedEvents: 8,
                comparedReplayEvents: 8,
                comparedTimelineSamples: 6,
                comparedInvalidationEvents: 4,
                comparedEpochs: 2,
                mismatchThreadId: -1,
                mismatchCycle: -1,
                mismatchField: string.Empty,
                expectedValue: string.Empty,
                actualValue: string.Empty);

            // Act
            Phase1EvidenceCorrelationReport correlation = report.CorrelatePhase1Evidence(traceSummary, determinismReport);

            // Assert
            Assert.False(correlation.IsAligned);
            Assert.False(correlation.InvalidationsAligned);
            Assert.Equal(1, correlation.MismatchCount);
        }

        [Fact]
        public void WhenGenerateSummaryThenReturnsNonEmptyString()
        {
            // Arrange
            var report = new PerformanceReport();

            // Act
            string summary = report.GenerateSummary();

            // Assert
            Assert.NotNull(summary);
            Assert.NotEmpty(summary);
        }

        [Fact]
        public void WhenGenerateSummaryWithDataThenContainsFormattedOutput()
        {
            // Arrange
            var report = new PerformanceReport
            {
                TotalInstructions = 1000000,
                TotalCycles = 500000,
                SuccessfulInjections = 50000,
                BankContentionStalls = 1234
            };

            // Act
            string summary = report.GenerateSummary();

            // Assert
            Assert.Contains("Total Instructions", summary);
            Assert.Contains("IPC", summary);
            Assert.Contains("1,000,000", summary); // Check formatting
        }

        #endregion

        #region 4.7 ExportToCSV Integration

        [Fact]
        public void WhenAllQ1CountersNonZeroThenAllInCSV()
        {
            // Arrange
            var report = new PerformanceReport
            {
                BankContentionStalls = 111,
                FspPipelineLatencyCycles = 222,
                L1BypassHits = 333
            };

            // Act
            string csv = report.ExportToCSV();

            // Assert - All three Q1 counters should appear in CSV
            Assert.Contains("Scheduler,BankContentionStalls,111", csv);
            Assert.Contains("Scheduler,FspPipelineLatencyCycles,222", csv);
            Assert.Contains("StreamRegFile,L1BypassHits,333", csv);
        }

        [Fact]
        public void WhenStreamIngressWarmTelemetryPresentThenCsvIncludesDetailedWarmCounters()
        {
            // Arrange
            var report = new PerformanceReport
            {
                ForegroundWarmAttempts = 7,
                ForegroundWarmSuccesses = 6,
                ForegroundWarmReuseHits = 2,
                ForegroundBypassHits = 5,
                AssistWarmAttempts = 4,
                AssistWarmSuccesses = 3,
                AssistWarmReuseHits = 1,
                AssistBypassHits = 2,
                StreamWarmTranslationRejects = 9,
                StreamWarmBackendRejects = 8,
                AssistWarmResidentBudgetRejects = 7,
                AssistWarmLoadingBudgetRejects = 6,
                AssistWarmNoVictimRejects = 5
            };

            // Act
            string csv = report.ExportToCSV();

            // Assert
            Assert.Contains("StreamRegFile,ForegroundWarmAttempts,7", csv);
            Assert.Contains("StreamRegFile,ForegroundWarmSuccesses,6", csv);
            Assert.Contains("StreamRegFile,ForegroundWarmReuseHits,2", csv);
            Assert.Contains("StreamRegFile,ForegroundBypassHits,5", csv);
            Assert.Contains("StreamRegFile,AssistWarmAttempts,4", csv);
            Assert.Contains("StreamRegFile,AssistWarmSuccesses,3", csv);
            Assert.Contains("StreamRegFile,AssistWarmReuseHits,1", csv);
            Assert.Contains("StreamRegFile,AssistBypassHits,2", csv);
            Assert.Contains("StreamRegFile,StreamWarmTranslationRejects,9", csv);
            Assert.Contains("StreamRegFile,StreamWarmBackendRejects,8", csv);
            Assert.Contains("StreamRegFile,AssistWarmResidentBudgetRejects,7", csv);
            Assert.Contains("StreamRegFile,AssistWarmLoadingBudgetRejects,6", csv);
            Assert.Contains("StreamRegFile,AssistWarmNoVictimRejects,5", csv);
        }

        [Fact]
        public void WhenPhase1ReplayMetricsNonZeroThenCsvIncludesPhaseTelemetry()
        {
            // Arrange
            var report = new PerformanceReport
            {
                ReplayEpochCount = 12,
                AverageReplayEpochLength = 3.5,
                StableDonorSlotRatio = 0.5,
                ReplayAwareCycles = 9,
                PhaseCertificateReadyHits = 7,
                PhaseCertificateReadyMisses = 2,
                EstimatedPhaseCertificateChecksSaved = 21,
                PhaseCertificateInvalidations = 5,
                PhaseCertificateMutationInvalidations = 3,
                PhaseCertificatePhaseMismatchInvalidations = 2,
                DeterministicReplayTransitions = 12
            };

            // Act
            string csv = report.ExportToCSV();

            // Assert
            Assert.Contains("Scheduler,ReplayEpochCount,12", csv);
            Assert.Contains("Scheduler,AverageReplayEpochLength,3.5", csv);
            Assert.Contains("Scheduler,PhaseCertificateReadyHits,7", csv);
            Assert.Contains("Scheduler,PhaseCertificateReuseHitRate,0.7777777777777778", csv);
            Assert.Contains("Scheduler,PhaseCertificateInvalidations,5", csv);
            Assert.Contains("Scheduler,PhaseCertificateInvalidationRate,0.5555555555555556", csv);
            Assert.Contains("Scheduler,DeterministicReplayTransitions,12", csv);
        }

        [Fact]
        public void WhenExportToCSVThenHasHeader()
        {
            // Arrange
            var report = new PerformanceReport();

            // Act
            string csv = report.ExportToCSV();

            // Assert
            Assert.StartsWith("Category,Metric,Value", csv);
        }

        [Fact]
        public void WhenExportToCSVThenContainsMultipleCategories()
        {
            // Arrange
            var report = new PerformanceReport
            {
                TotalBursts = 100,
                TotalInstructions = 1000,
                VectorOperations = 50,
                CompletedDmaTransfers = 10
            };

            // Act
            string csv = report.ExportToCSV();

            // Assert - Should have data from different categories
            Assert.Contains("Memory,", csv);
            Assert.Contains("Pipeline,", csv);
            Assert.Contains("Vector,", csv);
            Assert.Contains("DMA,", csv);
        }

        #endregion
    }
}
