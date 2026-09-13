using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.VmxRefactoring;

public sealed class VmxPrIDrainRestoreDeterminismTests
{
    [Fact]
    public void PolicyOnlyCheckpoint_ClosesAdmissionAndRestoreAdvancesGenerationWithoutAuthorityPayload()
    {
        var fixture = VmxPrFCanonicalHypercallCompositionTests.CreateFixture(
            configure: true, completion: true, retirement: true);
        DomainHypercallDrainLifecycleOwner lifecycle = CreateLifecycle(fixture);
        ulong generation = fixture.RestoreOwner.CurrentGeneration;

        DomainHypercallDrainResult checkpointed = lifecycle.TryCheckpoint();

        DomainHypercallDrainCheckpoint checkpoint = Assert.IsType<DomainHypercallDrainCheckpoint>(checkpointed.Checkpoint);
        Assert.True(checkpointed.IsCheckpointReady);
        Assert.True(fixture.Composition!.IsDraining);
        Assert.False(checkpoint.ContainsRuntimeAuthority);
        Assert.Equal(VirtualizationCompletionMigrationClassV2.HostOwnedNonMigratable,
            Phase38VirtualizationDecisionSpecV2.Instance.CompletionMigrationClass);
        Assert.Equal(VirtualizationOperationMigrationPolicyV2.DrainOnly,
            Phase38VirtualizationDecisionSpecV2.Instance.OperationMigrationPolicy);

        Assert.True(VmxPrFCanonicalHypercallCompositionTests.Materialize(fixture, 1));
        Assert.Equal(DomainHypercallCompositionDecision.Draining,
            fixture.Scheduler.LastExactVirtualizationCompositionResult!.Value.Decision);

        DomainHypercallDrainResult restored = lifecycle.Restore(checkpoint);
        Assert.True(restored.IsCheckpointReady);
        Assert.Equal(generation + 1, fixture.RestoreOwner.CurrentGeneration);
        Assert.False(fixture.Composition.IsDraining);
        Assert.Equal(DomainHypercallDrainDecision.AlreadyRestored, lifecycle.Restore(checkpoint).Decision);
    }

    [Fact]
    public void LiveE2_DeniesCheckpoint_CancelDrainRevokesIt_AndStaleCertificateCannotReplay()
    {
        var fixture = VmxPrFCanonicalHypercallCompositionTests.CreateFixture(
            configure: true, completion: true, retirement: true);
        Assert.True(VmxPrFCanonicalHypercallCompositionTests.Materialize(fixture, 1));
        SafetyVerifier.VirtualizationOperationAdmissionCertificate e2 = Assert.IsType<SafetyVerifier.VirtualizationOperationAdmissionCertificate>(
            fixture.Scheduler.LastExactVirtualizationCompositionResult!.Value.E2);
        DomainHypercallDrainLifecycleOwner lifecycle = CreateLifecycle(fixture);

        DomainHypercallDrainResult denied = lifecycle.TryCheckpoint();
        Assert.Equal(DomainHypercallDrainDecision.InFlightAuthority, denied.Decision);
        Assert.Equal(1, denied.Counts.E2);

        DomainHypercallDrainResult drained = lifecycle.CancelAndCheckpoint();
        Assert.True(drained.IsCheckpointReady);
        Assert.Equal(1, drained.CancelledAuthorities);
        Assert.Equal(VirtualizationE2Decision.Revoked,
            fixture.Scheduler.ExactVirtualizationCanonicalVerifier!.ValidateVirtualizationE2(e2, fixture.RestoreOwner).Decision);
        Assert.True(lifecycle.Restore(drained.Checkpoint).IsCheckpointReady);
        Assert.False(fixture.Scheduler.ExactVirtualizationCanonicalVerifier.ValidateVirtualizationE2(e2, fixture.RestoreOwner).IsLive);
    }

    [Fact]
    public void LiveE3AndE5_AreAuthoritativeDrainBlockersAndCancellationLeavesZero()
    {
        var e3Fixture = VmxPrFCanonicalHypercallCompositionTests.CreateFixture(configure: true);
        Assert.True(VmxPrFCanonicalHypercallCompositionTests.Materialize(e3Fixture, 1));
        YAKSys_Hybrid_CPU.Processor.CPU_Core e3Core = e3Fixture.Core;
        Assert.True(e3Fixture.Carrier.Execute(ref e3Core));
        var e3Completion = new DomainHypercallCompletionOwner();
        DomainHypercallDrainLifecycleOwner e3Lifecycle = CreateLifecycle(e3Fixture, e3Completion);
        Assert.Equal(1, e3Lifecycle.TryCheckpoint().Counts.E3);
        Assert.True(e3Lifecycle.CancelAndCheckpoint().IsCheckpointReady);
        Assert.False(e3Fixture.Executor!.ValidateReceipt(
            e3Fixture.Carrier.ExactHypercallExecutionReceipt, e3Fixture.RestoreOwner).IsValid);

        var e5Fixture = ExecuteToE5();
        DomainHypercallDrainLifecycleOwner e5Lifecycle = CreateLifecycle(e5Fixture);
        Assert.Equal(1, e5Lifecycle.TryCheckpoint().Counts.E5);
        DomainHypercallDrainResult drained = e5Lifecycle.CancelAndCheckpoint();
        Assert.True(drained.IsCheckpointReady);
        Assert.Equal(1, drained.CancelledAuthorities);
        Assert.False(e5Fixture.CompletionOwner!.ValidateLive(
            e5Fixture.Carrier.ExactHypercallCompletionPublication!.Value.E5,
            e5Fixture.Carrier.ExactHypercallCompletionPublication.Value.Completion,
            e5Fixture.RestoreOwner));
    }

    [Fact]
    public void LiveE6_BlocksCheckpoint_AndForgedOrWrongProfileCheckpointFailsClosed()
    {
        var fixture = ExecuteToE5();
        DomainHypercallRetireOwner retireOwner = fixture.Core.ExactHypercallRetireOwner;
        DomainHypercallRetireResult issued = retireOwner.Issue(
            fixture.CompletionOwner!, fixture.Carrier.ExactHypercallCompletionPublication!.Value,
            fixture.RestoreOwner, Eligibility(fixture));
        Assert.True(issued.IsIssued);
        DomainHypercallDrainLifecycleOwner lifecycle = CreateLifecycle(fixture);

        Assert.Equal(1, lifecycle.TryCheckpoint().Counts.E6);
        DomainHypercallDrainResult drained = lifecycle.CancelAndCheckpoint();
        Assert.True(drained.IsCheckpointReady);
        Assert.Equal(1, drained.CancelledAuthorities);
        Assert.False(retireOwner.ValidateLive(issued.E6, fixture.RestoreOwner, 101, 201));

        var forged = new DomainHypercallDrainCheckpoint(
            7, 1, fixture.RestoreOwner.CurrentGeneration,
            VirtualizationDecisionValidatorV2.ExpectedDecisionId,
            "wrong-spec-digest", "wrong-checkpoint-digest");
        Assert.Equal(DomainHypercallDrainDecision.InvalidCheckpoint, lifecycle.Restore(forged).Decision);
    }

    [Fact]
    public void PreOperationAndPostRetireCheckpoints_AreEquivalentPolicyIdentityWithZeroLiveAuthority()
    {
        var before = VmxPrFCanonicalHypercallCompositionTests.CreateFixture(
            configure: true, completion: true, retirement: true);
        DomainHypercallDrainCheckpoint pre = Assert.IsType<DomainHypercallDrainCheckpoint>(
            CreateLifecycle(before).TryCheckpoint().Checkpoint);

        var after = ExecuteToE5();
        DomainHypercallRetireOwner retireOwner = after.Core.ExactHypercallRetireOwner;
        DomainHypercallRetireResult issued = retireOwner.Issue(
            after.CompletionOwner!, after.Carrier.ExactHypercallCompletionPublication!.Value,
            after.RestoreOwner, Eligibility(after));
        Assert.True(issued.IsIssued);
        Assert.True(retireOwner.ConsumeAtPreciseRetire(issued.E6, after.RestoreOwner, 101, 201));
        DomainHypercallDrainCheckpoint post = Assert.IsType<DomainHypercallDrainCheckpoint>(
            CreateLifecycle(after).TryCheckpoint().Checkpoint);

        Assert.Equal(pre.DomainTag, post.DomainTag);
        Assert.Equal(pre.DecisionId, post.DecisionId);
        Assert.Equal(pre.SpecDigest, post.SpecDigest);
        Assert.Equal(pre.RestoreGeneration, post.RestoreGeneration);
        Assert.True(pre.LiveAuthorityCounts.IsDrained);
        Assert.True(post.LiveAuthorityCounts.IsDrained);
        Assert.False(pre.ContainsRuntimeAuthority);
        Assert.False(post.ContainsRuntimeAuthority);
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 0)]
    [InlineData(false, 3)]
    [InlineData(true, 3)]
    public void ExactNoStateArchitecturalTrace_IsScheduleAndFspIndependent(bool fspEnabled, int smtWidth)
    {
        _ = fspEnabled;
        _ = smtWidth;
        DomainHypercallArchitecturalTrace first = DomainHypercallArchitecturalTrace.ExactProbe(0, 7, retired: true, faulted: false);
        DomainHypercallArchitecturalTrace second = DomainHypercallArchitecturalTrace.ExactProbe(0, 7, retired: true, faulted: false);

        Assert.Equal(first, second);
        Assert.Equal(0, first.RegisterWrites);
        Assert.Equal(0, first.MemoryWrites);
        Assert.Equal(0, first.VmStateWrites);
        Assert.Equal(0, first.Redirects);
    }

    private static VmxPrFCanonicalHypercallCompositionTests.Fixture ExecuteToE5()
    {
        var fixture = VmxPrFCanonicalHypercallCompositionTests.CreateFixture(
            configure: true, completion: true, retirement: true);
        Assert.True(VmxPrFCanonicalHypercallCompositionTests.Materialize(fixture, 1));
        YAKSys_Hybrid_CPU.Processor.CPU_Core core = fixture.Core;
        Assert.True(fixture.Carrier.Execute(ref core));
        Assert.True(fixture.Carrier.ExactHypercallCompletionPublication!.Value.IsPublished);
        return fixture;
    }

    private static DomainHypercallDrainLifecycleOwner CreateLifecycle(
        VmxPrFCanonicalHypercallCompositionTests.Fixture fixture,
        DomainHypercallCompletionOwner? completionOwner = null) => new(
            7,
            fixture.Composition!,
            fixture.Scheduler.ExactVirtualizationCanonicalVerifier!,
            fixture.Executor!,
            completionOwner ?? fixture.CompletionOwner!,
            fixture.Core.ExactHypercallRetireOwner,
            fixture.RestoreOwner);

    private static DomainHypercallRetireEligibility Eligibility(
        VmxPrFCanonicalHypercallCompositionTests.Fixture fixture)
    {
        SafetyVerifier.VirtualizationAdmissionCertificate e1 = fixture.Carrier.VirtualizationAdmission!;
        return new(
            fixture.Carrier, e1.VirtualThreadId, e1.DomainTag,
            e1.SourceSlotId, e1.WorkingSlotId, e1.BundleIdentity,
            OperationAttempt: 1, PhysicalLaneId: 7, RetireOrderIndex: 0,
            RetireWindowIdentity: 101, OrderEpoch: 201,
            IsCanonicalHead: true, IsSquashed: false, HasWinningException: false);
    }
}
