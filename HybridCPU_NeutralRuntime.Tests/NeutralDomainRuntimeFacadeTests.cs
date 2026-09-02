using System.Reflection;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_NeutralRuntime.Tests;

public sealed class NeutralDomainRuntimeFacadeTests
{
    [Fact]
    public void OrdinaryServiceBindMaterializesNeutralOwnersWithoutDmaOrVmAuthority()
    {
        var facade = new NeutralDomainRuntimeFacade();

        var result = facade.Bind(NeutralDomainProfile.OrdinaryService);

        Assert.True(result.IsBound, result.Reason);
        Assert.True(result.Lease.IsMaterialized);
        Assert.Equal(1, facade.ActiveBindingCount);

        var context = facade.ResolveActiveContextForTesting(result.Lease);
        Assert.NotNull(context);
        Assert.True(context!.HasRequiredNeutralOwners);
        Assert.NotEqual(0UL, context.Execution.DomainTag);
        Assert.NotEqual(0UL, context.Memory.AddressSpaceTag);
        Assert.NotEqual(result.Lease.Handle.Value, context.Execution.DomainTag);
        Assert.NotEqual(result.Lease.Epoch.Value, context.Memory.AddressSpaceTag);

        Assert.False(context.Execution.CompatibilityProjectionEnabled);
        Assert.False(context.Execution.HasMaterializedGuestArchitecturalState);
        Assert.False(context.Memory.OwnsSecondStageTranslation);
        Assert.False(context.Io.OwnsDmaAuthority);
        Assert.False(context.Io.OwnsIommuAuthority);
        Assert.False(context.Io.CompatibilityProjectionEnabled);
    }

    [Fact]
    public void ExactEpochIsRequiredToCloseAndClosedLeaseCannotBeReused()
    {
        var facade = new NeutralDomainRuntimeFacade();
        var binding = facade.Bind(NeutralDomainProfile.OrdinaryService);
        Assert.True(binding.IsBound, binding.Reason);

        var stale = binding.Lease with
        {
            Epoch = new NeutralDomainBindingEpoch(binding.Lease.Epoch.Value + 1),
        };

        var staleClose = facade.Close(stale);
        Assert.Equal(NeutralDomainCloseDecision.Stale, staleClose.Decision);
        Assert.Equal(1, facade.ActiveBindingCount);
        Assert.NotNull(facade.ResolveActiveContextForTesting(binding.Lease));

        var close = facade.Close(binding.Lease);
        Assert.True(close.IsClosed, close.Reason);
        Assert.Equal(0, facade.ActiveBindingCount);
        Assert.Null(facade.ResolveActiveContextForTesting(binding.Lease));

        var duplicate = facade.Close(binding.Lease);
        Assert.Equal(NeutralDomainCloseDecision.Revoked, duplicate.Decision);
    }

    [Fact]
    public void UnsupportedProfileFailsWithoutMaterializingAuthority()
    {
        var facade = new NeutralDomainRuntimeFacade();

        var result = facade.Bind((NeutralDomainProfile)255);

        Assert.False(result.IsBound);
        Assert.Equal(NeutralDomainBindDecision.UnsupportedProfile, result.Decision);
        Assert.Equal(0, facade.ActiveBindingCount);
    }

    [Fact]
    public void PublicFacadeExposesOnlyOpaqueNeutralIdentityAndSemanticResults()
    {
        var forbiddenNames = new[]
        {
            "DomainTag",
            "AddressSpaceTag",
            "Capability",
            "Vmcs",
            "Vmx",
            "Dma",
            "Iommu",
            "Lane",
            "Opcode",
        };

        var publicMethods = typeof(NeutralDomainRuntimeFacade)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

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
