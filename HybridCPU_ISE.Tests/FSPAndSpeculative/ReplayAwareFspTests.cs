using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests
{
    public class ReplayAwareFspTests
    {
        [Fact]
        public void WhenReplayPhaseActiveThenInterCorePackingUsesStableDonorSlots()
        {
            var scheduler = new MicroOpScheduler();
            scheduler.SetReplayPhaseContext(new ReplayPhaseContext(
                isActive: true,
                epochId: 7,
                cachedPc: 0x1000,
                epochLength: 8,
                completedReplays: 2,
                validSlotCount: 2,
                stableDonorMask: 0x04,
                lastInvalidationReason: ReplayPhaseInvalidationReason.None));

            var bundle = new MicroOp[8];
            bundle[0] = MicroOpTestHelper.CreateScalarALU(0, 1, 2, 3);
            bundle[1] = MicroOpTestHelper.CreateNop();
            bundle[2] = MicroOpTestHelper.CreateNop();

            var candidate = MicroOpTestHelper.CreateScalarALU(1, 9, 10, 11);
            candidate.OwnerThreadId = 1;
            scheduler.Nominate(1, candidate);

            var packed = scheduler.PackBundle(bundle, currentThreadId: 0, stealEnabled: true, stealMask: 0xFF, donorMask: 0x06);

            Assert.IsType<NopMicroOp>(packed[1]);
            Assert.Same(candidate, packed[2]);
            Assert.Equal(1, scheduler.SuccessfulInjectionsCount);
        }

        [Fact]
        public void WhenReplayPhaseInactiveThenInterCorePackingKeepsOriginalStealOrder()
        {
            var scheduler = new MicroOpScheduler();
            scheduler.SetReplayPhaseContext(new ReplayPhaseContext(
                isActive: false,
                epochId: 0,
                cachedPc: 0,
                epochLength: 0,
                completedReplays: 0,
                validSlotCount: 0,
                stableDonorMask: 0,
                lastInvalidationReason: ReplayPhaseInvalidationReason.None));

            var bundle = new MicroOp[8];
            bundle[0] = MicroOpTestHelper.CreateScalarALU(0, 1, 2, 3);
            bundle[1] = MicroOpTestHelper.CreateNop();
            bundle[2] = MicroOpTestHelper.CreateNop();

            var candidate = MicroOpTestHelper.CreateScalarALU(1, 9, 10, 11);
            candidate.OwnerThreadId = 1;
            scheduler.Nominate(1, candidate);

            var packed = scheduler.PackBundle(bundle, currentThreadId: 0, stealEnabled: true, stealMask: 0xFF, donorMask: 0x06);

            Assert.Same(candidate, packed[1]);
            Assert.Equal(1, scheduler.SuccessfulInjectionsCount);
        }

        [Fact]
        public void WhenReplayPhaseActiveThenSmtPackingSkipsUnstableEmptySlots()
        {
            var scheduler = new MicroOpScheduler();
            scheduler.SetReplayPhaseContext(new ReplayPhaseContext(
                isActive: true,
                epochId: 11,
                cachedPc: 0x2000,
                epochLength: 6,
                completedReplays: 1,
                validSlotCount: 1,
                stableDonorMask: 0x04,
                lastInvalidationReason: ReplayPhaseInvalidationReason.None));

            var bundle = new MicroOp[8];
            bundle[0] = MicroOpTestHelper.CreateScalarALU(0, 1, 2, 3);

            var candidate = MicroOpTestHelper.CreateScalarALU(1, 9, 10, 11);
            scheduler.NominateSmtCandidate(1, candidate);

            var packed = scheduler.PackBundleIntraCoreSmt(bundle, ownerVirtualThreadId: 0, localCoreId: 0);

            Assert.Null(packed[1]);
            Assert.Same(candidate, packed[2]);
            Assert.Equal(1, scheduler.SmtInjectionsCount);
        }

        [Fact]
        public void WhenReplayPhaseHasNoCompletedReplaysThenSmtPackingFallsBackToGenericEmptySlotOrder()
        {
            var scheduler = new MicroOpScheduler();
            scheduler.SetReplayPhaseContext(new ReplayPhaseContext(
                isActive: true,
                epochId: 12,
                cachedPc: 0x2100,
                epochLength: 6,
                completedReplays: 0,
                validSlotCount: 1,
                stableDonorMask: 0x04,
                lastInvalidationReason: ReplayPhaseInvalidationReason.None));

            var bundle = new MicroOp[8];
            bundle[0] = MicroOpTestHelper.CreateScalarALU(0, 1, 2, 3);

            var candidate = MicroOpTestHelper.CreateScalarALU(1, 9, 10, 11);
            scheduler.NominateSmtCandidate(1, candidate);

            var packed = scheduler.PackBundleIntraCoreSmt(bundle, ownerVirtualThreadId: 0, localCoreId: 0);

            Assert.Same(candidate, packed[1]);
            Assert.Null(packed[2]);
            Assert.Equal(1, scheduler.SmtInjectionsCount);
        }
    }
}
