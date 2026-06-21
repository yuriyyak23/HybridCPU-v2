using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.SecureComputeRefactoring;

public sealed class SecureComputePhase21ConformanceMatrixTests
{
    [Fact]
    public void CurrentEvidencePackage_AcceptsOnlyFutureGatedConformanceMatrixAndNoAuthority()
    {
        SecureComputePhase21ConformanceEvidenceResult result =
            Policy.Classify(CurrentEvidence());

        Assert.Equal(
            SecureComputePhase21ConformanceDecision.CurrentMatrixAcceptedFuturePositiveGated,
            result.Decision);
        Assert.True(result.MatrixEvidenceAccepted);
        Assert.True(result.FuturePositiveTestsRemainGated);
        AssertNoRuntimeAuthority(result);
    }

    [Fact]
    public void PriorGateCoverage_IsRequiredBeforePhase21MatrixEvidenceCanBeConsumed()
    {
        (SecureComputePhase21ConformanceEvidenceRequest Request,
            SecureComputePhase21ConformanceDecision Decision)[] cases =
        [
            (CurrentEvidence() with { Phase13RegistryBackedAdmissionCannotExecuteCovered = false },
                SecureComputePhase21ConformanceDecision.DeniedMissingPhase13RegistryBackedAdmissionEvidence),
            (CurrentEvidence() with { Phase14CompletionRetireFailClosedCovered = false },
                SecureComputePhase21ConformanceDecision.DeniedMissingPhase14CompletionRetireEvidence),
            (CurrentEvidence() with { Phase15ManifestCoverageAndManifestOnlyDenialCovered = false },
                SecureComputePhase21ConformanceDecision.DeniedMissingPhase15ManifestEvidence),
            (CurrentEvidence() with { Phase16VisibilityCannotBecomeRuntimeEvidenceCovered = false },
                SecureComputePhase21ConformanceDecision.DeniedMissingPhase16VisibilityEvidence),
            (CurrentEvidence() with { Phase17VmxZeroAuthorityCovered = false },
                SecureComputePhase21ConformanceDecision.DeniedMissingPhase17VmxZeroAuthorityEvidence),
            (CurrentEvidence() with { Phase18NestedDesignFenceCovered = false },
                SecureComputePhase21ConformanceDecision.DeniedMissingPhase18NestedDesignFenceEvidence),
            (CurrentEvidence() with { Phase19NoCompilerChangeCovered = false },
                SecureComputePhase21ConformanceDecision.DeniedMissingPhase19NoCompilerChangeEvidence),
            (CurrentEvidence() with { Phase20FutureGatedRuntimeActivationCovered = false },
                SecureComputePhase21ConformanceDecision.DeniedMissingPhase20FutureGatedRuntimeEvidence),
            (CurrentEvidence() with { ReleaseEvidenceBoundaryCovered = false },
                SecureComputePhase21ConformanceDecision.DeniedMissingReleaseEvidenceBoundary),
        ];

        foreach ((SecureComputePhase21ConformanceEvidenceRequest request,
                 SecureComputePhase21ConformanceDecision expected) in cases)
        {
            SecureComputePhase21ConformanceEvidenceResult result =
                Policy.Classify(request);

            Assert.Equal(expected, result.Decision);
            Assert.False(result.MatrixEvidenceAccepted);
            AssertNoRuntimeAuthority(result);
        }
    }

    [Fact]
    public void AuthorityShortcutsRemainDeniedEvenWhenMatrixCoverageIsPresent()
    {
        (SecureComputePhase21ConformanceEvidenceRequest Request,
            SecureComputePhase21ConformanceDecision Decision)[] cases =
        [
            (CurrentEvidence() with { RequestsProductionActivation = true },
                SecureComputePhase21ConformanceDecision.DeniedProductionActivationOverclaim),
            (CurrentEvidence() with { RequestsCompilerSecureEmission = true },
                SecureComputePhase21ConformanceDecision.DeniedCompilerEmissionOverclaim),
            (CurrentEvidence() with { RequestsVmxOwnedSecureComputeAuthority = true },
                SecureComputePhase21ConformanceDecision.DeniedVmxAuthorityOverclaim),
            (CurrentEvidence() with { OpensPhase18NestedExecution = true },
                SecureComputePhase21ConformanceDecision.DeniedPhase18NestedOpened),
            (CurrentEvidence() with { TreatsManifestOnlyAsExecutionProof = true },
                SecureComputePhase21ConformanceDecision.DeniedManifestOnlyExecutionProof),
            (CurrentEvidence() with { TreatsDebugAttestationAsRuntimeEvidence = true },
                SecureComputePhase21ConformanceDecision.DeniedVisibilityOnlyRuntimeEvidence),
        ];

        foreach ((SecureComputePhase21ConformanceEvidenceRequest request,
                 SecureComputePhase21ConformanceDecision expected) in cases)
        {
            SecureComputePhase21ConformanceEvidenceResult result =
                Policy.Classify(request);

            Assert.Equal(expected, result.Decision);
            Assert.False(result.MatrixEvidenceAccepted);
            AssertNoRuntimeAuthority(result);
        }
    }

    [Fact]
    public void FuturePositivePathMatrix_CanBePackagedForPhase22ButStillDoesNotApproveRelease()
    {
        SecureComputePhase21ConformanceEvidenceResult result =
            Policy.Classify(CurrentEvidence() with { PositiveRuntimePathProven = true });

        Assert.Equal(
            SecureComputePhase21ConformanceDecision.PositivePathEvidenceMatrixAcceptedPhase22Required,
            result.Decision);
        Assert.True(result.MatrixEvidenceAccepted);
        Assert.False(result.FuturePositiveTestsRemainGated);
        AssertNoRuntimeAuthority(result);
    }

    [Fact]
    public void Source_DoesNotCreateExecutionPublicationVmxCompilerNestedOrReleaseAuthority()
    {
        string source = ReadProjectSource(
            "HybridCPU_ISE/CloseToRTL/Core/Virtualization/SecureCompute/Conformance/ReleaseGate/SecureComputePhase21ConformanceEvidencePolicy.cs");

        Assert.Contains("DeniedMissingPhase13RegistryBackedAdmissionEvidence", source);
        Assert.Contains("DeniedMissingPhase14CompletionRetireEvidence", source);
        Assert.Contains("DeniedMissingPhase15ManifestEvidence", source);
        Assert.Contains("DeniedMissingPhase16VisibilityEvidence", source);
        Assert.Contains("DeniedMissingPhase17VmxZeroAuthorityEvidence", source);
        Assert.Contains("DeniedMissingPhase18NestedDesignFenceEvidence", source);
        Assert.Contains("DeniedMissingPhase19NoCompilerChangeEvidence", source);
        Assert.Contains("DeniedMissingPhase20FutureGatedRuntimeEvidence", source);
        Assert.Contains("DeniedProductionActivationOverclaim", source);
        Assert.Contains("PositivePathEvidenceMatrixAcceptedPhase22Required", source);
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
    public void DocsAndReleaseGate_RecordCurrentMatrixClosureWithoutActivationApproval()
    {
        string phase21 = ReadSecureComputeActivationPlan("21_conformance_negative_positive_test_matrix.md");
        string release = ReadSecureComputeActivationPlan("22_limited_securecompute_release_gate.md");
        string backlog = ReadSecureComputeActivationPlan("23_open_decision_backlog.md");

        Assert.Contains("ADR-SC-CONFORMANCE-NEGATIVE-POSITIVE-MATRIX", phase21);
        Assert.Contains("SecureComputePhase21ConformanceEvidencePolicy", phase21);
        Assert.Contains("Implemented Phase 21 Conformance Evidence Gate Tests", phase21);
        Assert.Contains("Phase 21 is closed only for the current negative/future-gated conformance matrix", phase21);
        Assert.Contains("Phase 21 conformance evidence policy is a release-input classifier only", release);
        Assert.Contains("Phase 21 conformance matrix is closed only for current negative/future-gated evidence", backlog);
    }

    private static SecureComputePhase21ConformanceEvidencePolicy Policy =>
        SecureComputePhase21ConformanceEvidencePolicy.FailClosed;

    private static SecureComputePhase21ConformanceEvidenceRequest CurrentEvidence() =>
        new(
            Phase13RegistryBackedAdmissionCannotExecuteCovered: true,
            Phase14CompletionRetireFailClosedCovered: true,
            Phase15ManifestCoverageAndManifestOnlyDenialCovered: true,
            Phase16VisibilityCannotBecomeRuntimeEvidenceCovered: true,
            Phase17VmxZeroAuthorityCovered: true,
            Phase18NestedDesignFenceCovered: true,
            Phase19NoCompilerChangeCovered: true,
            Phase20FutureGatedRuntimeActivationCovered: true,
            ReleaseEvidenceBoundaryCovered: true);

    private static void AssertNoRuntimeAuthority(
        SecureComputePhase21ConformanceEvidenceResult result)
    {
        Assert.False(result.CreatesAnyRuntimeAuthority);
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
