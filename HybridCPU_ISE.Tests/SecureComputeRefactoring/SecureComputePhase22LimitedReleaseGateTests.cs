using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.SecureComputeRefactoring;

public sealed class SecureComputePhase22LimitedReleaseGateTests
{
    [Fact]
    public void CurrentPhase21Matrix_DoesNotApproveLimitedReleaseWithoutNamedPositiveRuntimePath()
    {
        SecureComputePhase21ConformanceEvidenceResult phase21 =
            Phase21Policy.Classify(CurrentPhase21Evidence());

        Assert.True(phase21.MatrixEvidenceAccepted);
        Assert.True(phase21.FuturePositiveTestsRemainGated);

        SecureComputePhase22LimitedReleaseGateResult release =
            ReleasePolicy.Classify(CurrentReleaseEvidence(phase21));

        Assert.Equal(
            SecureComputePhase22LimitedReleaseDecision.DeniedNoNamedPositiveRuntimePath,
            release.Decision);
        AssertNoReleaseAuthority(release);
    }

    [Fact]
    public void RequiresPhase21MatrixBeforeAnyReleaseEvidenceCanBeConsumed()
    {
        SecureComputePhase22LimitedReleaseGateResult release =
            ReleasePolicy.Classify(CurrentReleaseEvidence() with
            {
                Phase21MatrixEvidenceAccepted = false,
            });

        Assert.Equal(
            SecureComputePhase22LimitedReleaseDecision.DeniedPhase21MatrixMissing,
            release.Decision);
        AssertNoReleaseAuthority(release);
    }

    [Fact]
    public void FutureGatedPhase21PositiveTests_BlockProductionReleaseEvenWithNamedPathClaim()
    {
        SecureComputePhase22LimitedReleaseGateResult release =
            ReleasePolicy.Classify(CurrentReleaseEvidence() with
            {
                NamedPositiveRuntimePathProven = true,
                Phase21FuturePositiveTestsRemainGated = true,
            });

        Assert.Equal(
            SecureComputePhase22LimitedReleaseDecision.DeniedPhase21FutureGatedOnly,
            release.Decision);
        AssertNoReleaseAuthority(release);
    }

    [Fact]
    public void ReleasePrerequisitesFailClosedForHypotheticalNamedPathEvidence()
    {
        (SecureComputePhase22LimitedReleaseEvidenceRequest Request,
            SecureComputePhase22LimitedReleaseDecision Decision)[] cases =
        [
            (FutureReleaseEvidence() with { OwnerSpecificRfcAdrAccepted = false },
                SecureComputePhase22LimitedReleaseDecision.DeniedMissingOwnerSpecificRfcAdr),
            (FutureReleaseEvidence() with { ProductionOwnerCodeExists = false },
                SecureComputePhase22LimitedReleaseDecision.DeniedMissingProductionOwnerCode),
            (FutureReleaseEvidence() with { OwnerPathReachabilityProven = false },
                SecureComputePhase22LimitedReleaseDecision.DeniedMissingOwnerPathReachability),
            (FutureReleaseEvidence() with { TypedRequestResultModelExists = false },
                SecureComputePhase22LimitedReleaseDecision.DeniedMissingTypedRequestResult),
            (FutureReleaseEvidence() with { BackendResultOwnerBoundaryProven = false },
                SecureComputePhase22LimitedReleaseDecision.DeniedMissingBackendResultOwnerBoundary),
            (FutureReleaseEvidence() with { CompletionRetirePolicyForNamedPathProven = false },
                SecureComputePhase22LimitedReleaseDecision.DeniedMissingCompletionRetirePolicy),
            (FutureReleaseEvidence() with { MigrationManifestRestoreEvidenceComplete = false },
                SecureComputePhase22LimitedReleaseDecision.DeniedMissingMigrationManifestRestoreEvidence),
            (FutureReleaseEvidence() with { DebugAttestationVisibilityLimitsProven = false },
                SecureComputePhase22LimitedReleaseDecision.DeniedMissingDebugAttestationLimits),
            (FutureReleaseEvidence() with { VmxProjectionAfterNeutralResultOnly = false },
                SecureComputePhase22LimitedReleaseDecision.DeniedMissingVmxZeroAuthorityProjectionLimit),
            (FutureReleaseEvidence() with { CompilerNoEmissionDecisionRecorded = false },
                SecureComputePhase22LimitedReleaseDecision.DeniedMissingCompilerNoEmissionDecision),
            (FutureReleaseEvidence() with { Phase18NestedExecutionExcluded = false },
                SecureComputePhase22LimitedReleaseDecision.DeniedPhase18NestedNotExcluded),
            (FutureReleaseEvidence() with { ProductClaimScopedToNamedPath = false },
                SecureComputePhase22LimitedReleaseDecision.DeniedProductClaimOverreach),
            (FutureReleaseEvidence() with { BoundedRollbackProcedureReviewed = false },
                SecureComputePhase22LimitedReleaseDecision.DeniedMissingRollbackProcedure),
            (FutureReleaseEvidence() with { BackendExecutionAuthorityLocallyProven = false },
                SecureComputePhase22LimitedReleaseDecision.DeniedBackendExecutionAuthorityNotLocallyProven),
        ];

        foreach ((SecureComputePhase22LimitedReleaseEvidenceRequest request,
                 SecureComputePhase22LimitedReleaseDecision expected) in cases)
        {
            SecureComputePhase22LimitedReleaseGateResult release =
                ReleasePolicy.Classify(request);

            Assert.Equal(expected, release.Decision);
            AssertNoReleaseAuthority(release);
        }
    }

    [Fact]
    public void FullyPopulatedHypotheticalEvidence_StillRequiresFutureNamedPathReleaseApprovalImplementation()
    {
        SecureComputePhase22LimitedReleaseGateResult release =
            ReleasePolicy.Classify(FutureReleaseEvidence());

        Assert.Equal(
            SecureComputePhase22LimitedReleaseDecision.DeniedPhase22ManualApprovalNotImplemented,
            release.Decision);
        AssertNoReleaseAuthority(release);
    }

    [Fact]
    public void Source_DoesNotCreateReleaseExecutionPublicationVmxCompilerOrNestedAuthority()
    {
        string source = ReadProjectSource(
            "HybridCPU_ISE/CloseToRTL/Core/Virtualization/SecureCompute/Conformance/ReleaseGate/SecureComputePhase22LimitedReleaseGatePolicy.cs");

        Assert.Contains("DeniedNoNamedPositiveRuntimePath", source);
        Assert.Contains("DeniedPhase21MatrixMissing", source);
        Assert.Contains("DeniedPhase21FutureGatedOnly", source);
        Assert.Contains("DeniedMissingOwnerSpecificRfcAdr", source);
        Assert.Contains("DeniedMissingProductionOwnerCode", source);
        Assert.Contains("DeniedMissingOwnerPathReachability", source);
        Assert.Contains("DeniedMissingBackendResultOwnerBoundary", source);
        Assert.Contains("DeniedMissingCompletionRetirePolicy", source);
        Assert.Contains("DeniedMissingMigrationManifestRestoreEvidence", source);
        Assert.Contains("DeniedMissingDebugAttestationLimits", source);
        Assert.Contains("DeniedMissingVmxZeroAuthorityProjectionLimit", source);
        Assert.Contains("DeniedMissingCompilerNoEmissionDecision", source);
        Assert.Contains("DeniedPhase18NestedNotExcluded", source);
        Assert.Contains("DeniedProductClaimOverreach", source);
        Assert.Contains("DeniedMissingRollbackProcedure", source);
        Assert.Contains("DeniedBackendExecutionAuthorityNotLocallyProven", source);
        Assert.Contains("DeniedPhase22ManualApprovalNotImplemented", source);
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
    public void Docs_RecordFailClosedReleaseGateAndPhase18CompilerVmxBoundaries()
    {
        string phase21 = ReadSecureComputeActivationPlan("21_conformance_negative_positive_test_matrix.md");
        string phase22 = ReadSecureComputeActivationPlan("22_limited_securecompute_release_gate.md");
        string backlog = ReadSecureComputeActivationPlan("23_open_decision_backlog.md");

        Assert.Contains("SecureComputePhase22LimitedReleaseGatePolicy", phase22);
        Assert.Contains("Implemented Phase 22 Fail-Closed Limited Release Gate Tests", phase21);
        Assert.Contains("Phase 22 remains future-gated because no named positive runtime path is locally proven", phase22);
        Assert.Contains("Phase 18 nested execution remains excluded from limited release", phase22);
        Assert.Contains("compiler secure emission remains closed", phase22);
        Assert.Contains("Phase 22 limited release gate is implemented as a fail-closed release classifier", backlog);
    }

    private static SecureComputePhase21ConformanceEvidencePolicy Phase21Policy =>
        SecureComputePhase21ConformanceEvidencePolicy.FailClosed;

    private static SecureComputePhase22LimitedReleaseGatePolicy ReleasePolicy =>
        SecureComputePhase22LimitedReleaseGatePolicy.FailClosed;

    private static SecureComputePhase21ConformanceEvidenceRequest CurrentPhase21Evidence() =>
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

    private static SecureComputePhase22LimitedReleaseEvidenceRequest CurrentReleaseEvidence(
        SecureComputePhase21ConformanceEvidenceResult? phase21 = null)
    {
        SecureComputePhase21ConformanceEvidenceResult matrix =
            phase21 ?? Phase21Policy.Classify(CurrentPhase21Evidence());

        return new(
            matrix.MatrixEvidenceAccepted,
            matrix.FuturePositiveTestsRemainGated,
            NamedPositiveRuntimePathProven: false,
            OwnerSpecificRfcAdrAccepted: false,
            ProductionOwnerCodeExists: false,
            OwnerPathReachabilityProven: false,
            TypedRequestResultModelExists: false,
            BackendResultOwnerBoundaryProven: false,
            CompletionRetirePolicyForNamedPathProven: false,
            MigrationManifestRestoreEvidenceComplete: false,
            DebugAttestationVisibilityLimitsProven: false,
            VmxProjectionAfterNeutralResultOnly: false,
            CompilerNoEmissionDecisionRecorded: true,
            Phase18NestedExecutionExcluded: true,
            ProductClaimScopedToNamedPath: false,
            BoundedRollbackProcedureReviewed: false,
            BackendExecutionAuthorityLocallyProven: false);
    }

    private static SecureComputePhase22LimitedReleaseEvidenceRequest FutureReleaseEvidence() =>
        new(
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
            BackendExecutionAuthorityLocallyProven: true);

    private static void AssertNoReleaseAuthority(
        SecureComputePhase22LimitedReleaseGateResult result)
    {
        Assert.False(result.CreatesAnyReleaseAuthority);
        Assert.False(result.ReleaseEvidenceAccepted);
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
