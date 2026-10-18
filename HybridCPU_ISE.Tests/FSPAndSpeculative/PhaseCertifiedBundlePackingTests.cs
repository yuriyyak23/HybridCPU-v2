using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests
{
    public class PhaseCertifiedBundlePackingTests
    {
        [Fact]
        public void WhenStableReplayPhaseHasMultipleCandidatesThenCertificateReuseAccumulates()
        {
            var scheduler = new MicroOpScheduler();
            scheduler.SetReplayPhaseContext(new ReplayPhaseContext(
                isActive: true,
                epochId: 21,
                cachedPc: 0x3000,
                epochLength: 12,
                completedReplays: 4,
                validSlotCount: 5,
                stableDonorMask: 0xE0,
                lastInvalidationReason: ReplayPhaseInvalidationReason.None));

            var bundle = new MicroOp[8];
            for (int i = 0; i < 5; i++)
            {
                bundle[i] = MicroOpTestHelper.CreateScalarALU(0, (ushort)(i * 3 + 1), (ushort)(32 + i), (ushort)(48 + i));
            }

            scheduler.NominateSmtCandidate(1, MicroOpTestHelper.CreateScalarALU(1, 9, 10, 11));
            scheduler.NominateSmtCandidate(2, MicroOpTestHelper.CreateScalarALU(2, 17, 18, 19));
            scheduler.NominateSmtCandidate(3, MicroOpTestHelper.CreateScalarALU(3, 25, 26, 27));

            scheduler.PackBundleIntraCoreSmt(bundle, ownerVirtualThreadId: 0, localCoreId: 0);

            Assert.Equal(3, scheduler.SmtInjectionsCount);
            Assert.True(scheduler.PhaseCertificateReadyHits >= 3);
            Assert.True(scheduler.EstimatedPhaseCertificateChecksSaved > 0);
            Assert.Equal(3, scheduler.PhaseCertificateMutationInvalidations);
            Assert.Equal(ReplayPhaseInvalidationReason.CertificateMutation, scheduler.LastPhaseCertificateInvalidationReason);
        }

        [Fact]
        public void WhenStableReplayPhaseRepeatsThenBaseCertificateRefreshesWithoutStaleConflicts()
        {
            var scheduler = new MicroOpScheduler();
            scheduler.SetReplayPhaseContext(new ReplayPhaseContext(
                isActive: true,
                epochId: 22,
                cachedPc: 0x4000,
                epochLength: 16,
                completedReplays: 5,
                validSlotCount: 1,
                stableDonorMask: 0x02,
                lastInvalidationReason: ReplayPhaseInvalidationReason.None));

            var firstBundle = new MicroOp[8];
            firstBundle[0] = MicroOpTestHelper.CreateScalarALU(0, 1, 2, 3);
            scheduler.NominateSmtCandidate(1, MicroOpTestHelper.CreateScalarALU(1, 9, 10, 11));

            scheduler.PackBundleIntraCoreSmt(firstBundle, ownerVirtualThreadId: 0, localCoreId: 0);

            var secondBundle = new MicroOp[8];
            secondBundle[0] = MicroOpTestHelper.CreateScalarALU(0, 1, 2, 3);
            scheduler.NominateSmtCandidate(1, MicroOpTestHelper.CreateScalarALU(1, 9, 10, 11));

            scheduler.PackBundleIntraCoreSmt(secondBundle, ownerVirtualThreadId: 0, localCoreId: 0);

            Assert.Equal(2, scheduler.SmtInjectionsCount);
            Assert.True(scheduler.PhaseCertificateReadyHits >= 2);
            Assert.Equal(ReplayPhaseInvalidationReason.CertificateMutation, scheduler.LastPhaseCertificateInvalidationReason);
        }

        [Fact]
        public void WhenSameStableReplayPhaseIsRepublishedThenMatchingTemplateIsPreserved()
        {
            var scheduler = new MicroOpScheduler();
            var phase = new ReplayPhaseContext(
                isActive: true,
                epochId: 23,
                cachedPc: 0x4100,
                epochLength: 16,
                completedReplays: 5,
                validSlotCount: 1,
                stableDonorMask: 0x02,
                lastInvalidationReason: ReplayPhaseInvalidationReason.None);

            var firstBundle = new MicroOp[8];
            firstBundle[0] = MicroOpTestHelper.CreateScalarALU(0, 1, 2, 3);

            scheduler.SetReplayPhaseContext(phase);
            scheduler.NominateSmtCandidate(1, MicroOpTestHelper.CreateScalarALU(1, 9, 10, 11));
            scheduler.PackBundleIntraCoreSmt(firstBundle, ownerVirtualThreadId: 0, localCoreId: 0);

            scheduler.SetReplayPhaseContext(phase);

            var secondBundle = new MicroOp[8];
            secondBundle[0] = MicroOpTestHelper.CreateScalarALU(0, 1, 2, 3);
            scheduler.NominateSmtCandidate(1, MicroOpTestHelper.CreateScalarALU(1, 9, 10, 11));
            scheduler.PackBundleIntraCoreSmt(secondBundle, ownerVirtualThreadId: 0, localCoreId: 0);

            Assert.Equal(2, scheduler.SmtInjectionsCount);
            Assert.True(scheduler.PhaseCertificateReadyHits >= 2);
            Assert.Equal(0, scheduler.PhaseCertificatePhaseMismatchInvalidations);
            Assert.Equal(2, scheduler.PhaseCertificateMutationInvalidations);
            Assert.Equal(ReplayPhaseInvalidationReason.CertificateMutation, scheduler.LastPhaseCertificateInvalidationReason);
        }
    }
}
