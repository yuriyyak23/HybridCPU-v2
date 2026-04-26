using HybridCPU_ISE.Core;
using Xunit;
using YAKSys_Hybrid_CPU;

namespace HybridCPU_ISE.Tests.EvaluationAndMetrics
{
    public class PerformanceReportPhase06EligibilityTests
    {
        [Fact]
        public void WhenPhase1ValidationSummaryHasEligibilityTelemetryThenSummaryIncludesEligibilityGate()
        {
            var report = new PerformanceReport
            {
                EligibilityMaskedCycles = 4,
                EligibilityMaskedReadyCandidates = 10
            };

            string summary = report.GeneratePhase1ValidationSummary();

            Assert.Contains("Eligibility Gate", summary);
            Assert.Contains("masked cycles 4", summary);
            Assert.Contains("masked ready candidates 10", summary);
        }

        [Fact]
        public void WhenPhase3ExecutionSummaryHasEligibilitySnapshotThenSummaryIncludesSnapshotMasks()
        {
            var report = new PerformanceReport
            {
                EligibilityMaskedCycles = 2,
                EligibilityMaskedReadyCandidates = 3,
                LastEligibilityRequestedMask = 0x0F,
                LastEligibilityNormalizedMask = 0x05,
                LastEligibilityReadyPortMask = 0x07,
                LastEligibilityVisibleReadyMask = 0x05,
                LastEligibilityMaskedReadyMask = 0x02
            };

            string summary = report.GeneratePhase3ExecutionSummary();

            Assert.Contains("Eligibility Snapshot", summary);
            Assert.Contains("requested 0x0F", summary);
            Assert.Contains("masked 0x02", summary);
        }

        [Fact]
        public void WhenEligibilityTelemetryDivergesThenCorrelationFlagsEligibilityMismatch()
        {
            var report = new PerformanceReport
            {
                EligibilityMaskedCycles = 1,
                EligibilityMaskedReadyCandidates = 2
            };

            var traceSummary = new ReplayTraceEvidenceSummary(
                totalEvents: 1,
                replayPhaseEvents: 0,
                denseTimelineSamples: 0,
                writeBackSamples: 0,
                replayEpochCount: 0,
                totalEpochLength: 0,
                stableDonorSlotSamples: 0,
                totalReplaySlotSamples: 0,
                phaseCertificateReadyHits: 0,
                phaseCertificateReadyMisses: 0,
                estimatedPhaseCertificateChecksSaved: 0,
                phaseCertificateInvalidations: 0,
                invalidationObservations: 0,
                mutationInvalidationObservations: 0,
                phaseMismatchInvalidationObservations: 0,
                eligibilityMaskedCycles: 2,
                eligibilityMaskedReadyCandidates: 3,
                invalidationBursts: 0,
                longestInvalidationBurst: 0);
            var determinismReport = new ReplayDeterminismReport(
                isDeterministic: true,
                comparedEvents: 1,
                comparedReplayEvents: 0,
                comparedTimelineSamples: 0,
                comparedInvalidationEvents: 0,
                comparedEpochs: 0,
                mismatchThreadId: -1,
                mismatchCycle: -1,
                mismatchField: string.Empty,
                expectedValue: string.Empty,
                actualValue: string.Empty);

            Phase1EvidenceCorrelationReport correlation = report.CorrelatePhase1Evidence(traceSummary, determinismReport);

            Assert.False(correlation.EligibilityAligned);
            Assert.Equal(1, correlation.MismatchCount);
        }
    }
}
