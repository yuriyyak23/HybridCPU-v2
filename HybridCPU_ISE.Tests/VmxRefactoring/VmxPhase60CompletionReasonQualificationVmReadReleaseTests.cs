using System.Text.Json;

namespace HybridCPU_ISE.Tests.VmxRefactoring;

public sealed class VmxPhase60CompletionReasonQualificationVmReadReleaseTests
{
    [Fact]
    public void ReleaseRecord_SelectsOnlyProductionReachablePhase55Profile()
    {
        string root = VmxDocumentationMigrationClaimHygieneTests.FindRepositoryRoot();
        string path = Path.Combine(root, "HybridCPU_ISE", "docs", "ref2",
            "VirtualizationActivationPlan",
            "CompletionReasonQualificationExactVmReadProductionReleaseRecordV1.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement record = document.RootElement;

        Assert.Equal("ImmutableReleaseEvidenceOnlyNeverRuntimeAuthority",
            record.GetProperty("RecordRole").GetString());
        Assert.Equal("LimitedProductionReleaseOfCompletionReasonQualificationVmReadOnly",
            record.GetProperty("Claim").GetString());
        Assert.Equal("DELIVER_COMPLETION_REASON_QUALIFICATION_SCALAR_V1",
            record.GetProperty("ExactIdentity").GetProperty("OperationId").GetString());
        Assert.Equal(2, record.GetProperty("ExactIdentity").GetProperty("Fields").GetArrayLength());
        Assert.All(record.GetProperty("CompatibilityAuthority").EnumerateObject(),
            property => Assert.False(property.Value.GetBoolean()));
        Assert.Equal(7, record.GetProperty("Excluded").GetArrayLength());
    }

    [Fact]
    public void MachineCurrent_ClosesOneVmReadReleaseAndNoLaterPool()
    {
        string root = VmxDocumentationMigrationClaimHygieneTests.FindRepositoryRoot();
        string path = Path.Combine(root, "HybridCPU_ISE", "docs", "ref2",
            "VirtualizationActivationPlan", "VirtualizationActivationStatusV1.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement status = document.RootElement;

        Assert.Equal("ClosedExactCompletionReasonQualificationDefaultDisabled",
            status.GetProperty("Gates").GetProperty("ExactReadOnlyVmReadReleaseGate").GetString());
        Assert.Equal("None", status.GetProperty("NextOpenPool").GetString());
        Assert.Equal("NoneUntilSeparateOwnerAuthorizationForNextExactFieldOrOperation",
            status.GetProperty("NextCandidatePool").GetString());
        JsonElement phase = status.GetProperty("Phase60CompletionReasonQualificationExactVmReadRelease");
        Assert.Equal("ClosedGreenSubjectAndLaterNonSelfReferentialEvidence", phase.GetProperty("State").GetString());
        Assert.Equal("ExitReasonAndReasonBoundExitQualificationOnly",
            phase.GetProperty("ExactFields").GetString());
        Assert.Equal("Denied", phase.GetProperty("AdjacentProfiles").GetString());
    }

    [Fact]
    public void LaterEvidence_BindsSubjectAndKeepsEveryAdjacentAuthorityFalse()
    {
        string root = VmxDocumentationMigrationClaimHygieneTests.FindRepositoryRoot();
        string path = Path.Combine(root, "HybridCPU_ISE", "docs", "ref2",
            "VirtualizationActivationPlan", "evidence",
            "2026-08-13-phase60-completion-reason-qualification-vmread-release-clean-evidence.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement evidence = document.RootElement;

        Assert.Equal("57b2a1d805bdad547a4535e40a68316f88e0816c",
            evidence.GetProperty("SubjectCommitSha").GetString());
        Assert.Equal("41fc1b9c3c8a3a43792c50480164253b66ac0f72",
            evidence.GetProperty("SubjectTreeSha").GetString());
        Assert.Equal("LaterNonSelfReferentialEvidenceOnlyNeverRuntimeAuthority",
            evidence.GetProperty("RecordRole").GetString());
        Assert.Equal("None", evidence.GetProperty("NextOpenPool").GetString());
        Assert.Equal(4, evidence.GetProperty("UnreleasedExistingProfiles").GetArrayLength());
        Assert.All(evidence.GetProperty("ForbiddenAuthority").EnumerateObject(),
            property => Assert.False(property.Value.GetBoolean()));
    }

    [Fact]
    public void ProductionSource_HasExactConstructionMaterializationAndKillSwitchOnly()
    {
        string root = VmxDocumentationMigrationClaimHygieneTests.FindRepositoryRoot();
        string state = File.ReadAllText(Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Architecture", "State", "Architectural", "CPU_Core.StateData.cs"));
        string materialization = File.ReadAllText(Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "ExecutionFlow", "Materialization", "CPU_Core.PipelineExecution.Materialization.cs"));
        string lifecycle = File.ReadAllText(Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Runtime", "Events", "VmRead", "CompletionReasonQualificationVmReadScalarDeliveryCanonicalComposition.cs"));

        Assert.Contains("platformContext.EnableCompletionReasonQualificationVmRead", state);
        Assert.Contains("TryPrepareCompletionReasonQualificationVmReadAfterCanonicalValueRead", materialization);
        Assert.Contains("internal bool Disable()", lifecycle);
        Assert.Contains("_generation = checked(_generation + 1);", lifecycle);
        Assert.DoesNotContain("GuestPhysicalAddress or VmcsField.EptViolationQualification)\n                return new", lifecycle);
    }
}
