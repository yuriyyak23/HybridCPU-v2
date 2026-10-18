using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;

namespace YAKSys_Hybrid_CPU.VirtualizationDiagnostics;

internal sealed class ResearchRuntimeProbeScenario : IVirtualizationScenario
{
    public string Id => "research-runtime-probe";
    public string Description => "TESTING-only SafetyVerifier-admitted neutral state-minimal probe with exact-once diagnostics.";
    public DiagnosticSurfaceKind SurfaceKind => DiagnosticSurfaceKind.RuntimeContract;

    public Task ExecuteAsync(ScenarioExecutionContext context, CancellationToken cancellationToken)
    {
        for (int iteration = 0; iteration < context.Profile.Iterations; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var verifier = new SafetyVerifier();
            var owner = new ResearchVirtualizationRuntimeOwner();
            Attempt live = CreateAttempt(verifier, (ulong)iteration * 3 + 1);
            ResearchVirtualizationProbeAdmissionResult admission = Admit(verifier, owner, live);

            context.Check(admission.IsIssued,
                "SafetyVerifier must issue prototype E2 only over a live E1 carrier and neutral policy snapshot");
            SafetyVerifier.ResearchVirtualizationOperationAdmissionCertificate certificate = admission.Certificate
                ?? throw new InvalidOperationException("Research admission omitted its certificate.");
            context.Check(!certificate.HasNumericLeaf,
                "research certificate must not allocate or imply a VMCALL numeric leaf");
            context.Check(!certificate.CompletionPublicationAuthorized,
                "research certificate must not authorize completion publication");
            context.Check(!certificate.RetirePublicationAuthorized,
                "research certificate must not authorize retire publication");

            ResearchVirtualizationProbeExecutionResult execution = owner.Execute(verifier, certificate, live.Context);
            context.Check(execution.Succeeded, "neutral research operation must execute once");
            ResearchVirtualizationRuntimeOwner.ExecutionReceipt receipt = execution.Receipt
                ?? throw new InvalidOperationException("Research execution omitted its receipt.");
            context.Check(receipt.PayloadLength == 0, "first research slice must remain no-payload");
            context.Check(receipt.StateMutationCount == 0, "first research slice must remain no-state");
            context.Check(!receipt.CompletionPublicationAuthorized,
                "execution receipt must not authorize completion publication");
            context.Check(!receipt.RetirePublicationAuthorized,
                "execution receipt must not authorize retire publication");
            context.Check(
                owner.Execute(verifier, certificate, live.Context).Decision == ResearchVirtualizationProbeExecutionDecision.DeniedDuplicateAttempt,
                "research certificate must be consumed exactly once");
            context.Check(
                new ResearchVirtualizationRuntimeOwner().Execute(verifier, certificate, live.Context).Decision ==
                    ResearchVirtualizationProbeExecutionDecision.DeniedForeignOwner,
                "foreign neutral owner must reject the certificate");

            Attempt stalePolicyAttempt = CreateAttempt(verifier, (ulong)iteration * 3 + 2);
            SafetyVerifier.ResearchVirtualizationOperationAdmissionCertificate stalePolicyCertificate =
                Admit(verifier, owner, stalePolicyAttempt).Certificate
                ?? throw new InvalidOperationException("Research policy invalidation probe was not admitted.");

            Attempt staleContextAttempt = CreateAttempt(verifier, (ulong)iteration * 4 + 3001);
            SafetyVerifier.ResearchVirtualizationOperationAdmissionCertificate staleContextCertificate =
                Admit(verifier, owner, staleContextAttempt).Certificate
                ?? throw new InvalidOperationException("Research context invalidation probe was not admitted.");
            staleContextAttempt.Context.Invalidate();
            context.Check(
                owner.Execute(verifier, staleContextCertificate, staleContextAttempt.Context).Decision ==
                    ResearchVirtualizationProbeExecutionDecision.DeniedStaleRuntimeContext,
                "restore/runtime-context invalidation must deny an unconsumed research certificate");

            owner.InvalidatePolicy();
            context.Check(
                owner.Execute(verifier, stalePolicyCertificate, stalePolicyAttempt.Context).Decision ==
                    ResearchVirtualizationProbeExecutionDecision.DeniedStalePolicyGeneration,
                "policy invalidation must deny an unconsumed research certificate");

            Attempt staleE1Attempt = CreateAttempt(verifier, (ulong)iteration * 3 + 3);
            verifier.InvalidateVirtualizationAdmissions(ReplayPhaseInvalidationReason.Manual);
            context.Check(
                Admit(verifier, owner, staleE1Attempt).Decision ==
                    ResearchVirtualizationProbeAdmissionDecision.DeniedE1Carrier,
                "E1 invalidation must prevent prototype E2 issuance");

            var revocationVerifier = new SafetyVerifier();
            var revocationOwner = new ResearchVirtualizationRuntimeOwner();
            Attempt revokedAfterE2Attempt = CreateAttempt(revocationVerifier, (ulong)iteration * 5 + 6001);
            SafetyVerifier.ResearchVirtualizationOperationAdmissionCertificate revokedAfterE2Certificate =
                Admit(revocationVerifier, revocationOwner, revokedAfterE2Attempt).Certificate
                ?? throw new InvalidOperationException("Post-E2 revocation probe was not admitted.");
            revocationVerifier.InvalidateVirtualizationAdmissions(ReplayPhaseInvalidationReason.Manual);
            context.Check(
                revocationOwner.Execute(
                    revocationVerifier,
                    revokedAfterE2Certificate,
                    revokedAfterE2Attempt.Context).Decision ==
                ResearchVirtualizationProbeExecutionDecision.DeniedStaleAdmission,
                "E1 revocation after prototype E2 issuance must deny execution");

            context.Count("research_probe_executed");
            context.Count("duplicate_attempt_denied");
            context.Count("foreign_owner_denied");
            context.Count("stale_runtime_context_denied");
            context.Count("stale_policy_denied");
            context.Count("stale_e1_denied");
            context.Count("post_e2_revocation_denied");
            context.Trace("research-runtime-probe",
                ("carrierAttemptId", receipt.Identity.CarrierAttemptId),
                ("replayEpoch", receipt.Identity.ReplayEpoch),
                ("virtualThreadId", receipt.Identity.VirtualThreadId),
                ("domainTag", receipt.Identity.DomainTag),
                ("payloadLength", receipt.PayloadLength),
                ("stateMutationCount", receipt.StateMutationCount));
            context.CompleteIteration("SafetyVerifier-admitted neutral probe executed once; duplicate, foreign and stale paths denied.");
        }

        return Task.CompletedTask;
    }

    private static Attempt CreateAttempt(SafetyVerifier verifier, ulong replayEpoch)
    {
        ReplayPhaseContext phase = VirtualizationFixtures.ReplayPhase(replayEpoch);
        SmtBundleMetadata4Way bundle = VirtualizationFixtures.Bundle(domainTag: 7, operationCount: 1);
        VmxMicroOp carrier = VirtualizationFixtures.CreateVmCall(domainTag: 7);
        VirtualizationAdmissionIssueResult e1 =
            verifier.IssueVirtualizationAdmissionAfterStageB(phase, bundle, carrier, 7, 7);
        var context = new ResearchVirtualizationOperationContext(
            virtualThreadId: e1.Certificate!.VirtualThreadId,
            ownerContextId: e1.Certificate.OwnerContextId,
            domainTag: e1.Certificate.DomainTag,
            addressSpaceTag: 9,
            capabilityGeneration: 1,
            evidenceGeneration: 1,
            restoreGeneration: 1);
        return new(
            phase,
            bundle,
            carrier,
            e1.Certificate ?? throw new InvalidOperationException($"E1 was not issued: {e1.Decision}."),
            context);
    }

    private static ResearchVirtualizationProbeAdmissionResult Admit(
        SafetyVerifier verifier,
        ResearchVirtualizationRuntimeOwner owner,
        Attempt attempt) =>
        verifier.IssueResearchVirtualizationOperationAdmission(
            owner.CapturePolicy(),
            attempt.Context,
            attempt.Context.Capture(attempt.E1.AttemptId, attempt.E1.ReplayEpoch),
            attempt.Phase,
            attempt.Bundle,
            attempt.Carrier,
            7,
            7,
            attempt.E1);

    private sealed record Attempt(
        ReplayPhaseContext Phase,
        SmtBundleMetadata4Way Bundle,
        VmxMicroOp Carrier,
        SafetyVerifier.VirtualizationAdmissionCertificate E1,
        ResearchVirtualizationOperationContext Context);
}
