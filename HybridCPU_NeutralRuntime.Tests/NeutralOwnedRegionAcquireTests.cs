using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_NeutralRuntime.Tests;

public sealed class NeutralOwnedRegionAcquireTests
{
    [Fact]
    public void WritableMappingRequiresCloseBeforeAcquireFence()
    {
        var runtime = new NeutralDomainRuntimeFacade();
        var domain = runtime.Bind(NeutralDomainProfile.OrdinaryService).Lease;
        var mapping = runtime.MapOwnedRegion(
            domain,
            new NeutralOwnedRegionSlice(
                32,
                128,
                NeutralMemoryAccess.Read | NeutralMemoryAccess.Write)).Lease;

        var early = runtime.AcquireOwnedRegionVisibility(
            mapping,
            NeutralMemoryAcquireRequirement.AcquisitionFence);

        Assert.Equal(NeutralOwnedRegionAcquireDecision.NotClosed, early.Decision);
        Assert.False(early.IsSatisfied);
        Assert.Equal(0UL, runtime.AcquisitionSequenceForTesting(mapping));

        Assert.True(runtime.CloseOwnedRegionMapping(mapping).IsClosed);
        var acquired = runtime.AcquireOwnedRegionVisibility(
            mapping,
            NeutralMemoryAcquireRequirement.AcquisitionFence);

        Assert.True(acquired.IsSatisfied, acquired.Reason);
        Assert.Equal(mapping, acquired.Lease);
        Assert.Equal(
            NeutralMemoryAcquireOutcome.AcquisitionFenceSatisfied,
            acquired.Outcome);
        Assert.Equal(1UL, runtime.AcquisitionSequenceForTesting(mapping));
    }

    [Fact]
    public void StaleMappingCannotAcquireClosedLiveMapping()
    {
        var runtime = new NeutralDomainRuntimeFacade();
        var domain = runtime.Bind(NeutralDomainProfile.OrdinaryService).Lease;
        var mapping = runtime.MapOwnedRegion(
            domain,
            new NeutralOwnedRegionSlice(0, 64, NeutralMemoryAccess.Write)).Lease;
        Assert.True(runtime.CloseOwnedRegionMapping(mapping).IsClosed);

        var stale = mapping with
        {
            Epoch = new NeutralOwnedRegionMappingEpoch(mapping.Epoch.Value + 1),
        };
        var acquired = runtime.AcquireOwnedRegionVisibility(
            stale,
            NeutralMemoryAcquireRequirement.AcquisitionFence);

        Assert.Equal(NeutralOwnedRegionAcquireDecision.Stale, acquired.Decision);
        Assert.False(acquired.IsSatisfied);
        Assert.Equal(0UL, runtime.AcquisitionSequenceForTesting(stale));
    }

    [Fact]
    public void RevokedDomainCannotManufactureAcquireEvidence()
    {
        var runtime = new NeutralDomainRuntimeFacade();
        var domain = runtime.Bind(NeutralDomainProfile.OrdinaryService).Lease;
        var mapping = runtime.MapOwnedRegion(
            domain,
            new NeutralOwnedRegionSlice(0, 64, NeutralMemoryAccess.Write)).Lease;
        Assert.True(runtime.CloseOwnedRegionMapping(mapping).IsClosed);
        Assert.True(runtime.Close(domain).IsClosed);

        var acquired = runtime.AcquireOwnedRegionVisibility(
            mapping,
            NeutralMemoryAcquireRequirement.AcquisitionFence);

        Assert.Equal(NeutralOwnedRegionAcquireDecision.RevokedDomain, acquired.Decision);
        Assert.False(acquired.IsSatisfied);
    }

    [Fact]
    public void DuplicateAcquireIsIdempotentEvidenceNotNewAuthority()
    {
        var runtime = new NeutralDomainRuntimeFacade();
        var domain = runtime.Bind(NeutralDomainProfile.OrdinaryService).Lease;
        var mapping = runtime.MapOwnedRegion(
            domain,
            new NeutralOwnedRegionSlice(8, 32, NeutralMemoryAccess.Write)).Lease;
        Assert.True(runtime.CloseOwnedRegionMapping(mapping).IsClosed);

        var first = runtime.AcquireOwnedRegionVisibility(
            mapping,
            NeutralMemoryAcquireRequirement.AcquisitionFence);
        var second = runtime.AcquireOwnedRegionVisibility(
            mapping,
            NeutralMemoryAcquireRequirement.AcquisitionFence);

        Assert.True(first.IsSatisfied, first.Reason);
        Assert.True(second.IsSatisfied, second.Reason);
        Assert.Equal(1UL, runtime.AcquisitionSequenceForTesting(mapping));
        Assert.Equal(0, runtime.ActiveOwnedRegionMappingCount);
    }
}
