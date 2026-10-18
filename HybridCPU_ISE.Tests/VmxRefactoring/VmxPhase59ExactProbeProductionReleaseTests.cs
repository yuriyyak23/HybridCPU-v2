using System.Text.Json;

namespace HybridCPU_ISE.Tests.VmxRefactoring;

public sealed class VmxPhase59ExactProbeProductionReleaseTests
{
    [Fact]
    public void ReleaseRecord_SelectsOnlyExistingExactProbeAndIsNeverAuthority()
    {
        string root = VmxDocumentationMigrationClaimHygieneTests.FindRepositoryRoot();
        string path = Path.Combine(root, "HybridCPU_ISE", "docs", "ref2",
            "VirtualizationActivationPlan", "ProbeNoStateV1ExactProductionReleaseRecordV1.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement record = document.RootElement;

        Assert.Equal("ImmutableReleaseEvidenceOnlyNeverRuntimeAuthority",
            record.GetProperty("RecordRole").GetString());
        Assert.Equal("LimitedProductionReleaseOfProbeNoStateV1Only",
            record.GetProperty("Claim").GetString());
        Assert.Equal("e29d6b2150bf10c136ba3897eb17a9cab03c9967",
            record.GetProperty("ReleasedRuntimeSubjectSha").GetString());
        Assert.Equal("b3cd41275e548dc39fc7c0c7c5d411c339a0f29e",
            record.GetProperty("ReleasedRuntimeSubjectTree").GetString());

        JsonElement identity = record.GetProperty("ExactIdentity");
        Assert.Equal("PROBE_NO_STATE_V1", identity.GetProperty("OperationId").GetString());
        Assert.Equal("0x0001", identity.GetProperty("NumericLeaf").GetString());

        JsonElement authority = record.GetProperty("CompatibilityAuthority");
        Assert.All(authority.EnumerateObject(), property => Assert.False(property.Value.GetBoolean()));
        Assert.Equal(7, record.GetProperty("Excluded").GetArrayLength());
    }

    [Fact]
    public void MachineCurrent_ClosesOnlyExactProbeReleaseAndReturnsToNoOpenPool()
    {
        string root = VmxDocumentationMigrationClaimHygieneTests.FindRepositoryRoot();
        string path = Path.Combine(root, "HybridCPU_ISE", "docs", "ref2",
            "VirtualizationActivationPlan", "VirtualizationActivationStatusV1.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement status = document.RootElement;

        Assert.Equal("ClosedProductionExactProbeProfile",
            status.GetProperty("Gates").GetProperty("ReleaseGate").GetString());
        Assert.Equal("ClosedExactProfileProductionReachableDefaultDisabled",
            status.GetProperty("ExactActivation").GetString());
        Assert.Equal("LimitedProductionReleaseOfProbeNoStateV1Only",
            status.GetProperty("ReleaseClaim").GetString());
        Assert.Equal("None", status.GetProperty("NextOpenPool").GetString());
        Assert.Equal("NoneUntilSeparateOwnerAuthorizationForNextExactFieldOrOperation",
            status.GetProperty("NextCandidatePool").GetString());

        JsonElement phase = status.GetProperty("Phase59ExactProbeProductionRelease");
        Assert.Equal("ClosedGreenSubjectAndLaterNonSelfReferentialEvidence",
            phase.GetProperty("State").GetString());
        Assert.Equal("DisabledAbsentProfile", phase.GetProperty("ActivationDefault").GetString());
        Assert.Equal("NotOpened", phase.GetProperty("VmReadRelease").GetString());
    }

    [Fact]
    public void LaterEvidence_BindsSubjectWithoutBecomingAuthorityOrOpeningVmRead()
    {
        string root = VmxDocumentationMigrationClaimHygieneTests.FindRepositoryRoot();
        string path = Path.Combine(root, "HybridCPU_ISE", "docs", "ref2",
            "VirtualizationActivationPlan", "evidence",
            "2026-08-13-phase59-exact-probe-production-release-clean-evidence.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement evidence = document.RootElement;

        Assert.Equal("d6547321ae8c63a7eb4220bdc6b4360dc54b70d1",
            evidence.GetProperty("SubjectCommitSha").GetString());
        Assert.Equal("7666d9736f04611278e347c07c6d11794d717427",
            evidence.GetProperty("SubjectTreeSha").GetString());
        Assert.Equal("LaterNonSelfReferentialEvidenceOnlyNeverRuntimeAuthority",
            evidence.GetProperty("RecordRole").GetString());
        Assert.Equal("None", evidence.GetProperty("NextOpenPool").GetString());
        Assert.Equal("NoneUntilSeparateExactReadOnlyVmReadProfileReleaseSelection",
            evidence.GetProperty("NextCandidatePool").GetString());
        Assert.All(evidence.GetProperty("ForbiddenAuthority").EnumerateObject(),
            property => Assert.False(property.Value.GetBoolean()));
    }

    [Fact]
    public void ReleaseDocument_PreservesCanonicalConstructionRollbackAndExclusions()
    {
        string root = VmxDocumentationMigrationClaimHygieneTests.FindRepositoryRoot();
        string phase = File.ReadAllText(Path.Combine(root, "HybridCPU_ISE", "docs", "ref2",
            "VirtualizationActivationPlan", "59_probe_no_state_v1_exact_production_release.md"));

        Assert.Contains("CPU_Core.InitializePipeline", phase);
        Assert.Contains("CPU_Core.DisableConfiguredExactProbeRuntimeProfile", phase);
        Assert.Contains("default-disabled exact profile", phase);
        Assert.Contains("does not release any VMREAD profile", phase);
        Assert.Contains("never read by production code", phase);
        Assert.Contains("NextOpenPool = None", phase);
    }
}
