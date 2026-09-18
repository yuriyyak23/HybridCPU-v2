# Virtualization Refactoring Plan Index

Status date: 2026-06-05

This corpus replaces the older activation-oriented research plan with a readiness and refactoring plan grounded in the current code, VMX refactoring audits, and the Virtualization, SecureCompute, and Stream whitebooks.

## Source Corpus

- `HybridCPU_ISE/docs/ref2/deep-research-report (6).md`
- `HybridCPU_ISE/docs/VMXRefactoring/2026-05-24-vmx-current-model-completion-audit.md`
- `HybridCPU_ISE/docs/VMXRefactoring/audit3.md`
- `HybridCPU_ISE/docs/VMXRefactoring/audit4.md`
- `HybridCPU_ISE/docs/VMXRefactoring/audit5.md`
- architecture-reviewer Virtualiztion HybridCPU-v2 audit in `HybridCPU_ISE/docs/ref2/Risks/`
- `HybridCPU_ISE/docs/VMXRefactoring/SuccessClosed/`
- `Documentation/Virtualization WhiteBook/00_README.md`
- `Documentation/Virtualization WhiteBook/10_VMCS_Projection_And_Field_Access.md`
- `Documentation/Virtualization WhiteBook/12_Trap_Intercept_Completion_Retire.md`
- `Documentation/Virtualization WhiteBook/16_Current_State_And_Closure_Matrix.md`
- `Documentation/Virtualization WhiteBook/17_Roadmap_And_Residual_Risk.md`
- `Documentation/Virtualization WhiteBook/19_Source_References_And_Check_Commands.md`
- `Documentation/SecureCompute WhiteBook/SecureCompute HybridCPU-v2 WhiteBook.md`
- `Documentation/Stream WhiteBook/DmaStreamCompute/00_README.md`
- `Documentation/Stream WhiteBook/DmaStreamCompute/01_Current_Contract.md`
- `Documentation/Stream WhiteBook/ExternalAccelerators/00_README.md`
- `Documentation/Stream WhiteBook/ExternalAccelerators/01_L7_SDC_Executive_Summary.md`
- `Documentation/Stream WhiteBook/ExternalAccelerators/11_DmaStreamCompute_And_Assist_Separation.md`
- `Documentation/Stream WhiteBook/StreamEngine DmaStreamCompute/00_README.md`
- Live code anchors under `HybridCPU_ISE/CloseToHSL` and `HybridCPU_ISE/NonRTL`
- Focused test anchors under `HybridCPU_ISE.Tests`
- Folder name note: `VirtualiztionRefactoringNew` intentionally matches the repository path spelling.

## Reading Order

1. `00_refactoring_plan_index.md`
2. `01_current_state_inventory_and_authority_map.md`
3. `02_non_regression_baseline_and_guard_rails.md`
4. `03_runtime_boundary_admission_consolidation.md`
5. `04_vmread_field_by_field_projection_plan.md`
6. `05_privileged_execution_state_owner_decision.md`
7. `06_vmcs_write_and_compatibility_control_policy.md`
8. `07_hypercall_backend_owner_and_vmcall_decision.md`
9. `08_trap_completion_route_and_retire_publication.md`
10. `09_nested_virtualization_child_intent_plan.md`
11. `10_memory_io_iommu_lanes_and_stream_boundary.md`
12. `11_capability_evidence_and_securecompute_boundary.md`
13. `12_compiler_isa_runtime_no_emission_contract.md`
14. `13_conformance_golden_artifacts_and_static_gates.md`
15. `14_documentation_migration_and_claim_hygiene.md`
16. `15_final_readiness_review_and_next_work_order.md`
17. `16_external_audit_activation_readiness_addendum.md`

## Dependency Graph

```text
01 inventory
  -> 02 guard rails
  -> 03 runtime admission
  -> 04 VMREAD projection inventory
       -> 05 privileged execution-state decision
       -> 06 VMCS write/control policy
  -> 07 VMCALL backend owner decision
       -> 08 trap completion and retire publication
  -> 09 nested child intent
  -> 10 memory/I/O/lane/stream boundary
  -> 11 capability/evidence/SecureCompute boundary
  -> 12 compiler/ISA/runtime no-emission contract
  -> 13 conformance and static gates
  -> 14 documentation claim hygiene
  -> 15 final readiness review
  -> 16 external audit activation-readiness addendum
```

## Current-State Summary

- VMX is frozen compatibility frontend vocabulary, not the virtualization authority.
- Neutral runtime owners hold authority for domains, capabilities, evidence, memory, I/O, lanes, nested composition, trap policy, completion routing, retire publication, and SecureCompute policy.
- VMCS/VMCSv2 is generated/read-only/denied projection vocabulary, not a mutable state store.
- `VMREAD` currently opens only field-by-field after decode, projection validation, `RuntimeBoundaryAdmissionService`, generated schema lookup, explicit neutral owner value source, and evidence/access policy.
- Current projected VMREAD fields are `ExitReason`, `ExitQualification`, `GuestPhysicalAddress`, `EptViolationQualification`, `GuestCr3`, `EptPointer`, `Vpid`, `Cr3TargetCount`, `GuestPc`, `GuestSp`, and `GuestFlags`.
- Current denied VMREAD fields include `GuestCr0`, `GuestCr4`, `HostPc`, `HostSp`, `HostFlags`, `HostCr0`, `HostCr3`, compatibility-control fields, unknown fields, and all write paths.
- `VMCALL` has neutral trap projection, backend admission, route, and publication fences, but current production flow still uses missing-neutral-owner admission and projection-only denied publication.
- `TrapCompletionRouteDescriptor.RuntimeOwnedPublication` is future-gated behind a real neutral backend owner.
- SecureCompute is a neutral runtime descriptor/admission discipline. It is not VMX mode, secure VMCS, `VmxCaps` authority, CHERI ISA, tagged memory, or current positive secure backend execution.
- Stream/Lane6 and L7 have bounded current contours in the Stream whitebooks and code. They remain runtime/helper/model surfaces for this virtualization plan and do not become VMX authority, VMCS state, or SecureCompute authority.

## External Audit Activation Readiness Update

External audit verdict: GO for continuing the documentation/refactoring readiness corpus; NO-GO for active runtime virtualization. The plan remains a readiness corpus, not an activation approval.

Audit-required blockers now tracked by this corpus:

- every future positive path needs a complete neutral owner map with admission contract, evidence policy, migration class, completion route, retire rule, and negative tests;
- admission must remain separate from backend execution, completion publication, and retire publication;
- VMCALL remains admitted-denied until a neutral hypercall backend owner RFC/ADR exists;
- `TrapCompletionRouteDescriptor.RuntimeOwnedPublication` remains future-gated and must not be used by VMX frontend paths before a real backend owner and publication policy exist;
- VMREAD expansion must be field-by-field and matrix-backed for every generated schema entry;
- `GuestCr0` and `GuestCr4` require a separate neutral privileged execution-state owner;
- SecureCompute claims must stay projection/denial-only unless a secure runtime owner, visibility policy, evidence policy, and migration policy explicitly allow read-only projection;
- broad tests and golden artifacts are proof surfaces, not runtime authority.
- Phases 14, 15, and 16 are closed as documentation/readiness/test/static-gate work only. No immediate ISE CPU production-code task remains in this closure corpus; future production work requires a new owner-specific RFC/ADR.

## Full File List

- `00_refactoring_plan_index.md`
- `01_current_state_inventory_and_authority_map.md`
- `02_non_regression_baseline_and_guard_rails.md`
- `03_runtime_boundary_admission_consolidation.md`
- `04_vmread_field_by_field_projection_plan.md`
- `05_privileged_execution_state_owner_decision.md`
- `06_vmcs_write_and_compatibility_control_policy.md`
- `07_hypercall_backend_owner_and_vmcall_decision.md`
- `08_trap_completion_route_and_retire_publication.md`
- `09_nested_virtualization_child_intent_plan.md`
- `10_memory_io_iommu_lanes_and_stream_boundary.md`
- `11_capability_evidence_and_securecompute_boundary.md`
- `12_compiler_isa_runtime_no_emission_contract.md`
- `13_conformance_golden_artifacts_and_static_gates.md`
- `14_documentation_migration_and_claim_hygiene.md`
- `15_final_readiness_review_and_next_work_order.md`
- `16_external_audit_activation_readiness_addendum.md`

## Global Forbidden Regressions

- Do not restore `VmxExecutionUnit`, `VmcsManager`, `IVmcsManager`, `VmcsManagerAdapter`, `VmxRuntimeManager`, `VmcsProjectionRuntimeManager`, or `VmcsV2RuntimeManager`.
- Do not introduce an active VMCS pointer, VMCS field store, mutable VMCS cache, or VMX-owned runtime manager.
- Do not reopen legacy VMX backend authority.
- Do not treat VMREAD admission, VMCALL trap projection, completion projection, telemetry, evidence, conformance, or tests as runtime authority.
- Do not map `GuestCr0`, `GuestCr4`, host aliases, or compatibility controls from existing guest-only views.
- Do not use VMX, VMCS, `VmxCaps`, or compatibility aliases to activate, grant, materialize, migrate, checkpoint, or own SecureCompute.
- Do not use Stream, DmaStreamCompute, assist, L7-SDC, telemetry, tokens, replay evidence, or diagnostics as virtualization authority.
- Do not use test-only behavior as production requirement.

## Classification Rules

- `implemented`: production code and tests exist, the owner is neutral, and the docs identify the authority boundary.
- `projection-only`: compatibility vocabulary can expose a read-only/generated value after runtime admission, but does not own state or mutation.
- `denied`: the current correct behavior is explicit denial or fail-closed result with a named reason.
- `model/helper-only`: helper, token, telemetry, parser, diagnostic, or whitebook model surface exists, but does not authorize virtualization runtime behavior.
- `future-gated`: the shape is known, but an owner, policy, evidence class, route, publication rule, or conformance gate is missing.
- `forbidden`: the item would regress a closed authority boundary and must not be reintroduced.

## Shared Code Anchors

- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Services/RuntimeBoundaryAdmissionService.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Virtualization/Compatibility/Frontend/Handlers/VmxCompatibilityAdmissionService.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Virtualization/Compatibility/Frontend/Handlers/VmxCompatibilityAdmissionService.Traps.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Virtualization/Compatibility/Frontend/Projection/VmcsRead/VmcsReadOnlyValueProjectionService.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Virtualization/Compatibility/Generated/VmcsProjection/VmcsFieldProjectionSchema.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/Descriptors/ExecutionDomain/ExecutionDomainReadOnlyStateView.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Memory/Translation/MemoryDomainReadOnlyTranslationView.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Capabilities/CompatibilityControls/CompatibilityControlDescriptor.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Events/Hypercalls/HypercallBackendAdmissionPolicy.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Completion/Routing/TrapCompletionRoutePolicy.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Completion/Records/TrapCompletionPublicationFence.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Descriptors/Domain/SecureComputeDomainDescriptor.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Virtualization/SecureCompute/Compatibility/Projection/SecureComputeCompatibilityBoundaryMatrixPolicy.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Pipeline/MicroOps/Lane6DmaStream/DmaStreamComputeMicroOp.cs`
- `HybridCPU_ISE/NonRTL/Core/Execution/DmaStreamCompute/DmaStreamComputeDescriptorParser.cs`
- `HybridCPU_ISE/NonRTL/Core/Execution/DmaStreamCompute/DmaStreamComputeRuntime.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Pipeline/MicroOps/Lane7Accelerator/SystemDeviceCommandMicroOp.cs`

## Validation Expectation

After editing, run the requested forbidden-name, overclaim, anchor-presence, and `git diff --check` scans from the task prompt. Forbidden-name hits are acceptable only when the surrounding text classifies them as absent, forbidden, must-not-return, or static-check vocabulary. Overclaim hits should be zero.
