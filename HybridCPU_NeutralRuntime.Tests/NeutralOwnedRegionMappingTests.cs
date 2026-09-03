using System.Reflection;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_NeutralRuntime.Tests;

public sealed class NeutralOwnedRegionMappingTests
{
    [Fact]
    public void ExactSliceMappingUsesIndependentOpaqueIdentityAndNonCoherentModel()
    {
        var runtime = new NeutralDomainRuntimeFacade();
        var domain = runtime.Bind(NeutralDomainProfile.OrdinaryService);
        Assert.True(domain.IsBound, domain.Reason);

        var mapped = runtime.MapOwnedRegion(
            domain.Lease,
            new NeutralOwnedRegionSlice(
                Offset: 128,
                Length: 512,
                NeutralMemoryAccess.Read | NeutralMemoryAccess.Write));

        Assert.True(mapped.IsMapped, mapped.Reason);
        Assert.Equal(domain.Lease, mapped.Lease.DomainLease);
        Assert.Equal(128, mapped.Lease.Slice.Offset);
        Assert.Equal(512, mapped.Lease.Slice.Length);
        Assert.Equal(
            NeutralMemoryAccess.Read | NeutralMemoryAccess.Write,
            mapped.Lease.Slice.Access);
        Assert.Equal(NeutralMemoryCoherenceModel.NonCoherent, mapped.Lease.Coherence);
        Assert.Equal(1, runtime.ActiveOwnedRegionMappingCount);
        Assert.NotEqual(typeof(NeutralDomainBindingHandle), typeof(NeutralOwnedRegionMappingHandle));
        Assert.NotEqual(typeof(NeutralDomainBindingEpoch), typeof(NeutralOwnedRegionMappingEpoch));
    }

    [Fact]
    public void InvalidRangeAndAccessFailBeforeMappingMaterialization()
    {
        var runtime = new NeutralDomainRuntimeFacade();
        var domain = runtime.Bind(NeutralDomainProfile.OrdinaryService).Lease;

        var negative = runtime.MapOwnedRegion(
            domain,
            new NeutralOwnedRegionSlice(-1, 1, NeutralMemoryAccess.Read));
        var zeroLength = runtime.MapOwnedRegion(
            domain,
            new NeutralOwnedRegionSlice(0, 0, NeutralMemoryAccess.Read));
        var overflow = runtime.MapOwnedRegion(
            domain,
            new NeutralOwnedRegionSlice(long.MaxValue, 1, NeutralMemoryAccess.Read));
        var noAccess = runtime.MapOwnedRegion(
            domain,
            new NeutralOwnedRegionSlice(0, 1, NeutralMemoryAccess.None));
        var malformedAccess = runtime.MapOwnedRegion(
            domain,
            new NeutralOwnedRegionSlice(0, 1, (NeutralMemoryAccess)0x80));

        Assert.Equal(NeutralOwnedRegionMapDecision.InvalidRange, negative.Decision);
        Assert.Equal(NeutralOwnedRegionMapDecision.InvalidRange, zeroLength.Decision);
        Assert.Equal(NeutralOwnedRegionMapDecision.InvalidRange, overflow.Decision);
        Assert.Equal(NeutralOwnedRegionMapDecision.InvalidAccess, noAccess.Decision);
        Assert.Equal(NeutralOwnedRegionMapDecision.InvalidAccess, malformedAccess.Decision);
        Assert.Equal(0, runtime.ActiveOwnedRegionMappingCount);
    }

    [Fact]
    public void StaleOrRevokedDomainCannotMaterializeMappingAuthority()
    {
        var runtime = new NeutralDomainRuntimeFacade();
        var domain = runtime.Bind(NeutralDomainProfile.OrdinaryService);
        Assert.True(domain.IsBound, domain.Reason);

        var stale = domain.Lease with
        {
            Epoch = new NeutralDomainBindingEpoch(domain.Lease.Epoch.Value + 1),
        };
        var staleMap = runtime.MapOwnedRegion(
            stale,
            new NeutralOwnedRegionSlice(0, 64, NeutralMemoryAccess.Read));
        Assert.Equal(NeutralOwnedRegionMapDecision.Stale, staleMap.Decision);

        Assert.True(runtime.Close(domain.Lease).IsClosed);
        var revokedMap = runtime.MapOwnedRegion(
            domain.Lease,
            new NeutralOwnedRegionSlice(0, 64, NeutralMemoryAccess.Read));
        Assert.Equal(NeutralOwnedRegionMapDecision.Revoked, revokedMap.Decision);
        Assert.Equal(0, runtime.ActiveOwnedRegionMappingCount);
    }

    [Fact]
    public void ExplicitNonCoherentMappingRequiresPublicationFence()
    {
        var runtime = new NeutralDomainRuntimeFacade();
        var domain = runtime.Bind(NeutralDomainProfile.OrdinaryService).Lease;
        var mapping = runtime.MapOwnedRegion(
            domain,
            new NeutralOwnedRegionSlice(32, 96, NeutralMemoryAccess.Read)).Lease;

        var coherent = runtime.PrepareOwnedRegionVisibility(
            mapping,
            NeutralMemoryVisibilityRequirement.CoherentAccess);
        var maintenance = runtime.PrepareOwnedRegionVisibility(
            mapping,
            NeutralMemoryVisibilityRequirement.CacheMaintenance);
        var fence = runtime.PrepareOwnedRegionVisibility(
            mapping,
            NeutralMemoryVisibilityRequirement.PublicationFence);

        Assert.Equal(NeutralOwnedRegionVisibilityDecision.Unsupported, coherent.Decision);
        Assert.Equal(NeutralMemoryVisibilityOutcome.Unsupported, coherent.Outcome);
        Assert.False(coherent.IsSatisfied);
        Assert.Equal(NeutralOwnedRegionVisibilityDecision.Unsupported, maintenance.Decision);
        Assert.False(maintenance.IsSatisfied);

        Assert.True(fence.IsSatisfied, fence.Reason);
        Assert.Equal(mapping, fence.Lease);
        Assert.Equal(
            NeutralMemoryVisibilityOutcome.PublicationFenceSatisfied,
            fence.Outcome);
        Assert.Equal(1UL, runtime.PublicationSequenceForTesting(mapping));
    }

    [Fact]
    public void StaleMappingCannotPublishOrCloseLiveMapping()
    {
        var runtime = new NeutralDomainRuntimeFacade();
        var domain = runtime.Bind(NeutralDomainProfile.OrdinaryService).Lease;
        var mapping = runtime.MapOwnedRegion(
            domain,
            new NeutralOwnedRegionSlice(0, 64, NeutralMemoryAccess.Write)).Lease;
        var stale = mapping with
        {
            Epoch = new NeutralOwnedRegionMappingEpoch(mapping.Epoch.Value + 1),
        };

        var visibility = runtime.PrepareOwnedRegionVisibility(
            stale,
            NeutralMemoryVisibilityRequirement.PublicationFence);
        var close = runtime.CloseOwnedRegionMapping(stale);

        Assert.Equal(NeutralOwnedRegionVisibilityDecision.Stale, visibility.Decision);
        Assert.Equal(NeutralOwnedRegionCloseDecision.Stale, close.Decision);
        Assert.Equal(1, runtime.ActiveOwnedRegionMappingCount);

        Assert.True(runtime.CloseOwnedRegionMapping(mapping).IsClosed);
        Assert.Equal(0, runtime.ActiveOwnedRegionMappingCount);
    }

    [Fact]
    public void ExactCloseRevokesMappingAndDuplicateCloseIsNotNewClosureProof()
    {
        var runtime = new NeutralDomainRuntimeFacade();
        var domain = runtime.Bind(NeutralDomainProfile.OrdinaryService).Lease;
        var mapping = runtime.MapOwnedRegion(
            domain,
            new NeutralOwnedRegionSlice(4, 16, NeutralMemoryAccess.Read)).Lease;

        var close = runtime.CloseOwnedRegionMapping(mapping);
        var duplicate = runtime.CloseOwnedRegionMapping(mapping);
        var publishAfterClose = runtime.PrepareOwnedRegionVisibility(
            mapping,
            NeutralMemoryVisibilityRequirement.PublicationFence);

        Assert.True(close.IsClosed, close.Reason);
        Assert.Equal(mapping, close.Lease);
        Assert.Equal(NeutralOwnedRegionCloseDecision.Revoked, duplicate.Decision);
        Assert.Equal(NeutralOwnedRegionVisibilityDecision.Revoked, publishAfterClose.Decision);
        Assert.Equal(0, runtime.ActiveOwnedRegionMappingCount);
    }

    [Fact]
    public void PublicMappingSurfaceContainsNoHardwareAuthorityIdentifiers()
    {
        var forbiddenNames = new[]
        {
            "Physical",
            "PageTable",
            "Pte",
            "CacheLine",
            "Dma",
            "Iommu",
            "Vmcs",
            "Vmx",
            "Lane",
            "Opcode",
        };

        var publicMethods = typeof(NeutralDomainRuntimeFacade)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(static method =>
                method.Name.Contains("OwnedRegion", StringComparison.Ordinal) ||
                method.Name.Contains("Visibility", StringComparison.Ordinal))
            .ToArray();

        Assert.NotEmpty(publicMethods);
        foreach (var method in publicMethods)
        {
            var signatureTypes = method.GetParameters()
                .Select(static parameter => parameter.ParameterType)
                .Append(method.ReturnType)
                .Select(static type => type.FullName ?? type.Name)
                .ToArray();

            foreach (var signatureType in signatureTypes)
            {
                foreach (var forbidden in forbiddenNames)
                {
                    Assert.DoesNotContain(
                        forbidden,
                        signatureType,
                        StringComparison.OrdinalIgnoreCase);
                }
            }
        }
    }
}
