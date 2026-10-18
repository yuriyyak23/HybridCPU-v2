using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.VmxRefactoring;

public sealed class VmxPrJLifecycleConcurrencyTests
{
    private static readonly TimeSpan RaceTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task DrainVsE2ToE3_TransitionPreventsFalseCrossRegistryZero()
    {
        var fixture = CreateFixture(completion: false);
        Assert.True(VmxPrFCanonicalHypercallCompositionTests.Materialize(fixture, 1));
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        PauseTransition(fixture, DomainHypercallTransitionKind.E2ToE3, entered, release);

        Task<bool> execute = Task.Run(() =>
        {
            var core = fixture.Core;
            return fixture.Carrier.Execute(ref core);
        });
        Assert.True(entered.Wait(RaceTimeout));

        DomainHypercallDrainLifecycleOwner lifecycle = CreateLifecycle(fixture);
        Task<DomainHypercallDrainResult> drain = Task.Run(lifecycle.TryCheckpoint);
        Assert.True(SpinWait.SpinUntil(
            () => fixture.LifecycleGate.Observe().State == DomainHypercallLifecycleState.Draining,
            RaceTimeout));
        Assert.Equal(1, fixture.LifecycleGate.Observe().TransitionsInFlight);
        Assert.False(drain.IsCompleted);

        release.Set();
        Assert.True(await execute.WaitAsync(RaceTimeout));
        DomainHypercallDrainResult result = await drain.WaitAsync(RaceTimeout);
        Assert.Equal(DomainHypercallDrainDecision.InFlightAuthority, result.Decision);
        Assert.Equal(1, result.Counts.E3);
        Assert.False(result.Counts.IsDrained);
    }

    [Fact]
    public async Task DrainVsE3ToE5_TransitionPreventsFalseCrossRegistryZero()
    {
        var fixture = CreateFixture(completion: true);
        Assert.True(VmxPrFCanonicalHypercallCompositionTests.Materialize(fixture, 1));
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        PauseTransition(fixture, DomainHypercallTransitionKind.E3ToE5, entered, release);

        Task<bool> execute = Task.Run(() =>
        {
            var core = fixture.Core;
            return fixture.Carrier.Execute(ref core);
        });
        Assert.True(entered.Wait(RaceTimeout));

        DomainHypercallDrainLifecycleOwner lifecycle = CreateLifecycle(fixture);
        Task<DomainHypercallDrainResult> drain = Task.Run(lifecycle.TryCheckpoint);
        Assert.True(SpinWait.SpinUntil(
            () => fixture.LifecycleGate.Observe().State == DomainHypercallLifecycleState.Draining,
            RaceTimeout));
        Assert.Equal(1, fixture.LifecycleGate.Observe().TransitionsInFlight);
        Assert.False(drain.IsCompleted);

        release.Set();
        Assert.True(await execute.WaitAsync(RaceTimeout));
        DomainHypercallDrainResult result = await drain.WaitAsync(RaceTimeout);
        Assert.Equal(DomainHypercallDrainDecision.InFlightAuthority, result.Decision);
        Assert.Equal(1, result.Counts.E5);
    }

    [Fact]
    public async Task DrainVsE5ToE6_TransitionIsPartOfQuiescencePredicate()
    {
        var fixture = ExecuteToE5();
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        PauseTransition(fixture, DomainHypercallTransitionKind.E5ToE6, entered, release);

        Task<DomainHypercallRetireResult> issue = Task.Run(() =>
            fixture.Core.ExactHypercallRetireOwner.Issue(
                fixture.CompletionOwner!,
                fixture.Carrier.ExactHypercallCompletionPublication!.Value,
                fixture.RestoreOwner,
                Eligibility(fixture)));
        Assert.True(entered.Wait(RaceTimeout));

        Task<DomainHypercallDrainResult> drain = Task.Run(() => CreateLifecycle(fixture).TryCheckpoint());
        Assert.True(SpinWait.SpinUntil(
            () => fixture.LifecycleGate.Observe().State == DomainHypercallLifecycleState.Draining,
            RaceTimeout));
        Assert.Equal(1, fixture.LifecycleGate.Observe().TransitionsInFlight);
        Assert.False(drain.IsCompleted);

        release.Set();
        Assert.True((await issue.WaitAsync(RaceTimeout)).IsIssued);
        DomainHypercallDrainResult result = await drain.WaitAsync(RaceTimeout);
        Assert.Equal(DomainHypercallDrainDecision.InFlightAuthority, result.Decision);
        Assert.Equal(1, result.Counts.E6);
        Assert.False(result.Counts.IsDrained);
    }

    [Fact]
    public async Task RestoreVsNewE2_IsEitherDrainingDeniedOrBoundToPostRestoreGeneration()
    {
        for (int iteration = 0; iteration < 24; iteration++)
        {
            var fixture = CreateFixture(completion: true);
            DomainHypercallDrainLifecycleOwner lifecycle = CreateLifecycle(fixture);
            DomainHypercallDrainCheckpoint checkpoint = Assert.IsType<DomainHypercallDrainCheckpoint>(
                lifecycle.TryCheckpoint().Checkpoint);
            using var start = new ManualResetEventSlim();

            Task<DomainHypercallDrainResult> restore = Task.Run(() =>
            {
                start.Wait();
                return lifecycle.Restore(checkpoint);
            });
            Task<bool> materialize = Task.Run(() =>
            {
                start.Wait();
                return VmxPrFCanonicalHypercallCompositionTests.Materialize(fixture, 1);
            });
            start.Set();

            Assert.True((await restore.WaitAsync(RaceTimeout)).IsCheckpointReady);
            _ = await materialize.WaitAsync(RaceTimeout);
            DomainHypercallCompositionResult? composition =
                fixture.Scheduler.LastExactVirtualizationCompositionResult;
            if (composition is { } prepared && prepared.IsPrepared)
            {
                Assert.Equal(
                    fixture.RestoreOwner.CurrentGeneration,
                    prepared.E2!.RestoreGeneration);
            }
            else if (composition is not null)
            {
                Assert.Contains(composition.Value.Decision, new[]
                {
                    DomainHypercallCompositionDecision.Draining,
                    DomainHypercallCompositionDecision.E2Denied,
                });
            }
        }
    }

    [Fact]
    public async Task CancelVsE3Publication_WaitsForHandoffThenCancelsPublishedAuthority()
    {
        var fixture = CreateFixture(completion: false);
        Assert.True(VmxPrFCanonicalHypercallCompositionTests.Materialize(fixture, 1));
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        PauseTransition(fixture, DomainHypercallTransitionKind.E2ToE3, entered, release);

        Task<bool> execute = Task.Run(() =>
        {
            var core = fixture.Core;
            return fixture.Carrier.Execute(ref core);
        });
        Assert.True(entered.Wait(RaceTimeout));
        Task<DomainHypercallDrainResult> cancel = Task.Run(() =>
            CreateLifecycle(fixture).CancelAndCheckpoint(RaceTimeout));
        Assert.True(SpinWait.SpinUntil(
            () => fixture.LifecycleGate.Observe().State == DomainHypercallLifecycleState.Draining,
            RaceTimeout));
        Assert.False(cancel.IsCompleted);

        release.Set();
        Assert.True(await execute.WaitAsync(RaceTimeout));
        DomainHypercallDrainResult result = await cancel.WaitAsync(RaceTimeout);
        Assert.True(result.IsCheckpointReady);
        Assert.Equal(1, result.CancelledAuthorities);
        Assert.True(result.Counts.IsDrained);
    }

    [Fact]
    public async Task CancelVsE5ToE6_WaitsForHandoffThenCancelsPublishedAuthority()
    {
        var fixture = ExecuteToE5();
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        PauseTransition(fixture, DomainHypercallTransitionKind.E5ToE6, entered, release);

        Task<DomainHypercallRetireResult> issue = Task.Run(() =>
            fixture.Core.ExactHypercallRetireOwner.Issue(
                fixture.CompletionOwner!,
                fixture.Carrier.ExactHypercallCompletionPublication!.Value,
                fixture.RestoreOwner,
                Eligibility(fixture)));
        Assert.True(entered.Wait(RaceTimeout));
        Task<DomainHypercallDrainResult> cancel = Task.Run(() =>
            CreateLifecycle(fixture).CancelAndCheckpoint(RaceTimeout));
        Assert.True(SpinWait.SpinUntil(
            () => fixture.LifecycleGate.Observe().State == DomainHypercallLifecycleState.Draining,
            RaceTimeout));
        Assert.False(cancel.IsCompleted);

        release.Set();
        Assert.True((await issue.WaitAsync(RaceTimeout)).IsIssued);
        DomainHypercallDrainResult result = await cancel.WaitAsync(RaceTimeout);
        Assert.True(result.IsCheckpointReady);
        Assert.Equal(1, result.CancelledAuthorities);
        Assert.True(result.Counts.IsDrained);
    }

    [Fact]
    public async Task IndependentDomains_DrainConcurrentlyWithoutCrossDomainSerialization()
    {
        var first = VmxPrFCanonicalHypercallCompositionTests.CreateFixture(
            configure: true, completion: true, retirement: true, domainTag: 7);
        var second = VmxPrFCanonicalHypercallCompositionTests.CreateFixture(
            configure: true, completion: true, retirement: true, domainTag: 9);
        Assert.True(VmxPrFCanonicalHypercallCompositionTests.Materialize(first, 1));
        Assert.True(VmxPrFCanonicalHypercallCompositionTests.Materialize(second, 1));
        using var start = new ManualResetEventSlim();

        Task<DomainHypercallDrainResult> firstDrain = Task.Run(() =>
        {
            start.Wait();
            return CreateLifecycle(first).CancelAndCheckpoint(RaceTimeout);
        });
        Task<DomainHypercallDrainResult> secondDrain = Task.Run(() =>
        {
            start.Wait();
            return CreateLifecycle(second).CancelAndCheckpoint(RaceTimeout);
        });
        start.Set();

        DomainHypercallDrainResult[] results = await Task.WhenAll(firstDrain, secondDrain)
            .WaitAsync(RaceTimeout);
        Assert.All(results, result => Assert.True(result.IsCheckpointReady));
        Assert.All(results, result => Assert.Equal(1, result.CancelledAuthorities));
        Assert.Equal(DomainHypercallLifecycleState.Draining, first.LifecycleGate.Observe().State);
        Assert.Equal(DomainHypercallLifecycleState.Draining, second.LifecycleGate.Observe().State);
    }

    private static VmxPrFCanonicalHypercallCompositionTests.Fixture CreateFixture(bool completion) =>
        VmxPrFCanonicalHypercallCompositionTests.CreateFixture(
            configure: true,
            completion,
            retirement: true);

    private static VmxPrFCanonicalHypercallCompositionTests.Fixture ExecuteToE5()
    {
        var fixture = CreateFixture(completion: true);
        Assert.True(VmxPrFCanonicalHypercallCompositionTests.Materialize(fixture, 1));
        var core = fixture.Core;
        Assert.True(fixture.Carrier.Execute(ref core));
        Assert.True(fixture.Carrier.ExactHypercallCompletionPublication!.Value.IsPublished);
        return fixture;
    }

    private static DomainHypercallDrainLifecycleOwner CreateLifecycle(
        VmxPrFCanonicalHypercallCompositionTests.Fixture fixture) => new(
            fixture.LifecycleGate.DomainTag,
            fixture.Composition!,
            fixture.Scheduler.ExactVirtualizationCanonicalVerifier!,
            fixture.Executor!,
            fixture.CompletionOwner ?? new DomainHypercallCompletionOwner(),
            fixture.Core.ExactHypercallRetireOwner,
            fixture.RestoreOwner);

    private static void PauseTransition(
        VmxPrFCanonicalHypercallCompositionTests.Fixture fixture,
        DomainHypercallTransitionKind kind,
        ManualResetEventSlim entered,
        ManualResetEventSlim release)
    {
        fixture.LifecycleGate.TransitionGapTestHook = observed =>
        {
            if (observed != kind)
                return;
            entered.Set();
            Assert.True(release.Wait(RaceTimeout));
        };
    }

    private static DomainHypercallRetireEligibility Eligibility(
        VmxPrFCanonicalHypercallCompositionTests.Fixture fixture)
    {
        SafetyVerifier.VirtualizationAdmissionCertificate e1 = fixture.Carrier.VirtualizationAdmission!;
        return new(
            fixture.Carrier,
            e1.VirtualThreadId,
            e1.DomainTag,
            e1.SourceSlotId,
            e1.WorkingSlotId,
            e1.BundleIdentity,
            OperationAttempt: 1,
            PhysicalLaneId: 7,
            RetireOrderIndex: 0,
            RetireWindowIdentity: 101,
            OrderEpoch: 201,
            IsCanonicalHead: true,
            IsSquashed: false,
            HasWinningException: false);
    }
}
