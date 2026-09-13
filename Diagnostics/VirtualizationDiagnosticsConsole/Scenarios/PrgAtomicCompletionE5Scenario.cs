using YAKSys_Hybrid_CPU.Core;

namespace YAKSys_Hybrid_CPU.VirtualizationDiagnostics;

internal sealed class PrgAtomicCompletionE5Scenario : IVirtualizationScenario
{
    public string Id => "prg-atomic-completion-e5";
    public string Description =>
        "PR-G neutral completion-owner atomic CompletionRecord+E5 evidence with retire still denied.";
    public DiagnosticSurfaceKind SurfaceKind => DiagnosticSurfaceKind.ModelContract;

    public Task ExecuteAsync(ScenarioExecutionContext context, CancellationToken cancellationToken)
    {
        for (int iteration = 0; iteration < context.Profile.Iterations; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PrfCanonicalHypercallCompositionScenario.Fixture active =
                PrfCanonicalHypercallCompositionScenario.CreateFixture(
                    configure: true,
                    replayEpoch: checked((ulong)iteration * 2 + 1),
                    completion: true);
            context.Check(
                PrfCanonicalHypercallCompositionScenario.Materialize(active, 1),
                "canonical seam must produce exact E1/operands/E2");
            YAKSys_Hybrid_CPU.Processor.CPU_Core activeCore = active.Core;
            context.Check(active.Carrier.Execute(ref activeCore), "canonical execute must finish deterministically");
            DomainHypercallCompletionPublicationResult publication =
                active.Carrier.ExactHypercallCompletionPublication!.Value;
            context.Check(publication.IsPublished, "neutral owner must atomically publish one record and E5");
            context.Check(publication.Completion.RecordClass == CompletionRecordClass.Event,
                "exact no-state probe completion must be a neutral event");
            context.Check(publication.E5 is not null &&
                          active.CompletionOwner!.ValidateLive(
                              publication.E5, publication.Completion, active.RestoreOwner),
                "E5 must be opaque, owner-issued, record-bound and live");
            context.Check(!publication.RetirePublicationAuthorized &&
                          active.Carrier.CreateRetireEffect().IsFaulted,
                "PR-G must not grant retire or change the VMX fault contour");

            PrfCanonicalHypercallCompositionScenario.Fixture rollback =
                PrfCanonicalHypercallCompositionScenario.CreateFixture(
                    configure: true,
                    replayEpoch: checked((ulong)iteration * 2 + 2),
                    completion: false);
            context.Check(PrfCanonicalHypercallCompositionScenario.Materialize(rollback, 1),
                "rollback fixture must reach canonical E2");
            YAKSys_Hybrid_CPU.Processor.CPU_Core rollbackCore = rollback.Core;
            context.Check(rollback.Carrier.Execute(ref rollbackCore), "rollback execute must remain deterministic");
            context.Check(rollback.Carrier.ExactHypercallExecutionResult!.Value.IsExecuted &&
                          rollback.Carrier.ExactHypercallCompletionPublication is null &&
                          rollback.Carrier.CreateRetireEffect().IsFaulted,
                "missing completion owner must restore PR-F no-publication behavior");

            context.Count("atomic_completion_record_e5_publications");
            context.Count("live_owner_bound_e5_tokens");
            context.Count("missing_owner_rollbacks");
            context.Count("retire_publications", 0);
            context.Count("compatibility_completion_publications", 0);
            context.Count("frontend_record_constructions", 0);
            context.Trace("prg-atomic-completion-e5",
                ("evidenceClass", "neutral-completion-owner/e5/retire-denied"),
                ("attemptId", publication.E5!.AttemptId),
                ("virtualThreadId", publication.E5.VirtualThreadId),
                ("domainTag", publication.E5.DomainTag),
                ("executionSequence", publication.E5.ExecutionSequence),
                ("completionSequence", publication.E5.CompletionSequence),
                ("completionDigest", publication.E5.CompletionDigest),
                ("e5Digest", publication.E5.TokenDigest),
                ("retireAuthority", false));
            context.CompleteIteration(
                "Neutral owner published atomic record+E5; missing-owner rollback and retire denial held.");
        }

        return Task.CompletedTask;
    }
}
