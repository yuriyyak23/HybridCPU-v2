using HybridCPU_ISE.Core;
using Xunit;
using YAKSys_Hybrid_CPU;

namespace HybridCPU_ISE.Tests
{
    public class Phase4PerformanceReportTests
    {
        [Fact]
        public void WhenPhase4MetricsPresentThenSummaryIncludesDeterminismTaxAndIsolationStress()
        {
            var report = new PerformanceReport
            {
                DeterminismReferenceOpportunitySlots = 8,
                DeterminismReplayEligibleSlots = 5,
                DeterminismMaskedSlots = 3,
                DeterminismEstimatedLostSlots = 2,
                DeterminismConstrainedCycles = 1,
                DomainIsolationProbeAttempts = 10,
                DomainIsolationBlockedAttempts = 6,
                DomainIsolationCrossDomainBlocks = 4,
                DomainIsolationKernelToUserBlocks = 2
            };

            string summary = report.GeneratePhase4EvidenceSummary(new ReplayEnvelopeReport(
                isWithinEnvelope: true,
                kind: ReplayEnvelopeKind.TimingNoise,
                comparedEvents: 4,
                comparedReplayEvents: 4,
                comparedEnvelopeFields: 12,
                mismatchThreadId: -1,
                mismatchCycle: -1,
                mismatchField: string.Empty,
                expectedValue: string.Empty,
                actualValue: string.Empty,
                allowedEnvelope: string.Empty));

            Assert.Contains("Phase 4 Evidence Baseline", summary);
            Assert.Contains("Determinism Tax", summary);
            Assert.Contains("Domain Isolation Stress", summary);
            Assert.Contains("Determinism Envelope", summary);
        }

        [Fact]
        public void WhenPhase4MetricsAbsentThenGeneralSummaryOmitsPhase4Section()
        {
            var report = new PerformanceReport();

            string summary = report.GenerateSummary();

            Assert.DoesNotContain("Phase 4 Evidence Baseline", summary);
        }

        [Fact]
        public void WhenPhase4RegressionSnapshotGeneratedThenIncludesPerfTraceAndEnvelopeContours()
        {
            var report = new PerformanceReport
            {
                DeterminismReferenceOpportunitySlots = 8,
                DeterminismReplayEligibleSlots = 6,
                DeterminismMaskedSlots = 2,
                DeterminismEstimatedLostSlots = 2,
                DeterminismConstrainedCycles = 1,
                DomainIsolationProbeAttempts = 12,
                DomainIsolationBlockedAttempts = 5,
                DomainIsolationCrossDomainBlocks = 3,
                DomainIsolationKernelToUserBlocks = 2
            };

            string snapshot = report.GeneratePhase4RegressionSnapshot(
                new ReplayTraceEvidenceSummary(
                    totalEvents: 12,
                    replayPhaseEvents: 8,
                    denseTimelineSamples: 8,
                    writeBackSamples: 2,
                    replayEpochCount: 2,
                    totalEpochLength: 10,
                    stableDonorSlotSamples: 12,
                    totalReplaySlotSamples: 16,
                    phaseCertificateReadyHits: 5,
                    phaseCertificateReadyMisses: 1,
                    estimatedPhaseCertificateChecksSaved: 7,
                    phaseCertificateInvalidations: 1,
                    invalidationObservations: 1,
                    mutationInvalidationObservations: 0,
                    phaseMismatchInvalidationObservations: 1,
                    eligibilityMaskedCycles: 0,
                    eligibilityMaskedReadyCandidates: 0,
                    invalidationBursts: 1,
                    longestInvalidationBurst: 1),
                new ReplayDeterminismReport(
                    isDeterministic: true,
                    comparedEvents: 12,
                    comparedReplayEvents: 8,
                    comparedTimelineSamples: 8,
                    comparedInvalidationEvents: 1,
                    comparedEpochs: 2,
                    mismatchThreadId: -1,
                    mismatchCycle: -1,
                    mismatchField: string.Empty,
                    expectedValue: string.Empty,
                    actualValue: string.Empty),
                new ReplayEnvelopeReport(
                    isWithinEnvelope: true,
                    kind: ReplayEnvelopeKind.ResourcePressure,
                    comparedEvents: 12,
                    comparedReplayEvents: 8,
                    comparedEnvelopeFields: 20,
                    mismatchThreadId: -1,
                    mismatchCycle: -1,
                    mismatchField: string.Empty,
                    expectedValue: string.Empty,
                    actualValue: string.Empty,
                    allowedEnvelope: string.Empty));

            Assert.Contains("Phase 4 Regression Snapshot", snapshot);
            Assert.Contains("Opportunity Surface", snapshot);
            Assert.Contains("Isolation Surface", snapshot);
            Assert.Contains("Trace Evidence", snapshot);
            Assert.Contains("Envelope Result", snapshot);
            Assert.Contains("Snapshot Verdict: baseline stable, envelope within bounds", snapshot);
        }
    }
}
