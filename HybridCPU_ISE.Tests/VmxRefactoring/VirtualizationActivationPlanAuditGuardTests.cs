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
            "20_hv_rfc_intake_packet.md",
            "21_hv_owner_handoff_packet.md",
            "22_hv_no_response_fallback.md",
            "23_hv_replacement_intake_decision.md",
            "24_hv_late_owner_response_audit.md",
            "25_hv_wait_state_monitor.md",
            "26_hv_owner_artifact_recheck.md",
            "27_hv_wait_or_reselect_checkpoint.md",
            "28_hv_denied_chain_refresh.md",
            "29_hv_owner_response_watchdog.md",
            "30_hv_final_wait_baseline.md",
            "31_hv_post_baseline_artifact_sentry.md",
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
    public void DraftContainsConcreteLeafAbiAndKeepsApprovalClosed()
    {
        string phase06 = ReadPlan("06_neutral_hypercall_backend_owner_rfc.md");

        Assert.Contains("Candidate leaf ABI:", phase06);
        Assert.Contains("Operation: `VMCALL`.", phase06);
        Assert.Contains("Operand form: `HypercallLeafAndDescriptor`.", phase06);
        Assert.Contains("Default result: `NoPayload`.", phase06);
        Assert.Contains("## ISE-HV-LEAF-DECISION-04 - Verified ABI And Leaf Inventory", phase06);
        Assert.Contains("## ISE-HV-LEAF-DECISION-04 - Candidate Decision Matrix", phase06);
        Assert.Contains("No leaf is selected or reserved by this inventory", phase06);
        Assert.Contains("`259` is an instruction opcode, not a hypercall leaf ID", phase06);
        Assert.Contains("no production `VmxHypercallLeaf` enum", phase06);
        Assert.Contains("exact numeric leaf ID is `не доказано`, `future-gated`, and `требует owner-specific RFC/ADR`", phase06);
        Assert.Contains("`VmxFunctionLeaf.CapabilityQuery == 1`", phase06);
        Assert.Contains("SecureCompute tests use `0x10` as a local allowlist fixture", phase06);
        Assert.Contains("Test request values `HypercallLeafRegister: 2`", phase06);
        Assert.Contains("No numeric ID is selected, reserved, or recommended by this packet", phase06);
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
        string phase05 = ReadPlan("05_privileged_execution_state_owner_rfc.md");

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
        Assert.Contains("implemented guarded owner and read-only projection closure", phase05);
        Assert.Contains("AllowedOwnerMaterializedProjectionClosed", phase05);
        Assert.Contains("AllowedReadOnlyProjection", phase05);
        Assert.Contains("RevalidatedAfterRestore", phase05);
        Assert.Contains("Owner acceptance alone remains projection-closed", phase05);
        Assert.Contains("backend success, mutation, completion publication, and retire publication remain false", phase05);
    }

    [Fact]
    public void SecureComputeAuditClarifications_DoNotBecomeVirtualizationActivation()
    {
        string phase06 = ReadPlan("06_neutral_hypercall_backend_owner_rfc.md");
        string phase07 = ReadPlan("07_vmcall_success_path_activation_plan.md");
        string phase13 = ReadPlan("13_securecompute_virtualization_boundary_plan.md");
        string phase15 = ReadPlan("15_migration_checkpoint_restore_authority_plan.md");
        string phase16 = ReadPlan("16_conformance_negative_positive_test_matrix.md");
        string phase18 = ReadPlan("18_release_gate_for_limited_runtime_virtualization.md");

        Assert.Contains("candidate wording such as \"no-state, domain-local\" is not an exact leaf ID", phase06);
        Assert.Contains("AllowedSecureOperation", phase06);
        Assert.Contains("AllowedProofOnlyNoExecution", phase06);
        Assert.Contains("candidate class, not an exact leaf ID", phase07);
        Assert.Contains("AllowedSecureOperation", phase07);
        Assert.Contains("admission only; it is not secure backend execution", phase13);
        Assert.Contains("DeniedBackendExecutionClosed", phase13);
        Assert.Contains("policy structure, not proof that a production publication path executed", phase13);
        Assert.Contains("field-specific read-only compatibility contract", phase13);
        Assert.Contains("VMREAD output is not serialized authority", phase15);
        Assert.Contains("exact VMCALL leaf remains unresolved", phase16);
        Assert.Contains("do not satisfy any backend-execution arrow", phase18);
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
        Assert.Contains("can publish a completion record while retire remains denied", phase08);
        Assert.Contains("CompletionPublishedRetireDenied", phase08);
        Assert.Contains("CompletionPublicationAllowed == true", phase08);
        Assert.Contains("RetirePublicationAllowed == false", phase08);
        Assert.Contains("no direct `CompletionRecord` construction in VMX admission/frontend handler", phase08);
        Assert.Contains("only after `publicationFence.CompletionPublicationAllowed`", phase08);
        Assert.Contains("## ISE-COMP-ROUTE-01 / ISE-COMP-FENCE-02 - Closure Record", phase08);
        Assert.Contains("closed `FUTURE-GATED SCAFFOLDING / COMPLETION-ONLY-ROUTE-SEPARATED / RETIRE-DENIED / NO-VMX-FRONTEND-PUBLICATION / NO-PRODUCTION-CHANGE`", phase08);
        Assert.Contains("does not accept an RFC/ADR, authorize backend execution, publish completion from the VMX frontend", phase08);
        Assert.Contains("Handler-side completion construction remains forbidden", phase08);
        Assert.Contains("Completion is not retire", phase09);
        Assert.Contains("## ISE-HV-RETIRE-PUBLICATION-GATE-09 - Closure Record", phase09);
        Assert.Contains("closed `FUTURE-GATED RETIRE GATE / COMPLETION-NOT-RETIRE / NO-RETIRE-PUBLICATION / NO-PRODUCTION-CHANGE`", phase09);
        Assert.Contains("retire publication remains a separate future-gated owner rule", phase09);
        Assert.Contains("This closure does not accept an RFC/ADR, authorize backend execution, publish completion, publish retire", phase09);
        Assert.Contains("Completion publication is not retire publication", phase09);
        Assert.Contains("proof-only and admitted-denied semantics remain evidence-only and are not activation approval", phase18);
        Assert.Contains("AllowedProofOnlyNoExecution", phase18);
        Assert.Contains("AllowedAdmittedDenied", phase18);
    }

    [Fact]
    public void ConsumesPhase06RfcOnlyAfterAcceptance()
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
        Assert.Contains("Exact-leaf acceptance rules:", phase07);
        Assert.Contains("only one exact numeric VMCALL leaf ID", phase07);
        Assert.Contains("A leaf class, operation class, trap class, compatibility opcode, register selector, exit reason, owner ID, test fixture", phase07);
        Assert.Contains("`VMCALL == 259`, `VmExitReason.VmCall == 18`, VMFUNC leaves `1/7/8`", phase07);
        Assert.Contains("Phase 07 cannot infer the runtime leaf value or descriptor value", phase07);
        Assert.Contains("AllowedSecureOperation` is admission only and is not backend success", phase07);
        Assert.Contains("AllowedProofOnlyNoExecution` is proof-only and is not execution", phase07);
        Assert.Contains("decision-ready but non-consumable", phase07);
    }

    [Fact]
    public void HypercallRfcIntakePacket20_IsOwnerFacingOnlyAndDoesNotAllocateLeafOrAuthorizeBackend()
    {
        string index = ReadPlan("00_virtualization_activation_refactoring_index.md");
        string backlog = ReadPlan("19_open_decision_backlog.md");
        string phase20 = ReadPlan("20_hv_rfc_intake_packet.md");

        Assert.Contains("20_hv_rfc_intake_packet.md", index);
        Assert.Contains("Owner-facing RFC/ADR intake packet", index);
        Assert.Contains("# Phase 20 - Hypercall Owner RFC Intake Packet", phase20);
        Assert.Contains("Status: owner-facing RFC/ADR intake packet only", phase20);
        Assert.Contains("File name: `20_hv_rfc_intake_packet.md`", phase20);
        Assert.Contains("Purpose: prepare the owner-facing intake packet", phase20);
        Assert.Contains("Status: intake packet only; no RFC/ADR is accepted", phase20);
        Assert.Contains("no exact VMCALL leaf is selected, allocated, reserved, or recommended by this document", phase20);
        Assert.Contains("Authority owner: external neutral runtime owner for the exact hypercall backend path", phase20);
        Assert.Contains("field/operation, owner, value source, capability policy, evidence class, migration class, denial reason", phase20);
        Assert.Contains("## ISE-HV-RFC-INTAKE-PACKET-20 - Closure Record", phase20);
        Assert.Contains("Closure date: 2026-06-18.", phase20);
        Assert.Contains("closed `OWNER-FACING PACKET PREPARED / DRAFT-ONLY / OWNER-ACCEPTANCE-REQUIRED / NO-PRODUCTION-CHANGE`", phase20);
        Assert.Contains("Prepared packet: `RFC-HV-VMCALL-NO-STATE-OWNER-0001`", phase20);
        Assert.Contains("It does not accept the RFC/ADR", phase20);
        Assert.Contains("allocate an exact numeric VMCALL leaf", phase20);
        Assert.Contains("approve backend admission", phase20);
        Assert.Contains("authorize backend execution", phase20);
        Assert.Contains("publish completion", phase20);
        Assert.Contains("publish retire", phase20);
        Assert.Contains("replace `MissingNeutralOwner`", phase20);
        Assert.Contains("create a backend executor", phase20);
        Assert.Contains("construct a frontend completion record", phase20);

        Assert.Contains("Neutral runtime owners are asked to return exactly one of", phase20);
        Assert.Contains("| accept | exact numeric VMCALL leaf, argument ABI, result ABI", phase20);
        Assert.Contains("may reopen Phase 06B implementation review only", phase20);
        Assert.Contains("| reject | rejection reason", phase20);
        Assert.Contains("| amend | exact missing fields", phase20);
        Assert.Contains("Silence, lack of rejection, repository readiness, green tests, static scans", phase20);
        Assert.Contains("are not owner acceptance", phase20);

        Assert.Contains("`VMCALL` opcode is `259`", phase20);
        Assert.Contains("instruction opcode, not hypercall leaf ID", phase20);
        Assert.Contains("VMFUNC leaves", phase20);
        Assert.Contains("SecureCompute fixture", phase20);
        Assert.Contains("operation class | `NoStateNoPayloadDomainLocal`", phase20);
        Assert.Contains("leaf selection class | `CandidateOnlyNoNumericLeaf`", phase20);
        Assert.Contains("Owner Map Template", phase20);
        Assert.Contains("exact VMCALL leaf | owner-required; not proven", phase20);
        Assert.Contains("secure-domain behavior", phase20);
        Assert.Contains("must be non-secure or secure-no-effect", phase20);
        Assert.Contains("AllowedSecureOperation` and `AllowedProofOnlyNoExecution` remain non-execution evidence", phase20);
        Assert.Contains("Handoff result is not acceptance", phase20);
        Assert.Contains("production VMCALL remains `MissingNeutralOwner` and `ProjectionOnlyDenied`", phase20);

        Assert.Contains("ISE-HV-RFC-INTAKE-PACKET-20", backlog);
        Assert.Contains("The packet for `RFC-HV-VMCALL-NO-STATE-OWNER-0001` is ready for neutral runtime owner accept/reject/amend review", backlog);
        Assert.Contains("it grants no owner acceptance, exact leaf allocation, backend admission allow, backend execution", backlog);
        Assert.Contains("Phase 20 prepared owner-facing packet", backlog);
        Assert.Contains("Phase 21 recorded handoff and found no attributable owner response", backlog);
        Assert.Contains("exact numeric leaf not proven; RFC remains draft", backlog);
    }

    [Fact]
    public void HypercallOwnerHandoffPacket21_RecordsNoResponseWithoutAcceptanceOrRuntimePermission()
    {
        string index = ReadPlan("00_virtualization_activation_refactoring_index.md");
        string backlog = ReadPlan("19_open_decision_backlog.md");
        string phase21 = ReadPlan("21_hv_owner_handoff_packet.md");

        Assert.Contains("21_hv_owner_handoff_packet.md", index);
        Assert.Contains("Handoff and response-audit record", index);
        Assert.Contains("# Phase 21 - Hypercall Owner Handoff Packet", phase21);
        Assert.Contains("Status: handoff and response-audit record only", phase21);
        Assert.Contains("File name: `21_hv_owner_handoff_packet.md`", phase21);
        Assert.Contains("Purpose: record handoff of the Phase 20 owner-facing packet", phase21);
        Assert.Contains("Status: process closure only; no attributable neutral-runtime-owner accept/reject/amend artifact", phase21);
        Assert.Contains("Authority owner: external neutral runtime owner", phase21);
        Assert.Contains("field/operation, owner, value source, capability policy, evidence class, migration class, denial reason", phase21);
        Assert.Contains("## ISE-HV-OWNER-HANDOFF-PACKET-21 - Closure Record", phase21);
        Assert.Contains("Closure date: 2026-06-18.", phase21);
        Assert.Contains("closed `HANDOFF-RECORDED / RESPONSE-AUDITED / NO-RESPONSE / EXTERNAL-BLOCKED`", phase21);
        Assert.Contains("Phase 20 owner-facing packet `20_hv_rfc_intake_packet.md`", phase21);
        Assert.Contains("This closure records that the packet is ready for and has been handed to the external neutral runtime owner gate", phase21);
        Assert.Contains("It does not record acceptance, rejection, amendment, exact-leaf allocation", phase21);
        Assert.Contains("backend admission allow", phase21);
        Assert.Contains("backend execution authorization", phase21);
        Assert.Contains("completion publication authorization", phase21);
        Assert.Contains("retire publication authorization", phase21);
        Assert.Contains("VMX frontend wiring", phase21);

        Assert.Contains("Repository response audit result: no attributable external neutral-runtime-owner artifact was found", phase21);
        Assert.Contains("| accepted owner-specific RFC/ADR | absent | no implementation permission |", phase21);
        Assert.Contains("| exact numeric VMCALL leaf | absent | all numeric VMCALL leaves remain denied |", phase21);
        Assert.Contains("| neutral backend owner service | absent | `MissingNeutralOwner` remains production behavior |", phase21);
        Assert.Contains("Handoff completion is not owner acceptance", phase21);
        Assert.Contains("Response audit completion is not owner acceptance", phase21);
        Assert.Contains("No response is not acceptance", phase21);
        Assert.Contains("No rejection is not acceptance", phase21);
        Assert.Contains("Elapsed time is not acceptance", phase21);
        Assert.Contains("Repository-local readiness is not acceptance", phase21);
        Assert.Contains("SecureCompute `AllowedSecureOperation` is admission-only", phase21);
        Assert.Contains("Backend success, if later authorized by a neutral owner, is still not completion publication", phase21);
        Assert.Contains("Completion publication, if later authorized by a completion owner, is still not retire publication", phase21);

        Assert.Contains("Phase 06B remains closed/blocked", phase21);
        Assert.Contains("Phase 07 remains closed/blocked", phase21);
        Assert.Contains("production VMCALL must continue to use `HypercallBackendAdmissionRequest.MissingNeutralOwner(...)`", phase21);
        Assert.Contains("`HypercallBackendAdmissionDecision.Allowed` must remain absent", phase21);
        Assert.Contains("no code path may set `BackendExecutionAuthorized: true`", phase21);
        Assert.Contains("VMX frontend must not use `TrapCompletionRouteDescriptor.RuntimeOwnedPublication`", phase21);
        Assert.Contains("VMX frontend/admission handlers must not construct `CompletionRecord`", phase21);
        Assert.Contains("Acceptance, if received later, reopens only implementation review for the exact accepted path", phase21);

        Assert.Contains("ISE-HV-OWNER-HANDOFF-PACKET-21", backlog);
        Assert.Contains("response audit found no attributable external neutral-runtime-owner accept/reject/amend artifact", backlog);
        Assert.Contains("handoff completion and response audit completion grant no owner acceptance, exact leaf allocation, backend admission allow, backend execution", backlog);
        Assert.Contains("Phase 21 recorded handoff and found no attributable owner response", backlog);
        Assert.Contains("exact numeric leaf not proven; RFC remains draft", backlog);
    }

    [Fact]
    public void HypercallNoResponseFallback22_KeepsVmcallDeniedAndReplacementSelectionProcessOnly()
    {
        string index = ReadPlan("00_virtualization_activation_refactoring_index.md");
        string backlog = ReadPlan("19_open_decision_backlog.md");
        string phase22 = ReadPlan("22_hv_no_response_fallback.md");

        Assert.Contains("22_hv_no_response_fallback.md", index);
        Assert.Contains("No-response fallback", index);
        Assert.Contains("# Phase 22 - Hypercall No-Response Fallback", phase22);
        Assert.Contains("Status: no-response fallback and rollback record only", phase22);
        Assert.Contains("File name: `22_hv_no_response_fallback.md`", phase22);
        Assert.Contains("Purpose: define the fallback after Phase 21 found no attributable neutral-runtime-owner response", phase22);
        Assert.Contains("Status: process/readiness closure only; VMCALL backend path remains denied/future-gated", phase22);
        Assert.Contains("Authority owner: external neutral runtime owner for any exact positive path", phase22);
        Assert.Contains("field/operation, owner, value source, capability policy, evidence class, migration class, denial reason", phase22);
        Assert.Contains("## ISE-HV-NO-RESPONSE-ROLLBACK-22 - Closure Record", phase22);
        Assert.Contains("Closure date: 2026-06-18.", phase22);
        Assert.Contains("closed `NO-RESPONSE FALLBACK / WAIT-OR-RESELECT / VMCALL-DENIED-FUTURE-GATED / NO-PRODUCTION-CHANGE`", phase22);
        Assert.Contains("default action: wait for an attributable external neutral-runtime-owner response", phase22);
        Assert.Contains("optional action: choose a replacement owner-specific intake from Phase 19 backlog", phase22);
        Assert.Contains("prohibited action: treat no-response, fallback, replacement discussion, or backlog priority as implementation permission", phase22);
        Assert.Contains("This closure does not accept an RFC/ADR", phase22);
        Assert.Contains("allocate an exact numeric VMCALL leaf", phase22);
        Assert.Contains("approve backend admission", phase22);
        Assert.Contains("authorize backend execution", phase22);
        Assert.Contains("publish completion", phase22);
        Assert.Contains("publish retire", phase22);
        Assert.Contains("replace `MissingNeutralOwner`", phase22);
        Assert.Contains("create a backend executor", phase22);
        Assert.Contains("construct a frontend completion record", phase22);

        Assert.Contains("| wait for VMCALL owner response | selected default", phase22);
        Assert.Contains("| choose replacement backlog intake | allowed only as future process-only selection", phase22);
        Assert.Contains("| implement VMCALL backend despite no response | prohibited", phase22);
        Assert.Contains("| infer acceptance from silence or elapsed time | prohibited", phase22);
        Assert.Contains("| use Phase 20/21 documents as authority | prohibited", phase22);
        Assert.Contains("Replacement selection must not reuse VMCALL packet evidence as authority", phase22);
        Assert.Contains("Phase 06B remains blocked/future-gated", phase22);
        Assert.Contains("Phase 07 remains blocked/future-gated", phase22);
        Assert.Contains("production VMCALL remains `HypercallBackendAdmissionRequest.MissingNeutralOwner(...)`", phase22);
        Assert.Contains("route remains `ProjectionOnlyDenied`", phase22);
        Assert.Contains("No-response fallback is not owner acceptance", phase22);
        Assert.Contains("Rollback-to-wait is not owner acceptance", phase22);
        Assert.Contains("Replacement intake discussion is not owner acceptance", phase22);
        Assert.Contains("Backlog reprioritization is not implementation permission", phase22);
        Assert.Contains("Choosing a replacement intake does not open that replacement path", phase22);

        Assert.Contains("ISE-HV-NO-RESPONSE-ROLLBACK-22", backlog);
        Assert.Contains("Default fallback is to wait for an attributable VMCALL owner response", backlog);
        Assert.Contains("a future replacement intake may be selected only as a process-only backlog action", backlog);
        Assert.Contains("No-response fallback, rollback-to-wait, re-audit, replacement discussion, or backlog reprioritization grants no owner acceptance", backlog);
        Assert.Contains("Phase 22 fallback is wait-or-reselect with VMCALL denied/future-gated", backlog);
    }

    [Fact]
    public void HypercallReplacementIntakeDecision23_SelectsWaitWithoutReplacementOrRuntimePermission()
    {
        string index = ReadPlan("00_virtualization_activation_refactoring_index.md");
        string backlog = ReadPlan("19_open_decision_backlog.md");
        string phase23 = ReadPlan("23_hv_replacement_intake_decision.md");

        Assert.Contains("23_hv_replacement_intake_decision.md", index);
        Assert.Contains("Process-only decision to keep waiting", index);
        Assert.Contains("# Phase 23 - Hypercall Replacement Intake Decision", phase23);
        Assert.Contains("Status: process-only replacement-intake decision", phase23);
        Assert.Contains("File name: `23_hv_replacement_intake_decision.md`", phase23);
        Assert.Contains("Purpose: choose whether to keep waiting for the VMCALL owner response or select one replacement owner-specific intake", phase23);
        Assert.Contains("Status: process/readiness decision only; selected decision is to continue waiting", phase23);
        Assert.Contains("Authority owner: external neutral runtime owner for any exact positive path", phase23);
        Assert.Contains("field/operation, owner, value source, capability policy, evidence class, migration class, denial reason", phase23);
        Assert.Contains("## ISE-HV-REPLACEMENT-INTAKE-DECISION-23 - Closure Record", phase23);
        Assert.Contains("Closure date: 2026-06-18.", phase23);
        Assert.Contains("closed `PROCESS-ONLY WAIT DECISION / NO-REPLACEMENT-SELECTED / VMCALL-DENIED-FUTURE-GATED / NO-PRODUCTION-CHANGE`", phase23);
        Assert.Contains("Decision: continue waiting for an attributable external neutral-runtime-owner response", phase23);
        Assert.Contains("Replacement intake selected: none", phase23);
        Assert.Contains("This closure does not accept an RFC/ADR", phase23);
        Assert.Contains("allocate an exact numeric VMCALL leaf", phase23);
        Assert.Contains("approve backend admission", phase23);
        Assert.Contains("authorize backend execution", phase23);
        Assert.Contains("publish completion", phase23);
        Assert.Contains("publish retire", phase23);
        Assert.Contains("replace `MissingNeutralOwner`", phase23);
        Assert.Contains("create a backend executor", phase23);
        Assert.Contains("construct a frontend completion record", phase23);

        Assert.Contains("| continue waiting for VMCALL owner response | selected", phase23);
        Assert.Contains("| choose `GuestCr0`/`GuestCr4` projection widening | not selected", phase23);
        Assert.Contains("| choose VMWRITE | not selected", phase23);
        Assert.Contains("| choose nested child intent | not selected", phase23);
        Assert.Contains("| choose SecureCompute VMX visibility/backend | not selected", phase23);
        Assert.Contains("| choose Lane6/Lane7/Stream passthrough | not selected", phase23);
        Assert.Contains("| choose compiler controlled emission | not selected", phase23);
        Assert.Contains("| choose migration payloads for active path | not selected", phase23);
        Assert.Contains("| choose release claim wording | not selected", phase23);
        Assert.Contains("Choosing to wait is not owner acceptance", phase23);
        Assert.Contains("Not selecting a replacement is not owner acceptance", phase23);
        Assert.Contains("Replacement candidate ranking is not implementation permission", phase23);
        Assert.Contains("Waiting does not allocate an exact VMCALL leaf", phase23);
        Assert.Contains("Waiting does not reopen Phase 06B or Phase 07", phase23);
        Assert.Contains("A future replacement intake selection would still be process-only", phase23);
        Assert.Contains("production VMCALL remains `HypercallBackendAdmissionRequest.MissingNeutralOwner(...)`", phase23);
        Assert.Contains("route remains `ProjectionOnlyDenied`", phase23);

        Assert.Contains("ISE-HV-REPLACEMENT-INTAKE-DECISION-23", backlog);
        Assert.Contains("The process decision is to continue waiting for an attributable VMCALL owner response", backlog);
        Assert.Contains("no replacement intake is selected", backlog);
        Assert.Contains("Waiting, not selecting a replacement, candidate ranking, clean scans, green tests, or backlog priority grants no owner acceptance", backlog);
        Assert.Contains("Phase 23 selected wait/no replacement", backlog);
    }

    [Fact]
    public void HypercallLateOwnerResponseAudit24_FindsNoOwnerArtifactAndKeepsWaitDeniedFutureGated()
    {
        string index = ReadPlan("00_virtualization_activation_refactoring_index.md");
        string backlog = ReadPlan("19_open_decision_backlog.md");
        string phase24 = ReadPlan("24_hv_late_owner_response_audit.md");

        Assert.Contains("24_hv_late_owner_response_audit.md", index);
        Assert.Contains("Periodic late owner-response audit", index);
        Assert.Contains("# Phase 24 - Hypercall Late Owner Response Audit", phase24);
        Assert.Contains("Status: periodic late-owner-response audit only", phase24);
        Assert.Contains("File name: `24_hv_late_owner_response_audit.md`", phase24);
        Assert.Contains("Purpose: periodically audit whether an attributable neutral-runtime-owner response appeared", phase24);
        Assert.Contains("Status: response audit closure only; no attributable owner accept/reject/amend artifact", phase24);
        Assert.Contains("Authority owner: external neutral runtime owner for the exact VMCALL path", phase24);
        Assert.Contains("field/operation, owner, value source, capability policy, evidence class, migration class, denial reason", phase24);
        Assert.Contains("## ISE-HV-LATE-OWNER-RESPONSE-AUDIT-24 - Closure Record", phase24);
        Assert.Contains("Closure date: 2026-06-18.", phase24);
        Assert.Contains("closed `LATE-RESPONSE-AUDITED / NO-ATTRIBUTABLE-OWNER-RESPONSE / WAIT-DENIED-FUTURE-GATED / NO-PRODUCTION-CHANGE`", phase24);
        Assert.Contains("Audit result: no attributable external neutral-runtime-owner artifact was found", phase24);
        Assert.Contains("This closure does not accept an RFC/ADR", phase24);
        Assert.Contains("allocate an exact numeric VMCALL leaf", phase24);
        Assert.Contains("approve backend admission", phase24);
        Assert.Contains("authorize backend execution", phase24);
        Assert.Contains("publish completion", phase24);
        Assert.Contains("publish retire", phase24);
        Assert.Contains("replace `MissingNeutralOwner`", phase24);
        Assert.Contains("create a backend executor", phase24);
        Assert.Contains("construct a frontend completion record", phase24);

        Assert.Contains("| accepted exact-leaf RFC/ADR | absent | Phase 06B/Phase 07 remain blocked |", phase24);
        Assert.Contains("| rejected RFC/ADR | absent | draft remains unresolved |", phase24);
        Assert.Contains("| amended packet | absent | Phase 20 packet remains pending |", phase24);
        Assert.Contains("| exact numeric VMCALL leaf | absent | all numeric VMCALL leaves remain denied |", phase24);
        Assert.Contains("| accepted neutral owner service | absent | `MissingNeutralOwner` remains production behavior |", phase24);
        Assert.Contains("Periodic audit completion is not owner acceptance", phase24);
        Assert.Contains("No late response is not owner acceptance", phase24);
        Assert.Contains("Elapsed time is not owner acceptance", phase24);
        Assert.Contains("Clean scans and green tests are not owner acceptance", phase24);
        Assert.Contains("Phase 20 packet, Phase 21 handoff, Phase 22 fallback, Phase 23 wait decision, and this Phase 24 audit are not owner acceptance", phase24);
        Assert.Contains("production VMCALL remains `HypercallBackendAdmissionRequest.MissingNeutralOwner(...)`", phase24);
        Assert.Contains("route remains `ProjectionOnlyDenied`", phase24);
        Assert.Contains("replacement intake remains not selected", phase24);

        Assert.Contains("ISE-HV-LATE-OWNER-RESPONSE-AUDIT-24", backlog);
        Assert.Contains("The periodic audit found no attributable external neutral-runtime-owner accept/reject/amend artifact", backlog);
        Assert.Contains("audit completion, no-response, clean scans, green tests, and elapsed time grant no owner acceptance", backlog);
        Assert.Contains("Phase 24 late audit found no attributable owner response", backlog);
    }

    [Fact]
    public void HypercallWaitStateMonitor25_IsCadenceOnlyAndKeepsVmCallDeniedFutureGated()
    {
        string index = ReadPlan("00_virtualization_activation_refactoring_index.md");
        string backlog = ReadPlan("19_open_decision_backlog.md");
        string phase25 = ReadPlan("25_hv_wait_state_monitor.md");

        Assert.Contains("25_hv_wait_state_monitor.md", index);
        Assert.Contains("Monitor-only cadence for attributable VMCALL owner response", index);
        Assert.Contains("# Phase 25 - Hypercall Wait State Monitor", phase25);
        Assert.Contains("Status: monitor-only cadence for attributable VMCALL owner response", phase25);
        Assert.Contains("File name: `25_hv_wait_state_monitor.md`", phase25);
        Assert.Contains("Purpose: close the monitor-only cadence after Phase 24", phase25);
        Assert.Contains("Status: cadence closure only; no attributable owner accept/reject/amend artifact", phase25);
        Assert.Contains("Authority owner: external neutral runtime owner for the exact VMCALL path", phase25);
        Assert.Contains("field/operation, owner, value source, capability policy, evidence class, migration class, denial reason", phase25);
        Assert.Contains("## ISE-HV-WAIT-STATE-MONITOR-25 - Closure Record", phase25);
        Assert.Contains("Closure date: 2026-06-18.", phase25);
        Assert.Contains("closed `MONITOR-ONLY-CADENCE / NO-ATTRIBUTABLE-OWNER-RESPONSE / WAIT-DENIED-FUTURE-GATED / NO-PRODUCTION-CHANGE`", phase25);
        Assert.Contains("Monitor result: no attributable external neutral-runtime-owner artifact was found", phase25);
        Assert.Contains("This closure records the wait-state monitor cadence only", phase25);
        Assert.Contains("does not accept an RFC/ADR", phase25);
        Assert.Contains("allocate an exact numeric VMCALL leaf", phase25);
        Assert.Contains("approve backend admission", phase25);
        Assert.Contains("authorize backend execution", phase25);
        Assert.Contains("publish completion", phase25);
        Assert.Contains("publish retire", phase25);
        Assert.Contains("replace `MissingNeutralOwner`", phase25);
        Assert.Contains("create a backend executor", phase25);
        Assert.Contains("construct a frontend completion record", phase25);
        Assert.Contains("select a replacement intake", phase25);

        Assert.Contains("| accepted exact-leaf RFC/ADR | absent | Phase 06B/Phase 07 remain blocked |", phase25);
        Assert.Contains("| exact numeric VMCALL leaf | absent | all numeric VMCALL leaves remain denied |", phase25);
        Assert.Contains("| accepted neutral owner service | absent | `MissingNeutralOwner` remains production behavior |", phase25);
        Assert.Contains("| replacement intake decision | absent | Minimal VMCALL remains the selected wait target |", phase25);
        Assert.Contains("Monitor-only cadence is not owner acceptance", phase25);
        Assert.Contains("Repeated no-response is not owner acceptance", phase25);
        Assert.Contains("Backlog age is not owner acceptance", phase25);
        Assert.Contains("Closing Phase 25 is not a replacement intake selection", phase25);
        Assert.Contains("Phase 20 packet, Phase 21 handoff, Phase 22 fallback, Phase 23 wait decision, Phase 24 late audit, and this Phase 25 monitor are not owner acceptance", phase25);
        Assert.Contains("production VMCALL remains `HypercallBackendAdmissionRequest.MissingNeutralOwner(...)`", phase25);
        Assert.Contains("route remains `ProjectionOnlyDenied`", phase25);
        Assert.Contains("replacement intake remains not selected", phase25);

        Assert.Contains("ISE-HV-WAIT-STATE-MONITOR-25", backlog);
        Assert.Contains("The monitor-only cadence found no attributable external neutral-runtime-owner accept/reject/amend artifact", backlog);
        Assert.Contains("monitor completion, repeated no-response, backlog age, clean scans, green tests, and elapsed time grant no owner acceptance", backlog);
        Assert.Contains("Phase 25 monitor-only cadence found no attributable owner response", backlog);
    }

    [Fact]
    public void HypercallOwnerArtifactRecheck26_DoesNotFireWithoutAttributableExternalOwnerArtifact()
    {
        string index = ReadPlan("00_virtualization_activation_refactoring_index.md");
        string backlog = ReadPlan("19_open_decision_backlog.md");
        string phase26 = ReadPlan("26_hv_owner_artifact_recheck.md");

        Assert.Contains("26_hv_owner_artifact_recheck.md", index);
        Assert.Contains("Artifact-triggered owner response recheck", index);
        Assert.Contains("# Phase 26 - Hypercall Owner Artifact Recheck", phase26);
        Assert.Contains("Status: artifact-triggered response audit gate for attributable VMCALL owner response", phase26);
        Assert.Contains("File name: `26_hv_owner_artifact_recheck.md`", phase26);
        Assert.Contains("Purpose: close the artifact-triggered recheck gate after Phase 25", phase26);
        Assert.Contains("Status: artifact recheck closure only; no attributable owner accept/reject/amend artifact", phase26);
        Assert.Contains("Authority owner: external neutral runtime owner for the exact VMCALL path", phase26);
        Assert.Contains("field/operation, owner, value source, capability policy, evidence class, migration class, denial reason", phase26);
        Assert.Contains("if no attributable owner artifact is found, the artifact-triggered gate does not fire", phase26);
        Assert.Contains("## ISE-HV-OWNER-ARTIFACT-RECHECK-26 - Closure Record", phase26);
        Assert.Contains("Closure date: 2026-06-18.", phase26);
        Assert.Contains("closed `ARTIFACT-RECHECKED / NO-ATTRIBUTABLE-OWNER-ARTIFACT / GATE-NOT-FIRED / WAIT-DENIED-FUTURE-GATED / NO-PRODUCTION-CHANGE`", phase26);
        Assert.Contains("Recheck result: no attributable external neutral-runtime-owner artifact was found", phase26);
        Assert.Contains("artifact-triggered response audit gate did not fire", phase26);
        Assert.Contains("does not accept an RFC/ADR", phase26);
        Assert.Contains("allocate an exact numeric VMCALL leaf", phase26);
        Assert.Contains("approve backend admission", phase26);
        Assert.Contains("authorize backend execution", phase26);
        Assert.Contains("publish completion", phase26);
        Assert.Contains("publish retire", phase26);
        Assert.Contains("replace `MissingNeutralOwner`", phase26);
        Assert.Contains("create a backend executor", phase26);
        Assert.Contains("construct a frontend completion record", phase26);
        Assert.Contains("select a replacement intake", phase26);

        Assert.Contains("| attributable accepted exact-leaf RFC/ADR | absent | not fired | Phase 06B/Phase 07 remain blocked |", phase26);
        Assert.Contains("| exact numeric VMCALL leaf in accepted owner artifact | absent | not fired | all numeric VMCALL leaves remain denied |", phase26);
        Assert.Contains("| accepted neutral owner service | absent | not fired | `MissingNeutralOwner` remains production behavior |", phase26);
        Assert.Contains("| replacement intake decision | absent | not fired | Minimal VMCALL remains the selected wait target |", phase26);
        Assert.Contains("Artifact recheck completion is not owner acceptance", phase26);
        Assert.Contains("Absence of an artifact is not owner acceptance", phase26);
        Assert.Contains("Repository-local draft text is not an attributable external owner artifact", phase26);
        Assert.Contains("Clean scans and green tests are not an attributable external owner artifact", phase26);
        Assert.Contains("The artifact-triggered gate cannot fire on docs, tests, scans, or monitor cadence", phase26);
        Assert.Contains("Closing Phase 26 is not a replacement intake selection", phase26);
        Assert.Contains("production VMCALL remains `HypercallBackendAdmissionRequest.MissingNeutralOwner(...)`", phase26);
        Assert.Contains("route remains `ProjectionOnlyDenied`", phase26);
        Assert.Contains("replacement intake remains not selected", phase26);

        Assert.Contains("ISE-HV-OWNER-ARTIFACT-RECHECK-26", backlog);
        Assert.Contains("The artifact-triggered response audit gate found no attributable external neutral-runtime-owner accept/reject/amend artifact", backlog);
        Assert.Contains("artifact recheck completion, gate non-fire, repository-local draft text, clean scans, green tests, backlog age, and elapsed time grant no owner acceptance", backlog);
        Assert.Contains("Phase 26 artifact-triggered recheck found no attributable owner artifact and did not fire the gate", backlog);
    }

    [Fact]
    public void HypercallWaitOrReselectCheckpoint27_ContinuesWaitWithoutReplacementSelection()
    {
        string index = ReadPlan("00_virtualization_activation_refactoring_index.md");
        string backlog = ReadPlan("19_open_decision_backlog.md");
        string phase27 = ReadPlan("27_hv_wait_or_reselect_checkpoint.md");

        Assert.Contains("27_hv_wait_or_reselect_checkpoint.md", index);
        Assert.Contains("Process-only wait-or-reselect checkpoint", index);
        Assert.Contains("# Phase 27 - Hypercall Wait Or Reselect Checkpoint", phase27);
        Assert.Contains("Status: process-only checkpoint for continuing wait-state", phase27);
        Assert.Contains("File name: `27_hv_wait_or_reselect_checkpoint.md`", phase27);
        Assert.Contains("Purpose: close the wait-or-reselect checkpoint after Phase 26", phase27);
        Assert.Contains("Status: checkpoint closure only; no attributable owner accept/reject/amend artifact is found, and no replacement intake is selected", phase27);
        Assert.Contains("Authority owner: external neutral runtime owner for the exact VMCALL path", phase27);
        Assert.Contains("field/operation, owner, value source, capability policy, evidence class, migration class, denial reason", phase27);
        Assert.Contains("if no attributable owner artifact and no explicit process reselect decision are found", phase27);
        Assert.Contains("## ISE-HV-WAIT-OR-RESELECT-CHECKPOINT-27 - Closure Record", phase27);
        Assert.Contains("Closure date: 2026-06-18.", phase27);
        Assert.Contains("closed `PROCESS-CHECKPOINT / WAIT-CONTINUES / NO-REPLACEMENT-SELECTED / NO-ATTRIBUTABLE-OWNER-ARTIFACT / WAIT-DENIED-FUTURE-GATED / NO-PRODUCTION-CHANGE`", phase27);
        Assert.Contains("Checkpoint result: continue waiting for an attributable external neutral-runtime-owner artifact", phase27);
        Assert.Contains("no replacement intake is selected", phase27);
        Assert.Contains("This closure records the wait-or-reselect checkpoint only", phase27);
        Assert.Contains("does not accept an RFC/ADR", phase27);
        Assert.Contains("allocate an exact numeric VMCALL leaf", phase27);
        Assert.Contains("approve backend admission", phase27);
        Assert.Contains("authorize backend execution", phase27);
        Assert.Contains("publish completion", phase27);
        Assert.Contains("publish retire", phase27);
        Assert.Contains("replace `MissingNeutralOwner`", phase27);
        Assert.Contains("create a backend executor", phase27);
        Assert.Contains("construct a frontend completion record", phase27);
        Assert.Contains("select a replacement intake", phase27);

        Assert.Contains("| attributable accepted exact-leaf RFC/ADR | absent | no activation | Phase 06B/Phase 07 remain blocked |", phase27);
        Assert.Contains("| exact numeric VMCALL leaf in accepted owner artifact | absent | no leaf | all numeric VMCALL leaves remain denied |", phase27);
        Assert.Contains("| accepted neutral owner service | absent | no backend owner | `MissingNeutralOwner` remains production behavior |", phase27);
        Assert.Contains("| explicit process reselect decision | absent | no replacement selected | Minimal VMCALL remains the selected wait target |", phase27);
        Assert.Contains("| replacement candidate ranking | present in backlog only | non-authority | ranking does not choose a replacement |", phase27);
        Assert.Contains("Wait-or-reselect checkpoint closure is not owner acceptance", phase27);
        Assert.Contains("Continuing to wait is not owner acceptance", phase27);
        Assert.Contains("Not selecting a replacement is not owner acceptance", phase27);
        Assert.Contains("Replacement candidate availability is not replacement intake selection", phase27);
        Assert.Contains("Backlog priority and candidate ranking are not replacement intake selection", phase27);
        Assert.Contains("Closing Phase 27 is not an accepted RFC/ADR, rejection, amendment, or replacement intake selection", phase27);
        Assert.Contains("production VMCALL remains `HypercallBackendAdmissionRequest.MissingNeutralOwner(...)`", phase27);
        Assert.Contains("route remains `ProjectionOnlyDenied`", phase27);
        Assert.Contains("replacement intake remains not selected", phase27);

        Assert.Contains("ISE-HV-WAIT-OR-RESELECT-CHECKPOINT-27", backlog);
        Assert.Contains("The process-only checkpoint continues waiting for an attributable external neutral-runtime-owner artifact", backlog);
        Assert.Contains("selects no replacement intake", backlog);
        Assert.Contains("checkpoint closure, wait continuation, no-reselect, candidate availability, backlog priority, clean scans, green tests, backlog age, and elapsed time grant no owner acceptance", backlog);
        Assert.Contains("Phase 27 wait-or-reselect checkpoint continues wait and selects no replacement", backlog);
    }

    [Fact]
    public void HypercallDeniedChainRefresh28_PreservesMissingOwnerProjectionOnlyAndPublicationDenials()
    {
        string index = ReadPlan("00_virtualization_activation_refactoring_index.md");
        string backlog = ReadPlan("19_open_decision_backlog.md");
        string phase28 = ReadPlan("28_hv_denied_chain_refresh.md");

        Assert.Contains("28_hv_denied_chain_refresh.md", index);
        Assert.Contains("Readiness-only refresh of the VMCALL denied production chain", index);
        Assert.Contains("# Phase 28 - Hypercall Denied Chain Refresh", phase28);
        Assert.Contains("Status: readiness-only refresh of the current VMCALL denied production chain", phase28);
        Assert.Contains("File name: `28_hv_denied_chain_refresh.md`", phase28);
        Assert.Contains("Purpose: close the denied-chain refresh after Phase 27", phase28);
        Assert.Contains("`MissingNeutralOwner` -> backend denied -> `ProjectionOnlyDenied` -> completion publication denied -> retire publication denied", phase28);
        Assert.Contains("Status: denied-chain refresh closure only", phase28);
        Assert.Contains("Authority owner: external neutral runtime owner for the exact VMCALL path", phase28);
        Assert.Contains("field/operation, owner, value source, capability policy, evidence class, migration class, denial reason", phase28);
        Assert.Contains("## ISE-HV-DENIED-CHAIN-REFRESH-28 - Closure Record", phase28);
        Assert.Contains("Closure date: 2026-06-18.", phase28);
        Assert.Contains("closed `DENIED-CHAIN-REFRESH / MISSING-OWNER-PRESERVED / PROJECTION-ONLY-DENIED / COMPLETION-RETIRE-DENIED / WAIT-DENIED-FUTURE-GATED / NO-PRODUCTION-CHANGE`", phase28);
        Assert.Contains("Refresh result: current production VMCALL remains denied from admission through completion and retire", phase28);
        Assert.Contains("This closure records the denied production chain only", phase28);
        Assert.Contains("does not accept an RFC/ADR", phase28);
        Assert.Contains("allocate an exact numeric VMCALL leaf", phase28);
        Assert.Contains("approve backend admission", phase28);
        Assert.Contains("authorize backend execution", phase28);
        Assert.Contains("publish completion", phase28);
        Assert.Contains("publish retire", phase28);
        Assert.Contains("replace `MissingNeutralOwner`", phase28);
        Assert.Contains("create a backend executor", phase28);
        Assert.Contains("construct a frontend completion record", phase28);
        Assert.Contains("select a replacement intake", phase28);

        Assert.Contains("| VMCALL owner | `HypercallBackendAdmissionRequest.MissingNeutralOwner(...)` | accepted neutral runtime owner for exact VMCALL leaf | backend admission remains denied |", phase28);
        Assert.Contains("| backend admission | no `HypercallBackendAdmissionDecision.Allowed` | accepted exact-leaf RFC/ADR with full owner map | backend execution remains unauthorized |", phase28);
        Assert.Contains("| backend execution | no `BackendExecutionAuthorized: true` | backend owner execution contract and capability policy | no backend executor may run |", phase28);
        Assert.Contains("| completion route | `TrapCompletionRouteRequest.ProjectionOnlyDenied(...)` | completion owner route contract after backend success | route remains admitted-denied/projection-only |", phase28);
        Assert.Contains("| completion publication | fence result has `CompletionPublicationAllowed == false` | neutral completion publication owner | no completion record may be constructed in frontend |", phase28);
        Assert.Contains("| retire publication | fence result has `RetirePublicationAllowed == false` | separate retire owner and evidence contract | no retire publication may occur |", phase28);
        Assert.Contains("Denied-chain refresh is not owner acceptance", phase28);
        Assert.Contains("Preserving `MissingNeutralOwner` is not owner acceptance", phase28);
        Assert.Contains("Preserving `ProjectionOnlyDenied` is not backend success", phase28);
        Assert.Contains("Route scaffolding is not VMX frontend runtime publication", phase28);
        Assert.Contains("Completion fence scaffolding is not frontend completion construction", phase28);
        Assert.Contains("production VMCALL remains `HypercallBackendAdmissionRequest.MissingNeutralOwner(...)`", phase28);
        Assert.Contains("backend execution remains unauthorized", phase28);
        Assert.Contains("route remains `ProjectionOnlyDenied`", phase28);
        Assert.Contains("completion publication remains denied", phase28);
        Assert.Contains("retire publication remains denied", phase28);

        Assert.Contains("ISE-HV-DENIED-CHAIN-REFRESH-28", backlog);
        Assert.Contains("The readiness-only refresh confirmed the current VMCALL production chain remains `MissingNeutralOwner` -> backend denied -> `ProjectionOnlyDenied` -> completion publication denied -> retire publication denied", backlog);
        Assert.Contains("denied-chain refresh, preserved missing owner, preserved projection-only route, route scaffolding, completion fence scaffolding, clean scans, green tests, closed audits, and current denied behavior grant no owner acceptance", backlog);
        Assert.Contains("Phase 28 denied-chain refresh preserves `MissingNeutralOwner` -> backend denied -> `ProjectionOnlyDenied` -> completion/retire denied", backlog);
    }

    [Fact]
    public void HypercallOwnerResponseWatchdog29_IsRepeatCheckOnlyAndPreservesDeniedChain()
    {
        string index = ReadPlan("00_virtualization_activation_refactoring_index.md");
        string backlog = ReadPlan("19_open_decision_backlog.md");
        string phase29 = ReadPlan("29_hv_owner_response_watchdog.md");

        Assert.Contains("29_hv_owner_response_watchdog.md", index);
        Assert.Contains("Watchdog-only repeat owner-response check", index);
        Assert.Contains("# Phase 29 - Hypercall Owner Response Watchdog", phase29);
        Assert.Contains("Status: watchdog-only repeat check for attributable VMCALL owner response", phase29);
        Assert.Contains("File name: `29_hv_owner_response_watchdog.md`", phase29);
        Assert.Contains("Purpose: close the owner-response watchdog check after Phase 28", phase29);
        Assert.Contains("Status: watchdog closure only; no attributable owner accept/reject/amend artifact", phase29);
        Assert.Contains("Authority owner: external neutral runtime owner for the exact VMCALL path", phase29);
        Assert.Contains("timer/cadence evidence are not authority", phase29);
        Assert.Contains("field/operation, owner, value source, capability policy, evidence class, migration class, denial reason", phase29);
        Assert.Contains("if no attributable owner artifact and no explicit process reselect decision are found, the watchdog does not move state", phase29);
        Assert.Contains("## ISE-HV-OWNER-RESPONSE-WATCHDOG-29 - Closure Record", phase29);
        Assert.Contains("Closure date: 2026-06-18.", phase29);
        Assert.Contains("closed `WATCHDOG-ONLY / NO-ATTRIBUTABLE-OWNER-RESPONSE / DENIED-CHAIN-PRESERVED / WAIT-DENIED-FUTURE-GATED / NO-PRODUCTION-CHANGE`", phase29);
        Assert.Contains("Watchdog result: no attributable external neutral-runtime-owner artifact was found", phase29);
        Assert.Contains("denied production chain remains unchanged", phase29);
        Assert.Contains("This closure records the watchdog-only response check", phase29);
        Assert.Contains("does not accept an RFC/ADR", phase29);
        Assert.Contains("allocate an exact numeric VMCALL leaf", phase29);
        Assert.Contains("approve backend admission", phase29);
        Assert.Contains("authorize backend execution", phase29);
        Assert.Contains("publish completion", phase29);
        Assert.Contains("publish retire", phase29);
        Assert.Contains("replace `MissingNeutralOwner`", phase29);
        Assert.Contains("create a backend executor", phase29);
        Assert.Contains("construct a frontend completion record", phase29);
        Assert.Contains("select a replacement intake", phase29);
        Assert.Contains("start an activation timer", phase29);

        Assert.Contains("| attributable accepted exact-leaf RFC/ADR | absent | no state move | Phase 06B/Phase 07 remain blocked |", phase29);
        Assert.Contains("| accepted neutral owner service | absent | no backend owner | `MissingNeutralOwner` remains production behavior |", phase29);
        Assert.Contains("| accepted completion/retire rules | absent | no publication path | completion and retire remain denied |", phase29);
        Assert.Contains("| explicit process reselect decision | absent | no replacement selected | Minimal VMCALL remains the selected wait target |", phase29);
        Assert.Contains("| watchdog tick or timeout | local process signal only | non-authority | no state transition |", phase29);
        Assert.Contains("| stable denied chain | present | non-authority | does not become implementation permission |", phase29);
        Assert.Contains("Watchdog closure is not owner acceptance", phase29);
        Assert.Contains("Watchdog tick is not owner acceptance", phase29);
        Assert.Contains("Watchdog timeout is not owner acceptance", phase29);
        Assert.Contains("Repeated silence is not owner acceptance", phase29);
        Assert.Contains("Stable denied behavior is not implementation permission", phase29);
        Assert.Contains("production VMCALL remains `HypercallBackendAdmissionRequest.MissingNeutralOwner(...)`", phase29);
        Assert.Contains("backend execution remains unauthorized", phase29);
        Assert.Contains("route remains `ProjectionOnlyDenied`", phase29);
        Assert.Contains("completion publication remains denied", phase29);
        Assert.Contains("retire publication remains denied", phase29);

        Assert.Contains("ISE-HV-OWNER-RESPONSE-WATCHDOG-29", backlog);
        Assert.Contains("The watchdog-only repeat check found no attributable external neutral-runtime-owner accept/reject/amend artifact", backlog);
        Assert.Contains("watchdog closure, watchdog tick, watchdog timeout, repeated silence, stable denied behavior, clean scans, green tests, closed audits, and elapsed time grant no owner acceptance", backlog);
        Assert.Contains("Phase 29 watchdog found no attributable owner response and preserved the denied chain", backlog);
    }

    [Fact]
    public void HypercallFinalWaitBaseline30_ClosesCurrentSeriesWithoutReleaseOrImplementationPermission()
    {
        string index = ReadPlan("00_virtualization_activation_refactoring_index.md");
        string backlog = ReadPlan("19_open_decision_backlog.md");
        string phase30 = ReadPlan("30_hv_final_wait_baseline.md");

        Assert.Contains("30_hv_final_wait_baseline.md", index);
        Assert.Contains("Final wait-baseline snapshot for the current VMCALL owner-response series", index);
        Assert.Contains("# Phase 30 - Hypercall Final Wait Baseline", phase30);
        Assert.Contains("Status: final wait-baseline snapshot for the current VMCALL owner-response wait series", phase30);
        Assert.Contains("File name: `30_hv_final_wait_baseline.md`", phase30);
        Assert.Contains("Purpose: close the current Phase 26-30 wait series", phase30);
        Assert.Contains("Status: final wait-baseline closure only", phase30);
        Assert.Contains("Authority owner: external neutral runtime owner for the exact VMCALL path", phase30);
        Assert.Contains("final wording are not authority", phase30);
        Assert.Contains("field/operation, owner, value source, capability policy, evidence class, migration class, denial reason", phase30);
        Assert.Contains("final baseline remains `MissingNeutralOwner` / backend denied / `ProjectionOnlyDenied`", phase30);
        Assert.Contains("## ISE-HV-FINAL-WAIT-BASELINE-30 - Closure Record", phase30);
        Assert.Contains("Closure date: 2026-06-18.", phase30);
        Assert.Contains("closed `FINAL-WAIT-BASELINE / NO-ATTRIBUTABLE-OWNER-ARTIFACT / NO-REPLACEMENT-SELECTED / DENIED-CHAIN-PRESERVED / WAIT-DENIED-FUTURE-GATED / NO-PRODUCTION-CHANGE`", phase30);
        Assert.Contains("Final baseline result: the current VMCALL owner-response wait series closes", phase30);
        Assert.Contains("no replacement intake is selected", phase30);
        Assert.Contains("denied production chain remains unchanged", phase30);
        Assert.Contains("This closure records the final wait-baseline snapshot for the current series only", phase30);
        Assert.Contains("does not accept an RFC/ADR", phase30);
        Assert.Contains("allocate an exact numeric VMCALL leaf", phase30);
        Assert.Contains("approve backend admission", phase30);
        Assert.Contains("authorize backend execution", phase30);
        Assert.Contains("publish completion", phase30);
        Assert.Contains("publish retire", phase30);
        Assert.Contains("replace `MissingNeutralOwner`", phase30);
        Assert.Contains("create a backend executor", phase30);
        Assert.Contains("construct a frontend completion record", phase30);
        Assert.Contains("select a replacement intake", phase30);
        Assert.Contains("make a release claim", phase30);

        Assert.Contains("| attributable accepted exact-leaf RFC/ADR | absent | no activation | Phase 06B/Phase 07 remain blocked |", phase30);
        Assert.Contains("| exact numeric VMCALL leaf in accepted owner artifact | absent | no leaf | all numeric VMCALL leaves remain denied |", phase30);
        Assert.Contains("| backend admission allow | absent | no backend admission | backend execution remains unauthorized |", phase30);
        Assert.Contains("| backend execution authorization | absent | no backend execution | no backend executor may run |", phase30);
        Assert.Contains("| completion publication owner | absent | no completion publication | no frontend completion record may be constructed |", phase30);
        Assert.Contains("| retire publication owner | absent | no retire publication | no retire publication may occur |", phase30);
        Assert.Contains("| explicit process reselect decision | absent | no replacement selected | Minimal VMCALL remains the selected wait target |", phase30);
        Assert.Contains("| final wait-baseline wording | present | non-authority | does not become release or activation claim |", phase30);
        Assert.Contains("Final wait-baseline closure is not owner acceptance", phase30);
        Assert.Contains("Closing the current wait series is not owner acceptance", phase30);
        Assert.Contains("Final wording is not a release claim", phase30);
        Assert.Contains("Final wording is not implementation permission", phase30);
        Assert.Contains("production VMCALL remains `HypercallBackendAdmissionRequest.MissingNeutralOwner(...)`", phase30);
        Assert.Contains("backend execution remains unauthorized", phase30);
        Assert.Contains("route remains `ProjectionOnlyDenied`", phase30);
        Assert.Contains("completion publication remains denied", phase30);
        Assert.Contains("retire publication remains denied", phase30);

        Assert.Contains("ISE-HV-FINAL-WAIT-BASELINE-30", backlog);
        Assert.Contains("The final wait-baseline snapshot for the current VMCALL owner-response series found no attributable external neutral-runtime-owner accept/reject/amend artifact", backlog);
        Assert.Contains("final baseline closure, closed-series wording, stable denied behavior, clean scans, green tests, closed audits, backlog wording, and elapsed time grant no owner acceptance", backlog);
        Assert.Contains("Phase 30 final wait-baseline closes the current wait series with no owner artifact, no exact leaf, no replacement, and denied chain preserved", backlog);
    }

    [Fact]
    public void HypercallPostBaselineArtifactSentry31_IsSentryOnlyAndKeepsWaitDeniedFutureGated()
    {
        string index = ReadPlan("00_virtualization_activation_refactoring_index.md");
        string backlog = ReadPlan("19_open_decision_backlog.md");
        string phase31 = ReadPlan("31_hv_post_baseline_artifact_sentry.md");

        Assert.Contains("31_hv_post_baseline_artifact_sentry.md", index);
        Assert.Contains("Post-baseline sentry-only check for attributable owner artifact", index);
        Assert.Contains("# Phase 31 - Hypercall Post Baseline Artifact Sentry", phase31);
        Assert.Contains("Status: post-baseline sentry-only check for attributable VMCALL owner artifact", phase31);
        Assert.Contains("File name: `31_hv_post_baseline_artifact_sentry.md`", phase31);
        Assert.Contains("Purpose: perform a post-baseline sentry-only check after Phase 30", phase31);
        Assert.Contains("Status: sentry-only closure", phase31);
        Assert.Contains("Authority owner: external neutral runtime owner for the exact VMCALL path", phase31);
        Assert.Contains("sentry wording are not authority", phase31);
        Assert.Contains("field/operation, owner, value source, capability policy, evidence class, migration class, denial reason", phase31);
        Assert.Contains("post-baseline sentry remains `MissingNeutralOwner` / backend denied / `ProjectionOnlyDenied`", phase31);
        Assert.Contains("## ISE-HV-POST-BASELINE-ARTIFACT-SENTRY-31 - Closure Record", phase31);
        Assert.Contains("Closure date: 2026-06-19.", phase31);
        Assert.Contains("closed `POST-BASELINE-SENTRY / NO-ATTRIBUTABLE-OWNER-ARTIFACT / NO-REPLACEMENT-SELECTED / WAIT-DENIED-FUTURE-GATED / NO-PRODUCTION-CHANGE`", phase31);
        Assert.Contains("Sentry result: the post-baseline sentry-only check found no attributable external neutral-runtime-owner artifact", phase31);
        Assert.Contains("no replacement intake is selected", phase31);
        Assert.Contains("denied production chain remains unchanged", phase31);
        Assert.Contains("This closure records a sentry-only observation after Phase 30", phase31);
        Assert.Contains("does not accept an RFC/ADR", phase31);
        Assert.Contains("allocate an exact numeric VMCALL leaf", phase31);
        Assert.Contains("approve backend admission", phase31);
        Assert.Contains("authorize backend execution", phase31);
        Assert.Contains("publish completion", phase31);
        Assert.Contains("publish retire", phase31);
        Assert.Contains("replace `MissingNeutralOwner`", phase31);
        Assert.Contains("create a backend executor", phase31);
        Assert.Contains("construct a frontend completion record", phase31);
        Assert.Contains("select a replacement intake", phase31);
        Assert.Contains("make a release claim", phase31);

        Assert.Contains("| attributable accepted exact-leaf RFC/ADR | absent | no activation | Phase 06B/Phase 07 remain blocked |", phase31);
        Assert.Contains("| exact numeric VMCALL leaf in accepted owner artifact | absent | no leaf | all numeric VMCALL leaves remain denied |", phase31);
        Assert.Contains("| backend admission allow | absent | no backend admission | backend execution remains unauthorized |", phase31);
        Assert.Contains("| backend execution authorization | absent | no backend execution | no backend executor may run |", phase31);
        Assert.Contains("| completion publication owner | absent | no completion publication | no frontend completion record may be constructed |", phase31);
        Assert.Contains("| retire publication owner | absent | no retire publication | no retire publication may occur |", phase31);
        Assert.Contains("| explicit process reselect decision | absent | no replacement selected | Minimal VMCALL remains the selected wait target |", phase31);
        Assert.Contains("| post-baseline sentry tick | local process signal only | non-authority | no state transition |", phase31);
        Assert.Contains("| closed final baseline | present | non-authority | does not become release, owner acceptance, or activation claim |", phase31);
        Assert.Contains("Post-baseline sentry closure is not owner acceptance", phase31);
        Assert.Contains("A sentry tick is not owner acceptance", phase31);
        Assert.Contains("A sentry tick is not replacement intake selection", phase31);
        Assert.Contains("Closed final baseline is not owner acceptance", phase31);
        Assert.Contains("Final baseline is not a release claim", phase31);
        Assert.Contains("Stable denied behavior is not implementation permission", phase31);
        Assert.Contains("production VMCALL remains `HypercallBackendAdmissionRequest.MissingNeutralOwner(...)`", phase31);
        Assert.Contains("backend execution remains unauthorized", phase31);
        Assert.Contains("route remains `ProjectionOnlyDenied`", phase31);
        Assert.Contains("completion publication remains denied", phase31);
        Assert.Contains("retire publication remains denied", phase31);
        Assert.Contains("replacement intake remains not selected", phase31);

        Assert.Contains("ISE-HV-POST-BASELINE-ARTIFACT-SENTRY-31", backlog);
        Assert.Contains("The post-baseline sentry-only check found no attributable external neutral-runtime-owner accept/reject/amend artifact", backlog);
        Assert.Contains("sentry closure, sentry tick, closed-series baseline, stable denied behavior, clean scans, green tests, backlog wording, and elapsed time grant no owner acceptance", backlog);
        Assert.Contains("Phase 31 post-baseline sentry found no owner artifact and no replacement selection", backlog);
    }

    [Fact]
    public void OwnerDecision05_ClosesNoGoWithoutForgingNeutralOwnerAcceptance()
    {
        string phase06 = ReadPlan("06_neutral_hypercall_backend_owner_rfc.md");
        string phase07 = ReadPlan("07_vmcall_success_path_activation_plan.md");
        string backlog = ReadPlan("19_open_decision_backlog.md");
        string priorDecision = File.ReadAllText(Path.Combine(
            VmxDocumentationMigrationClaimHygieneTests.FindRepositoryRoot(),
            "HybridCPU_ISE",
            "docs",
            "ref2",
            "VirtualiztionRefactoringNew",
            "07_hypercall_backend_owner_and_vmcall_decision.md"));

        Assert.Contains("## ISE-HV-RFC-OWNER-DECISION-05 - Closure Decision", phase06);
        Assert.Contains("Decision date: 2026-06-12.", phase06);
        Assert.Contains("closed `NO-GO` for Phase 06B implementation and Phase 07 backend execution", phase06);
        Assert.Contains("not an owner acceptance or RFC rejection on behalf of neutral runtime owners", phase06);
        Assert.Contains("This decision task is closed. The implementation gate is not open.", phase06);
        Assert.Contains("externally accepted neutral-runtime-owner RFC/ADR artifact", phase06);
        Assert.Contains("one exact numeric VMCALL leaf", phase06);

        Assert.Contains("ISE-HV-RFC-OWNER-DECISION-05", phase07);
        Assert.Contains("closed `NO-GO` on 2026-06-12", phase07);
        Assert.Contains("must not interpret closure of the decision task as closure of Phase 06B", phase07);
        Assert.Contains("neutral runtime owners must accept or reject an exact-leaf RFC/ADR", phase07);

        Assert.Contains("ISE-HV-RFC-OWNER-DECISION-05", backlog);
        Assert.Contains("Phase 06B/07 remain blocked", backlog);

        Assert.Contains(
            "accepted as a denial/readiness hardening decision only",
            priorDecision);
        Assert.Contains("does not authorize VMCALL backend execution", priorDecision);
        Assert.DoesNotContain(
            "accepted neutral hypercall backend owner",
            priorDecision,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OwnerAcceptanceHandoff06_ClosesWithoutTreatingSilenceAsAcceptance()
    {
        string phase06 = ReadPlan("06_neutral_hypercall_backend_owner_rfc.md");
        string phase07 = ReadPlan("07_vmcall_success_path_activation_plan.md");
        string backlog = ReadPlan("19_open_decision_backlog.md");

        Assert.Contains("## ISE-HV-OWNER-ACCEPTANCE-HANDOFF-06 - Closure Record", phase06);
        Assert.Contains("Handoff date: 2026-06-12.", phase06);
        Assert.Contains("closed `NO-DECISION / RETURNED-BLOCKED`", phase06);
        Assert.Contains("does not record acceptance or rejection on behalf of neutral runtime owners", phase06);
        Assert.Contains("No exact numeric leaf is selected, allocated, or reserved", phase06);
        Assert.Contains("Production VMCALL must continue to use `HypercallBackendAdmissionRequest.MissingNeutralOwner(...)`", phase06);
        Assert.Contains("Silence, repository readiness, a draft descriptor, an operation class, or this closure record is not an owner decision", phase06);

        Assert.Contains("ISE-HV-OWNER-ACCEPTANCE-HANDOFF-06", phase07);
        Assert.Contains("not an accepted RFC/ADR and is not consumable by Phase 07", phase07);
        Assert.Contains("A handoff record, readiness matrix, operation class, or lack of rejection cannot satisfy this gate", phase07);

        Assert.Contains("ISE-HV-OWNER-ACCEPTANCE-HANDOFF-06", backlog);
        Assert.Contains("grants no Phase 06B or Phase 07 permission", backlog);
        Assert.Contains("owner handoff closed `NO-DECISION / RETURNED-BLOCKED`", backlog);
    }

    [Fact]
    public void OwnerResponse07_ClosesNoResponseWithoutOpeningPhase06BOrPhase07()
    {
        string phase06 = ReadPlan("06_neutral_hypercall_backend_owner_rfc.md");
        string phase07 = ReadPlan("07_vmcall_success_path_activation_plan.md");
        string backlog = ReadPlan("19_open_decision_backlog.md");

        Assert.Contains("## ISE-HV-OWNER-RESPONSE-07 - Closure Record", phase06);
        Assert.Contains("Response audit date: 2026-06-12.", phase06);
        Assert.Contains("closed `NO-RESPONSE / EXTERNAL-BLOCKED`", phase06);
        Assert.Contains("No response is not acceptance.", phase06);
        Assert.Contains("No rejection is not acceptance.", phase06);
        Assert.Contains("its `NO-GO` remains applicable", phase06);
        Assert.Contains("Phase 06B and Phase 07 remain blocked/future-gated", phase06);
        Assert.Contains("audit is complete, not because the external owner dependency is satisfied", phase06);

        Assert.Contains("ISE-HV-OWNER-RESPONSE-07", phase07);
        Assert.Contains("production VMCALL remains admitted-denied", phase07);
        Assert.Contains("owner silence, absence of rejection, elapsed time, repository-local readiness", phase07);
        Assert.Contains("Only an attributable accepted exact-leaf RFC/ADR is consumable", phase07);

        Assert.Contains("ISE-HV-OWNER-RESPONSE-07", backlog);
        Assert.Contains("Audit completion is not owner acceptance", backlog);
        Assert.Contains("response audit closed `NO-RESPONSE / EXTERNAL-BLOCKED`", backlog);
    }

    [Fact]
    public void BlockedBaseline08_FreezesDeniedChainWithoutOpeningPhase08()
    {
        string phase07 = ReadPlan("07_vmcall_success_path_activation_plan.md");
        string phase08 = ReadPlan("08_trap_completion_route_publication_plan.md");
        string backlog = ReadPlan("19_open_decision_backlog.md");

        Assert.Contains("## ISE-HV-PHASE07-BLOCKED-BASELINE-08 - Closure Record", phase07);
        Assert.Contains("Baseline date: 2026-06-12.", phase07);
        Assert.Contains("closed `BLOCKED-BASELINE / NO-ACTIVATION`", phase07);
        Assert.Contains("does not close Phase 07 as implemented and does not permit Phase 08 consumption", phase07);
        Assert.Contains("`HypercallBackendAdmissionRequest.MissingNeutralOwner(...)` remains", phase07);
        Assert.Contains("no `HypercallBackendAdmissionDecision.Allowed`", phase07);
        Assert.Contains("no `BackendExecutionAuthorized: true`", phase07);
        Assert.Contains("`TrapCompletionRouteRequest.ProjectionOnlyDenied(...)` remains", phase07);
        Assert.Contains("no handler-side `CompletionRecord`", phase07);
        Assert.Contains("neither positive descriptor is connected to VMX frontend", phase07);

        Assert.Contains("ISE-HV-PHASE07-BLOCKED-BASELINE-08", phase08);
        Assert.Contains("Phase 08 cannot consume that closure as backend success", phase08);
        Assert.Contains("both positive route descriptors remain disconnected", phase08);

        Assert.Contains("ISE-HV-PHASE07-BLOCKED-BASELINE-08", backlog);
        Assert.Contains("grants no Phase 07 implementation or Phase 08 consumption", backlog);
        Assert.Contains("Phase 07 denied baseline frozen `BLOCKED-BASELINE / NO-ACTIVATION`", backlog);
        Assert.Contains("ISE-COMP-ROUTE-01", backlog);
        Assert.Contains("ISE-COMP-FENCE-02", backlog);
        Assert.Contains("ISE-HV-RETIRE-PUBLICATION-GATE-09", backlog);
        Assert.Contains("Retire publication remains a separate future-gated owner rule", backlog);
    }

    [Fact]
    public void VmreadDeniedSurfaces09_ClosesDeniedBaselineWithoutProjectionWidening()
    {
        string phase04 = ReadPlan("04_vmread_projection_completion_and_denial_matrix.md");
        string backlog = ReadPlan("19_open_decision_backlog.md");

        Assert.Contains("## ISE-VMREAD-DENIED-SURFACES-09 - Closure Record", phase04);
        Assert.Contains("Closure date: 2026-06-18.", phase04);
        Assert.Contains("closed `DENIED-BASELINE / NO-PROJECTION-WIDENING`", phase04);
        Assert.Contains("`HostPc`, `HostSp`, `HostFlags`, `HostCr0`", phase04);
        Assert.Contains("`HostCr3`", phase04);
        Assert.Contains("compatibility-control VMREAD values", phase04);
        Assert.Contains("`HostAddressSpaceOwnerMissing`", phase04);
        Assert.Contains("`CompatibilityControlValueProjectionDenied`", phase04);
        Assert.Contains("`MemoryDomainReadOnlyTranslationView` is not a `HostCr3` value source", phase04);
        Assert.Contains("Projection denial is not backend execution, completion publication, retire publication", phase04);

        Assert.Contains("ISE-VMREAD-DENIED-SURFACES-09", backlog);
        Assert.Contains("Host execution aliases | denied baseline closed by `ISE-VMREAD-DENIED-SURFACES-09`", backlog);
        Assert.Contains("Compatibility-control VMREAD values | denied baseline closed by `ISE-VMREAD-DENIED-SURFACES-09`", backlog);
    }

    [Fact]
    public void VmwriteDenyBaseline10_ClosesAllWriteDenialWithoutWriteOwner()
    {
        string phase10 = ReadPlan("10_vmwrite_neutral_owner_policy.md");
        string backlog = ReadPlan("19_open_decision_backlog.md");

        Assert.Contains("## ISE-VMWRITE-DENY-BASELINE-10 - Closure Record", phase10);
        Assert.Contains("Closure date: 2026-06-18.", phase10);
        Assert.Contains("closed `DENIED-BASELINE / NO-WRITE-OWNER`", phase10);
        Assert.Contains("does not implement a neutral write owner", phase10);
        Assert.Contains("`VmcsFieldProjectionSchema.CanWrite(...)` is false for every entry", phase10);
        Assert.Contains("`VmcsFieldAliasDecision.WriteDenied`", phase10);
        Assert.Contains("decode can name operand form but creates no write authority", phase10);
        Assert.Contains("`ReadOnly` schema access is not write permission", phase10);
        Assert.Contains("Any future write path requires a neutral write-owner RFC/ADR", phase10);

        Assert.Contains("ISE-VMWRITE-DENY-BASELINE-10", backlog);
        Assert.Contains("VMWRITE | denied baseline closed by `ISE-VMWRITE-DENY-BASELINE-10`", backlog);
    }

    [Fact]
    public void NestedChildIntentBaseline11_ClosesDeniedFutureGateWithoutVmcsAuthority()
    {
        string phase11 = ReadPlan("11_nested_child_intent_owner_rfc.md");
        string backlog = ReadPlan("19_open_decision_backlog.md");

        Assert.Contains("## ISE-NESTED-CHILD-INTENT-BASELINE-11 - Closure Record", phase11);
        Assert.Contains("Closure date: 2026-06-18.", phase11);
        Assert.Contains("closed `DENIED/FUTURE-GATED BASELINE / NO-NESTED-ACTIVATION`", phase11);
        Assert.Contains("does not accept a nested owner RFC/ADR", phase11);
        Assert.Contains("does not give Shadow VMCS, VMCS12, or VMCS02 runtime authority", phase11);
        Assert.Contains("read-only compatibility projection", phase11);
        Assert.Contains("`ShadowVmcsNestedProjectionService` fails closed", phase11);
        Assert.Contains("`DeniedNestedVmcsAuthority`", phase11);
        Assert.Contains("`DeniedMutableShadowVmcsAuthority`", phase11);
        Assert.Contains("AllowedDesignFence` is not backend success", phase11);
        Assert.Contains("VMREAD/VMWRITE projection, SecureCompute admission, compiler metadata", phase11);
        Assert.Contains("accepted neutral child-intent owner RFC/ADR", phase11);

        Assert.Contains("ISE-NESTED-CHILD-INTENT-BASELINE-11", backlog);
        Assert.Contains("production VMX paths do not call nested enablement", backlog);
        Assert.Contains("Nested child intent | denied/future-gated baseline closed by `ISE-NESTED-CHILD-INTENT-BASELINE-11`", backlog);
        Assert.Contains("Shadow VMCS/VMCS12/VMCS02 remain non-authority", backlog);
    }

    [Fact]
    public void MemoryIoLaneStreamBoundary12_ClosesDeniedBaselineWithoutPassthroughAuthority()
    {
        string phase12 = ReadPlan("12_memory_io_iommu_lane_stream_boundary_activation_plan.md");
        string backlog = ReadPlan("19_open_decision_backlog.md");

        Assert.Contains("## ISE-MEM-IO-LANE-STREAM-BOUNDARY-12 - Closure Record", phase12);
        Assert.Contains("Closure date: 2026-06-18.", phase12);
        Assert.Contains("closed `DENIED/FUTURE-GATED BASELINE / NO-PASSTHROUGH-AUTHORITY`", phase12);
        Assert.Contains("does not implement VMX-driven DMA/IOMMU, lane passthrough", phase12);
        Assert.Contains("`VmxCompatibilityIoAliasesAreReadOnlyDenied`", phase12);
        Assert.Contains("`DmaAuthorityService` requires I/O domain authority", phase12);
        Assert.Contains("compatibility-only descriptor returns `RuntimeAuthorityRequired`", phase12);
        Assert.Contains("no VMCS/migration authority", phase12);
        Assert.Contains("Memory VMREAD projection is not memory mutation, DMA authority, IOMMU authority", phase12);
        Assert.Contains("Lane6/Lane7/Stream tokens, telemetry, replay evidence", phase12);
        Assert.Contains("must not be called from VMX frontend admission/dispatch/retire", phase12);
        Assert.Contains("owner-specific RFC/ADR with capability, evidence, migration, completion, retire", phase12);

        Assert.Contains("ISE-MEM-IO-LANE-STREAM-BOUNDARY-12", backlog);
        Assert.Contains("Memory-owned VMREAD remains projection-only", backlog);
        Assert.Contains("Lane6/Lane7/Stream passthrough | denied/future-gated baseline closed by `ISE-MEM-IO-LANE-STREAM-BOUNDARY-12`", backlog);
        Assert.Contains("model/helper-only evidence remains non-authority", backlog);
    }

    [Fact]
    public void SecureComputeVmxBoundary13_ClosesProofOnlyBaselineWithoutVmxAuthority()
    {
        string phase13 = ReadPlan("13_securecompute_virtualization_boundary_plan.md");
        string backlog = ReadPlan("19_open_decision_backlog.md");

        Assert.Contains("## ISE-SECCOMP-VMX-BOUNDARY-13 - Closure Record", phase13);
        Assert.Contains("Closure date: 2026-06-18.", phase13);
        Assert.Contains("closed `DENIED/PROOF-ONLY BASELINE / NO-SECCOMP-VMX-ACTIVATION`", phase13);
        Assert.Contains("does not accept a SecureCompute owner RFC/ADR", phase13);
        Assert.Contains("does not make VMX, VMCS, Shadow VMCS, or `VmxCaps` an authority source", phase13);
        Assert.Contains("AllowedSecureOperation` | admission-only secure-domain result", phase13);
        Assert.Contains("AllowedProofOnlyNoExecution` | proof-only/no-execution result", phase13);
        Assert.Contains("`BackendExecutionAuthorized == false`", phase13);
        Assert.Contains("`VmcsProjection`, `VmxCapsProjection`, `ShadowVmcsProjection` | denied as backend owner sources", phase13);
        Assert.Contains("`DeniedNonNeutralAuthoritySource`", phase13);
        Assert.Contains("VMREAD output is not SecureCompute activation", phase13);
        Assert.Contains("VMCS schema, operand decode, and compatibility fields create no secure mutation authority", phase13);
        Assert.Contains("a true isolated predicate is not proof that production completion or retire publication occurred", phase13);
        Assert.Contains("lane/stream telemetry, or frontend state cannot become secure checkpoint authority", phase13);
        Assert.Contains("`SecureDomainAdmissionDecision.AllowedSecureOperation` is not backend success", phase13);
        Assert.Contains("`SecureBackendOwnerAdmissionDecision.AllowedProofOnlyNoExecution` is not execution", phase13);
        Assert.Contains("Completion publication, if ever added by a secure runtime owner, is still not retire publication", phase13);
        Assert.Contains("Guarded `GuestCr0`/`GuestCr4` projection remains field-local read-only", phase13);

        Assert.Contains("ISE-SECCOMP-VMX-BOUNDARY-13", backlog);
        Assert.Contains("SecureCompute remains owned by secure runtime descriptors and policies", backlog);
        Assert.Contains("AllowedSecureOperation` is admission-only", backlog);
        Assert.Contains("AllowedProofOnlyNoExecution` is proof-only/no-execution", backlog);
        Assert.Contains("SecureCompute VMX visibility | denied/proof-only baseline closed by `ISE-SECCOMP-VMX-BOUNDARY-13`", backlog);
        Assert.Contains("SecureCompute backend execution | outside this virtualization path", backlog);
        Assert.Contains("no secure backend execution, completion publication, retire publication, or migration authority is opened", backlog);
    }

    [Fact]
    public void CompilerNoEmissionGate14_ClosesDeniedFutureGateWithoutEmissionAuthority()
    {
        string phase14 = ReadPlan("14_compiler_no_emission_to_controlled_emission_gate.md");
        string backlog = ReadPlan("19_open_decision_backlog.md");

        Assert.Contains("## ISE-COMPILER-NOEMISSION-GATE-14 - Closure Record", phase14);
        Assert.Contains("Closure date: 2026-06-18.", phase14);
        Assert.Contains("closed `DENIED/FUTURE-GATED BASELINE / NO-COMPILER-EMISSION-AUTHORITY`", phase14);
        Assert.Contains("does not approve a controlled-emission RFC/ADR", phase14);
        Assert.Contains("does not make compiler metadata an execution authority", phase14);
        Assert.Contains("does not add VMX/SecureCompute/lane/stream emission authority", phase14);
        Assert.Contains("compiler VMX opcode metadata | diagnostic/raw transport vocabulary only", phase14);
        Assert.Contains("`CompilerHelperEmittable == false`", phase14);
        Assert.Contains("VMCS descriptor sideband | validation-only sideband", phase14);
        Assert.Contains("`CanAttachToExecutableCompilerInstruction == false`", phase14);
        Assert.Contains("virtualization no-emission regression gate | allows generated compatibility projection only", phase14);
        Assert.Contains("direct substrate emission, host-owned evidence, native lane token, and unvalidated descriptor emission are denied", phase14);
        Assert.Contains("`NoEmissionDenied` and `DirectHandlerEmissionDenied` prevent direct VMX handler emission", phase14);
        Assert.Contains("`NoEmissionValidationDenied` proves decode vocabulary is not compiler emission authority", phase14);
        Assert.Contains("`CompilerEmissionAuthorized: false`", phase14);
        Assert.Contains("helper, token, telemetry, replay, or descriptor metadata cannot become VMX/SecureCompute passthrough", phase14);
        Assert.Contains("Compiler metadata, opcode classification, examples, goldens, and generated artifacts are not runtime authority", phase14);
        Assert.Contains("Any future controlled compiler emission requires a separate compiler RFC/ADR", phase14);

        Assert.Contains("ISE-COMPILER-NOEMISSION-GATE-14", backlog);
        Assert.Contains("Compiler VMX metadata remains diagnostic/raw transport vocabulary", backlog);
        Assert.Contains("VMCS sidebands remain validation-only", backlog);
        Assert.Contains("SecureCompute controlled emission keeps `CompilerEmissionAuthorized: false`", backlog);
        Assert.Contains("Compiler controlled emission | denied/future-gated baseline closed by `ISE-COMPILER-NOEMISSION-GATE-14`", backlog);
        Assert.Contains("metadata, examples, sidebands, decode, no-emission validation, and lowering readiness are non-authority", backlog);
    }

    [Fact]
    public void MigrationPayloadAuthority15_ClosesDeniedFutureGateWithoutPayloadAuthority()
    {
        string phase15 = ReadPlan("15_migration_checkpoint_restore_authority_plan.md");
        string backlog = ReadPlan("19_open_decision_backlog.md");

        Assert.Contains("## ISE-MIGRATION-PAYLOAD-AUTHORITY-15 - Closure Record", phase15);
        Assert.Contains("Closure date: 2026-06-18.", phase15);
        Assert.Contains("closed `DENIED/FUTURE-GATED BASELINE / NO-MIGRATION-PAYLOAD-AUTHORITY`", phase15);
        Assert.Contains("does not approve a migration format", phase15);
        Assert.Contains("does not classify an active VMCALL payload", phase15);
        Assert.Contains("does not make VMCS projection metadata, compiler artifacts, SecureCompute proof-only admissions", phase15);
        Assert.Contains("VMCS projection metadata | denied as payload authority", phase15);
        Assert.Contains("`DeniedVmcsProjectionAuthority` / `DeniedCompatibilityProjectionAuthority`", phase15);
        Assert.Contains("compiler artifacts and generated examples | metadata/readiness only", phase15);
        Assert.Contains("SecureCompute proof-only backend owner admission | proof-only/no-execution evidence", phase15);
        Assert.Contains("`AllowedProofOnlyNoExecution` cannot become runtime execution, completion, retire, or migration payload authority", phase15);
        Assert.Contains("lane/stream evidence | denied as host-owned/native evidence", phase15);
        Assert.Contains("scheduler evidence, backend binding evidence, native token evidence, debug traces, telemetry, and replay evidence", phase15);
        Assert.Contains("completion-owned VMREAD projection | recomputed projection only", phase15);
        Assert.Contains("guarded `GuestCr0`/`GuestCr4` projection | owner revalidated after restore", phase15);
        Assert.Contains("`RevalidatedAfterRestore`", phase15);
        Assert.Contains("internal backend result and internal completion record are not checkpoint authority", phase15);
        Assert.Contains("VMCS projection metadata and compatibility projection metadata cannot be migration/checkpoint/restore authority", phase15);
        Assert.Contains("Completion publication, if ever allowed by a neutral owner, still does not imply checkpoint payload authority", phase15);

        Assert.Contains("ISE-MIGRATION-PAYLOAD-AUTHORITY-15", backlog);
        Assert.Contains("VMCS/compatibility projection metadata, compiler artifacts, SecureCompute proof-only admissions", backlog);
        Assert.Contains("lane/stream tokens, telemetry, replay evidence, backend bindings, debug traces", backlog);
        Assert.Contains("`GuestCr0`/`GuestCr4` remain `RevalidatedAfterRestore`", backlog);
        Assert.Contains("Migration payloads for active path | denied/future-gated baseline closed by `ISE-MIGRATION-PAYLOAD-AUTHORITY-15`", backlog);
        Assert.Contains("VMCS projection metadata, compiler artifacts, SecureCompute proof-only, lane/stream evidence, completion projection, and VMREAD output are non-authority", backlog);
    }

    [Fact]
    public void ConformanceGates16_CloseReadinessOnlyWithoutActivationAuthority()
    {
        string phase16 = ReadPlan("16_conformance_negative_positive_test_matrix.md");
        string backlog = ReadPlan("19_open_decision_backlog.md");

        Assert.Contains("## ISE-CONFORMANCE-GATES-16 - Closure Record", phase16);
        Assert.Contains("Closure date: 2026-06-18.", phase16);
        Assert.Contains("closed `READINESS-ONLY / NEGATIVE-MATRIX-CLOSED / POSITIVE-FIXTURES-FUTURE-GATED`", phase16);
        Assert.Contains("does not accept an owner RFC/ADR", phase16);
        Assert.Contains("does not enable positive backend execution tests", phase16);
        Assert.Contains("does not convert a green suite, generated parity fixture, golden artifact, static scan, or positive-looking fixture into activation proof", phase16);
        Assert.Contains("negative VMX/frontend scans | runnable now", phase16);
        Assert.Contains("must keep `MissingNeutralOwner`, `ProjectionOnlyDenied`, no `Allowed`, no `BackendExecutionAuthorized: true`", phase16);
        Assert.Contains("guarded `GuestCr0`/`GuestCr4` positive projection tests | current positive surface only", phase16);
        Assert.Contains("field-local projection only; no mutation, backend, completion, retire, or widening", phase16);
        Assert.Contains("hypercall positive-looking fixtures | future-gated", phase16);
        Assert.Contains("exact numeric leaf remains unresolved", phase16);
        Assert.Contains("SecureCompute proof/conformance fixtures | denial/proof-only", phase16);
        Assert.Contains("`AllowedSecureOperation` and `AllowedProofOnlyNoExecution` are not backend execution or publication", phase16);
        Assert.Contains("generated parity and golden artifacts | conformance evidence only", phase16);
        Assert.Contains("generated or golden evidence is not runtime authority", phase16);
        Assert.Contains("future positive tests | not merged/not enabled by this phase", phase16);
        Assert.Contains("must separately assert backend execution, completion route, completion fence, retire rule, and adjacent denials", phase16);
        Assert.Contains("A broad green suite is not activation approval", phase16);
        Assert.Contains("Positive-looking tests or fixture names are not exact leaf IDs", phase16);
        Assert.Contains("Proof-only, admitted-denied, manifest-only, visibility-only, no-emission, and readiness-only results must remain non-execution semantics", phase16);

        Assert.Contains("ISE-CONFORMANCE-GATES-16", backlog);
        Assert.Contains("Conformance tests, generated parity, static scans, goldens, and positive-looking fixtures remain boundary/readiness evidence only", backlog);
        Assert.Contains("they grant no owner acceptance, exact leaf, backend execution, completion publication, retire publication", backlog);
        Assert.Contains("Release claim wording | future; conformance matrix closed readiness-only by `ISE-CONFORMANCE-GATES-16`", backlog);
        Assert.Contains("green tests/scans/goldens/PR order/release notes remain non-authority", backlog);
    }

    [Fact]
    public void RolloutOrderGate17_ClosesSequencingOnlyWithoutImplementationPermission()
    {
        string phase17 = ReadPlan("17_phase_rollout_and_pr_order.md");
        string backlog = ReadPlan("19_open_decision_backlog.md");

        Assert.Contains("## ISE-ROLLOUT-ORDER-GATE-17 - Closure Record", phase17);
        Assert.Contains("Closure date: 2026-06-18.", phase17);
        Assert.Contains("closed `SEQUENCING-ONLY / READINESS-ONLY / NO-IMPLEMENTATION-PERMISSION`", phase17);
        Assert.Contains("does not accept an owner RFC/ADR", phase17);
        Assert.Contains("does not allocate an exact VMCALL leaf", phase17);
        Assert.Contains("does not convert closed audit records into implementation permission", phase17);
        Assert.Contains("does not permit bundling an RFC draft with backend execution", phase17);
        Assert.Contains("docs/process PRs | may land as readiness documentation", phase17);
        Assert.Contains("negative/static gate PRs | may land before or with future implementation", phase17);
        Assert.Contains("green tests and scans are not implementation permission", phase17);
        Assert.Contains("owner RFC/ADR PR | required before production code", phase17);
        Assert.Contains("draft, silence, handoff, response audit, or closure record is not acceptance", phase17);
        Assert.Contains("backend owner implementation PR | blocked until accepted exact-owner RFC/ADR", phase17);
        Assert.Contains("cannot include unrelated VMREAD widening, VMWRITE, SecureCompute, nested, lane/stream, compiler, or migration payload authority", phase17);
        Assert.Contains("backend success is not completion publication", phase17);
        Assert.Contains("completion publication is not retire publication", phase17);
        Assert.Contains("PR order is sequencing guidance, not authority", phase17);
        Assert.Contains("Closing audits, baselines, handoffs, response checks, conformance gates, or backlog rows is not implementation permission", phase17);
        Assert.Contains("owner first, backend execution second, completion route/fence third, retire last", phase17);

        Assert.Contains("ISE-ROLLOUT-ORDER-GATE-17", backlog);
        Assert.Contains("Rollout order, PR sequencing, closed audits, handoffs, response checks, conformance gates, and backlog rows remain process/readiness evidence only", backlog);
        Assert.Contains("they grant no owner acceptance, exact leaf, backend execution, completion publication, retire publication", backlog);
        Assert.Contains("rollout order closed sequencing-only by `ISE-ROLLOUT-ORDER-GATE-17`", backlog);
        Assert.Contains("green tests/scans/goldens/PR order/release notes remain non-authority", backlog);
    }

    [Fact]
    public void ReleaseGate18_ClosesFinalClaimOnlyWithoutBroadReleaseOrImplementationPermission()
    {
        string phase18 = ReadPlan("18_release_gate_for_limited_runtime_virtualization.md");
        string backlog = ReadPlan("19_open_decision_backlog.md");

        Assert.Contains("## ISE-RELEASE-GATE-18 - Closure Record", phase18);
        Assert.Contains("Closure date: 2026-06-18.", phase18);
        Assert.Contains("closed `READINESS/FINAL-CLAIM GATE / NO-BROAD-RELEASE-CLAIM / NO-IMPLEMENTATION-PERMISSION`", phase18);
        Assert.Contains("does not accept an owner RFC/ADR", phase18);
        Assert.Contains("does not allocate an exact VMCALL leaf", phase18);
        Assert.Contains("approve a broad virtualization release claim", phase18);
        Assert.Contains("closed audits, green tests, static scans, golden artifacts, PR order, rollout sequencing", phase18);
        Assert.Contains("claim-boundary", phase18);
        Assert.Contains("closed audit records and backlog rows | readiness history only", phase18);
        Assert.Contains("no release claim", phase18);
        Assert.Contains("green negative/static tests and scans | regression evidence only", phase18);
        Assert.Contains("rollout/PR order | sequencing evidence only", phase18);
        Assert.Contains("no implementation permission", phase18);
        Assert.Contains("exact owner-specific RFC/ADR | required input, not enough by itself", phase18);
        Assert.Contains("remain gated until chain is complete", phase18);
        Assert.Contains("backend success evidence | one arrow in a larger chain", phase18);
        Assert.Contains("not completion publication", phase18);
        Assert.Contains("completion publication evidence | completion-only evidence", phase18);
        Assert.Contains("not retire publication", phase18);
        Assert.Contains("release notes or marketing wording | claim boundary only", phase18);
        Assert.Contains("no broad VMX/SecureCompute/compiler/lane/stream claim", phase18);
        Assert.Contains("Release review records whether the exact path met the gate; it is not a runtime owner", phase18);
        Assert.Contains("A final claim can only name one exact accepted path and its excluded surfaces", phase18);
        Assert.Contains("Closed audits, Phase 16 conformance closure, Phase 17 rollout order, and this Phase 18 closure remain non-authority evidence", phase18);
        Assert.Contains("Broad claims for VMX, SecureCompute through VMX, nested virtualization, VMWRITE, compiler emission", phase18);

        Assert.Contains("ISE-RELEASE-GATE-18", backlog);
        Assert.Contains("Release review, release notes, final-claim wording, closed audits, green tests/scans/goldens, PR order", backlog);
        Assert.Contains("remain claim-boundary/readiness evidence only", backlog);
        Assert.Contains("they grant no owner acceptance, exact leaf, backend execution, completion publication, retire publication", backlog);
        Assert.Contains("release gate closed final-claim/readiness-only by `ISE-RELEASE-GATE-18`", backlog);
        Assert.Contains("green tests/scans/goldens/PR order/release notes remain non-authority", backlog);
    }

    [Fact]
    public void BacklogDoc_RecordsConfirmedClosuresAndKeepsRemainingItemsFutureGated()
    {
        string backlog = ReadPlan("19_open_decision_backlog.md");

        Assert.Contains("## ISE-BACKLOG-RFC-INTAKE-19 - Closure Record", backlog);
        Assert.Contains("selected as next RFC/ADR intake", backlog);
        Assert.Contains("State: closed `INTAKE-SELECTED / OWNER-ACCEPTANCE-REQUIRED / DENIED-FUTURE-GATED`", backlog);
        Assert.Contains("## Confirmed Closures From Current Audit", backlog);
        Assert.Contains("ISE-VMX-GUARD-01", backlog);
        Assert.Contains("ISE-VMX-GUARD-03", backlog);
        Assert.Contains("ISE-HV-RFC-01", backlog);
        Assert.Contains("ISE-HV-OWNERMAP-02", backlog);
        Assert.Contains("ISE-HV-ADMISSION-03", backlog);
        Assert.Contains("ISE-HV-LEAF-DECISION-04", backlog);
        Assert.Contains("ISE-HV-RFC-OWNER-DECISION-05", backlog);
        Assert.Contains("ISE-VMREAD-DENIED-SURFACES-09", backlog);
        Assert.Contains("ISE-VMWRITE-DENY-BASELINE-10", backlog);
        Assert.Contains("ISE-NESTED-CHILD-INTENT-BASELINE-11", backlog);
        Assert.Contains("ISE-MEM-IO-LANE-STREAM-BOUNDARY-12", backlog);
        Assert.Contains("ISE-SECCOMP-VMX-BOUNDARY-13", backlog);
        Assert.Contains("ISE-COMPILER-NOEMISSION-GATE-14", backlog);
        Assert.Contains("ISE-MIGRATION-PAYLOAD-AUTHORITY-15", backlog);
        Assert.Contains("ISE-CONFORMANCE-GATES-16", backlog);
        Assert.Contains("ISE-ROLLOUT-ORDER-GATE-17", backlog);
        Assert.Contains("ISE-RELEASE-GATE-18", backlog);
        Assert.Contains("ISE-HV-RFC-INTAKE-PACKET-20", backlog);
        Assert.Contains("ISE-HV-OWNER-HANDOFF-PACKET-21", backlog);
        Assert.Contains("ISE-HV-NO-RESPONSE-ROLLBACK-22", backlog);
        Assert.Contains("ISE-HV-REPLACEMENT-INTAKE-DECISION-23", backlog);
        Assert.Contains("ISE-HV-LATE-OWNER-RESPONSE-AUDIT-24", backlog);
        Assert.Contains("ISE-HV-WAIT-STATE-MONITOR-25", backlog);
        Assert.Contains("ISE-HV-OWNER-ARTIFACT-RECHECK-26", backlog);
        Assert.Contains("ISE-HV-WAIT-OR-RESELECT-CHECKPOINT-27", backlog);
        Assert.Contains("ISE-HV-DENIED-CHAIN-REFRESH-28", backlog);
        Assert.Contains("ISE-HV-OWNER-RESPONSE-WATCHDOG-29", backlog);
        Assert.Contains("ISE-HV-FINAL-WAIT-BASELINE-30", backlog);
        Assert.Contains("ISE-HV-RETIRE-PUBLICATION-GATE-09", backlog);
        Assert.Contains("ISE-COMP-ROUTE-01", backlog);
        Assert.Contains("ISE-COMP-FENCE-02", backlog);
        Assert.Contains("draft-only denied skeleton", backlog);
        Assert.Contains("future-gated route scaffolding", backlog);
        Assert.Contains("future-gated neutral fence scaffolding", backlog);
        Assert.Contains("CompletionPublishedRetireDenied", backlog);
        Assert.Contains("docs level", backlog);
        Assert.Contains("exact numeric VMCALL leaf remains `не доказано`", backlog);
        Assert.Contains("No production activation path is approved", backlog);
        Assert.Contains("future-gated", backlog);
        Assert.Contains("Lane6/Lane7/Stream passthrough", backlog);
        Assert.Contains("Compiler controlled emission", backlog);
    }

    [Fact]
    public void BacklogRfcIntake19_SelectsOneIntakeWithoutOwnerAcceptanceOrImplementationPermission()
    {
        string backlog = ReadPlan("19_open_decision_backlog.md");

        Assert.Contains("## ISE-BACKLOG-RFC-INTAKE-19 - Closure Record", backlog);
        Assert.Contains("Closure date: 2026-06-18.", backlog);
        Assert.Contains("closed `INTAKE-SELECTED / OWNER-ACCEPTANCE-REQUIRED / DENIED-FUTURE-GATED`", backlog);
        Assert.Contains("Selected next owner-specific RFC/ADR intake: `Minimal VMCALL backend owner`", backlog);
        Assert.Contains("This selection chooses the next intake queue item only", backlog);
        Assert.Contains("It does not accept an RFC/ADR", backlog);
        Assert.Contains("allocate an exact numeric VMCALL leaf", backlog);
        Assert.Contains("approve `HypercallBackendAdmissionDecision.Allowed`", backlog);
        Assert.Contains("authorize backend execution", backlog);
        Assert.Contains("publish completion", backlog);
        Assert.Contains("publish retire", backlog);
        Assert.Contains("widen `GuestCr0`/`GuestCr4`", backlog);
        Assert.Contains("grant migration, SecureCompute, compiler, lane/stream, nested, I/O, IOMMU", backlog);

        Assert.Contains("Minimal VMCALL backend owner | selected as next RFC/ADR intake", backlog);
        Assert.Contains("attributable neutral-runtime-owner acceptance or rejection with exact numeric leaf", backlog);
        Assert.Contains("argument ABI, value/result source, capability policy, evidence class, migration class, completion rule, retire rule, and adjacent denials", backlog);
        Assert.Contains("all runtime behavior until accepted", backlog);
        Assert.Contains("`GuestCr0`/`GuestCr4` projection widening | not selected", backlog);
        Assert.Contains("VMWRITE | not selected", backlog);
        Assert.Contains("Nested child intent | not selected", backlog);
        Assert.Contains("SecureCompute VMX visibility/backend | not selected", backlog);
        Assert.Contains("Lane6/Lane7/Stream passthrough | not selected", backlog);
        Assert.Contains("Compiler controlled emission | not selected", backlog);
        Assert.Contains("Migration payloads for active path | not selected as standalone activation", backlog);
        Assert.Contains("Release claim wording | not selected as implementation input", backlog);

        Assert.Contains("Intake selection is not owner acceptance", backlog);
        Assert.Contains("Backlog priority is not implementation permission", backlog);
        Assert.Contains("Exact leaf class, opcode, exit reason, owner ID, register selector, SecureCompute fixture value, or VMFUNC leaf is not an exact numeric VMCALL leaf", backlog);
        Assert.Contains("Draft, silence, handoff, no-response audit, blocked baseline, closed conformance gate, rollout order, release gate, or this closure record is not acceptance", backlog);
        Assert.Contains("Phase 06B/Phase 07 production work remains blocked/future-gated", backlog);
        Assert.Contains("Positive-looking readiness evidence cannot replace the full owner map or convert admitted-denied/proof-only semantics into execution", backlog);
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
