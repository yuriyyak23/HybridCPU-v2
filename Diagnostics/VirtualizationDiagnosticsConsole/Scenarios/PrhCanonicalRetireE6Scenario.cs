using YAKSys_Hybrid_CPU.Core;

namespace YAKSys_Hybrid_CPU.VirtualizationDiagnostics;

internal sealed class PrhCanonicalRetireE6Scenario : IVirtualizationScenario
{
    public string Id => "prh-canonical-retire-e6";
    public string Description =>
        "PR-H canonical retire-owner E5-to-E6 consume-once no-state evidence; compatibility remains fault-only.";
    public DiagnosticSurfaceKind SurfaceKind => DiagnosticSurfaceKind.ModelContract;

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
                "canonical seam must materialize E1/operands/E2");
            YAKSys_Hybrid_CPU.Processor.CPU_Core core = fixture.Core;
            context.Check(fixture.Carrier.Execute(ref core), "exact no-state execute must finish");
            DomainHypercallCompletionPublicationResult publication =
                fixture.Carrier.ExactHypercallCompletionPublication!.Value;
            SafetyVerifier.VirtualizationAdmissionCertificate e1 =
                fixture.Carrier.VirtualizationAdmission!;
            ulong window = checked((ulong)iteration * 2 + 1);
            ulong epoch = checked((ulong)iteration * 2 + 2);
            var eligibility = new DomainHypercallRetireEligibility(
                fixture.Carrier, e1.VirtualThreadId, e1.DomainTag,
                e1.SourceSlotId, e1.WorkingSlotId, e1.BundleIdentity,
                OperationAttempt: checked((ulong)iteration + 1),
                PhysicalLaneId: 7,
                RetireOrderIndex: 0,
                RetireWindowIdentity: window,
                OrderEpoch: epoch,
                IsCanonicalHead: true,
                IsSquashed: false,
                HasWinningException: false);
            DomainHypercallRetireOwner owner = fixture.Core.ExactHypercallRetireOwner;
            DomainHypercallRetireResult issued = owner.Issue(
                fixture.CompletionOwner!, publication, fixture.RestoreOwner, eligibility);
            context.Check(issued.IsIssued && issued.E6 is not null,
                "canonical retire owner must issue one opaque E6 from live E5");
            context.Check(owner.ConsumeAtPreciseRetire(issued.E6, fixture.RestoreOwner, window, epoch),
                "precise retire must consume E6 exactly once");
            context.Check(!owner.ConsumeAtPreciseRetire(issued.E6, fixture.RestoreOwner, window, epoch),
                "duplicate E6 consume must fail closed");
            context.Check(fixture.Carrier.CreateRetireEffect().IsFaulted &&
                          !fixture.Carrier.ExactHypercallExecutionReceipt!.HasStateEffect,
                "compatibility effect must remain fault-only and exact probe must remain no-state");

            context.Count("canonical_e6_issuances");
            context.Count("precise_e6_consumptions");
            context.Count("duplicate_e6_denials");
            context.Count("register_writes", 0);
            context.Count("memory_writes", 0);
            context.Count("vm_state_writes", 0);
            context.Count("compatibility_success_effects", 0);
            context.Trace("prh-canonical-retire-e6",
                ("evidenceClass", "canonical-retire-owner/e6/no-state"),
                ("attemptId", issued.E6!.AttemptId),
                ("retireWindowIdentity", issued.E6.RetireWindowIdentity),
                ("orderEpoch", issued.E6.OrderEpoch),
                ("e5Digest", issued.E6.E5Digest),
                ("e6Digest", issued.E6.GrantDigest),
                ("compatibilityRetireAuthority", false));
            context.CompleteIteration(
                "Canonical retire owner consumed one E5/E6 chain; duplicate and compatibility shortcuts stayed denied.");
        }

        return Task.CompletedTask;
    }
}
