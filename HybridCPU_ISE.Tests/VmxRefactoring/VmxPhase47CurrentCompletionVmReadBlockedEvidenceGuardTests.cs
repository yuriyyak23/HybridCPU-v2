using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

namespace HybridCPU_ISE.Tests.VmxRefactoring;

public sealed class VmxPhase47CurrentCompletionVmReadBlockedEvidenceGuardTests
{
    private const string Subject = "6cbcfe773b8b5076df3cd4b58d511b21dca26161";
    private const string SubjectTree = "3e1cc2ccbe65c754f50a726c912c3678b65d9649";
    private const string EvidenceFile =
        "2026-08-12-phase47-current-completion-vmread-e0-blocked-evidence.json";

    [Fact]
    public void CurrentStatus_RecordsNeutralP48BProgressWithoutD2OrProductionProjection()
    {
        using JsonDocument status = ReadPlanJson("VirtualizationActivationStatusV1.json");
        JsonElement root = status.RootElement;
        JsonElement phase47 = root.GetProperty("VmReadCurrentCompletionScalarDeliveryCandidate");
        Assert.Equal("BlockedE0IncompleteExactProducerFieldSemanticCoverage",
            phase47.GetProperty("State").GetString());
        Assert.Equal(Subject, phase47.GetProperty("E0SubjectSha").GetString());
        Assert.Equal(SubjectTree, phase47.GetProperty("E0SubjectTree").GetString());
        Assert.Equal("ProvenNeutralPhase48AAtPostLateEffectsPreRetireVisibilityCertificate",
            phase47.GetProperty("CanonicalCommitPoint").GetString());
        Assert.Equal("ExactRegisteredCanonicalPipelineTrapEntryProducerOnly",
            phase47.GetProperty("EligibleProducerClasses").GetString());
        Assert.Equal("NeutralExplicitPresenceAndSemanticObservationContractImplementedNoProjectionAuthorization",
            phase47.GetProperty("FieldValidityContract").GetString());
        Assert.Equal("DomainCompletionObservationOwnerNeutralReadOnly",
            phase47.GetProperty("DomainCurrentCompletionOwner").GetString());
        Assert.Equal("RuntimeOwnedNonZeroMonotonicNeutralObservationGeneration",
            phase47.GetProperty("CompletionGeneration").GetString());
        Assert.Equal("NotMaterialized", phase47.GetProperty("SpecV2").GetString());
        Assert.Equal("NotMaterialized", phase47.GetProperty("AcceptanceRecordV2").GetString());
        Assert.Equal("NotAuthorized", phase47.GetProperty("ProductionImplementation").GetString());
        Assert.Equal("NoAutomaticActivationExpansion", root.GetProperty("NextOpenPool").GetString());
        Assert.Equal("NoneUntilSeparateNeutralCpuTranslationFaultCompletionProducerAuthorization",
            root.GetProperty("NextCandidatePool").GetString());
    }

    [Fact]
    public void Evidence_IsLaterNonSelfReferentialAndHashesCleanSubjectBytes()
    {
        string repositoryRoot = VmxDocumentationMigrationClaimHygieneTests.FindRepositoryRoot();
        Assert.Equal(SubjectTree, Git(repositoryRoot, "rev-parse", $"{Subject}^{{tree}}"));

        using JsonDocument status = ReadPlanJson(
            "Phase47CurrentCompletionVmReadScalarDeliveryE0StatusV1.json");
        Assert.Equal(Subject,
            status.RootElement.GetProperty("e0_subject_commit_sha").GetString());
        Assert.Equal(JsonValueKind.Null,
            status.RootElement.GetProperty("later_evidence_commit_sha").ValueKind);
        Assert.False(status.RootElement.GetProperty("domain_current_completion_owner_created").GetBoolean());
        Assert.False(status.RootElement.GetProperty("spec_v2_materialized").GetBoolean());
        Assert.False(status.RootElement.GetProperty("acceptance_record_v2_materialized").GetBoolean());

        using JsonDocument evidence = ReadEvidenceJson();
        JsonElement root = evidence.RootElement;
        Assert.Equal("later_non_self_referential_blocked_e0_evidence",
            root.GetProperty("record_kind").GetString());
        Assert.True(root.GetProperty("non_self_referential").GetBoolean());
        Assert.False(root.GetProperty("runtime_authority").GetBoolean());
        Assert.Equal(Subject,
            root.GetProperty("e0_subject").GetProperty("commit_sha").GetString());
        foreach (JsonProperty source in root.GetProperty("source_hashes_sha256").EnumerateObject())
        {
            byte[] bytes = GitBytes(repositoryRoot, "show", $"{Subject}:{source.Name}");
            string actual = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            Assert.Equal(source.Value.GetString(), actual);
        }
        Assert.False(root.GetProperty("next_field_group_opened").GetBoolean());
    }

    [Fact]
    public void Evidence_DeniesEveryForbiddenSurrogateAuthority()
    {
        using JsonDocument evidence = ReadEvidenceJson();
        JsonElement denied = evidence.RootElement.GetProperty("verified_denials");
        foreach (JsonProperty property in denied.EnumerateObject())
            Assert.False(property.Value.GetBoolean(), property.Name);

        JsonElement findings = evidence.RootElement.GetProperty("e0_findings");
        Assert.True(findings.GetProperty("caller_supplied_completion").GetBoolean());
        Assert.Equal(0, findings.GetProperty("production_compatibility_exit_factory_callers").GetInt32());
        Assert.False(findings.GetProperty("nonforgeable_architectural_commit_evidence").GetBoolean());
        Assert.False(findings.GetProperty("explicit_per_field_validity").GetBoolean());
        Assert.Empty(findings.GetProperty("eligible_completion_record_classes").EnumerateArray());
    }

    private static JsonDocument ReadPlanJson(string fileName) => JsonDocument.Parse(
        File.ReadAllText(Path.Combine(
            VmxDocumentationMigrationClaimHygieneTests.FindRepositoryRoot(),
            "HybridCPU_ISE", "docs", "ref2", "VirtualizationActivationPlan", fileName)));

    private static JsonDocument ReadEvidenceJson() => JsonDocument.Parse(
        File.ReadAllText(Path.Combine(
            VmxDocumentationMigrationClaimHygieneTests.FindRepositoryRoot(),
            "HybridCPU_ISE", "docs", "ref2", "VirtualizationActivationPlan", "evidence", EvidenceFile)));

    private static string Git(string workingDirectory, params string[] arguments) =>
        System.Text.Encoding.UTF8.GetString(GitBytes(workingDirectory, arguments)).TrimEnd();

    private static byte[] GitBytes(string workingDirectory, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (string argument in arguments) startInfo.ArgumentList.Add(argument);
        using Process process = Process.Start(startInfo)!;
        using var output = new MemoryStream();
        process.StandardOutput.BaseStream.CopyTo(output);
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, error);
        return output.ToArray();
    }
}
