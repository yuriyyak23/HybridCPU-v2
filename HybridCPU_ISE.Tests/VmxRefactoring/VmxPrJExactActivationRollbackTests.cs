using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.VmxRefactoring;

public sealed class VmxPrJExactActivationRollbackTests
{
    [Fact]
    public void Profile_DefaultsDisabled_AndRejectsEveryNonExactActivationRequest()
    {
        var fixture = VmxPrFCanonicalHypercallCompositionTests.CreateFixture(configure: false);
        var profile = new DomainHypercallExactRuntimeProfile(
            7,
            fixture.Scheduler,
            fixture.Core.ExactHypercallRetireOwner);

        Assert.Equal(
            DomainHypercallLifecycleState.DisabledFaultOnly,
            profile.LifecycleGate.Observe().State);
        Assert.False(fixture.Scheduler.HasExactVirtualizationComposition);

        DomainHypercallExactActivationRequest adjacent =
            DomainHypercallExactActivationRequest.Phase38Exact with { NumericLeaf = 0x0002 };
        DomainHypercallExactActivationResult denied = profile.Activate(adjacent);
        Assert.Equal(DomainHypercallExactActivationDecision.DeniedNonExactProfile, denied.Decision);
        Assert.False(denied.ExactBindingPresent);
        Assert.False(denied.ExactGrantLive);
        Assert.Equal(DomainHypercallLifecycleState.DisabledFaultOnly, denied.Lifecycle.State);
    }

    [Fact]
    public void ExactProfile_UsesNeutralDomainCapabilityContourAndOnlyExactProbeExecutes()
    {
        var fixture = VmxPrFCanonicalHypercallCompositionTests.CreateFixture(configure: false);
        var profile = new DomainHypercallExactRuntimeProfile(
            7,
            fixture.Scheduler,
            fixture.Core.ExactHypercallRetireOwner);

        DomainHypercallExactActivationResult activated =
            profile.Activate(DomainHypercallExactActivationRequest.Phase38Exact);
        Assert.True(activated.IsActivated);
        Assert.True(fixture.Scheduler.HasExactVirtualizationComposition);
        Assert.Equal(DomainHypercallLifecycleState.ActiveExactProfile, activated.Lifecycle.State);

        Assert.True(VmxPrFCanonicalHypercallCompositionTests.Materialize(fixture, 1));
        DomainHypercallCompositionResult prepared = Assert.IsType<DomainHypercallCompositionResult>(
            fixture.Scheduler.LastExactVirtualizationCompositionResult);
        Assert.True(prepared.IsPrepared);
        Assert.Equal(VirtualizationDecisionValidatorV2.ExpectedDecisionId, prepared.E2!.DecisionId);
        Assert.Equal(VirtualizationDecisionValidatorV2.ExpectedOperationNamespace, prepared.E2.OperationNamespace);
        Assert.Equal((ushort)0x0001, prepared.E2.NumericLeaf);
        Assert.Equal(VirtualizationDecisionValidatorV2.ExpectedOperationId, prepared.E2.OperationId);
        Assert.Equal(VirtualizationDecisionValidatorV2.ExpectedOwnerId, prepared.E2.OwnerId);

        var core = fixture.Core;
        Assert.True(fixture.Carrier.Execute(ref core));
        Assert.True(fixture.Carrier.ExactHypercallExecutionResult!.Value.IsExecuted);
        Assert.True(fixture.Carrier.ExactHypercallCompletionPublication!.Value.IsPublished);
        Assert.True(fixture.Carrier.CreateRetireEffect().IsFaulted);
    }

    [Fact]
    public void KillSwitch_ClosesE2DrainsAllOwnersRevokesBindingAndRestoresFaultOnlyFallback()
    {
        var fixture = VmxPrFCanonicalHypercallCompositionTests.CreateFixture(configure: false);
        var profile = new DomainHypercallExactRuntimeProfile(
            7,
            fixture.Scheduler,
            fixture.Core.ExactHypercallRetireOwner);
        Assert.True(profile.Activate(DomainHypercallExactActivationRequest.Phase38Exact).IsActivated);
        Assert.True(VmxPrFCanonicalHypercallCompositionTests.Materialize(fixture, 1));
        Assert.Equal(1, profile.DrainOwner!.ObserveLiveAuthorities().E2);

        DomainHypercallKillSwitchResult killed = profile.KillSwitch(TimeSpan.FromSeconds(5));

        Assert.True(killed.IsDeterministicFaultOnly);
        Assert.Equal(new[]
        {
            DomainHypercallKillSwitchStep.NewE2Closed,
            DomainHypercallKillSwitchStep.TransitionsQuiescent,
            DomainHypercallKillSwitchStep.RegistriesQuiescent,
            DomainHypercallKillSwitchStep.ExactBindingAndGrantRevoked,
            DomainHypercallKillSwitchStep.DeterministicFaultOnlyFallbackRestored,
        }, killed.Trace);
        Assert.False(fixture.Scheduler.HasExactVirtualizationComposition);
        Assert.False(killed.ExactBindingPresent);
        Assert.False(killed.ExactGrantLive);
        Assert.True(killed.Counts.IsDrained);
        Assert.Equal(DomainHypercallLifecycleState.DisabledFaultOnly, killed.Lifecycle.State);
        Assert.False(profile.LifecycleGate.TryBeginTransition(
            7,
            DomainHypercallTransitionKind.NewE2,
            out _));

        var core = fixture.Core;
        Assert.True(fixture.Carrier.Execute(ref core));
        DomainHypercallExecutionResult denied = Assert.IsType<DomainHypercallExecutionResult>(
            fixture.Carrier.ExactHypercallExecutionResult);
        Assert.Equal(DomainHypercallExecutionDecision.Disabled, denied.Decision);
        Assert.Null(denied.Receipt);
        Assert.True(fixture.Carrier.CreateRetireEffect().IsFaulted);
    }

    [Fact]
    public async Task ConcurrentKillSwitches_CannotRevokeAReplacementActivation()
    {
        var fixture = VmxPrFCanonicalHypercallCompositionTests.CreateFixture(configure: false);
        var profile = new DomainHypercallExactRuntimeProfile(
            7,
            fixture.Scheduler,
            fixture.Core.ExactHypercallRetireOwner);
        Assert.True(profile.Activate(DomainHypercallExactActivationRequest.Phase38Exact).IsActivated);

        Task<DomainHypercallKillSwitchResult> first = Task.Run(
            () => profile.KillSwitch(TimeSpan.FromSeconds(5)));
        Task<DomainHypercallKillSwitchResult> second = Task.Run(
            () => profile.KillSwitch(TimeSpan.FromSeconds(5)));
        DomainHypercallKillSwitchResult[] results = await Task.WhenAll(first, second);

        Assert.Contains(results, result => result.Decision == DomainHypercallKillSwitchDecision.DisabledFaultOnly);
        Assert.Contains(results, result => result.Decision == DomainHypercallKillSwitchDecision.AlreadyDisabledFaultOnly);
        Assert.All(results, result => Assert.True(result.IsDeterministicFaultOnly));
        Assert.False(fixture.Scheduler.HasExactVirtualizationComposition);
        Assert.Equal(DomainHypercallLifecycleState.DisabledFaultOnly, profile.LifecycleGate.Observe().State);

        DomainHypercallExactActivationResult reactivated =
            profile.Activate(DomainHypercallExactActivationRequest.Phase38Exact);
        Assert.True(reactivated.IsActivated);
        Assert.True(fixture.Scheduler.HasExactVirtualizationComposition);
    }
}
