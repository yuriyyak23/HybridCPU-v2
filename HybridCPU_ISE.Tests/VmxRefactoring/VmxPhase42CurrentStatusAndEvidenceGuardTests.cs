using System.Diagnostics;
using System.Text.Json;

namespace HybridCPU_ISE.Tests.VmxRefactoring;

public sealed class VmxPhase42CurrentStatusAndEvidenceGuardTests
{
    private const string SubjectCommit =
        "253e33435b1500a04ecde9228631fb3fab547d15";
    private const string SubjectTree =
        "9868fc233bf1c394e3791068eac0f76e063ea623";
    private const string DecisionId =
        "D2-HV-VMREAD-SCALAR-DELIVERY-V1-GUEST-CR0-CR4-0001";
    private const string EvidenceFile =
        "2026-08-11-phase42-guest-cr0-cr4-vmread-scalar-delivery-clean-evidence.json";

    [Fact]
    public void CurrentStatus_PinsExactPhase42ProfileAndNoAutomaticExpansion()
    {
        using JsonDocument status = ReadPlanJson("VirtualizationActivationStatusV1.json");
        JsonElement root = status.RootElement;
        JsonElement phase42 = root.GetProperty("VmReadScalarDeliveryD2");

        Assert.Equal("ClosedExactProductionCompositionDefaultDisabled",
            phase42.GetProperty("State").GetString());
        Assert.Equal(DecisionId, phase42.GetProperty("DecisionId").GetString());
        Assert.Equal(SubjectCommit,
            phase42.GetProperty("ImplementationSubjectSha").GetString());
        Assert.Equal(SubjectTree,
            phase42.GetProperty("ImplementationSubjectTree").GetString());
        Assert.Equal(
            new[] { "VmcsField.GuestCr0", "VmcsField.GuestCr4" },
            phase42.GetProperty("ExactFields").EnumerateArray()
                .Select(field => field.GetString()).ToArray());
        Assert.Equal("PrivilegedExecutionStateOwnerPolicy",
            phase42.GetProperty("SourceOwner").GetString());
        Assert.Equal("ScalarU64ToDestinationRegister",
            phase42.GetProperty("ResultAbi").GetString());
        Assert.Equal("ArchitecturalRegisterResultOnly",
            phase42.GetProperty("EffectClass").GetString());
        Assert.Equal("None", phase42.GetProperty("CapabilityRequirement").GetString());
        Assert.Equal("DrainOnly", phase42.GetProperty("Migration").GetString());
        Assert.Equal("Disabled", phase42.GetProperty("ActivationDefault").GetString());
        Assert.Equal("CanonicalRetireCoordinatorOnly",
            phase42.GetProperty("ArchitecturalCommit").GetString());
        Assert.Equal("Denied", phase42.GetProperty("BackendExecution").GetString());
        Assert.Equal("Denied", phase42.GetProperty("TrapCompletion").GetString());
        Assert.Equal("Denied", phase42.GetProperty("VmxRetireEffect").GetString());
        Assert.Equal("Denied", phase42.GetProperty("AdjacentFields").GetString());

        Assert.Equal("NoAutomaticActivationExpansion",
            root.GetProperty("NextOpenPool").GetString());
        Assert.Equal("NoneUntilSeparateNeutralCpuTranslationFaultCompletionProducerAuthorization",
            root.GetProperty("NextCandidatePool").GetString());
    }

    [Fact]
    public void ImplementationStatus_ReferencesEarlierSubjectAndLaterEvidenceWithoutSelfSha()
    {
        using JsonDocument status = ReadPlanJson(
            "Phase42VmReadScalarDeliveryImplementationStatusV1.json");
        JsonElement root = status.RootElement;

        Assert.Equal("ClosedByLaterNonSelfReferentialEvidenceRecord",
            root.GetProperty("status").GetString());
        Assert.Equal(DecisionId, root.GetProperty("decision_id").GetString());
        Assert.Equal(SubjectCommit, root.GetProperty("subject_commit_sha").GetString());
        Assert.Equal(SubjectTree, root.GetProperty("subject_tree_sha").GetString());
        Assert.EndsWith(EvidenceFile,
            root.GetProperty("implementation_evidence_record").GetString());
        Assert.Equal(JsonValueKind.Null,
            root.GetProperty("later_evidence_commit_sha").ValueKind);
        Assert.False(root.GetProperty("vmcs_value_source").GetBoolean());
        Assert.False(root.GetProperty("vmwrite").GetBoolean());
        Assert.False(root.GetProperty("backend_execution").GetBoolean());
        Assert.False(root.GetProperty("trap_completion_publication").GetBoolean());
        Assert.False(root.GetProperty("vmx_retire_effect").GetBoolean());
        Assert.False(root.GetProperty("compiler_change").GetBoolean());
    }

    [Fact]
    public void LaterEvidence_IsNonAuthorityAndBindsTheImmutableSubject()
    {
        using JsonDocument evidence = ReadEvidenceJson();
        JsonElement root = evidence.RootElement;
        JsonElement subject = root.GetProperty("implementation_subject");
        JsonElement authority = root.GetProperty("verified_authority_boundaries");
        JsonElement activation = root.GetProperty("activation_and_rollback");

        Assert.Equal("later_non_self_referential_implementation_evidence",
            root.GetProperty("record_kind").GetString());
        Assert.True(root.GetProperty("non_self_referential").GetBoolean());
        Assert.False(root.GetProperty("runtime_authority").GetBoolean());
        Assert.Equal(SubjectCommit, subject.GetProperty("commit_sha").GetString());
        Assert.Equal(SubjectTree, subject.GetProperty("tree_sha").GetString());
        Assert.Equal("clean", subject.GetProperty("status_at_verification").GetString());
        Assert.Equal(0, subject.GetProperty("ignored_csharp_under_close_to_hsl").GetInt32());
        Assert.Equal(0, subject.GetProperty("untracked_csharp_under_close_to_hsl").GetInt32());

        Assert.False(authority.GetProperty("governance_policy_grants_runtime_authority").GetBoolean());
        Assert.False(authority.GetProperty("receipt_grants_source_authority").GetBoolean());
        Assert.False(authority.GetProperty("compatibility_frontend_authority").GetBoolean());
        Assert.False(authority.GetProperty("vmcs_value_or_backing_store_source").GetBoolean());
        Assert.False(authority.GetProperty("direct_architectural_register_write").GetBoolean());
        Assert.True(authority.GetProperty("canonical_prf_rename_writeback_only").GetBoolean());
        Assert.True(authority.GetProperty("canonical_retire_coordinator_commit_only").GetBoolean());
        Assert.False(authority.GetProperty("vmcall_e5_e6_reuse").GetBoolean());
        Assert.False(authority.GetProperty("probe_capability_bit_41_reuse").GetBoolean());

        Assert.Equal("Disabled", activation.GetProperty("default").GetString());
        Assert.Equal("DrainOnly", activation.GetProperty("migration").GetString());
        Assert.True(activation.GetProperty("disable_invalidates_outstanding_receipts").GetBoolean());
        Assert.True(activation.GetProperty("replay_change_invalidates_outstanding_receipts").GetBoolean());
    }

    [Fact]
    public void RecordedSourceHashes_AreWellFormedAndSubjectsContainEverySource()
    {
        using JsonDocument evidence = ReadEvidenceJson();
        JsonElement hashes = evidence.RootElement.GetProperty("source_hashes_sha256");
        string repositoryRoot =
            VmxDocumentationMigrationClaimHygieneTests.FindRepositoryRoot();
        string[] subjectSources =
        [
            "HybridCPU_ISE.Tests/VmxRefactoring/VirtualizationActivationPlanAuditGuardTests.cs",
            "HybridCPU_ISE.Tests/VmxRefactoring/VmxPhase41ScalarDeliveryProductionTests.cs",
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/ExecutionFlow/Materialization/CPU_Core.PipelineExecution.Materialization.cs",
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/MicroOps/Types/MicroOp.IO.cs",
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/Retire/Evidence/CPU_Core.PipelineExecution.VmxRetire.cs",
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/Scheduling/Smt/MicroOpScheduler.ExactVmReadScalarDelivery.cs",
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/Scheduling/Smt/MicroOpScheduler.SMT.cs",
            "HybridCPU_ISE/CloseToHSL/Core/Runtime/Events/VmRead/VmReadScalarDeliveryCanonicalComposition.cs",
            "HybridCPU_ISE/CloseToHSL/Core/Runtime/Governance/Virtualization/VmReadScalarDeliveryAcceptedPolicyResolver.cs",
            "HybridCPU_ISE/CloseToHSL/Core/Virtualization/Compatibility/Frontend/Handlers/VmxCompatibilityAdmissionService.cs",
        ];

        foreach (string relativePath in subjectSources)
        {
            string? expected = hashes.GetProperty(relativePath).GetString();
            byte[] committed = ReadCommittedBlob(
                repositoryRoot,
                SubjectCommit,
                relativePath);
            Assert.Equal(64, expected?.Length);
            Assert.NotEmpty(committed);
        }
    }

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
        using var bytes = new MemoryStream();
        process.StandardOutput.BaseStream.CopyTo(bytes);
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0,
            $"Unable to read committed blob '{commit}:{relativePath}': {error}");
        return bytes.ToArray();
    }

    private static JsonDocument ReadPlanJson(string fileName) => JsonDocument.Parse(
        File.ReadAllText(Path.Combine(
            VmxDocumentationMigrationClaimHygieneTests.FindRepositoryRoot(),
            "HybridCPU_ISE", "docs", "ref2", "VirtualizationActivationPlan",
            fileName)));

    private static JsonDocument ReadEvidenceJson() => JsonDocument.Parse(
        File.ReadAllText(Path.Combine(
            VmxDocumentationMigrationClaimHygieneTests.FindRepositoryRoot(),
            "HybridCPU_ISE", "docs", "ref2", "VirtualizationActivationPlan",
            "evidence", EvidenceFile)));
}
