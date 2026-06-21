using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.SecureComputeRefactoring;

public sealed class SecurePositiveRuntimeExecutionActivationPhase20Tests
{
    [Fact]
    public void RegistryBackedAdmission_CannotBecomeRuntimeExecutionCompletionOrRetire()
    {
        SecureHypercallBackendContractAdmissionResult phase13 =
            SecureHypercallBackendContractAdmissionPolicy.Default.Admit(
                SecureHypercallBackendOwnerAbiRegistry.ProductionContract,
                ContractRequest());

        Assert.True(phase13.IsProofOnly);
        Assert.False(phase13.BackendExecutionAuthorized);
        Assert.False(phase13.CompletionPublicationAuthorized);
        Assert.False(phase13.RetirePublicationAuthorized);

        SecurePositiveRuntimeExecutionActivationResult phase20 =
            Policy.ClassifyPhase13(phase13);

        Assert.Equal(
            SecurePositiveRuntimeExecutionActivationDecision.DeniedPhase13ProofOnlyAdmission,
            phase20.Decision);
        AssertNoActivation(phase20);
    }

    [Fact]
    public void ProofOnlyAndAdmittedDeniedAdmissions_CannotExecutePublishCompletionOrRetire()
    {
        SecureBackendOwnerAdmissionResult proofOnly =
            SecureBackendOwnerAdmissionPolicy.Default.Admit(
                new SecureBackendOwnerAdmissionRequest(
                    Owner(),
                    SecureBackendRfcAdrState.Approved,
                    CurrentEpoch,
                    RequestsBackendExecution: false));
        SecureIoHypercallAdmissionResult admittedDenied =
            SecureIoHypercallAdmissionResult.AllowedAdmittedDenied();

        SecureCompletionRetirePublicationResult proofOnlyCompletion =
            PublicationPolicy.AdmitCompletionFromBackendOwnerAdmission(proofOnly);
        SecureCompletionRetirePublicationResult admittedDeniedCompletion =
            PublicationPolicy.AdmitCompletionFromSecureIoHypercallAdmission(admittedDenied);

        Assert.Equal(
            SecureBackendOwnerAdmissionDecision.AllowedProofOnlyNoExecution,
            proofOnly.Decision);
        Assert.Equal(
            SecureIoHypercallAdmissionDecision.AllowedAdmittedDenied,
            admittedDenied.Decision);
        Assert.False(proofOnly.BackendExecutionAuthorized);
        Assert.False(admittedDenied.BackendExecutionAuthorized);
        Assert.False(proofOnlyCompletion.CompletionPublished);
        Assert.False(admittedDeniedCompletion.CompletionPublished);

        SecurePositiveRuntimeExecutionActivationResult proofOnlyPhase20 =
            Policy.ClassifyBackendOwnerAdmission(proofOnly);
        SecurePositiveRuntimeExecutionActivationResult admittedDeniedPhase20 =
            Policy.ClassifySecureIoHypercallAdmission(admittedDenied);

        Assert.Equal(
            SecurePositiveRuntimeExecutionActivationDecision.DeniedProofOnlyOwnerAdmission,
            proofOnlyPhase20.Decision);
        Assert.Equal(
            SecurePositiveRuntimeExecutionActivationDecision.DeniedAdmittedDeniedHypercallAdmission,
            admittedDeniedPhase20.Decision);
        AssertNoActivation(proofOnlyPhase20);
        AssertNoActivation(admittedDeniedPhase20);
    }

    [Fact]
    public void PublicationVocabulary_DoesNotProvePhase20OwnerPathReachability()
    {
        SecureCompletionRetirePublicationResult completion =
            PublicationPolicy.AdmitCompletion(new SecureCompletionPublicationAuthorityRequest(
                SecurePublicationPathKind.NeutralRuntimeBackendResult,
                SecurePublicationBackendResultState.InternalNeutralResult,
                BackendResultOwner: Owner(),
                CompletionOwner: Owner(),
                PublicationFence: CompletionFence(),
                CurrentEpoch,
                CompletionRecordMaterialized: true));

        Assert.True(completion.CompletionPublished);
        Assert.False(completion.RetirePublished);

        SecurePositiveRuntimeExecutionActivationResult phase20 =
            Policy.ClassifyPhase14(completion);

        Assert.Equal(
            SecurePositiveRuntimeExecutionActivationDecision.DeniedPhase14PublicationVocabularyOnly,
            phase20.Decision);
        AssertNoActivation(phase20);
    }

    [Fact]
    public void ManifestCoverage_IsRequiredButManifestOnlyIsNotExecutionProof()
    {
        SecureOutputManifestClassificationResult missingManifest =
            SecureOutputManifestClassificationPolicy.FailClosed.ClassifyManifest(
                CompleteManifest()[..^1]);
        SecureOutputManifestClassificationResult completeManifest =
            SecureOutputManifestClassificationPolicy.FailClosed.ClassifyManifest(
                CompleteManifest());

        Assert.Equal(
            SecureOutputManifestClassificationDecision.DeniedMissingManifestEntry,
            missingManifest.Decision);
        Assert.Equal(
            SecureOutputManifestClassificationDecision.CompleteManifestClassified,
            completeManifest.Decision);

        SecurePositiveRuntimeExecutionActivationResult missingPhase20 =
            Policy.ClassifyPhase15(missingManifest);
        SecurePositiveRuntimeExecutionActivationResult completePhase20 =
            Policy.ClassifyPhase15(completeManifest);

        Assert.Equal(
            SecurePositiveRuntimeExecutionActivationDecision.DeniedMissingMigrationOutputManifestEvidence,
            missingPhase20.Decision);
        Assert.Equal(
            SecurePositiveRuntimeExecutionActivationDecision.DeniedPhase15ManifestOnlyEvidence,
            completePhase20.Decision);
        AssertNoActivation(missingPhase20);
        AssertNoActivation(completePhase20);
    }

    [Fact]
    public void Visibility_Phase17Projection_Phase18DesignFence_AndPhase19NoEmissionCannotBecomeRuntimeEvidence()
    {
        SecureDebugAttestationVisibilityResult visibility =
            SecureDebugAttestationVisibilityResult.Classified(
                SecureEvidenceVisibilityClass.GuestVisible,
                guestVisible: true,
                hostOnly: false,
                debugOnly: false,
                attestationOnly: true,
                reason: "test visibility");
        SecureComputeNamedPathVmxZeroAuthorityResult projection =
            SecureComputeNamedPathVmxZeroAuthorityPolicy.FailClosed.Classify(
                new SecureComputeNamedPathVmxZeroAuthorityRequest(
                    SecureComputeNamedPositivePath.FutureRestrictedRuntimeExecution,
                    HasNeutralRuntimeResult: true,
                    RequestsCompatibilityProjection: true));
        SecureNestedDomainAdmissionResult nested =
            SecureNestedDomainAdmissionPolicy.Default.AdmitCheckpointPayload(
                SecureNestedCheckpointPayloadClass.NeutralChildIntentDescriptor);
        SecureComputeControlledEmissionResult noCompilerChange =
            SecureComputeControlledEmissionGatePolicy.FailClosed.Classify(
                new SecureComputeControlledEmissionRequest(
                    SecureComputeCompilerEmissionPath.NoCompilerChange,
                    RequestsCompilerEmission: false));

        Assert.True(visibility.IsAllowed);
        Assert.True(projection.IsAllowed);
        Assert.True(nested.IsAllowed);
        Assert.Equal(
            SecureComputeControlledEmissionDecision.NoEmissionPreserved,
            noCompilerChange.Decision);

        SecurePositiveRuntimeExecutionActivationResult visibilityPhase20 =
            Policy.ClassifyPhase16(visibility);
        SecurePositiveRuntimeExecutionActivationResult projectionPhase20 =
            Policy.ClassifyPhase17(projection);
        SecurePositiveRuntimeExecutionActivationResult nestedPhase20 =
            Policy.ClassifyPhase18(nested);
        SecurePositiveRuntimeExecutionActivationResult compilerPhase20 =
            Policy.ClassifyPhase19(noCompilerChange);

        Assert.Equal(
            SecurePositiveRuntimeExecutionActivationDecision.DeniedPhase16VisibilityOnlyEvidence,
            visibilityPhase20.Decision);
        Assert.Equal(
            SecurePositiveRuntimeExecutionActivationDecision.DeniedPhase17VmxZeroAuthorityOnly,
            projectionPhase20.Decision);
        Assert.Equal(
            SecurePositiveRuntimeExecutionActivationDecision.DeniedPhase18NestedDesignFence,
            nestedPhase20.Decision);
        Assert.Equal(
            SecurePositiveRuntimeExecutionActivationDecision.DeniedPhase19NoCompilerChangeOnly,
            compilerPhase20.Decision);
        AssertNoActivation(visibilityPhase20);
        AssertNoActivation(projectionPhase20);
        AssertNoActivation(nestedPhase20);
        AssertNoActivation(compilerPhase20);
    }

    [Fact]
    public void FutureNamedPath_PreconditionsFailClosedUntilOwnerPathReachabilityAndReleaseEvidenceExist()
    {
        Assert.Equal(
            SecurePositiveRuntimeExecutionActivationDecision.DeniedMissingRuntimeAuthorityOwner,
            ClassifyFuture().Decision);
        Assert.Equal(
            SecurePositiveRuntimeExecutionActivationDecision.DeniedMissingBackendResultOwnerBoundary,
            ClassifyFuture(RuntimeAuthorityOwner: Owner()).Decision);
        Assert.Equal(
            SecurePositiveRuntimeExecutionActivationDecision.DeniedMissingOwnerPathReachabilityProof,
            ClassifyFuture(RuntimeAuthorityOwner: Owner(), BackendResultOwnerBoundaryProven: true).Decision);
        Assert.Equal(
            SecurePositiveRuntimeExecutionActivationDecision.DeniedMissingNeutralBackendResult,
            ClassifyFuture(
                RuntimeAuthorityOwner: Owner(),
                BackendResultOwnerBoundaryProven: true,
                OwnerPathReachabilityProven: true).Decision);
        Assert.Equal(
            SecurePositiveRuntimeExecutionActivationDecision.DeniedMissingMigrationOutputManifestEvidence,
            ClassifyFuture(
                RuntimeAuthorityOwner: Owner(),
                BackendResultOwnerBoundaryProven: true,
                OwnerPathReachabilityProven: true,
                NeutralBackendResultProduced: true).Decision);
        Assert.Equal(
            SecurePositiveRuntimeExecutionActivationDecision.DeniedMissingRestoreRules,
            ClassifyFuture(
                RuntimeAuthorityOwner: Owner(),
                BackendResultOwnerBoundaryProven: true,
                OwnerPathReachabilityProven: true,
                NeutralBackendResultProduced: true,
                OutputManifestCoverageComplete: true).Decision);
        Assert.Equal(
            SecurePositiveRuntimeExecutionActivationDecision.DeniedPhase18NestedDesignFence,
            ClassifyFuture(
                RuntimeAuthorityOwner: Owner(),
                BackendResultOwnerBoundaryProven: true,
                OwnerPathReachabilityProven: true,
                NeutralBackendResultProduced: true,
                OutputManifestCoverageComplete: true,
                RestoreRulesClassified: true,
                Phase18NestedExecutionExcluded: false).Decision);
        Assert.Equal(
            SecurePositiveRuntimeExecutionActivationDecision.DeniedMissingCompilerNoEmissionDecision,
            ClassifyFuture(
                RuntimeAuthorityOwner: Owner(),
                BackendResultOwnerBoundaryProven: true,
                OwnerPathReachabilityProven: true,
                NeutralBackendResultProduced: true,
                OutputManifestCoverageComplete: true,
                RestoreRulesClassified: true).Decision);

        SecurePositiveRuntimeExecutionActivationResult releaseGate =
            ClassifyFuture(
                RuntimeAuthorityOwner: Owner(),
                BackendResultOwnerBoundaryProven: true,
                OwnerPathReachabilityProven: true,
                NeutralBackendResultProduced: true,
                OutputManifestCoverageComplete: true,
                RestoreRulesClassified: true,
                CompilerNoEmissionDecisionRecorded: true);

        Assert.Equal(
            SecurePositiveRuntimeExecutionActivationDecision.DeniedProductionReleaseGate,
            releaseGate.Decision);
        AssertNoActivation(releaseGate);
    }

    [Fact]
    public void Sources_DoNotCreateBackendPublicationVmxNestedReleaseOrCompilerAuthority()
    {
        string source = ReadProjectSource(
            "HybridCPU_ISE/CloseToRTL/Core/Runtime/Domains/SecureCompute/Policies/Runtime/SecurePositiveRuntimeExecutionActivationPolicy.cs");

        Assert.Contains("DeniedPhase13ProofOnlyAdmission", source);
        Assert.Contains("DeniedAdmittedDeniedHypercallAdmission", source);
        Assert.Contains("DeniedPhase14PublicationVocabularyOnly", source);
        Assert.Contains("DeniedPhase15ManifestOnlyEvidence", source);
        Assert.Contains("DeniedPhase16VisibilityOnlyEvidence", source);
        Assert.Contains("DeniedPhase17VmxZeroAuthorityOnly", source);
        Assert.Contains("DeniedPhase18NestedDesignFence", source);
        Assert.Contains("DeniedPhase19NoCompilerChangeOnly", source);
        Assert.Contains("DeniedProductionReleaseGate", source);
        Assert.Contains("RuntimeExecutionAuthorized: false", source);
        Assert.Contains("BackendResultAccepted: false", source);
        Assert.Contains("CompletionPublicationAuthorized: false", source);
        Assert.Contains("RetirePublicationAuthorized: false", source);
        Assert.Contains("VmxAuthorityAuthorized: false", source);
        Assert.Contains("CompilerEmissionAuthorized: false", source);
        Assert.Contains("NestedExecutionAuthorized: false", source);
        Assert.Contains("ProductionReleaseApproved: false", source);

        Assert.DoesNotContain("RuntimeExecutionAuthorized: true", source);
        Assert.DoesNotContain("BackendResultAccepted: true", source);
        Assert.DoesNotContain("CompletionPublicationAuthorized: true", source);
        Assert.DoesNotContain("RetirePublicationAuthorized: true", source);
        Assert.DoesNotContain("VmxAuthorityAuthorized: true", source);
        Assert.DoesNotContain("CompilerEmissionAuthorized: true", source);
        Assert.DoesNotContain("NestedExecutionAuthorized: true", source);
        Assert.DoesNotContain("ProductionReleaseApproved: true", source);
        Assert.DoesNotContain("SecureBackendExecutionRequest", source);
        Assert.DoesNotContain("SecureBackendExecutionDecision", source);
        Assert.DoesNotContain("AllowedInternalExecutionNoPublication", source);
        Assert.DoesNotContain("AllowedCompletionRecordNoPublication", source);
        Assert.DoesNotContain("SecureCompletionRecord", source);
        Assert.DoesNotContain("NoSideEffectProbe", source);
        Assert.DoesNotContain("VmxCaps.Secure", source);
        Assert.DoesNotContain("VmcsManager", source);
        Assert.DoesNotContain("IVmcsManager", source);
        Assert.DoesNotContain("VmxExecutionUnit", source);
        Assert.DoesNotContain("ReadFieldValue(", source);
        Assert.DoesNotContain("WriteFieldValue(", source);
    }

    [Fact]
    public void DocsAndReleaseMatrix_RecordFutureGatedVerdictAndNoProductionActivation()
    {
        string phase20 = ReadSecureComputeActivationPlan("20_positive_secure_runtime_execution_activation_plan.md");
        string conformance = ReadSecureComputeActivationPlan("21_conformance_negative_positive_test_matrix.md");
        string release = ReadSecureComputeActivationPlan("22_limited_securecompute_release_gate.md");
        string backlog = ReadSecureComputeActivationPlan("23_open_decision_backlog.md");

        Assert.Contains("ADR-SC-POSITIVE-RUNTIME-EXECUTION-ACTIVATION", phase20);
        Assert.Contains("Phase 20 remains future-gated", phase20);
        Assert.Contains("no production SecureCompute activation is approved", phase20);
        Assert.Contains("SecurePositiveRuntimeExecutionActivationPolicy", phase20);
        Assert.Contains("owner/path/reachability scoped source-scan policy", phase20);
        Assert.Contains("Implemented Phase 20 Future-Gated Runtime Activation Tests", conformance);
        Assert.Contains("Phase 20 tests proving manifest coverage is required but manifest-only records are not execution proof", release);
        Assert.Contains("Phase 20 remains future-gated because no named positive runtime owner/path/reachability chain is locally proven", backlog);
    }

    private static SecurePositiveRuntimeExecutionActivationPolicy Policy =>
        SecurePositiveRuntimeExecutionActivationPolicy.FailClosed;

    private static SecureCompletionRetirePublicationAuthorityPolicy PublicationPolicy =>
        SecureCompletionRetirePublicationAuthorityPolicy.Default;

    private static SecureRevocationEpoch CurrentEpoch =>
        SecureHypercallBackendOwnerAbiRegistry.OwnerEpoch;

    private static SecureBackendOwnerDescriptor Owner() =>
        SecureHypercallBackendOwnerAbiRegistry.CreateOwnerDescriptor(
            epoch: CurrentEpoch);

    private static SecureCompletionPublicationFence CompletionFence() =>
        new(
            SecureCompletionFenceState.CompletionAllowed,
            SecureRetirePublicationRule.CompletionFenceRequired);

    private static SecureHypercallBackendContractRequest ContractRequest() =>
        new(
            SecureHypercallBackendOwnerAbiRegistry.TransportOpcode,
            SecureHypercallBackendOwnerAbiRegistry.DecodedLeaf,
            SecureHypercallBackendOwnerAbiRegistry.ServiceId,
            SecureHypercallBackendOwnerAbiRegistry.ContractVersion,
            Owner(),
            CurrentEpoch,
            SecureHypercallBackendOwnerAbiRegistry.RequiredGrant,
            EvidenceValidated: true,
            EvidenceEpoch: CurrentEpoch,
            IoPolicy: null,
            ValidatedDomainTag: 7,
            Arguments: Array.Empty<SecureHypercallBackendArgument>(),
            IsReplay: false,
            IdempotentRetry: false,
            ReplayTokenMatches: false,
            CancellationRequested: false);

    private static SecureOutputManifestEntry[] CompleteManifest() =>
    [
        SecureOutputManifestEntry.RequestState(),
        SecureOutputManifestEntry.InternalBackendResult(),
        SecureOutputManifestEntry.InternalCompletionRecord(),
        SecureOutputManifestEntry.GuestVisibleOutput(),
        SecureOutputManifestEntry.RetireVisibleState(),
        SecureOutputManifestEntry.RecomputedAfterRestoreState(),
    ];

    private static SecurePositiveRuntimeExecutionActivationResult ClassifyFuture(
        SecureBackendOwnerDescriptor? RuntimeAuthorityOwner = null,
        bool BackendResultOwnerBoundaryProven = false,
        bool OwnerPathReachabilityProven = false,
        bool NeutralBackendResultProduced = false,
        bool OutputManifestCoverageComplete = false,
        bool RestoreRulesClassified = false,
        bool CompilerNoEmissionDecisionRecorded = false,
        bool Phase18NestedExecutionExcluded = true) =>
        Policy.Classify(new SecurePositiveRuntimeExecutionActivationRequest(
            SecurePositiveRuntimePathCandidate.FutureNamedPositiveRuntimePath,
            RuntimeAuthorityOwner,
            BackendResultOwnerBoundaryProven,
            OwnerPathReachabilityProven,
            NeutralBackendResultProduced,
            OutputManifestCoverageComplete,
            RestoreRulesClassified,
            CompilerNoEmissionDecisionRecorded,
            Phase18NestedExecutionExcluded));

    private static void AssertNoActivation(
        SecurePositiveRuntimeExecutionActivationResult result)
    {
        Assert.True(result.IsFutureGated);
        Assert.False(result.RuntimeExecutionAuthorized);
        Assert.False(result.BackendResultAccepted);
        Assert.False(result.CompletionPublicationAuthorized);
        Assert.False(result.RetirePublicationAuthorized);
        Assert.False(result.VmxAuthorityAuthorized);
        Assert.False(result.CompilerEmissionAuthorized);
        Assert.False(result.NestedExecutionAuthorized);
        Assert.False(result.ProductionReleaseApproved);
    }

    private static string ReadSecureComputeActivationPlan(string fileName) =>
        File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "HybridCPU_ISE",
            "docs",
            "ref2",
            "SecureComputeActivationPlan",
            fileName));

    private static string ReadProjectSource(params string[] relativePaths)
    {
        string root = FindRepositoryRoot();
        return string.Concat(relativePaths.Select(path => File.ReadAllText(Path.Combine(
            root,
            path.Replace('/', Path.DirectorySeparatorChar)))));
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "HybridCPU_ISE.sln")) ||
                Directory.Exists(Path.Combine(directory.FullName, "HybridCPU_ISE")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate HybridCPU ISE repository root.");
    }
}
