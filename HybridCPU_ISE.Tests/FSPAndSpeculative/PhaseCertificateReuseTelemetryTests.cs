using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests
{
    public class PhaseCertificateReuseTelemetryTests
    {
        [Fact]
        public void WhenStableReplayPhaseRepeatsThenReuseCountersGrowMonotonically()
        {
            var scheduler = new MicroOpScheduler();
            var phase = CreateStablePhase(epochId: 41);

            scheduler.SetReplayPhaseContext(phase);
            PackStableSmtBundle(scheduler);
            SchedulerPhaseMetrics firstMetrics = scheduler.GetPhaseMetrics();

            scheduler.SetReplayPhaseContext(phase);
            PackStableSmtBundle(scheduler);
            SchedulerPhaseMetrics secondMetrics = scheduler.GetPhaseMetrics();

            Assert.True(secondMetrics.PhaseCertificateReadyHits > firstMetrics.PhaseCertificateReadyHits);
            Assert.True(secondMetrics.EstimatedChecksSaved > firstMetrics.EstimatedChecksSaved);
        }

        [Fact]
        public void WhenMutationAndPhaseMismatchBothOccurThenInvalidationTelemetryRemainsInterpretable()
        {
            var scheduler = new MicroOpScheduler();
            scheduler.SetReplayPhaseContext(CreateStablePhase(epochId: 42));

            PackStableSmtBundle(scheduler);
            scheduler.SetReplayPhaseContext(CreateStablePhase(epochId: 43));

            SchedulerPhaseMetrics metrics = scheduler.GetPhaseMetrics();

            Assert.True(metrics.PhaseCertificateMutationInvalidations > 0);
            Assert.True(metrics.PhaseCertificatePhaseMismatchInvalidations > 0);
            Assert.Equal(ReplayPhaseInvalidationReason.PhaseMismatch, metrics.LastCertificateInvalidationReason);
        }

        [Fact]
        public void WhenPhase3ReuseTelemetrySummarizedThenPerformanceReportHighlightsReuseAndInvalidations()
        {
            var scheduler = new MicroOpScheduler();
            var phase = CreateStablePhase(epochId: 44);

            scheduler.SetReplayPhaseContext(phase);
            PackStableSmtBundle(scheduler);
            scheduler.SetReplayPhaseContext(phase);
            PackStableSmtBundle(scheduler);

            SchedulerPhaseMetrics metrics = scheduler.GetPhaseMetrics();
            var report = new PerformanceReport
            {
                ReplayEpochCount = 1,
                ReplayAwareCycles = metrics.ReplayAwareCycles,
                StableDonorSlotRatio = 0.375,
                PhaseCertificateReadyHits = metrics.PhaseCertificateReadyHits,
                PhaseCertificateReadyMisses = metrics.PhaseCertificateReadyMisses,
                EstimatedPhaseCertificateChecksSaved = metrics.EstimatedChecksSaved,
                PhaseCertificateInvalidations = metrics.PhaseCertificateInvalidations,
                PhaseCertificateMutationInvalidations = metrics.PhaseCertificateMutationInvalidations,
                PhaseCertificatePhaseMismatchInvalidations = metrics.PhaseCertificatePhaseMismatchInvalidations
            };

            string summary = report.GeneratePhase3ExecutionSummary();

            Assert.Contains("Phase 3 Execution Contour", summary);
            Assert.Contains("Phase-Certified Packing", summary);
            Assert.Contains("Invalidation Surface", summary);
        }

        private static ReplayPhaseContext CreateStablePhase(ulong epochId)
        {
            return new ReplayPhaseContext(
                isActive: true,
                epochId: epochId,
                cachedPc: 0x5200,
                epochLength: 12,
                completedReplays: 4,
                validSlotCount: 5,
                stableDonorMask: 0xE0,
                lastInvalidationReason: ReplayPhaseInvalidationReason.None);
        }

        private static void PackStableSmtBundle(MicroOpScheduler scheduler)
        {
            var bundle = new MicroOp[8];
            for (int i = 0; i < 5; i++)
            {
                bundle[i] = MicroOpTestHelper.CreateScalarALU(0, (ushort)(i * 3 + 1), (ushort)(32 + i), (ushort)(48 + i));
            }

            scheduler.NominateSmtCandidate(1, MicroOpTestHelper.CreateScalarALU(1, 9, 10, 11));
            scheduler.NominateSmtCandidate(2, MicroOpTestHelper.CreateScalarALU(2, 17, 18, 19));
            scheduler.NominateSmtCandidate(3, MicroOpTestHelper.CreateScalarALU(3, 25, 26, 27));
            scheduler.PackBundleIntraCoreSmt(bundle, ownerVirtualThreadId: 0, localCoreId: 0);
        }
    }
}
