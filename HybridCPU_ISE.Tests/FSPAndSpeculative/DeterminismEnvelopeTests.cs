using HybridCPU_ISE.Core;
using System.IO;
using Xunit;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests
{
    public class DeterminismEnvelopeTests
    {
        [Fact]
        public void WhenTimingNoiseStaysWithinEnvelopeThenReplayEvidenceRemainsEquivalent()
        {
            TraceSink baseline = CreateTrace(cycleOffset: 0, activeMemoryOffset: 0, bankDepthOffset: 0, readyDepthOffset: 0);
            TraceSink candidate = CreateTrace(cycleOffset: 1, activeMemoryOffset: 0, bankDepthOffset: 0, readyDepthOffset: 0);

            ReplayDeterminismReport strictReport = ReplayEngine.CompareRepeatedRuns(baseline, candidate);
            ReplayEnvelopeReport envelopeReport = ReplayEngine.CompareRepeatedRunsWithinEnvelope(
                baseline,
                candidate,
                ReplayEnvelopeConfiguration.CreateTimingNoise(maxCycleDrift: 1));

            Assert.False(strictReport.IsDeterministic);
            Assert.True(envelopeReport.IsWithinEnvelope, envelopeReport.Describe());
            Assert.True(envelopeReport.ComparedReplayEvents > 0);
        }

        [Fact]
        public void WhenResourcePressureLeavesEnvelopeThenReplayReportShowsExplicitDivergence()
        {
            TraceSink baseline = CreateTrace(cycleOffset: 0, activeMemoryOffset: 0, bankDepthOffset: 0, readyDepthOffset: 0);
            TraceSink candidate = CreateTrace(cycleOffset: 0, activeMemoryOffset: 2, bankDepthOffset: 0, readyDepthOffset: 0);

            ReplayEnvelopeReport envelopeReport = ReplayEngine.CompareRepeatedRunsWithinEnvelope(
                baseline,
                candidate,
                ReplayEnvelopeConfiguration.CreateResourcePressure(
                    maxActiveMemoryRequestDelta: 1,
                    maxBankQueueDepthDelta: 1,
                    maxReadyQueueDepthDelta: 1));

            Assert.False(envelopeReport.IsWithinEnvelope);
            Assert.Equal("ActiveMemoryRequests", envelopeReport.MismatchField);
            Assert.Contains("allowed +/-1", envelopeReport.Describe());
        }

        [Fact]
        public void WhenEligibilityDiagnosticsRecordedThenReplaySummaryAndBinaryRoundTripPreserveThem()
        {
            string tempTrace = Path.GetTempFileName();

            try
            {
                TraceSink trace = CreateTrace(
                    cycleOffset: 0,
                    activeMemoryOffset: 0,
                    bankDepthOffset: 0,
                    readyDepthOffset: 0,
                    eligibilityMaskedCycles: 2,
                    eligibilityMaskedReadyCandidates: 3,
                    lastEligibilityRequestedMask: 0x0F,
                    lastEligibilityNormalizedMask: 0x05,
                    lastEligibilityReadyPortMask: 0x07,
                    lastEligibilityVisibleReadyMask: 0x05,
                    lastEligibilityMaskedReadyMask: 0x02);

                ReplayTraceEvidenceSummary inMemorySummary = ReplayEngine.SummarizeReplayPhaseEvidence(trace);
                ReplayEpochEvidenceSummary[] inMemoryEpochs = ReplayEngine.SummarizeReplayEpochEvidence(trace);

                trace.ExportBinaryTrace(tempTrace);
                var replay = new ReplayEngine(tempTrace);
                ReplayTraceEvidenceSummary binarySummary = replay.SummarizeReplayPhaseEvidence();
                ReplayEpochEvidenceSummary[] binaryEpochs = replay.SummarizeReplayEpochEvidence();

                Assert.Equal(2, inMemorySummary.EligibilityMaskedCycles);
                Assert.Equal(3, inMemorySummary.EligibilityMaskedReadyCandidates);
                Assert.Single(inMemoryEpochs);
                Assert.Equal(2, inMemoryEpochs[0].EligibilityMaskedCycles);
                Assert.Equal(3, inMemoryEpochs[0].EligibilityMaskedReadyCandidates);
                Assert.Equal(inMemorySummary.EligibilityMaskedCycles, binarySummary.EligibilityMaskedCycles);
                Assert.Equal(inMemorySummary.EligibilityMaskedReadyCandidates, binarySummary.EligibilityMaskedReadyCandidates);
                Assert.Single(binaryEpochs);
                Assert.Equal(2, binaryEpochs[0].EligibilityMaskedCycles);
                Assert.Equal(3, binaryEpochs[0].EligibilityMaskedReadyCandidates);
            }
            finally
            {
                File.Delete(tempTrace);
            }
        }

        [Fact]
        public void WhenEligibilityDiagnosticsDivergeThenReplayReportNamesEligibilityField()
        {
            TraceSink baseline = CreateTrace(
                cycleOffset: 0,
                activeMemoryOffset: 0,
                bankDepthOffset: 0,
                readyDepthOffset: 0,
                eligibilityMaskedCycles: 1);
            TraceSink candidate = CreateTrace(
                cycleOffset: 0,
                activeMemoryOffset: 0,
                bankDepthOffset: 0,
                readyDepthOffset: 0,
                eligibilityMaskedCycles: 2);

            ReplayDeterminismReport report = ReplayEngine.CompareRepeatedRuns(baseline, candidate);

            Assert.False(report.IsDeterministic);
            Assert.Equal("EligibilityMaskedCycles", report.MismatchField);
        }

        private static TraceSink CreateTrace(
            long cycleOffset,
            int activeMemoryOffset,
            int bankDepthOffset,
            int readyDepthOffset,
            long eligibilityMaskedCycles = 0,
            long eligibilityMaskedReadyCandidates = 0,
            byte lastEligibilityRequestedMask = 0,
            byte lastEligibilityNormalizedMask = 0,
            byte lastEligibilityReadyPortMask = 0,
            byte lastEligibilityVisibleReadyMask = 0,
            byte lastEligibilityMaskedReadyMask = 0)
        {
            var trace = new TraceSink(TraceFormat.JSON, "phase4-envelope.json");
            trace.SetEnabled(true);
            trace.SetLevel(TraceLevel.Full);

            var phase = new ReplayPhaseContext(
                isActive: true,
                epochId: 53,
                cachedPc: 0x9000,
                epochLength: 8,
                completedReplays: 1,
                validSlotCount: 4,
                stableDonorMask: 0x0F,
                lastInvalidationReason: ReplayPhaseInvalidationReason.None);

            var metrics = new SchedulerPhaseMetrics
            {
                ReplayAwareCycles = 2,
                PhaseCertificateReadyHits = 5,
                PhaseCertificateReadyMisses = 1,
                EstimatedChecksSaved = 7,
                DeterminismReferenceOpportunitySlots = 4,
                DeterminismReplayEligibleSlots = 3,
                DeterminismMaskedSlots = 1,
                DeterminismEstimatedLostSlots = 1,
                DeterminismConstrainedCycles = 1,
                EligibilityMaskedCycles = eligibilityMaskedCycles,
                EligibilityMaskedReadyCandidates = eligibilityMaskedReadyCandidates,
                LastEligibilityRequestedMask = lastEligibilityRequestedMask,
                LastEligibilityNormalizedMask = lastEligibilityNormalizedMask,
                LastEligibilityReadyPortMask = lastEligibilityReadyPortMask,
                LastEligibilityVisibleReadyMask = lastEligibilityVisibleReadyMask,
                LastEligibilityMaskedReadyMask = lastEligibilityMaskedReadyMask
            };

            for (int cycle = 0; cycle < 2; cycle++)
            {
                trace.RecordPhaseAwareState(
                    new FullStateTraceEvent
                    {
                        ThreadId = 0,
                        CycleNumber = 100 + cycle + cycleOffset,
                        BundleId = cycle,
                        OpIndex = 0,
                        Opcode = 0x10,
                        PipelineStage = "CYCLE",
                        CurrentFSPPolicy = "ReplayAwarePhase1.DenseTimeline",
                        ActiveMemoryRequests = 2 + activeMemoryOffset,
                        MemorySubsystemCycle = 200 + cycle + cycleOffset,
                        BankQueueDepths = new[] { 1 + bankDepthOffset, 2 + bankDepthOffset },
                        ThreadReadyQueueDepths = new[] { 3 + readyDepthOffset, 1 + readyDepthOffset, 0, 0 }
                    },
                    phase,
                    metrics,
                    phaseCertificateTemplateReusable: true);
            }

            return trace;
        }
    }
}
