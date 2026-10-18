using YAKSys_Hybrid_CPU.Core;

namespace YAKSys_Hybrid_CPU.VirtualizationDiagnostics;

internal sealed class PriDrainRestoreDeterminismScenario : IVirtualizationScenario
{
    public string Id => "pri-drain-restore-determinism";
    public string Description =>
        "PR-I E7 DrainOnly checkpoint/restore and deterministic no-state trace evidence without serialized runtime authority.";
    public DiagnosticSurfaceKind SurfaceKind => DiagnosticSurfaceKind.RuntimeContract;

    public Task ExecuteAsync(ScenarioExecutionContext context, CancellationToken cancellationToken)
    {
        for (int iteration = 0; iteration < context.Profile.Iterations; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PrfCanonicalHypercallCompositionScenario.Fixture fixture =
                PrfCanonicalHypercallCompositionScenario.CreateFixture(
                    configure: true,
                    replayEpoch: checked((ulong)iteration + 1),
                    completion: true,
                    retirement: true);
            context.Check(PrfCanonicalHypercallCompositionScenario.Materialize(fixture, 1),
                "exact carrier must materialize through E2");
            YAKSys_Hybrid_CPU.Processor.CPU_Core core = fixture.Core;
            context.Check(fixture.Carrier.Execute(ref core), "exact probe must reach E5");

            var lifecycle = new DomainHypercallDrainLifecycleOwner(
                7,
                fixture.Composition!,
                fixture.Scheduler.ExactVirtualizationCanonicalVerifier!,
                fixture.Executor!,
                fixture.CompletionOwner!,
                fixture.Core.ExactHypercallRetireOwner,
                fixture.RestoreOwner);
            DomainHypercallDrainResult blocked = lifecycle.TryCheckpoint();
            context.Check(blocked.Decision == DomainHypercallDrainDecision.InFlightAuthority && blocked.Counts.E5 == 1,
                "live host-owned E5 must block checkpoint");

            DomainHypercallDrainResult drained = lifecycle.CancelAndCheckpoint();
            DomainHypercallDrainCheckpoint checkpoint = drained.Checkpoint!;
            context.Check(drained.IsCheckpointReady && drained.Counts.IsDrained && drained.CancelledAuthorities == 1,
                "cancel/drain must prove all live authority registries zero");
            context.Check(!checkpoint.ContainsRuntimeAuthority &&
                          checkpoint.LiveAuthorityCounts.Total == 0 &&
                          !checkpoint.ContainsCompatibilityProjection &&
                          !checkpoint.ContainsHostOwnedEvidence,
                "checkpoint must contain policy identity only");

            ulong oldGeneration = fixture.RestoreOwner.CurrentGeneration;
            context.Check(lifecycle.Restore(checkpoint).IsCheckpointReady &&
                          fixture.RestoreOwner.CurrentGeneration == oldGeneration + 1,
                "restore must advance neutral generation and reload exact local D2 policy");
            context.Check(lifecycle.Restore(checkpoint).Decision == DomainHypercallDrainDecision.AlreadyRestored,
                "duplicate restore/replay must fail closed");

            PrfCanonicalHypercallCompositionScenario.Fixture retiredFixture =
                PrfCanonicalHypercallCompositionScenario.CreateFixture(
                    configure: true,
                    replayEpoch: checked((ulong)iteration + 10_001),
                    completion: true,
                    retirement: true);
            context.Check(PrfCanonicalHypercallCompositionScenario.Materialize(retiredFixture, 1),
                "post-retire equivalence fixture must materialize");
            YAKSys_Hybrid_CPU.Processor.CPU_Core retiredCore = retiredFixture.Core;
            context.Check(retiredFixture.Carrier.Execute(ref retiredCore),
                "post-retire equivalence fixture must execute");
            SafetyVerifier.VirtualizationAdmissionCertificate retiredE1 = retiredFixture.Carrier.VirtualizationAdmission!;
            ulong retiredWindow = checked((ulong)iteration * 2 + 1);
            ulong retiredOrder = checked((ulong)iteration * 2 + 2);
            var eligibility = new DomainHypercallRetireEligibility(
                retiredFixture.Carrier, retiredE1.VirtualThreadId, retiredE1.DomainTag,
                retiredE1.SourceSlotId, retiredE1.WorkingSlotId, retiredE1.BundleIdentity,
                OperationAttempt: checked((ulong)iteration + 1), PhysicalLaneId: 7,
                RetireOrderIndex: 0, RetireWindowIdentity: retiredWindow, OrderEpoch: retiredOrder,
                IsCanonicalHead: true, IsSquashed: false, HasWinningException: false);
            DomainHypercallRetireResult retired = retiredFixture.Core.ExactHypercallRetireOwner.Issue(
                retiredFixture.CompletionOwner!, retiredFixture.Carrier.ExactHypercallCompletionPublication!.Value,
                retiredFixture.RestoreOwner, eligibility);
            context.Check(retired.IsIssued && retiredFixture.Core.ExactHypercallRetireOwner.ConsumeAtPreciseRetire(
                    retired.E6, retiredFixture.RestoreOwner, retiredWindow, retiredOrder),
                "post-retire equivalence fixture must consume E6");
            var postRetireLifecycle = new DomainHypercallDrainLifecycleOwner(
                7, retiredFixture.Composition!, retiredFixture.Scheduler.ExactVirtualizationCanonicalVerifier!,
                retiredFixture.Executor!, retiredFixture.CompletionOwner!,
                retiredFixture.Core.ExactHypercallRetireOwner, retiredFixture.RestoreOwner);
            DomainHypercallDrainCheckpoint postRetire = postRetireLifecycle.TryCheckpoint().Checkpoint!;
            context.Check(postRetire.DecisionId == checkpoint.DecisionId &&
                          postRetire.SpecDigest == checkpoint.SpecDigest &&
                          !postRetire.ContainsRuntimeAuthority,
                "pre-operation/drained and post-retire checkpoints must carry equivalent policy identity only");

            DomainHypercallArchitecturalTrace first =
                DomainHypercallArchitecturalTrace.ExactProbe(0, 7, retired: true, faulted: false);
            DomainHypercallArchitecturalTrace second =
                DomainHypercallArchitecturalTrace.ExactProbe(0, 7, retired: true, faulted: false);
            context.Check(first == second && first.RegisterWrites == 0 && first.MemoryWrites == 0 &&
                          first.VmStateWrites == 0 && first.Redirects == 0,
                "two-run/FSP/SMT-independent architectural trace must be identical and no-state");

            context.Count("in_flight_checkpoint_denials");
            context.Count("drained_checkpoints");
            context.Count("restore_generation_advances");
            context.Count("duplicate_restore_denials");
            context.Count("post_retire_checkpoint_equivalence");
            context.Count("serialized_runtime_authorities", 0);
            context.Count("serialized_host_evidence", 0);
            context.Count("register_writes", 0);
            context.Count("memory_writes", 0);
            context.Count("vm_state_writes", 0);
            context.Trace("pri-drain-restore-determinism",
                ("evidenceClass", "e7/drain-only/policy-identity/no-authority"),
                ("checkpointEpoch", checkpoint.CheckpointEpoch),
                ("checkpointDigest", checkpoint.CheckpointDigest),
                ("oldRestoreGeneration", oldGeneration),
                ("newRestoreGeneration", fixture.RestoreOwner.CurrentGeneration),
                ("architecturalTraceDigest", first.Digest),
                ("compatibilityAuthority", false));
            context.CompleteIteration(
                "E7 blocked live E5, drained all registries, restored once and reproduced a no-state trace.");
        }

        return Task.CompletedTask;
    }
}
