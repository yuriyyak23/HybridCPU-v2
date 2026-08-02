namespace YAKSys_Hybrid_CPU.Core;

public enum SecureComputePhase21ConformanceDecision : byte
{
    CurrentMatrixAcceptedFuturePositiveGated = 0,
    PositivePathEvidenceMatrixAcceptedPhase22Required = 1,
    DeniedMissingPhase13RegistryBackedAdmissionEvidence = 2,
    DeniedMissingPhase14CompletionRetireEvidence = 3,
    DeniedMissingPhase15ManifestEvidence = 4,
    DeniedMissingPhase16VisibilityEvidence = 5,
    DeniedMissingPhase17VmxZeroAuthorityEvidence = 6,
    DeniedMissingPhase18NestedDesignFenceEvidence = 7,
    DeniedMissingPhase19NoCompilerChangeEvidence = 8,
    DeniedMissingPhase20FutureGatedRuntimeEvidence = 9,
    DeniedMissingReleaseEvidenceBoundary = 10,
    DeniedProductionActivationOverclaim = 11,
    DeniedCompilerEmissionOverclaim = 12,
    DeniedVmxAuthorityOverclaim = 13,
    DeniedPhase18NestedOpened = 14,
    DeniedManifestOnlyExecutionProof = 15,
    DeniedVisibilityOnlyRuntimeEvidence = 16,
}

public readonly record struct SecureComputePhase21ConformanceEvidenceRequest(
    bool Phase13RegistryBackedAdmissionCannotExecuteCovered,
    bool Phase14CompletionRetireFailClosedCovered,
    bool Phase15ManifestCoverageAndManifestOnlyDenialCovered,
    bool Phase16VisibilityCannotBecomeRuntimeEvidenceCovered,
    bool Phase17VmxZeroAuthorityCovered,
    bool Phase18NestedDesignFenceCovered,
    bool Phase19NoCompilerChangeCovered,
    bool Phase20FutureGatedRuntimeActivationCovered,
    bool ReleaseEvidenceBoundaryCovered,
    bool PositiveRuntimePathProven = false,
    bool RequestsProductionActivation = false,
    bool RequestsCompilerSecureEmission = false,
    bool RequestsVmxOwnedSecureComputeAuthority = false,
    bool OpensPhase18NestedExecution = false,
    bool TreatsManifestOnlyAsExecutionProof = false,
    bool TreatsDebugAttestationAsRuntimeEvidence = false);

public readonly record struct SecureComputePhase21ConformanceEvidenceResult(
    SecureComputePhase21ConformanceDecision Decision,
    bool MatrixEvidenceAccepted,
    bool FuturePositiveTestsRemainGated,
    bool RuntimeExecutionAuthorized,
    bool BackendResultAccepted,
    bool CompletionPublicationAuthorized,
    bool RetirePublicationAuthorized,
    bool VmxAuthorityAuthorized,
    bool CompilerEmissionAuthorized,
    bool NestedExecutionAuthorized,
    bool ProductionReleaseApproved,
    string Reason)
{
    public bool CreatesAnyRuntimeAuthority =>
        RuntimeExecutionAuthorized ||
        BackendResultAccepted ||
        CompletionPublicationAuthorized ||
        RetirePublicationAuthorized ||
        VmxAuthorityAuthorized ||
        CompilerEmissionAuthorized ||
        NestedExecutionAuthorized ||
        ProductionReleaseApproved;

    public static SecureComputePhase21ConformanceEvidenceResult Accepted(
        SecureComputePhase21ConformanceDecision decision,
        bool futurePositiveTestsRemainGated,
        string reason) =>
        new(
            decision,
            MatrixEvidenceAccepted: true,
            FuturePositiveTestsRemainGated: futurePositiveTestsRemainGated,
            RuntimeExecutionAuthorized: false,
            BackendResultAccepted: false,
            CompletionPublicationAuthorized: false,
            RetirePublicationAuthorized: false,
            VmxAuthorityAuthorized: false,
            CompilerEmissionAuthorized: false,
            NestedExecutionAuthorized: false,
            ProductionReleaseApproved: false,
            reason);

    public static SecureComputePhase21ConformanceEvidenceResult Denied(
        SecureComputePhase21ConformanceDecision decision,
        string reason) =>
        new(
            decision,
            MatrixEvidenceAccepted: false,
            FuturePositiveTestsRemainGated: true,
            RuntimeExecutionAuthorized: false,
            BackendResultAccepted: false,
            CompletionPublicationAuthorized: false,
            RetirePublicationAuthorized: false,
            VmxAuthorityAuthorized: false,
            CompilerEmissionAuthorized: false,
            NestedExecutionAuthorized: false,
            ProductionReleaseApproved: false,
            reason);
}

public sealed class SecureComputePhase21ConformanceEvidencePolicy
{
    public static SecureComputePhase21ConformanceEvidencePolicy FailClosed { get; } = new();

    public SecureComputePhase21ConformanceEvidenceResult Classify(
        SecureComputePhase21ConformanceEvidenceRequest request)
    {
        SecureComputePhase21ConformanceEvidenceResult shortcut = DenyAuthorityShortcut(request);
        if (shortcut.Decision != SecureComputePhase21ConformanceDecision.CurrentMatrixAcceptedFuturePositiveGated)
        {
            return shortcut;
        }

        SecureComputePhase21ConformanceEvidenceResult missingEvidence = DenyMissingEvidence(request);
        if (missingEvidence.Decision != SecureComputePhase21ConformanceDecision.CurrentMatrixAcceptedFuturePositiveGated)
        {
            return missingEvidence;
        }

        if (request.PositiveRuntimePathProven)
        {
            return SecureComputePhase21ConformanceEvidenceResult.Accepted(
                SecureComputePhase21ConformanceDecision.PositivePathEvidenceMatrixAcceptedPhase22Required,
                futurePositiveTestsRemainGated: false,
                "Phase 21 can package a named positive-path evidence matrix for Phase 22, but it does not approve release.");
        }

        return SecureComputePhase21ConformanceEvidenceResult.Accepted(
            SecureComputePhase21ConformanceDecision.CurrentMatrixAcceptedFuturePositiveGated,
            futurePositiveTestsRemainGated: true,
            "Phase 21 accepts the current negative conformance matrix only as future-gated evidence.");
    }

    private static SecureComputePhase21ConformanceEvidenceResult DenyAuthorityShortcut(
        SecureComputePhase21ConformanceEvidenceRequest request)
    {
        if (request.RequestsProductionActivation)
        {
            return Deny(
                SecureComputePhase21ConformanceDecision.DeniedProductionActivationOverclaim,
                "Phase 21 conformance evidence cannot approve production activation.");
        }

        if (request.RequestsCompilerSecureEmission)
        {
            return Deny(
                SecureComputePhase21ConformanceDecision.DeniedCompilerEmissionOverclaim,
                "Phase 21 conformance evidence cannot open compiler secure emission.");
        }

        if (request.RequestsVmxOwnedSecureComputeAuthority)
        {
            return Deny(
                SecureComputePhase21ConformanceDecision.DeniedVmxAuthorityOverclaim,
                "Phase 21 conformance evidence cannot make VMX own SecureCompute authority.");
        }

        if (request.OpensPhase18NestedExecution)
        {
            return Deny(
                SecureComputePhase21ConformanceDecision.DeniedPhase18NestedOpened,
                "Phase 21 keeps Phase 18 nested execution future/design-fenced.");
        }

        if (request.TreatsManifestOnlyAsExecutionProof)
        {
            return Deny(
                SecureComputePhase21ConformanceDecision.DeniedManifestOnlyExecutionProof,
                "Phase 21 requires Phase 15 manifest coverage but manifest-only evidence is not execution proof.");
        }

        if (request.TreatsDebugAttestationAsRuntimeEvidence)
        {
            return Deny(
                SecureComputePhase21ConformanceDecision.DeniedVisibilityOnlyRuntimeEvidence,
                "Phase 21 requires Phase 16 visibility coverage but debug/attestation visibility is not runtime evidence.");
        }

        return SecureComputePhase21ConformanceEvidenceResult.Denied(
            SecureComputePhase21ConformanceDecision.CurrentMatrixAcceptedFuturePositiveGated,
            string.Empty);
    }

    private static SecureComputePhase21ConformanceEvidenceResult DenyMissingEvidence(
        SecureComputePhase21ConformanceEvidenceRequest request)
    {
        if (!request.Phase13RegistryBackedAdmissionCannotExecuteCovered)
        {
            return Deny(
                SecureComputePhase21ConformanceDecision.DeniedMissingPhase13RegistryBackedAdmissionEvidence,
                "Phase 21 requires Phase 13 registry-backed admission no-execution evidence.");
        }

        if (!request.Phase14CompletionRetireFailClosedCovered)
        {
            return Deny(
                SecureComputePhase21ConformanceDecision.DeniedMissingPhase14CompletionRetireEvidence,
                "Phase 21 requires Phase 14 completion/retire fail-closed evidence.");
        }

        if (!request.Phase15ManifestCoverageAndManifestOnlyDenialCovered)
        {
            return Deny(
                SecureComputePhase21ConformanceDecision.DeniedMissingPhase15ManifestEvidence,
                "Phase 21 requires Phase 15 manifest coverage and manifest-only denial evidence.");
        }

        if (!request.Phase16VisibilityCannotBecomeRuntimeEvidenceCovered)
        {
            return Deny(
                SecureComputePhase21ConformanceDecision.DeniedMissingPhase16VisibilityEvidence,
                "Phase 21 requires Phase 16 debug/attestation visibility denial evidence.");
        }

        if (!request.Phase17VmxZeroAuthorityCovered)
        {
            return Deny(
                SecureComputePhase21ConformanceDecision.DeniedMissingPhase17VmxZeroAuthorityEvidence,
                "Phase 21 requires Phase 17 VMX zero-authority evidence.");
        }

        if (!request.Phase18NestedDesignFenceCovered)
        {
            return Deny(
                SecureComputePhase21ConformanceDecision.DeniedMissingPhase18NestedDesignFenceEvidence,
                "Phase 21 requires explicit Phase 18 nested design-fence evidence.");
        }

        if (!request.Phase19NoCompilerChangeCovered)
        {
            return Deny(
                SecureComputePhase21ConformanceDecision.DeniedMissingPhase19NoCompilerChangeEvidence,
                "Phase 21 requires Phase 19 no-compiler-change evidence.");
        }

        if (!request.Phase20FutureGatedRuntimeActivationCovered)
        {
            return Deny(
                SecureComputePhase21ConformanceDecision.DeniedMissingPhase20FutureGatedRuntimeEvidence,
                "Phase 21 requires Phase 20 future-gated runtime activation evidence.");
        }

        if (!request.ReleaseEvidenceBoundaryCovered)
        {
            return Deny(
                SecureComputePhase21ConformanceDecision.DeniedMissingReleaseEvidenceBoundary,
                "Phase 21 requires release-evidence boundary coverage for Phase 22 consumption.");
        }

        return SecureComputePhase21ConformanceEvidenceResult.Denied(
            SecureComputePhase21ConformanceDecision.CurrentMatrixAcceptedFuturePositiveGated,
            string.Empty);
    }

    private static SecureComputePhase21ConformanceEvidenceResult Deny(
        SecureComputePhase21ConformanceDecision decision,
        string reason) =>
        SecureComputePhase21ConformanceEvidenceResult.Denied(
            decision,
            reason);
}
