using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf129bbReplayEpochActiveZeroInvalidBehaviorCutoverTests
{
    [Fact]
    public void ActiveZeroIngressDemotesBeforeReplayTelemetryAndReuse()
    {
        var scheduler = new MicroOpScheduler();
        long before = scheduler.ReplayAwareCycles;

        scheduler.SetReplayPhaseContext(new ReplayPhaseContext(
            isActive: true, epochId: 0, cachedPc: 0x1000, epochLength: 8,
            completedReplays: 1, validSlotCount: 8, stableDonorMask: 0xff,
            lastInvalidationReason: ReplayPhaseInvalidationReason.None));

        ReplayPhaseContext published = scheduler.TestGetReplayPhaseContext();
        Assert.False(published.IsActive);
        Assert.Equal(0UL, published.EpochId);
        Assert.Equal(ReplayPhaseInvalidationReason.InactivePhase,
            published.LastInvalidationReason);
        Assert.False(published.CanReusePhaseCertificate);
        Assert.Equal(before, scheduler.ReplayAwareCycles);
    }

    [Fact]
    public void ValidActiveEpochRemainsUnchanged()
    {
        var scheduler = new MicroOpScheduler();
        scheduler.SetReplayPhaseContext(new ReplayPhaseContext(
            isActive: true, epochId: 7, cachedPc: 0x1000, epochLength: 8,
            completedReplays: 1, validSlotCount: 8, stableDonorMask: 0xff,
            lastInvalidationReason: ReplayPhaseInvalidationReason.None));

        ReplayPhaseContext published = scheduler.TestGetReplayPhaseContext();
        Assert.True(published.IsActive);
        Assert.Equal(7UL, published.EpochId);
        Assert.Equal(1L, scheduler.ReplayAwareCycles);
    }
}
