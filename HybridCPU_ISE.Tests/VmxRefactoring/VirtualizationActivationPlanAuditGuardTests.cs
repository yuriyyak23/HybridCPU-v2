using System;
using System.IO;
using System.Linq;

namespace HybridCPU_ISE.Tests.VmxRefactoring;

public sealed class VirtualizationActivationPlanAuditGuardTests
{
    [Fact]
    public void ActivationPlanDirectory_ContainsRequiredDocsAndStaysReadinessOnly()
    {
        string planRoot = GetPlanRoot();
        string[] requiredFiles =
        [
            "00_virtualization_activation_refactoring_index.md",
            "01_current_state_and_gap_matrix.md",
            "02_global_forbidden_regressions_and_static_gates.md",
            "03_owner_specific_rfc_adr_process.md",
            "04_vmread_projection_completion_and_denial_matrix.md",
            "05_privileged_execution_state_owner_rfc.md",
            "06_neutral_hypercall_backend_owner_rfc.md",
            "07_vmcall_success_path_activation_plan.md",
            "08_trap_completion_route_publication_plan.md",
            "09_retire_publication_activation_plan.md",
            "10_vmwrite_neutral_owner_policy.md",
            "11_nested_child_intent_owner_rfc.md",
            "12_memory_io_iommu_lane_stream_boundary_activation_plan.md",
            "13_securecompute_virtualization_boundary_plan.md",
            "14_compiler_no_emission_to_controlled_emission_gate.md",
            "15_migration_checkpoint_restore_authority_plan.md",
            "16_conformance_negative_positive_test_matrix.md",
            "17_phase_rollout_and_pr_order.md",
            "18_release_gate_for_limited_runtime_virtualization.md",
            "19_open_decision_backlog.md",
        ];

        foreach (string file in requiredFiles)
        {
            Assert.True(File.Exists(Path.Combine(planRoot, file)), file);
        }

        string index = ReadPlan("00_virtualization_activation_refactoring_index.md");
        Assert.Contains("planning corpus only", index);
        Assert.Contains("does not approve activation", index);
        Assert.Contains("AllowedProofOnlyNoExecution", index);
        Assert.Contains("AllowedAdmittedDenied", index);
        Assert.Contains("no positive path may merge without negative tests", index);
    }

    [Fact]
    public void EveryPhaseFile_UsesExternalAuditContractFieldsAndFullOwnerMap()
    {
        foreach (string file in Directory
                     .GetFiles(GetPlanRoot(), "*.md", SearchOption.TopDirectoryOnly)
                     .OrderBy(path => path, StringComparer.Ordinal))
        {
            string text = File.ReadAllText(file);
            Assert.Contains("## 2026-06-11 Audit Contract", text);

            foreach (string requiredField in new[]
                     {
                         "File name:",
                         "Purpose:",
                         "Status:",
                         "Scope:",
                         "No-goals:",
                         "Code anchors:",
                         "Authority owner:",
                         "Required RFC/ADR:",
                         "Acceptance criteria:",
                         "Tests/static scans:",
                         "Risks:",
                         "Next-gate dependency:",
                     })
            {
                Assert.Contains(requiredField, text);
            }

            Assert.Contains(
                "field/operation, owner, value source, capability policy, evidence class, migration class, denial reason",
                text);
        }
    }

    [Fact]
    public void ActivationPlanDocs_RecordStrictSemanticDenials()
    {
        string index = ReadPlan("00_virtualization_activation_refactoring_index.md");

        foreach (string semanticDenial in new[]
                 {
                     "admission != execution",
                     "backend success != completion publication",
                     "completion publication != retire publication",
                     "proof-only != activation approval",
                     "admitted-denied != backend success",
                 })
        {
            Assert.Contains(semanticDenial, index);
        }
    }

    [Fact]
    public void FuturePositivePhaseTitles_DoNotReadAsCurrentActivation()
    {
        string phase07 = ReadPlan("07_vmcall_success_path_activation_plan.md");
        string phase08 = ReadPlan("08_trap_completion_route_publication_plan.md");
        string phase09 = ReadPlan("09_retire_publication_activation_plan.md");
        string phase18 = ReadPlan("18_release_gate_for_limited_runtime_virtualization.md");

        Assert.Contains("# Phase 07 - Future-Gated VMCALL Backend Success Path Plan", phase07);
        Assert.Contains("Status: future-gated", phase07);
        Assert.Contains("This phase does not approve activation.", phase07);
        Assert.Contains("current VMCALL remains admitted-denied", phase07);

        Assert.Contains("# Phase 08 - Future-Gated Trap Completion Route Publication Plan", phase08);
        Assert.Contains("Status: future-gated completion publication gate", phase08);
        Assert.Contains("backend success != completion publication", phase08);
        Assert.Contains("This phase does not approve current completion publication.", phase08);

        Assert.Contains("# Phase 09 - Future-Gated Retire Publication Plan", phase09);
        Assert.Contains("Status: future retire gate", phase09);
        Assert.Contains("completion publication != retire publication", phase09);

        Assert.Contains("No activation is approved by this document", phase18);
        Assert.Contains("any missing arrow means", phase18);
    }

    [Fact]
    public void Phase06DraftContainsConcreteLeafAbiAndKeepsApprovalClosed()
    {
        string phase06 = ReadPlan("06_neutral_hypercall_backend_owner_rfc.md");

        Assert.Contains("Candidate leaf ABI:", phase06);
        Assert.Contains("Operation: `VMCALL`.", phase06);
        Assert.Contains("Operand form: `HypercallLeafAndDescriptor`.", phase06);
        Assert.Contains("Default result: `NoPayload`.", phase06);
        Assert.Contains("Identifier: `RFC-HV-VMCALL-NO-STATE-OWNER-0001`.", phase06);
        Assert.Contains("Decision state: draft only. Not accepted.", phase06);
        Assert.Contains("Current denied owner map:", phase06);
        Assert.Contains("exact numeric leaf is not selected", phase06);
        Assert.Contains("NeutralHypercallBackendOwnerDescriptor", phase06);
        Assert.Contains("DeniedNeutralBackendOwnerRfcAdr", phase06);
        Assert.Contains("draft-only owner descriptor skeleton", phase06);
        Assert.Contains("## Phase 06B Closure - Blocked By Draft RFC", phase06);
        Assert.Contains("Phase 06B is closed as blocked/future-gated", phase06);
        Assert.Contains("`NeutralHypercallBackendOwnerRfcAdrState` has no accepted state", phase06);
        Assert.Contains("no code path may set `BackendExecutionAuthorized: true`", phase06);
        Assert.Contains("`HypercallBackendAdmissionDecision` has no allowed backend execution decision", phase06);
        Assert.Contains("PR-06A", phase06);
        Assert.Contains("PR-06B", phase06);
        Assert.Contains("Semantics ladder:", phase06);
        Assert.Contains("backend success != completion publication", phase06);
        Assert.Contains("completion publication != retire publication", phase06);
        Assert.Contains("proof-only != activation approval", phase06);
        Assert.DoesNotContain("activation approved", phase06, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OwnerProcessAndVmreadDocs_RequireExplicitOwnerMapsAndNoProofOnlyMisread()
    {
        string phase03 = ReadPlan("03_owner_specific_rfc_adr_process.md");
        string phase04 = ReadPlan("04_vmread_projection_completion_and_denial_matrix.md");

        Assert.Contains(
            "explicit owner map with field or operation, owner, value source, capability policy, evidence class, migration class, denial reason, and current result",
            phase03);
        Assert.Contains("AllowedProofOnlyNoExecution", phase03);
        Assert.Contains("AllowedAdmittedDenied", phase03);
        Assert.Contains("What is the exact owner map for the chosen path?", phase03);

        Assert.Contains("schema membership is not field availability", phase04);
        Assert.Contains(
            "columns: field, owner, value source, access policy, evidence class, migration policy, current result, denial reason, and test anchor",
            phase04);
        Assert.Contains("AllowedProofOnlyNoExecution", phase04);
    }

    [Fact]
    public void CompletionAndRetireDocs_KeepPublicationAndRetireSeparate()
    {
        string phase07 = ReadPlan("07_vmcall_success_path_activation_plan.md");
        string phase08 = ReadPlan("08_trap_completion_route_publication_plan.md");
        string phase09 = ReadPlan("09_retire_publication_activation_plan.md");
        string phase18 = ReadPlan("18_release_gate_for_limited_runtime_virtualization.md");

        Assert.Contains("backend success is not completion publication", phase07);
        Assert.Contains("completion publication is not retire publication", phase08);
        Assert.Contains("RuntimeOwnedCompletionPublication", phase08);
        Assert.Contains("exists as the split completion-only route descriptor", phase08);
        Assert.Contains("RuntimeOwnedPublication` remains the coupled completion+retire descriptor", phase08);
        Assert.Contains("keeps route authorization for completion separate from retire", phase08);
        Assert.Contains("no direct `CompletionRecord` construction in VMX admission/frontend handler", phase08);
        Assert.Contains("only after `publicationFence.CompletionPublicationAllowed`", phase08);
        Assert.Contains("Completion is not retire", phase09);
        Assert.Contains("proof-only and admitted-denied semantics remain evidence-only and are not activation approval", phase18);
        Assert.Contains("AllowedProofOnlyNoExecution", phase18);
        Assert.Contains("AllowedAdmittedDenied", phase18);
    }

    [Fact]
    public void Phase07ConsumesPhase06RfcOnlyAfterAcceptance()
    {
        string phase07 = ReadPlan("07_vmcall_success_path_activation_plan.md");

        Assert.Contains("## RFC/ADR Consumption Contract", phase07);
        Assert.Contains("RFC-HV-VMCALL-NO-STATE-OWNER-0001", phase07);
        Assert.Contains("only after the RFC/ADR state changes from draft to accepted by neutral runtime owners", phase07);
        Assert.Contains("draft-only runtime skeleton", phase07);
        Assert.Contains("exact leaf ID", phase07);
        Assert.Contains("neutral backend owner service", phase07);
        Assert.Contains("backend executor result type", phase07);
        Assert.Contains("capability policy", phase07);
        Assert.Contains("evidence class", phase07);
        Assert.Contains("migration class", phase07);
        Assert.Contains("denial reasons", phase07);
        Assert.Contains("adjacent denials", phase07);
        Assert.Contains("## Phase 06B Dependency Closure", phase07);
        Assert.Contains("Phase 06B is closed as blocked/future-gated", phase07);
        Assert.Contains("no `HypercallBackendAdmissionDecision.Allowed`", phase07);
        Assert.Contains("no `BackendExecutionAuthorized: true`", phase07);
        Assert.Contains("no `TrapCompletionRouteDescriptor.RuntimeOwnedPublication` in VMX frontend", phase07);
        Assert.Contains("draft RFC only", phase07);
        Assert.Contains("must remain denied", phase07);
        Assert.Contains("backend success is not completion publication", phase07);
        Assert.Contains("completion publication is not retire publication", phase07);
    }

    [Fact]
    public void BacklogDoc_RecordsConfirmedClosuresAndKeepsRemainingItemsFutureGated()
    {
        string backlog = ReadPlan("19_open_decision_backlog.md");

        Assert.Contains("## Confirmed Closures From Current Audit", backlog);
        Assert.Contains("ISE-VMX-GUARD-01", backlog);
        Assert.Contains("ISE-VMX-GUARD-03", backlog);
        Assert.Contains("ISE-HV-RFC-01", backlog);
        Assert.Contains("ISE-HV-OWNERMAP-02", backlog);
        Assert.Contains("ISE-HV-ADMISSION-03", backlog);
        Assert.Contains("ISE-COMP-ROUTE-01", backlog);
        Assert.Contains("ISE-COMP-FENCE-02", backlog);
        Assert.Contains("draft-only denied skeleton", backlog);
        Assert.Contains("future-gated route scaffolding", backlog);
        Assert.Contains("current fence denies completion-record publication", backlog);
        Assert.Contains("docs level", backlog);
        Assert.Contains("No production activation path is approved", backlog);
        Assert.Contains("future-gated", backlog);
        Assert.Contains("Lane6/Lane7/Stream passthrough", backlog);
        Assert.Contains("Compiler controlled emission", backlog);
    }

    [Fact]
    public void ReleaseGateDocs_DoNotClaimFeatureCompleteness()
    {
        string docs = string.Join(
            Environment.NewLine,
            Directory
                .GetFiles(GetPlanRoot(), "*.md", SearchOption.TopDirectoryOnly)
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(File.ReadAllText));

        foreach (string forbidden in new[]
                 {
                    "runtime activation approved",
                    "activation approved",
                    "production ready",
                    "all VMX supported",
                    "SecureCompute supported via VMX",
                     "VMWRITE supported",
                     "VMCALL backend success allowed",
                     "completion publication allowed",
                     "retire publication allowed",
                 })
        {
            Assert.DoesNotContain(forbidden, docs, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string ReadPlan(string fileName) =>
        File.ReadAllText(Path.Combine(GetPlanRoot(), fileName));

    private static string GetPlanRoot() =>
        Path.Combine(
            VmxDocumentationMigrationClaimHygieneTests.FindRepositoryRoot(),
            "HybridCPU_ISE",
            "docs",
            "ref2",
            "VirtualizationActivationPlan");
}
