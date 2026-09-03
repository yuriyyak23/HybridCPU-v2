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
    public void StartParkResumeTransitionsAreExactAndSynchronous()
    {
        var facade = new NeutralDomainRuntimeFacade();
        var binding = facade.Bind(NeutralDomainProfile.OrdinaryService);
        Assert.True(binding.IsBound, binding.Reason);

        var start = facade.TransitionExecution(
            binding.Lease,
            NeutralExecutionTransition.Start);
        Assert.True(start.IsTransitioned, start.Reason);
        Assert.Equal(binding.Lease, start.Lease);
        Assert.Equal(NeutralExecutionTransition.Start, start.Transition);
        Assert.Equal(NeutralExecutionState.Running, start.State);

        var park = facade.TransitionExecution(
            binding.Lease,
            NeutralExecutionTransition.Park);
        Assert.True(park.IsTransitioned, park.Reason);
        Assert.Equal(NeutralExecutionState.Parked, park.State);

        var resume = facade.TransitionExecution(
            binding.Lease,
            NeutralExecutionTransition.Resume);
        Assert.True(resume.IsTransitioned, resume.Reason);
        Assert.Equal(NeutralExecutionState.Running, resume.State);
    }

    [Fact]
    public void InvalidExecutionOrderIsDeniedWithoutChangingState()
    {
        var facade = new NeutralDomainRuntimeFacade();
        var binding = facade.Bind(NeutralDomainProfile.OrdinaryService);
        Assert.True(binding.IsBound, binding.Reason);

        var prematurePark = facade.TransitionExecution(
            binding.Lease,
            NeutralExecutionTransition.Park);
        Assert.Equal(
            NeutralExecutionTransitionDecision.InvalidTransition,
            prematurePark.Decision);
        Assert.Equal(NeutralExecutionState.Ready, prematurePark.State);

        var start = facade.TransitionExecution(
            binding.Lease,
            NeutralExecutionTransition.Start);
        Assert.True(start.IsTransitioned, start.Reason);

        var duplicateStart = facade.TransitionExecution(
            binding.Lease,
            NeutralExecutionTransition.Start);
        Assert.Equal(
            NeutralExecutionTransitionDecision.InvalidTransition,
            duplicateStart.Decision);
        Assert.Equal(NeutralExecutionState.Running, duplicateStart.State);

        var park = facade.TransitionExecution(
            binding.Lease,
            NeutralExecutionTransition.Park);
        Assert.True(park.IsTransitioned, park.Reason);
        Assert.Equal(NeutralExecutionState.Parked, park.State);
    }

    [Fact]
    public void StaleExecutionLeaseCannotTransitionLiveAuthority()
    {
        var facade = new NeutralDomainRuntimeFacade();
        var binding = facade.Bind(NeutralDomainProfile.OrdinaryService);
        Assert.True(binding.IsBound, binding.Reason);

        var stale = binding.Lease with
        {
            Epoch = new NeutralDomainBindingEpoch(binding.Lease.Epoch.Value + 1),
        };

        var staleStart = facade.TransitionExecution(
            stale,
            NeutralExecutionTransition.Start);
        Assert.Equal(NeutralExecutionTransitionDecision.Stale, staleStart.Decision);
        Assert.Equal(NeutralExecutionState.Ready, staleStart.State);

        var exactStart = facade.TransitionExecution(
            binding.Lease,
            NeutralExecutionTransition.Start);
        Assert.True(exactStart.IsTransitioned, exactStart.Reason);
        Assert.Equal(NeutralExecutionState.Running, exactStart.State);
    }

    [Fact]
    public void ClosedBindingCannotProduceExecutionTransitions()
    {
        var facade = new NeutralDomainRuntimeFacade();
        var binding = facade.Bind(NeutralDomainProfile.OrdinaryService);
        Assert.True(binding.IsBound, binding.Reason);
        Assert.True(facade.TransitionExecution(
            binding.Lease,
            NeutralExecutionTransition.Start).IsTransitioned);
        Assert.True(facade.Close(binding.Lease).IsClosed);

        var park = facade.TransitionExecution(
            binding.Lease,
            NeutralExecutionTransition.Park);

        Assert.Equal(NeutralExecutionTransitionDecision.Revoked, park.Decision);
        Assert.Equal(0, facade.ActiveBindingCount);
    }

    [Fact]
    public void UndefinedExecutionTransitionFailsClosedWithoutMutation()
    {
        var facade = new NeutralDomainRuntimeFacade();
        var binding = facade.Bind(NeutralDomainProfile.OrdinaryService);
        Assert.True(binding.IsBound, binding.Reason);

        var malformed = facade.TransitionExecution(
            binding.Lease,
            (NeutralExecutionTransition)255);

        Assert.Equal(NeutralExecutionTransitionDecision.Faulted, malformed.Decision);
        Assert.Equal(NeutralExecutionState.Ready, malformed.State);

        var exactStart = facade.TransitionExecution(
            binding.Lease,
            NeutralExecutionTransition.Start);
        Assert.True(exactStart.IsTransitioned, exactStart.Reason);
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
            "Iommu",
            "BusAddress",
            "Physical",
            "PageTable",
            "Pte",
            "Descriptor",
            "ScatterGather",
            "Queue",
            "Vector",
            "Controller",
            "Lane",
            "Opcode",
            "Bundle",
            "Slot",
            "Smt",
        };
        var allowedDmaSignatureTypes = new HashSet<string>(StringComparer.Ordinal)
        {
            typeof(NeutralDmaRange).FullName!,
            typeof(NeutralDmaDirection).FullName!,
            typeof(NeutralDmaGrant).FullName!,
            typeof(NeutralDmaGrantResult).FullName!,
            typeof(NeutralDmaGrantCloseResult).FullName!,
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

                if (signatureType.Contains("Dma", StringComparison.OrdinalIgnoreCase))
                    Assert.Contains(signatureType, allowedDmaSignatureTypes);
            }
        }

        var dmaMethods = publicMethods
            .Select(static method => method.Name)
            .Where(static name => name.Contains("Dma", StringComparison.OrdinalIgnoreCase))
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(
            new[]
            {
                nameof(NeutralDomainRuntimeFacade.BindDmaGrant),
                nameof(NeutralDomainRuntimeFacade.CloseDmaGrant),
                "get_" + nameof(NeutralDomainRuntimeFacade.ActiveDmaGrantCount),
            }.OrderBy(static name => name, StringComparer.Ordinal),
            dmaMethods);
        Assert.DoesNotContain(publicMethods, static method =>
            method.Name.Contains("Submit", StringComparison.OrdinalIgnoreCase) ||
            method.Name.Contains("CompleteDma", StringComparison.OrdinalIgnoreCase) ||
            method.Name.Contains("Iommu", StringComparison.OrdinalIgnoreCase) ||
            method.Name.Contains("BusAddress", StringComparison.OrdinalIgnoreCase));
    }
}
