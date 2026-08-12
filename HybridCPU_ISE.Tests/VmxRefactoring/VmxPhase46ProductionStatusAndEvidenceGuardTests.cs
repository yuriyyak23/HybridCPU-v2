using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

namespace HybridCPU_ISE.Tests.VmxRefactoring;

public sealed class VmxPhase46ProductionStatusAndEvidenceGuardTests
{
    private const string DecisionId =
        "D2-HV-VMREAD-SCALAR-DELIVERY-V1-GUEST-CR3-EPTP-VPID-CR3TC-0003";
    private const string CompositionSubject =
        "356c49f9d6384ec8bbb34c8f6a72a793447b7c3a";
    private const string CompositionTree =
        "19d2a4378a28d4295570610ebf3f1461e4fc705e";
    private const string PipelineSubject =
        "6560d27386e78ee91fed00cdb8bd6af2a752b8ad";
    private const string PipelineTree =
        "ea960cd29c9c60f4186ec3f6ac4b7c8fc7163af1";
    private const string VerificationBasis =
        "25453f76858875ccc183dc70155734948c96f741";
    private const string VerificationTree =
        "ae49d8f89becd9296c973f8a79ebe3659459cdfe";
    private const string EvidenceFile =
        "2026-08-12-phase46-memory-owned-vmread-scalar-delivery-clean-evidence.json";

    [Fact]
    public void CurrentStatus_ClosesOnlyExactDefaultDisabledMemoryOwnedProfile()
    {
        using JsonDocument status = ReadPlanJson("VirtualizationActivationStatusV1.json");
        JsonElement root = status.RootElement;
        JsonElement phase46 = root.GetProperty("VmReadMemoryOwnedScalarDeliveryD2");
        Assert.Equal("ClosedExactProductionCompositionDefaultDisabled",
            phase46.GetProperty("State").GetString());
        Assert.Equal(DecisionId, phase46.GetProperty("DecisionId").GetString());
        Assert.Equal(CompositionSubject, phase46.GetProperty("CompositionSubjectSha").GetString());
        Assert.Equal(CompositionTree, phase46.GetProperty("CompositionSubjectTree").GetString());
        Assert.Equal(PipelineSubject, phase46.GetProperty("PipelineIntegrationSubjectSha").GetString());
        Assert.Equal(PipelineTree, phase46.GetProperty("PipelineIntegrationSubjectTree").GetString());
        Assert.Equal(VerificationBasis, phase46.GetProperty("CleanVerificationBasisSha").GetString());
        Assert.Equal(VerificationTree, phase46.GetProperty("CleanVerificationBasisTree").GetString());
        Assert.Equal(new[]
            {
                "VmcsField.GuestCr3", "VmcsField.EptPointer",
                "VmcsField.Vpid", "VmcsField.Cr3TargetCount",
            },
            phase46.GetProperty("ExactFields").EnumerateArray()
                .Select(item => item.GetString()).ToArray());
        Assert.Equal("MemoryDomainDescriptorViaCanonicalMemoryDomainRuntime",
            phase46.GetProperty("SourceOwner").GetString());
        Assert.Equal("RuntimeOwnedNonZeroCurrentAddressSpaceGenerationAtomicCapture",
            phase46.GetProperty("FreshnessAuthority").GetString());
        Assert.Equal("None", phase46.GetProperty("CapabilityRequirement").GetString());
        Assert.Equal("DrainOnly", phase46.GetProperty("Migration").GetString());
        Assert.Equal("Disabled", phase46.GetProperty("ActivationDefault").GetString());
        Assert.Equal("CanonicalRetireCoordinatorOnly",
            phase46.GetProperty("ArchitecturalCommit").GetString());
        Assert.Equal("Denied", phase46.GetProperty("BackendExecution").GetString());
        Assert.Equal("Denied", phase46.GetProperty("TrapCompletion").GetString());
        Assert.Equal("Denied", phase46.GetProperty("VmxRetireEffect").GetString());
        Assert.Equal("Denied", phase46.GetProperty("Vmwrite").GetString());
        Assert.Equal("Denied", phase46.GetProperty("AdjacentFields").GetString());
        Assert.Equal("NoAutomaticActivationExpansion", root.GetProperty("NextOpenPool").GetString());
        Assert.Equal("NoneUntilSeparateNeutralCpuTranslationFaultCompletionProducerAuthorization",
            root.GetProperty("NextCandidatePool").GetString());
    }

    [Fact]
    public void LaterEvidence_IsNonSelfReferentialAndBindsAllCleanSubjects()
    {
        string repositoryRoot = VmxDocumentationMigrationClaimHygieneTests.FindRepositoryRoot();
        Assert.Equal(CompositionTree, Git(repositoryRoot, "rev-parse", $"{CompositionSubject}^{{tree}}"));
        Assert.Equal(PipelineTree, Git(repositoryRoot, "rev-parse", $"{PipelineSubject}^{{tree}}"));
        Assert.Equal(VerificationTree, Git(repositoryRoot, "rev-parse", $"{VerificationBasis}^{{tree}}"));

        using JsonDocument status = ReadPlanJson(
            "Phase46MemoryOwnedVmReadScalarDeliveryImplementationStatusV1.json");
        JsonElement implementation = status.RootElement;
        Assert.Equal("ClosedByLaterNonSelfReferentialEvidenceRecord",
            implementation.GetProperty("status").GetString());
        Assert.Equal(CompositionSubject,
            implementation.GetProperty("composition_subject_commit_sha").GetString());
        Assert.Equal(PipelineSubject,
            implementation.GetProperty("pipeline_integration_subject_commit_sha").GetString());
        Assert.Equal(VerificationBasis,
            implementation.GetProperty("clean_verification_basis_commit_sha").GetString());
        Assert.Equal(JsonValueKind.Null,
            implementation.GetProperty("later_evidence_commit_sha").ValueKind);
        Assert.False(implementation.GetProperty("next_field_group_opened").GetBoolean());

        using JsonDocument evidence = ReadEvidenceJson();
        JsonElement root = evidence.RootElement;
        JsonElement subjects = root.GetProperty("implementation_subjects");
        Assert.Equal("later_non_self_referential_implementation_evidence",
            root.GetProperty("record_kind").GetString());
        Assert.True(root.GetProperty("non_self_referential").GetBoolean());
        Assert.False(root.GetProperty("runtime_authority").GetBoolean());
        Assert.Equal(CompositionSubject,
            subjects.GetProperty("production_composition").GetProperty("commit_sha").GetString());
        Assert.Equal(PipelineSubject,
            subjects.GetProperty("pipeline_integration").GetProperty("commit_sha").GetString());
        Assert.Equal(VerificationBasis,
            subjects.GetProperty("clean_verification_basis").GetProperty("commit_sha").GetString());
        Assert.Equal("clean", subjects.GetProperty("clean_verification_basis")
            .GetProperty("status_at_verification").GetString());
        Assert.Equal(0, subjects.GetProperty("clean_verification_basis")
            .GetProperty("ignored_csharp_under_close_to_hsl").GetInt32());
        Assert.Equal(0, subjects.GetProperty("clean_verification_basis")
            .GetProperty("untracked_csharp_under_close_to_hsl").GetInt32());
        Assert.False(root.GetProperty("next_field_group_opened").GetBoolean());
    }

    [Fact]
    public void EvidenceHashes_MatchCleanVerificationBasisAndAuthorityRemainsNeutral()
    {
        using JsonDocument evidence = ReadEvidenceJson();
        JsonElement root = evidence.RootElement;
        string repositoryRoot = VmxDocumentationMigrationClaimHygieneTests.FindRepositoryRoot();
        foreach (JsonProperty source in root.GetProperty("source_hashes_sha256").EnumerateObject())
        {
            byte[] sourceBytes = GitBytes(repositoryRoot, "show", $"{VerificationBasis}:{source.Name}");
            string actual = Convert.ToHexString(SHA256.HashData(sourceBytes)).ToLowerInvariant();
            Assert.Equal(source.Value.GetString(), actual);
        }

        JsonElement authority = root.GetProperty("verified_authority_boundaries");
        Assert.Equal("runtime-owned non-zero current AddressSpaceGeneration; caller cannot assert freshness",
            authority.GetProperty("source_generation").GetString());
        Assert.True(authority.GetProperty("atomic_source_capture").GetBoolean());
        Assert.False(authority.GetProperty("governance_policy_grants_runtime_authority").GetBoolean());
        Assert.False(authority.GetProperty("receipt_grants_source_authority").GetBoolean());
        Assert.False(authority.GetProperty("compatibility_frontend_authority").GetBoolean());
        Assert.False(authority.GetProperty("vmcs_value_or_backing_store_source").GetBoolean());
        Assert.False(authority.GetProperty("direct_architectural_register_write").GetBoolean());
        Assert.True(authority.GetProperty("canonical_prf_rename_writeback_only").GetBoolean());
        Assert.True(authority.GetProperty("canonical_retire_coordinator_commit_only").GetBoolean());
        Assert.False(authority.GetProperty("backend_execution").GetBoolean());
        Assert.False(authority.GetProperty("trap_completion_publication").GetBoolean());
        Assert.False(authority.GetProperty("vmx_retire_effect").GetBoolean());
        Assert.False(authority.GetProperty("vmcall_e2_e7_reuse").GetBoolean());
        Assert.False(authority.GetProperty("probe_capability_bit_41_reuse").GetBoolean());
    }

    [Fact]
    public void ProductionDocument_DeniesBroadOrAdjacentClaims()
    {
        string text = File.ReadAllText(Path.Combine(
            VmxDocumentationMigrationClaimHygieneTests.FindRepositoryRoot(),
            "HybridCPU_ISE", "docs", "ref2", "VirtualizationActivationPlan",
            "46_guest_cr3_eptp_vpid_cr3_target_count_vmread_scalar_delivery_production.md"));
        Assert.Contains("default disabled", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("No later VMREAD field group is opened", text, StringComparison.Ordinal);
        Assert.Contains("no compatibility/frontend authority", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("VMCS scalar/backing-store source", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("RetireCoordinator", text, StringComparison.Ordinal);
        Assert.DoesNotContain("VMX fully activated", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Virtualization complete", text, StringComparison.OrdinalIgnoreCase);
    }

    private static JsonDocument ReadPlanJson(string fileName) => JsonDocument.Parse(
        File.ReadAllText(Path.Combine(
            VmxDocumentationMigrationClaimHygieneTests.FindRepositoryRoot(),
            "HybridCPU_ISE", "docs", "ref2", "VirtualizationActivationPlan", fileName)));

    private static JsonDocument ReadEvidenceJson() => JsonDocument.Parse(
        File.ReadAllText(Path.Combine(
            VmxDocumentationMigrationClaimHygieneTests.FindRepositoryRoot(),
            "HybridCPU_ISE", "docs", "ref2", "VirtualizationActivationPlan",
            "evidence", EvidenceFile)));

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
