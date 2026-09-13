using YAKSys_Hybrid_CPU.Core;

namespace YAKSys_Hybrid_CPU.SecureComputeDiagnostics;

internal sealed class FailClosedBoundaryScenario : ISecureComputeScenario
{
    public string Id => "fail-closed-boundary";
    public string Description => "Verifies runtime activation, VMX, compiler emission and limited release remain zero-authority.";
    public DiagnosticSurfaceKind SurfaceKind => DiagnosticSurfaceKind.PolicyClassifier;
    public string AuthorityCeiling => "Negative/future-gated classifier evidence only; never execution or release approval.";

    public Task ExecuteAsync(ScenarioExecutionContext context, CancellationToken cancellationToken)
    {
        for (int iteration = 0; iteration < context.Profile.Iterations; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SecurePositiveRuntimeExecutionActivationResult runtime =
                SecurePositiveRuntimeExecutionActivationPolicy.FailClosed.Classify(
                    new SecurePositiveRuntimeExecutionActivationRequest(
                        SecurePositiveRuntimePathCandidate.FutureNamedPositiveRuntimePath));
            context.Check(runtime.IsFutureGated, "positive runtime path remains future-gated");

            SecureComputeNamedPathVmxZeroAuthorityResult vmx =
                SecureComputeNamedPathVmxZeroAuthorityPolicy.FailClosed.Classify(
                    new SecureComputeNamedPathVmxZeroAuthorityRequest(
                        SecureComputeNamedPositivePath.FutureRestrictedRuntimeExecution,
                        HasNeutralRuntimeResult: true,
                        RequestsCompatibilityProjection: true));
            context.Check(vmx.IsAllowed, "read-only compatibility projection is classified");
            context.Check(!vmx.CreatesAnyVmxAuthority, "classified projection creates no VMX authority");
            context.Check(!vmx.CompletionPublicationAuthorized && !vmx.RetirePublicationAuthorized,
                "VMX projection creates no completion/retire authority");

            SecureComputeControlledEmissionResult compiler =
                SecureComputeControlledEmissionGatePolicy.FailClosed.Classify(
                    new SecureComputeControlledEmissionRequest(
                        SecureComputeCompilerEmissionPath.FutureControlledEmission,
                        RequestsCompilerEmission: true,
                        HasPositiveNeutralRuntimeOwner: true,
                        HasControlledEmissionRfc: true,
                        HasReleaseApproval: true,
                        BackendExecutionAuthorized: true));
            context.Check(!compiler.IsAllowed && !compiler.CreatesAnyEmissionAuthority,
                "compiler emission remains denied even with caller assertions set");

            SecureComputePhase22LimitedReleaseGateResult release =
                SecureComputePhase22LimitedReleaseGatePolicy.FailClosed.Classify(new(
                    Phase21MatrixEvidenceAccepted: true,
                    Phase21FuturePositiveTestsRemainGated: false,
                    NamedPositiveRuntimePathProven: true,
                    OwnerSpecificRfcAdrAccepted: true,
                    ProductionOwnerCodeExists: true,
                    OwnerPathReachabilityProven: true,
                    TypedRequestResultModelExists: true,
                    BackendResultOwnerBoundaryProven: true,
                    CompletionRetirePolicyForNamedPathProven: true,
                    MigrationManifestRestoreEvidenceComplete: true,
                    DebugAttestationVisibilityLimitsProven: true,
                    VmxProjectionAfterNeutralResultOnly: true,
                    CompilerNoEmissionDecisionRecorded: true,
                    Phase18NestedExecutionExcluded: true,
                    ProductClaimScopedToNamedPath: true,
                    BoundedRollbackProcedureReviewed: true,
                    BackendExecutionAuthorityLocallyProven: true));
            context.Check(!release.CreatesAnyReleaseAuthority && !release.ProductionReleaseApproved,
                "Phase 22 remains hard-denied even when all evidence flags are true");

            context.Count("runtime_activation_denied");
            context.Count("vmx_zero_authority_projection_classified");
            context.Count("compiler_emission_denied");
            context.Count("limited_release_denied");
            context.Trace("fail-closed", ("runtime", runtime.Decision), ("vmx", vmx.Decision),
                ("compiler", compiler.Decision), ("release", release.Decision));
            context.CompleteIteration("Fail-closed boundary completed.");
        }

        context.Finding("C2-RELEASE-EVIDENCE", DiagnosticSeverity.Blocker,
            "Limited release remains hard-denied",
            "Phase 22 intentionally creates no release authority. Independent named-path owner/path/reachability evidence is still absent from this diagnostic.");
        context.Finding("VMX-ZERO-AUTHORITY", DiagnosticSeverity.Information,
            "VMX remains a read-only projection boundary",
            "The only accepted VMX case exposes a compatibility projection after a neutral result and authorizes no SecureCompute state, completion or retire effect.");
        return Task.CompletedTask;
    }
}
