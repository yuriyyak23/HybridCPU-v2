using System.Diagnostics;
using System.Text.Json;

namespace HybridCPU_ISE.Tests.VmxRefactoring;

public sealed class VmxPhase43ProductionStatusAndEvidenceGuardTests
{
    private const string DecisionId =
        "D2-HV-VMREAD-SCALAR-DELIVERY-V1-GUEST-PC-SP-FLAGS-0002";
    private const string SourceEpochSubject =
        "21ca5bece0d17fdb69f4ecd4bbd1a7c9ef61f141";
    private const string SourceEpochTree =
        "2040f157bdbcad3576c9abec6e82c91aff62a2cf";
    private const string CompositionSubject =
        "ee5627c022a8f0922fd2477b54a53a06b9b2aa71";
    private const string CompositionTree =
        "5f097109564255f2a0628a48ead983837625bada";
    private const string EvidenceFile =
        "2026-08-12-phase43-guest-pc-sp-flags-vmread-scalar-delivery-clean-evidence.json";

    [Fact]
    public void CurrentStatus_ClosesOnlyExactDefaultDisabledProfile()
    {
        using JsonDocument status = ReadPlanJson("VirtualizationActivationStatusV1.json");
        JsonElement root = status.RootElement;
        JsonElement phase43 = root.GetProperty("VmReadGuestPcSpFlagsScalarDeliveryD2");
        Assert.Equal("ClosedExactProductionCompositionDefaultDisabled",
            phase43.GetProperty("State").GetString());
        Assert.Equal(DecisionId, phase43.GetProperty("DecisionId").GetString());
        Assert.Equal(SourceEpochSubject, phase43.GetProperty("SourceEpochSubjectSha").GetString());
        Assert.Equal(SourceEpochTree, phase43.GetProperty("SourceEpochSubjectTree").GetString());
        Assert.Equal(CompositionSubject, phase43.GetProperty("ImplementationSubjectSha").GetString());
        Assert.Equal(CompositionTree, phase43.GetProperty("ImplementationSubjectTree").GetString());
        Assert.Equal(new[] { "VmcsField.GuestPc", "VmcsField.GuestSp", "VmcsField.GuestFlags" },
            phase43.GetProperty("ExactFields").EnumerateArray().Select(item => item.GetString()).ToArray());
        Assert.Equal("ExecutionDomainDescriptor", phase43.GetProperty("SourceOwner").GetString());
        Assert.Equal("None", phase43.GetProperty("CapabilityRequirement").GetString());
        Assert.Equal("DrainOnly", phase43.GetProperty("Migration").GetString());
        Assert.Equal("Disabled", phase43.GetProperty("ActivationDefault").GetString());
        Assert.Equal("ClosedRuntimeIssuedNonZeroAtomicCapture",
            phase43.GetProperty("SourceEpochProductionGate").GetString());
        Assert.Equal("CanonicalRetireCoordinatorOnly",
            phase43.GetProperty("ArchitecturalCommit").GetString());
        Assert.Equal("Denied", phase43.GetProperty("BackendExecution").GetString());
        Assert.Equal("Denied", phase43.GetProperty("TrapCompletion").GetString());
        Assert.Equal("Denied", phase43.GetProperty("VmxRetireEffect").GetString());
        Assert.Equal("Denied", phase43.GetProperty("AdjacentFields").GetString());
        Assert.Equal("NoAutomaticActivationExpansion", root.GetProperty("NextOpenPool").GetString());
        Assert.Equal("NoneUntilSeparateNeutralCpuTranslationFaultCompletionProducerAuthorization",
            root.GetProperty("NextCandidatePool").GetString());
    }

    [Fact]
    public void LaterEvidence_IsNonSelfReferentialAndBindsBothCleanSubjects()
    {
        using JsonDocument status = ReadPlanJson(
            "Phase43GuestPcSpFlagsVmReadScalarDeliveryImplementationStatusV1.json");
        JsonElement implementation = status.RootElement;
        Assert.Equal("ClosedByLaterNonSelfReferentialEvidenceRecord",
            implementation.GetProperty("status").GetString());
        Assert.Equal(SourceEpochSubject,
            implementation.GetProperty("source_epoch_subject_commit_sha").GetString());
        Assert.Equal(CompositionSubject,
            implementation.GetProperty("composition_subject_commit_sha").GetString());
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
        Assert.Equal(SourceEpochSubject,
            subjects.GetProperty("source_epoch_gate").GetProperty("commit_sha").GetString());
        Assert.Equal(SourceEpochTree,
            subjects.GetProperty("source_epoch_gate").GetProperty("tree_sha").GetString());
        Assert.Equal(CompositionSubject,
            subjects.GetProperty("production_composition").GetProperty("commit_sha").GetString());
        Assert.Equal(CompositionTree,
            subjects.GetProperty("production_composition").GetProperty("tree_sha").GetString());
        Assert.Equal("clean", subjects.GetProperty("production_composition")
            .GetProperty("status_at_verification").GetString());
        Assert.Equal(0, subjects.GetProperty("production_composition")
            .GetProperty("ignored_csharp_under_close_to_hsl").GetInt32());
        Assert.Equal(0, subjects.GetProperty("production_composition")
            .GetProperty("untracked_csharp_under_close_to_hsl").GetInt32());
        Assert.False(root.GetProperty("next_field_group_opened").GetBoolean());
    }

    [Fact]
    public void EvidenceHashes_AreWellFormedSubjectsContainSourcesAndAuthorityRemainsNeutral()
    {
        using JsonDocument evidence = ReadEvidenceJson();
        JsonElement root = evidence.RootElement;
        JsonElement hashes = root.GetProperty("source_hashes_sha256");
        string repositoryRoot = VmxDocumentationMigrationClaimHygieneTests.FindRepositoryRoot();
        foreach (JsonProperty source in hashes.EnumerateObject())
        {
            string subject = source.Name.StartsWith(
                    "HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/Admission/Execution/",
                    StringComparison.Ordinal) ||
                source.Name ==
                    "HybridCPU_ISE.Tests/VmxRefactoring/VmxPhase43ExecutionDomainSourceEpochGateTests.cs"
                ? SourceEpochSubject
                : CompositionSubject;
            Assert.Equal(64, source.Value.GetString()?.Length);
            Assert.NotEmpty(ReadCommittedBlob(repositoryRoot, subject, source.Name));
        }

        JsonElement authority = root.GetProperty("verified_authority_boundaries");
        Assert.Equal("runtime-issued non-zero epoch; caller cannot assert current epoch",
            authority.GetProperty("source_epoch").GetString());
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
            "44_guest_pc_sp_flags_vmread_scalar_delivery_production.md"));
        Assert.Contains("default disabled", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("No later VMREAD field group is opened", text, StringComparison.Ordinal);
        Assert.Contains("no VMCS scalar/backing-store source", text, StringComparison.OrdinalIgnoreCase);
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

    private static byte[] ReadCommittedBlob(
        string repositoryRoot,
        string commit,
        string relativePath)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                WorkingDirectory = repositoryRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            },
        };
        process.StartInfo.ArgumentList.Add("show");
        process.StartInfo.ArgumentList.Add($"{commit}:{relativePath}");
        Assert.True(process.Start());
        using var output = new MemoryStream();
        process.StandardOutput.BaseStream.CopyTo(output);
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, error);
        return output.ToArray();
    }
}
