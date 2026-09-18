# SecureCompute Refactoring Plan Index

## Status Date

Status date: 2026-06-03.

This directory is the updated SecureCompute phased refactoring and activation-readiness plan for the live HybridCPU ISE codebase. It updates `HybridCPU_ISE/docs/ref2/deep-research-report (7).md` into explicit phase files under the required `SecureComputerefactoringNew` path.

This plan does not reopen the existing `Plan/00-13` closure history and does not convert `Plan2/14-securecompute-open-decision-backlog.md` into an implementation phase.

## Source Corpus

- `HybridCPU_ISE/docs/ref2/deep-research-report (7).md`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Plan/`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Plan2/14-securecompute-open-decision-backlog.md`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Plan2/SecureCompute RFC HybridCPU-v2.md`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Docs/`
- `Documentation/SecureCompute WhiteBook/SecureCompute HybridCPU-v2 WhiteBook.md`
- `Documentation/Virtualization WhiteBook/00_README.md`
- `Documentation/Virtualization WhiteBook/15_Security_Invariants.md`
- `Documentation/Virtualization WhiteBook/16_Current_State_And_Closure_Matrix.md`
- `Documentation/Virtualization WhiteBook/17_Roadmap_And_Residual_Risk.md`
- `HybridCPU_ISE/docs/VMXRefactoring/audit3.md`
- `HybridCPU_ISE/docs/VMXRefactoring/audit4.md`
- `HybridCPU_ISE/docs/VMXRefactoring/audit5.md`
- `HybridCPU_ISE/docs/VMXRefactoring/ОСНОВЫ и ПРАВИЛА VMX.md`
- `HybridCPU_ISE.Tests/SecureComputeRefactoring/`
- `HybridCPU_ISE.Tests/VmxRefactoring/SecureComputeVmxPhase8BoundaryMatrixTests.cs`
- `HybridCPU_ISE.Tests/VmxRefactoring/SecureComputeVmxPhase9NestedFenceTests.cs`
- `HybridCPU_ISE.Tests/VmxRefactoring/SecureComputeVmxPhase10ReleaseGateTests.cs`
- `HybridCPU_ISE.Tests/VmxRefactoring/SecureComputeVmxDenialGuardTests.cs`
- `HybridCPU_ISE/docs/ref2/Risks/Архитектурный-ревизор SecureCompute HybridCPU-v2.md`

## Reading Order

Read `00_securecompute_refactoring_plan_index.md` first, then phases `01` through `21` in numeric order. The dependency order is intentional:

- Phases `01-04` define scope, invariants, inventory and activation.
- Phases `05-06` preserve no-effect and no-emission baselines.
- Phases `07-10` cover descriptor materialization, admission and grant authority.
- Phases `11-17` cover evidence, debug, memory, I/O, hypercall, publication and migration.
- Phases `18-19` lock VMX compatibility and nested design fences.
- Phases `20-21` keep positive secure backend runtime execution behind RFC/ADR and release-gate proof.
- Phase `22` imports the external architecture audit into a risk/readiness matrix without changing the execution closure state.

## Dependency Graph

```text
01 -> 02 -> 03 -> 04
04 -> 05 -> 06
04 -> 07 -> 08 -> 09 -> 10
09 -> 11 -> 12
09 -> 13 -> 14 -> 15 -> 16 -> 17
02 -> 18
10 -> 19
16 -> 20 -> 21
18 -> 21
17 -> 21
21 -> 22
```

## Current-State Summary

The live codebase already has a bounded SecureCompute baseline:

- `SecureComputeDomainDescriptor` is the neutral root descriptor; `SecureComputeSecurityLevel.None` normalizes to `Disabled`; absent, disabled and unmaterialized states are no-effect for ordinary operations.
- `DomainRuntimeContext` can carry optional `SecureCompute`, `DomainTag` and `AddressSpaceTag` values.
- `RuntimeBoundaryAdmissionService` in `HybridCPU_ISE/CloseToHSL/Core/Runtime/Services/RuntimeBoundaryAdmissionService.cs` owns Stage B runtime admission and invokes secure checks only for non-ordinary `SecureDomainOperationClass` values with enabled descriptors.
- `SecureMemoryDomainDescriptor`, `SecureMemoryAdmissionPolicy`, `DomainMeasurementDescriptor`, `SecureEvidencePolicy`, `SecureMigrationDescriptor`, `SecureIoDomainDescriptor` and `SecureHypercallDescriptor` exist as neutral runtime policy surfaces.
- `SecureGrantHandle`, `SecureGrantAuthorityPolicy`, `SecureAuthorityBounds` and `SecurePolicyDerivationRecord` implement Layer 2 descriptor/grant discipline, not a new ISA.
- `SecureComputeCompatibilityBoundary`, `SecureComputeCompatibilityBoundaryMatrixPolicy`, `SecureComputeVmReadVisibilityPolicy`, `SecureComputeVmWriteDenyPolicy` and `SecureComputeNoEmissionContract` enforce VMX/VMCS/VmxCaps and compiler/ISA boundaries.
- `SecureBackendOwnerAdmissionPolicy` can return `AllowedProofOnlyNoExecution`; it denies `RequestsBackendExecution` and keeps backend execution closed.
- `SecureIoHypercallAdmissionPolicy` can return `AllowedAdmittedDenied` while `BackendExecutionAuthorized`, `CompletionPublicationAuthorized` and `RetirePublicationAuthorized` remain false; admitted-denied recognition is not backend success or publication authority.
- `SecureBackendOwnerAdmissionPolicy` can accept an approved proof chain only as `AllowedProofOnlyNoExecution`; RFC/ADR approval, `ProofChainAccepted`, owner materialization and Phase 20 subphase labels are not typed execution, completion record, retire publication, nested execution or activation evidence.
- VMREAD schema entries, including `GuestCr0`, `GuestCr4` and compatibility-control fields, are not current readable values by themselves. Generated schema ownership is separate from neutral value source admission.
- Nested child intent, parent-child monotonicity, nested projection/checkpoint services and nested evidence/telemetry/checkpoint facts are design-fence/admission facts only, not nested execution, mutable nested secure state, guest/runtime authority or production activation evidence.
- Lane6/Stream/L7 bounded execution contours may exist outside SecureCompute, but they are not SecureCompute, VMX or virtualization authority.

## Full File List

- `00_securecompute_refactoring_plan_index.md`
- `01_current_state_and_scope.md`
- `02_architecture_invariants_and_closure_taxonomy.md`
- `03_existing_baseline_inventory.md`
- `04_activation_gate_definition.md`
- `05_no_effect_and_disabled_equivalence.md`
- `06_compiler_isa_vliw_no_emission_boundary.md`
- `07_domain_descriptor_materialization.md`
- `08_subdescriptor_materialization_and_completeness.md`
- `09_runtime_admission_boundary.md`
- `10_capability_grant_monotonicity.md`
- `11_measurement_and_evidence_visibility.md`
- `12_debug_observability_and_attestation_boundary.md`
- `13_memory_and_private_domain_policy.md`
- `14_io_lane_boundary.md`
- `15_hypercall_and_trap_policy.md`
- `16_completion_and_retire_publication.md`
- `17_migration_checkpoint_restore.md`
- `18_vmx_compatibility_deny_projection.md`
- `19_nested_domain_design_fence.md`
- `20_positive_runtime_execution_rfc_gate.md`
- `21_release_gate_and_activation_checklist.md`
- `22_external_audit_risks_and_readiness_matrix.md`

## Global Forbidden Regressions

- Do not move SecureCompute authority into VMX, VMCS, VMREAD, VMWRITE or VmxCaps.
- Do not add a VMX mode for SecureCompute or a VMCS-backed SecureCompute state store.
- Do not add CHERI-like instruction semantics, hardware memory tags, or capability-aware LOAD/STORE/FETCH semantics in this plan.
- Do not treat documentation closure, proof-only admission, shell/no-effect descriptors, VMX compatibility projection, tests, telemetry or evidence as runtime authority.
- Do not claim SecureCompute activation unless a real neutral backend owner, execution semantics, capability/evidence policy, completion fence and retire publication path exist.
- Do not read `AllowedProofOnlyNoExecution` or `AllowedAdmittedDenied` as backend success.
- Do not treat Phase 20 RFC/ADR approval, `ProofChainAccepted`, owner descriptors, typed-execution vocabulary, completion-record vocabulary or retire-publication vocabulary as secure backend execution, nested execution, completion/retire publication or production activation evidence.
- Do not serialize host-owned evidence, VMCS metadata, compatibility projection metadata, raw keys, raw measurement secrets or active host pointers as SecureCompute migration authority.
- Do not treat generated schema `ReadOnly` entries as currently readable values without neutral value owner, policy and tests.
- Do not treat `TrapCompletionRouteDescriptor`, `TrapCompletionRouteService`, `TrapCompletionPublicationFence`, route classes or publication classes as permission without runtime admission, neutral trap/backend authorization and retire rule approval.
- Do not use Stream/Lane6/L7 bounded contours as SecureCompute, VMX or virtualization authority.
- Do not treat nested child intent, parent-child monotonicity, nested checkpoint, VMCS12/VMCS02, Shadow VMCS, nested evidence, telemetry or checkpoint facts as nested backend execution, mutable nested secure state, migration/checkpoint authority, guest/runtime authority or production activation evidence.
- Do not silently normalize actual repository directory names. If an external audit mentions `VirtualiztionRefactoringNew`, preserve that factual spelling for that corpus; this SecureCompute corpus remains `SecureComputerefactoringNew`.

## Closure Taxonomy

- `implemented`: production code exists for a bounded, named behavior and has matching tests.
- `shell`: type or API surface exists but does not authorize effects.
- `no-effect`: absent/disabled/unmaterialized descriptor preserves ordinary behavior.
- `fail-closed`: missing owner, missing policy, stale epoch, bad evidence, private pointer or compatibility authority is denied.
- `proof-only`: a proof chain may be accepted as policy evidence, but no backend execution or publication follows.
- `admitted-denied`: a path is recognized and intentionally denied before backend success.
- `design-fence`: future direction is fenced by neutral descriptors/tests but no execution path is opened.
- `release-gate`: source/doc/test guard prevents forbidden product claims or authority regressions.
- `future`: tracked in Plan2 or a future RFC/ADR, not in current runtime activation.

## How To Classify Work

Classify a topic as `implemented` only when the code path has a neutral owner, runtime semantics, tests and no forbidden authority shortcut. Classify descriptor existence without effects as `shell`. Classify absent/disabled behavior as `no-effect`. Classify denied missing-owner or missing-policy behavior as `fail-closed`. Classify `AllowedProofOnlyNoExecution` as `proof-only`. Classify `AllowedAdmittedDenied` as `admitted-denied`. Classify nested or VMX projection directions without runtime execution as `design-fence`. Classify doc-lint/source guards as `release-gate`. Classify positive secure backend runtime execution as `future` until Phase 20 and Phase 21 criteria are met by real code and tests.
