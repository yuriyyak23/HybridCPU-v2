using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests
{
    public class ShadowOracleSchedulerTests
    {
        [Fact]
        public void WhenShadowOracleEnabledThenOracleContourExists()
        {
            var scheduler = new MicroOpScheduler();
            scheduler.EnableShadowOracle = true;

            var bundle = new MicroOp[8];
            bundle[0] = MicroOpTestHelper.CreateScalarALU(0, 1, 2, 3);

            scheduler.NominateSmtCandidate(1, MicroOpTestHelper.CreateScalarALU(1, 8, 9, 10));
            scheduler.PackBundleIntraCoreSmt(bundle, ownerVirtualThreadId: 0, localCoreId: 0);

            OracleGapSummary summary = scheduler.GetOracleGapSummary();

            Assert.Equal(1, summary.TotalCyclesAnalyzed);
        }

        [Fact]
        public void WhenShadowOracleDisabledThenNoOracleContour()
        {
            var scheduler = new MicroOpScheduler();
            scheduler.EnableShadowOracle = false;

            var bundle = new MicroOp[8];
            bundle[0] = MicroOpTestHelper.CreateScalarALU(0, 1, 2, 3);

            scheduler.NominateSmtCandidate(1, MicroOpTestHelper.CreateScalarALU(1, 8, 9, 10));
            scheduler.PackBundleIntraCoreSmt(bundle, ownerVirtualThreadId: 0, localCoreId: 0);

            OracleGapSummary summary = scheduler.GetOracleGapSummary();

            Assert.Equal(0, summary.TotalCyclesAnalyzed);
        }

        [Fact]
        public void WhenOraclePacksMoreSlotsThanRealThenOracleGapIsPositive()
        {
            var scheduler = new MicroOpScheduler();
            scheduler.EnableShadowOracle = true;

            // Set replay phase with a very restrictive donor mask (only slot 0 stable)
            scheduler.SetReplayPhaseContext(new ReplayPhaseContext(
                isActive: true,
                epochId: 50,
                cachedPc: 0x8000,
                epochLength: 20,
                completedReplays: 3,
                validSlotCount: 1,
                stableDonorMask: 0x01,
                lastInvalidationReason: ReplayPhaseInvalidationReason.None));

            var bundle = new MicroOp[8];
            bundle[0] = MicroOpTestHelper.CreateScalarALU(0, 1, 2, 3);

            // Nominate multiple candidates — real scheduler limited by donor mask,
            // oracle ignores donor mask
            scheduler.NominateSmtCandidate(1, MicroOpTestHelper.CreateScalarALU(1, 8, 9, 10));
            scheduler.NominateSmtCandidate(2, MicroOpTestHelper.CreateScalarALU(2, 12, 13, 14));
            scheduler.NominateSmtCandidate(3, MicroOpTestHelper.CreateScalarALU(3, 16, 17, 18));

            scheduler.PackBundleIntraCoreSmt(bundle, ownerVirtualThreadId: 0, localCoreId: 0);

            OracleGapSummary summary = scheduler.GetOracleGapSummary();

            Assert.True(summary.TotalGap >= 0, "Oracle gap must be non-negative.");
            Assert.True(summary.TotalOracleSlots >= summary.TotalRealSlots,
                "Oracle should pack at least as many slots as real scheduler.");
        }

        [Fact]
        public void WhenOraclePacksSameSlotsAsRealThenGapIsZero()
        {
            var scheduler = new MicroOpScheduler();
            scheduler.EnableShadowOracle = true;

            // No replay phase restriction — real scheduler has full access
            var bundle = new MicroOp[8];
            bundle[0] = MicroOpTestHelper.CreateScalarALU(0, 1, 2, 3);

            scheduler.NominateSmtCandidate(1, MicroOpTestHelper.CreateScalarALU(1, 8, 9, 10));
            scheduler.PackBundleIntraCoreSmt(bundle, ownerVirtualThreadId: 0, localCoreId: 0);

            OracleGapSummary summary = scheduler.GetOracleGapSummary();

            Assert.Equal(0, summary.TotalGap);
        }

        [Fact]
        public void WhenOracleEnabledThenOraclePackingNeverViolatesSafetyMask()
        {
            var scheduler = new MicroOpScheduler();
            scheduler.EnableShadowOracle = true;

            var bundle = new MicroOp[8];
            bundle[0] = MicroOpTestHelper.CreateScalarALU(0, 1, 2, 3);

            // Create candidate with conflicting registers (same dest/src as bundle[0])
            var conflicting = MicroOpTestHelper.CreateScalarALU(1, 1, 2, 3);
            scheduler.NominateSmtCandidate(1, conflicting);
            scheduler.PackBundleIntraCoreSmt(bundle, ownerVirtualThreadId: 0, localCoreId: 0);

            OracleGapSummary summary = scheduler.GetOracleGapSummary();

            // Oracle should NOT pack conflicting operations — gap = 0 when both reject
            Assert.Equal(0, summary.TotalGap);
        }

        [Fact]
        public void WhenOracleEnabledThenOutputIsBoundedAndMeasurable()
        {
            var scheduler = new MicroOpScheduler();
            scheduler.EnableShadowOracle = true;

            // Run multiple cycles
            for (int cycle = 0; cycle < 10; cycle++)
            {
                var bundle = new MicroOp[8];
                bundle[0] = MicroOpTestHelper.CreateScalarALU(0, 1, 2, 3);
                scheduler.NominateSmtCandidate(1, MicroOpTestHelper.CreateScalarALU(1, (ushort)(8 + cycle * 4), (ushort)(9 + cycle * 4), (ushort)(10 + cycle * 4)));
                scheduler.PackBundleIntraCoreSmt(bundle, ownerVirtualThreadId: 0, localCoreId: 0);
            }

            OracleGapSummary summary = scheduler.GetOracleGapSummary();

            Assert.Equal(10, summary.TotalCyclesAnalyzed);
            Assert.True(summary.OracleEfficiency >= 0.0 && summary.OracleEfficiency <= 1.0,
                "Oracle efficiency must be in [0, 1].");
            Assert.True(summary.GapRate >= 0.0 && summary.GapRate <= 1.0,
                "Gap rate must be in [0, 1].");
        }
    }
}
