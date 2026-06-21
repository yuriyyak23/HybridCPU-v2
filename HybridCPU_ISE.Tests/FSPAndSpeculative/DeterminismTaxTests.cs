using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests
{
    public class DeterminismTaxTests
    {
        [Fact]
        public void WhenReplayStableMaskRemovesEmptySlotsThenSchedulerReportsDeterminismTax()
        {
            var scheduler = new MicroOpScheduler();
            scheduler.SetReplayPhaseContext(new ReplayPhaseContext(
                isActive: true,
                epochId: 41,
                cachedPc: 0x7000,
                epochLength: 12,
                completedReplays: 2,
                validSlotCount: 2,
                stableDonorMask: 0x03,
                lastInvalidationReason: ReplayPhaseInvalidationReason.None));

            var bundle = new MicroOp[8];
            bundle[0] = MicroOpTestHelper.CreateScalarALU(0, 1, 2, 3);
            bundle[1] = MicroOpTestHelper.CreateScalarALU(0, 4, 5, 6);

            scheduler.NominateSmtCandidate(1, MicroOpTestHelper.CreateScalarALU(1, 8, 9, 10));
            scheduler.NominateSmtCandidate(2, MicroOpTestHelper.CreateScalarALU(2, 12, 13, 14));
            scheduler.NominateSmtCandidate(3, MicroOpTestHelper.CreateScalarALU(3, 16, 17, 18));

            scheduler.PackBundleIntraCoreSmt(bundle, ownerVirtualThreadId: 0, localCoreId: 0);
            SchedulerPhaseMetrics metrics = scheduler.GetPhaseMetrics();

            Assert.Equal(6, metrics.DeterminismReferenceOpportunitySlots);
            Assert.Equal(0, metrics.DeterminismReplayEligibleSlots);
            Assert.Equal(6, metrics.DeterminismMaskedSlots);
            Assert.Equal(3, metrics.DeterminismEstimatedLostSlots);
            Assert.Equal(1, metrics.DeterminismConstrainedCycles);
        }

        [Fact]
        public void WhenReplayPhaseInactiveThenDeterminismTaxRemainsZero()
        {
            var scheduler = new MicroOpScheduler();
            var bundle = new MicroOp[8];
            bundle[0] = MicroOpTestHelper.CreateScalarALU(0, 1, 2, 3);

            scheduler.NominateSmtCandidate(1, MicroOpTestHelper.CreateScalarALU(1, 8, 9, 10));
            scheduler.PackBundleIntraCoreSmt(bundle, ownerVirtualThreadId: 0, localCoreId: 0);
            SchedulerPhaseMetrics metrics = scheduler.GetPhaseMetrics();

            Assert.Equal(0, metrics.DeterminismReferenceOpportunitySlots);
            Assert.Equal(0, metrics.DeterminismMaskedSlots);
            Assert.Equal(0, metrics.DeterminismEstimatedLostSlots);
        }
    }
}
