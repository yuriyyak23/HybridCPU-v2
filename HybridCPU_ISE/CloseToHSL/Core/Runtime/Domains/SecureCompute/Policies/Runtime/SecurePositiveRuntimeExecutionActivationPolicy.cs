namespace YAKSys_Hybrid_CPU.Core;

public enum SecurePositiveRuntimePathCandidate : byte
{
    None = 0,
    ProofOnlyBackendOwnerAdmission = 1,
    AdmittedDeniedSecureHypercallAdmission = 2,
    Phase13RegistryBackedHypercallContract = 3,
    Phase14PublicationLadderVocabulary = 4,
    Phase15OutputManifestClassification = 5,
    Phase16DebugAttestationVisibility = 6,
    Phase17VmxZeroAuthorityProjection = 7,
    Phase18NestedChildIntentDesignFence = 8,
    Phase19NoCompilerChangeDecision = 9,
    FutureNamedPositiveRuntimePath = 10,
}

public enum SecurePositiveRuntimeExecutionActivationDecision : byte
{
    FutureGatedNoNamedPositivePath = 0,
    DeniedProofOnlyOwnerAdmission = 1,
    DeniedAdmittedDeniedHypercallAdmission = 2,
    DeniedPhase13ProofOnlyAdmission = 3,
    DeniedPhase14PublicationVocabularyOnly = 4,
    DeniedPhase15ManifestOnlyEvidence = 5,
    DeniedPhase16VisibilityOnlyEvidence = 6,
    DeniedPhase17VmxZeroAuthorityOnly = 7,
    DeniedPhase18NestedDesignFence = 8,
    DeniedPhase19NoCompilerChangeOnly = 9,
    DeniedMissingRuntimeAuthorityOwner = 10,
    DeniedMissingBackendResultOwnerBoundary = 11,
    DeniedMissingOwnerPathReachabilityProof = 12,
    DeniedMissingNeutralBackendResult = 13,
    DeniedMissingMigrationOutputManifestEvidence = 14,
    DeniedMissingRestoreRules = 15,
    DeniedMissingCompilerNoEmissionDecision = 16,
    DeniedProductionReleaseGate = 17,
}

public readonly record struct SecurePositiveRuntimeExecutionActivationRequest(
    SecurePositiveRuntimePathCandidate Candidate,
    SecureBackendOwnerDescriptor? RuntimeAuthorityOwner = null,
    bool BackendResultOwnerBoundaryProven = false,
    bool OwnerPathReachabilityProven = false,
    bool NeutralBackendResultProduced = false,
    bool OutputManifestCoverageComplete = false,
    bool RestoreRulesClassified = false,
    bool CompilerNoEmissionDecisionRecorded = false,
    bool Phase18NestedExecutionExcluded = true);

public readonly record struct SecurePositiveRuntimeExecutionActivationResult(
    SecurePositiveRuntimeExecutionActivationDecision Decision,
    SecurePositiveRuntimePathCandidate Candidate,
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
    public bool IsFutureGated =>
        !RuntimeExecutionAuthorized &&
        !BackendResultAccepted &&
        !CompletionPublicationAuthorized &&
        !RetirePublicationAuthorized &&
        !VmxAuthorityAuthorized &&
        !CompilerEmissionAuthorized &&
        !NestedExecutionAuthorized &&
        !ProductionReleaseApproved;

    public static SecurePositiveRuntimeExecutionActivationResult FutureGated(
        SecurePositiveRuntimeExecutionActivationDecision decision,
        SecurePositiveRuntimePathCandidate candidate,
        string reason) =>
        new(
            decision,
            candidate,
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

public sealed class SecurePositiveRuntimeExecutionActivationPolicy
{
    public static SecurePositiveRuntimeExecutionActivationPolicy FailClosed { get; } = new();

    public SecurePositiveRuntimeExecutionActivationResult Classify(
        SecurePositiveRuntimeExecutionActivationRequest request)
    {
        SecurePositiveRuntimeExecutionActivationResult currentGate =
            DenyCurrentEvidenceOnlyCandidate(request.Candidate);
        if (currentGate.Decision != SecurePositiveRuntimeExecutionActivationDecision.FutureGatedNoNamedPositivePath)
        {
            return currentGate;
        }

        if (request.Candidate != SecurePositiveRuntimePathCandidate.FutureNamedPositiveRuntimePath)
        {
            return currentGate;
        }

        if (!IsMaterializedNeutralOwner(request.RuntimeAuthorityOwner))
        {
            return Deny(
                SecurePositiveRuntimeExecutionActivationDecision.DeniedMissingRuntimeAuthorityOwner,
                request.Candidate,
                "Phase 20 requires an exact neutral runtime authority owner before any positive path can execute.");
        }

        if (!request.BackendResultOwnerBoundaryProven)
        {
            return Deny(
                SecurePositiveRuntimeExecutionActivationDecision.DeniedMissingBackendResultOwnerBoundary,
                request.Candidate,
                "Phase 20 requires a backend-result owner boundary that is not admission, visibility or manifest evidence.");
        }

        if (!request.OwnerPathReachabilityProven)
        {
            return Deny(
                SecurePositiveRuntimeExecutionActivationDecision.DeniedMissingOwnerPathReachabilityProof,
                request.Candidate,
                "Phase 20 requires scoped owner/path/reachability proof from the named owner to the runtime path.");
        }

        if (!request.NeutralBackendResultProduced)
        {
            return Deny(
                SecurePositiveRuntimeExecutionActivationDecision.DeniedMissingNeutralBackendResult,
                request.Candidate,
                "Phase 20 requires a neutral backend result produced by the named runtime owner.");
        }

        if (!request.OutputManifestCoverageComplete)
        {
            return Deny(
                SecurePositiveRuntimeExecutionActivationDecision.DeniedMissingMigrationOutputManifestEvidence,
                request.Candidate,
                "Phase 20 requires complete migration/output-manifest coverage, but manifest coverage alone is not execution proof.");
        }

        if (!request.RestoreRulesClassified)
        {
            return Deny(
                SecurePositiveRuntimeExecutionActivationDecision.DeniedMissingRestoreRules,
                request.Candidate,
                "Phase 20 requires restore-time revalidation or recomputation rules for every classified output.");
        }

        if (!request.Phase18NestedExecutionExcluded)
        {
            return Deny(
                SecurePositiveRuntimeExecutionActivationDecision.DeniedPhase18NestedDesignFence,
                request.Candidate,
                "Phase 18 nested execution, mutable nested state and nested publication remain design-fenced.");
        }

        if (!request.CompilerNoEmissionDecisionRecorded)
        {
            return Deny(
                SecurePositiveRuntimeExecutionActivationDecision.DeniedMissingCompilerNoEmissionDecision,
                request.Candidate,
                "Phase 20 requires the Phase 19 no-compiler-change decision for the named path.");
        }

        return Deny(
            SecurePositiveRuntimeExecutionActivationDecision.DeniedProductionReleaseGate,
            request.Candidate,
            "Phase 20 planning evidence does not approve production activation; Phase 22 remains the release gate.");
    }

    public SecurePositiveRuntimeExecutionActivationResult ClassifyPhase13(
        SecureHypercallBackendContractAdmissionResult admission) =>
        admission.IsProofOnly
            ? Deny(
                SecurePositiveRuntimeExecutionActivationDecision.DeniedPhase13ProofOnlyAdmission,
                SecurePositiveRuntimePathCandidate.Phase13RegistryBackedHypercallContract,
                "Registry-backed Phase 13 admission is proof-only and cannot become runtime execution.")
            : Deny(
                SecurePositiveRuntimeExecutionActivationDecision.FutureGatedNoNamedPositivePath,
                SecurePositiveRuntimePathCandidate.Phase13RegistryBackedHypercallContract,
                "Phase 13 did not produce a positive named runtime path.");

    public SecurePositiveRuntimeExecutionActivationResult ClassifyBackendOwnerAdmission(
        SecureBackendOwnerAdmissionResult admission) =>
        admission.Decision == SecureBackendOwnerAdmissionDecision.AllowedProofOnlyNoExecution
            ? Deny(
                SecurePositiveRuntimeExecutionActivationDecision.DeniedProofOnlyOwnerAdmission,
                SecurePositiveRuntimePathCandidate.ProofOnlyBackendOwnerAdmission,
                "Proof-only backend owner admission cannot become runtime execution.")
            : Deny(
                SecurePositiveRuntimeExecutionActivationDecision.FutureGatedNoNamedPositivePath,
                SecurePositiveRuntimePathCandidate.ProofOnlyBackendOwnerAdmission,
                "Backend owner admission did not produce a positive named runtime path.");

    public SecurePositiveRuntimeExecutionActivationResult ClassifySecureIoHypercallAdmission(
        SecureIoHypercallAdmissionResult admission) =>
        admission.IsAdmittedDenied
            ? Deny(
                SecurePositiveRuntimeExecutionActivationDecision.DeniedAdmittedDeniedHypercallAdmission,
                SecurePositiveRuntimePathCandidate.AdmittedDeniedSecureHypercallAdmission,
                "Admitted-denied secure hypercall recognition cannot become runtime execution.")
            : admission.IsPolicyAdmissionOnly
                ? Deny(
                    SecurePositiveRuntimeExecutionActivationDecision.DeniedAdmittedDeniedHypercallAdmission,
                    SecurePositiveRuntimePathCandidate.AdmittedDeniedSecureHypercallAdmission,
                    "Secure policy admission cannot become runtime execution.")
                : Deny(
                    SecurePositiveRuntimeExecutionActivationDecision.FutureGatedNoNamedPositivePath,
                    SecurePositiveRuntimePathCandidate.AdmittedDeniedSecureHypercallAdmission,
                    "Secure hypercall admission did not produce a positive named runtime path.");

    public SecurePositiveRuntimeExecutionActivationResult ClassifyPhase14(
        SecureCompletionRetirePublicationResult publication) =>
        Deny(
            SecurePositiveRuntimeExecutionActivationDecision.DeniedPhase14PublicationVocabularyOnly,
            SecurePositiveRuntimePathCandidate.Phase14PublicationLadderVocabulary,
            publication.CompletionPublished || publication.RetirePublished
                ? "Phase 14 publication vocabulary cannot prove Phase 20 owner/path/reachability or backend-result production."
                : "Phase 14 fail-closed publication denial cannot become runtime execution.");

    public SecurePositiveRuntimeExecutionActivationResult ClassifyPhase15(
        SecureOutputManifestClassificationResult manifest) =>
        manifest.ManifestClassified
            ? Deny(
                SecurePositiveRuntimeExecutionActivationDecision.DeniedPhase15ManifestOnlyEvidence,
                SecurePositiveRuntimePathCandidate.Phase15OutputManifestClassification,
                "Phase 15 output-manifest coverage is required evidence, not runtime execution proof.")
            : Deny(
                SecurePositiveRuntimeExecutionActivationDecision.DeniedMissingMigrationOutputManifestEvidence,
                SecurePositiveRuntimePathCandidate.Phase15OutputManifestClassification,
                "Missing Phase 15 output-manifest coverage blocks any Phase 20 positive path.");

    public SecurePositiveRuntimeExecutionActivationResult ClassifyPhase16(
        SecureDebugAttestationVisibilityResult visibility) =>
        visibility.IsAllowed
            ? Deny(
                SecurePositiveRuntimeExecutionActivationDecision.DeniedPhase16VisibilityOnlyEvidence,
                SecurePositiveRuntimePathCandidate.Phase16DebugAttestationVisibility,
                "Phase 16 debug/attestation visibility cannot become runtime evidence.")
            : Deny(
                SecurePositiveRuntimeExecutionActivationDecision.FutureGatedNoNamedPositivePath,
                SecurePositiveRuntimePathCandidate.Phase16DebugAttestationVisibility,
                "Phase 16 denied visibility does not produce a runtime path.");

    public SecurePositiveRuntimeExecutionActivationResult ClassifyPhase17(
        SecureComputeNamedPathVmxZeroAuthorityResult zeroAuthority) =>
        zeroAuthority.IsAllowed
            ? Deny(
                SecurePositiveRuntimeExecutionActivationDecision.DeniedPhase17VmxZeroAuthorityOnly,
                SecurePositiveRuntimePathCandidate.Phase17VmxZeroAuthorityProjection,
                "Phase 17 VMX compatibility projection remains zero-authority and cannot become runtime execution.")
            : Deny(
                SecurePositiveRuntimeExecutionActivationDecision.DeniedPhase17VmxZeroAuthorityOnly,
                SecurePositiveRuntimePathCandidate.Phase17VmxZeroAuthorityProjection,
                "Phase 17 denial preserves zero-authority and does not produce a runtime path.");

    public SecurePositiveRuntimeExecutionActivationResult ClassifyPhase18(
        SecureNestedDomainAdmissionResult nested) =>
        nested.IsAllowed
            ? Deny(
                SecurePositiveRuntimeExecutionActivationDecision.DeniedPhase18NestedDesignFence,
                SecurePositiveRuntimePathCandidate.Phase18NestedChildIntentDesignFence,
                "Phase 18 nested child intent remains design-fenced and cannot become runtime execution.")
            : Deny(
                SecurePositiveRuntimeExecutionActivationDecision.DeniedPhase18NestedDesignFence,
                SecurePositiveRuntimePathCandidate.Phase18NestedChildIntentDesignFence,
                "Phase 18 nested denial preserves the design fence and does not produce a runtime path.");

    public SecurePositiveRuntimeExecutionActivationResult ClassifyPhase19(
        SecureComputeControlledEmissionResult compilerDecision) =>
        compilerDecision.Decision == SecureComputeControlledEmissionDecision.NoEmissionPreserved
            ? Deny(
                SecurePositiveRuntimeExecutionActivationDecision.DeniedPhase19NoCompilerChangeOnly,
                SecurePositiveRuntimePathCandidate.Phase19NoCompilerChangeDecision,
                "Phase 19 no-compiler-change is required, but it is not runtime execution proof.")
            : Deny(
                SecurePositiveRuntimeExecutionActivationDecision.DeniedPhase19NoCompilerChangeOnly,
                SecurePositiveRuntimePathCandidate.Phase19NoCompilerChangeDecision,
                "Phase 19 denied compiler emission does not produce a runtime path.");

    private static SecurePositiveRuntimeExecutionActivationResult DenyCurrentEvidenceOnlyCandidate(
        SecurePositiveRuntimePathCandidate candidate) =>
        candidate switch
        {
            SecurePositiveRuntimePathCandidate.Phase13RegistryBackedHypercallContract =>
                Deny(
                    SecurePositiveRuntimeExecutionActivationDecision.DeniedPhase13ProofOnlyAdmission,
                    candidate,
                    "Phase 13 registry-backed admission is proof-only and cannot execute."),
            SecurePositiveRuntimePathCandidate.ProofOnlyBackendOwnerAdmission =>
                Deny(
                    SecurePositiveRuntimeExecutionActivationDecision.DeniedProofOnlyOwnerAdmission,
                    candidate,
                    "Proof-only owner admission is not runtime execution."),
            SecurePositiveRuntimePathCandidate.AdmittedDeniedSecureHypercallAdmission =>
                Deny(
                    SecurePositiveRuntimeExecutionActivationDecision.DeniedAdmittedDeniedHypercallAdmission,
                    candidate,
                    "Admitted-denied hypercall recognition is not runtime execution."),
            SecurePositiveRuntimePathCandidate.Phase14PublicationLadderVocabulary =>
                Deny(
                    SecurePositiveRuntimeExecutionActivationDecision.DeniedPhase14PublicationVocabularyOnly,
                    candidate,
                    "Phase 14 publication ladder vocabulary is not a runtime owner path."),
            SecurePositiveRuntimePathCandidate.Phase15OutputManifestClassification =>
                Deny(
                    SecurePositiveRuntimeExecutionActivationDecision.DeniedPhase15ManifestOnlyEvidence,
                    candidate,
                    "Phase 15 manifest classification is evidence coverage only."),
            SecurePositiveRuntimePathCandidate.Phase16DebugAttestationVisibility =>
                Deny(
                    SecurePositiveRuntimeExecutionActivationDecision.DeniedPhase16VisibilityOnlyEvidence,
                    candidate,
                    "Phase 16 visibility is not runtime evidence."),
            SecurePositiveRuntimePathCandidate.Phase17VmxZeroAuthorityProjection =>
                Deny(
                    SecurePositiveRuntimeExecutionActivationDecision.DeniedPhase17VmxZeroAuthorityOnly,
                    candidate,
                    "Phase 17 compatibility projection remains zero-authority."),
            SecurePositiveRuntimePathCandidate.Phase18NestedChildIntentDesignFence =>
                Deny(
                    SecurePositiveRuntimeExecutionActivationDecision.DeniedPhase18NestedDesignFence,
                    candidate,
                    "Phase 18 nested child intent remains future/design-fenced."),
            SecurePositiveRuntimePathCandidate.Phase19NoCompilerChangeDecision =>
                Deny(
                    SecurePositiveRuntimeExecutionActivationDecision.DeniedPhase19NoCompilerChangeOnly,
                    candidate,
                    "Phase 19 no-compiler-change is not runtime execution proof."),
            _ =>
                Deny(
                    SecurePositiveRuntimeExecutionActivationDecision.FutureGatedNoNamedPositivePath,
                    candidate,
                    "Phase 20 has no locally proven named positive runtime owner/path/reachability chain."),
        };

    private static bool IsMaterializedNeutralOwner(
        SecureBackendOwnerDescriptor? owner) =>
        owner is { } value &&
        value.Materialized &&
        value.HasIdentity &&
        value.IsNeutralSource &&
        value.HasProofChain &&
        value.NegativeTestsPresent;

    private static SecurePositiveRuntimeExecutionActivationResult Deny(
        SecurePositiveRuntimeExecutionActivationDecision decision,
        SecurePositiveRuntimePathCandidate candidate,
        string reason) =>
        SecurePositiveRuntimeExecutionActivationResult.FutureGated(
            decision,
            candidate,
            reason);
}
