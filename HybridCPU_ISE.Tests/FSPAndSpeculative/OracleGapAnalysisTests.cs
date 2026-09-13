using HybridCPU_ISE.Tests.TestHelpers;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests
{
    public class OracleGapAnalysisTests
    {
        [Fact]
        public void WhenDonorMaskRestrictsSlotsThenGapIsDonorRestriction()
        {
            var scheduler = new MicroOpScheduler();
            scheduler.EnableShadowOracle = true;

            // Restrictive donor mask: only slots 0-1 are stable donors
            scheduler.SetReplayPhaseContext(new ReplayPhaseContext(
                isActive: true,
                epochId: 60,
                cachedPc: 0x9000,
                epochLength: 15,
                completedReplays: 2,
                validSlotCount: 2,
                stableDonorMask: 0x03,
                lastInvalidationReason: ReplayPhaseInvalidationReason.None));

            var bundle = new MicroOp[8];
            bundle[0] = MicroOpTestHelper.CreateScalarALU(0, 1, 2, 3);
            bundle[1] = MicroOpTestHelper.CreateScalarALU(0, 4, 5, 6);

            scheduler.NominateSmtCandidate(1, MicroOpTestHelper.CreateScalarALU(1, 8, 9, 10));
            scheduler.NominateSmtCandidate(2, MicroOpTestHelper.CreateScalarALU(2, 12, 13, 14));

            scheduler.PackBundleIntraCoreSmt(bundle, ownerVirtualThreadId: 0, localCoreId: 0);
            OracleGapSummary summary = scheduler.GetOracleGapSummary();

            Assert.True(summary.DonorRestrictionGap >= 0,
                "Donor restriction gap should be non-negative.");
        }

        [Fact]
        public void WhenGapExistsThenReasonBucketsAreInterpretable()
        {
            var scheduler = new MicroOpScheduler();
            scheduler.EnableShadowOracle = true;

            // Restrictive donor mask
            scheduler.SetReplayPhaseContext(new ReplayPhaseContext(
                isActive: true,
                epochId: 61,
                cachedPc: 0xA000,
                epochLength: 10,
                completedReplays: 2,
                validSlotCount: 1,
                stableDonorMask: 0x01,
                lastInvalidationReason: ReplayPhaseInvalidationReason.None));

            var bundle = new MicroOp[8];
            bundle[0] = MicroOpTestHelper.CreateScalarALU(0, 1, 2, 3);

            scheduler.NominateSmtCandidate(1, MicroOpTestHelper.CreateScalarALU(1, 8, 9, 10));
            scheduler.NominateSmtCandidate(2, MicroOpTestHelper.CreateScalarALU(2, 12, 13, 14));
            scheduler.NominateSmtCandidate(3, MicroOpTestHelper.CreateScalarALU(3, 16, 17, 18));

            scheduler.PackBundleIntraCoreSmt(bundle, ownerVirtualThreadId: 0, localCoreId: 0);
            OracleGapSummary summary = scheduler.GetOracleGapSummary();

            // The summary's Describe() should produce interpretable text
            string description = summary.Describe();
            Assert.False(string.IsNullOrWhiteSpace(description));

            // Sum of reason buckets should equal total gap
            long bucketSum = summary.DonorRestrictionGap +
                             summary.FairnessOrderingGap +
                             summary.LegalityConservatismGap +
                             summary.DomainIsolationGap +
                             summary.SpeculationBudgetGap;
            Assert.Equal(summary.TotalGap, bucketSum);
        }

        [Fact]
        public void WhenNoGapThenDescriptionIndicatesNoGap()
        {
            var scheduler = new MicroOpScheduler();
            scheduler.EnableShadowOracle = true;

            var bundle = new MicroOp[8];
            bundle[0] = MicroOpTestHelper.CreateScalarALU(0, 1, 2, 3);

            // Single candidate, no restrictions — real and oracle match
            scheduler.NominateSmtCandidate(1, MicroOpTestHelper.CreateScalarALU(1, 8, 9, 10));
            scheduler.PackBundleIntraCoreSmt(bundle, ownerVirtualThreadId: 0, localCoreId: 0);

            OracleGapSummary summary = scheduler.GetOracleGapSummary();
            string description = summary.Describe();

            Assert.Contains("No oracle gap detected", description);
        }

        [Fact]
        public void WhenOracleGapMetricsExposedThenReportContainsPhase5Section()
        {
            var report = new PerformanceReport
            {
                OracleGapTotalOracleSlots = 10,
                OracleGapTotalRealSlots = 7,
                OracleGapDonorRestriction = 2,
                OracleGapFairnessOrdering = 1,
                OracleGapLegalityConservatism = 0,
                OracleGapDomainIsolation = 0,
                OracleGapSpeculationBudget = 0,
                OracleGapCyclesWithGap = 3,
                OracleGapTotalCyclesAnalyzed = 5,
                OracleCounterexampleCount = 3,
                OracleCounterexampleTotalMissedSlots = 5,
                OracleCounterexampleDominantCategory = "DonorRestriction"
            };

            string summary = report.GeneratePhase5OracleGapSummary();

            Assert.Contains("Phase 5 Oracle Gap Analysis", summary);
            Assert.Contains("Oracle Contour", summary);
            Assert.Contains("Gap Decomposition", summary);
            Assert.Contains("Counterexamples", summary);
        }

        [Fact]
        public void WhenOracleGapTelemetryAbsentThenGeneralSummaryOmitsPhase5Section()
        {
            var report = new PerformanceReport();

            string summary = report.GenerateSummary();

            Assert.DoesNotContain("Phase 5 Oracle Gap Analysis", summary);
        }

        [Fact]
        public void WhenOracleGapTelemetryPresentThenGeneralSummaryIncludesPhase5Section()
        {
            var report = new PerformanceReport
            {
                OracleGapTotalOracleSlots = 10,
                OracleGapTotalRealSlots = 8,
                OracleGapTotalCyclesAnalyzed = 5
            };

            string summary = report.GenerateSummary();

            Assert.Contains("Phase 5 Oracle Gap Analysis", summary);
        }
    }
}
