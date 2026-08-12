using YAKSys_Hybrid_CPU.Core;

namespace YAKSys_Hybrid_CPU.VirtualizationDiagnostics;

internal sealed class PrjExactReleaseActivationRollbackScenario : IVirtualizationScenario
{
    public string Id => "prj-exact-release-activation-rollback";
    public string Description =>
        "PR-J real default-disabled exact profile activation, lifecycle drain, kill-switch, and fault-only rollback contract.";
    public DiagnosticSurfaceKind SurfaceKind => DiagnosticSurfaceKind.RuntimeContract;

    public Task ExecuteAsync(ScenarioExecutionContext context, CancellationToken cancellationToken)
    {
        for (int iteration = 0; iteration < context.Profile.Iterations; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PrfCanonicalHypercallCompositionScenario.Fixture fixture =
                PrfCanonicalHypercallCompositionScenario.CreateFixture(
                    configure: false,
                    replayEpoch: checked((ulong)iteration + 1));
            var profile = new DomainHypercallExactRuntimeProfile(
                7,
                fixture.Scheduler,
                fixture.Core.ExactHypercallRetireOwner);

            DomainHypercallLifecycleSnapshot before = profile.LifecycleGate.Observe();
            context.Check(
                before.State == DomainHypercallLifecycleState.DisabledFaultOnly &&
                !fixture.Scheduler.HasExactVirtualizationComposition,
                "exact activation must default disabled with no scheduler binding");

            DomainHypercallExactActivationResult adjacent = profile.Activate(
                DomainHypercallExactActivationRequest.Phase38Exact with { NumericLeaf = 0x0002 });
            context.Check(
                adjacent.Decision == DomainHypercallExactActivationDecision.DeniedNonExactProfile &&
                !adjacent.ExactBindingPresent && !adjacent.ExactGrantLive,
                "adjacent leaf activation must fail before provisioning");

            DomainHypercallExactActivationResult activated =
                profile.Activate(DomainHypercallExactActivationRequest.Phase38Exact);
            context.Check(activated.IsActivated,
                "only the exact accepted Phase-38 profile must activate");
            context.Check(
                PrfCanonicalHypercallCompositionScenario.Materialize(fixture, 1),
                "activated exact profile must reach one E2 through the canonical scheduler seam");
            DomainHypercallLiveAuthorityCounts live = profile.DrainOwner!.ObserveLiveAuthorities();
            context.Check(live.E2 == 1 && live.Total == 1,
                "pre-kill-switch diagnostic must observe exactly one live E2");

            DomainHypercallKillSwitchResult killed = profile.KillSwitch(TimeSpan.FromSeconds(5));
            context.Check(killed.IsDeterministicFaultOnly,
                "kill switch must drain all owners, revoke binding/grant, and restore fault-only fallback");
            context.Check(killed.Trace.SequenceEqual(new[]
            {
                DomainHypercallKillSwitchStep.NewE2Closed,
                DomainHypercallKillSwitchStep.TransitionsQuiescent,
                DomainHypercallKillSwitchStep.RegistriesQuiescent,
                DomainHypercallKillSwitchStep.ExactBindingAndGrantRevoked,
                DomainHypercallKillSwitchStep.DeterministicFaultOnlyFallbackRestored,
            }), "kill-switch trace order must match the release contract");

            YAKSys_Hybrid_CPU.Processor.CPU_Core core = fixture.Core;
            context.Check(fixture.Carrier.Execute(ref core),
                "an already-carried dispatch must return through the deterministic disabled contour");
            context.Check(
                fixture.Carrier.ExactHypercallExecutionResult?.Decision ==
                    DomainHypercallExecutionDecision.Disabled &&
                fixture.Carrier.CreateRetireEffect().IsFaulted,
                "rollback must publish neither completion nor retire authority");

            context.Count("default_disabled_states");
            context.Count("adjacent_activation_denials");
            context.Count("exact_profile_activations");
            context.Count("new_e2_closed");
            context.Count("transition_quiescence_proofs");
            context.Count("registry_quiescence_proofs");
            context.Count("exact_binding_revocations");
            context.Count("exact_grant_revocations");
            context.Count("fault_only_rollbacks");
            context.Count("compatibility_authority_publications", 0);
            context.Count("completion_shortcuts", 0);
            context.Count("retire_shortcuts", 0);
            context.Trace("prj-exact-release-activation-rollback",
                ("evidenceClass", "release/lifecycle/activation/rollback/non-authority"),
                ("decisionId", DomainHypercallExactActivationRequest.Phase38Exact.DecisionId),
                ("namespace", DomainHypercallExactActivationRequest.Phase38Exact.OperationNamespace),
                ("leaf", DomainHypercallExactActivationRequest.Phase38Exact.NumericLeaf),
                ("operation", DomainHypercallExactActivationRequest.Phase38Exact.OperationId),
                ("ownerId", DomainHypercallExactActivationRequest.Phase38Exact.OwnerId),
                ("lifecycleEpochBefore", before.LifecycleEpoch),
                ("lifecycleEpochAfter", killed.Lifecycle.LifecycleEpoch),
                ("transitionsInFlightAfter", killed.Counts.TransitionsInFlight),
                ("e2After", killed.Counts.E2),
                ("e3After", killed.Counts.E3),
                ("e5After", killed.Counts.E5),
                ("e6After", killed.Counts.E6),
                ("killSwitchTrace", string.Join('>', killed.Trace)),
                ("exactBindingPresent", killed.ExactBindingPresent),
                ("exactGrantLive", killed.ExactGrantLive),
                ("compatibilityAuthority", false));
            context.CompleteIteration(
                "Exact activation and ordered kill-switch completed with zero live authorities and fault-only fallback.");
        }

        return Task.CompletedTask;
    }
}
