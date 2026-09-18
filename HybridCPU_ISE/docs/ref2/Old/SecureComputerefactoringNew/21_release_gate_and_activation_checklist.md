# Phase 21 - Release Gate And Activation Checklist

## Goal

Provide the final release gate and activation checklist. SecureCompute activation may be claimed only when every checklist item is backed by code, tests, docs and negative conformance. If any item remains docs-only, shell/no-effect, fail-closed, proof-only, admitted-denied, design-fence or future, activation remains blocked.

## Current Code Baseline

Current baseline is strong but not activation-complete. It has no-effect descriptor semantics, Stage B admission, fail-closed memory/migration/evidence policies, admitted-denied hypercall policy, Layer 2 grant discipline, VMX deny/projection matrix, nested design fence, release gates and proof-only backend owner gate. It does not have positive secure backend runtime execution.

External audit verdict to preserve in release wording:

```text
NOT AN ACTIVATION PLAN.
NOT A SECURECOMPUTE PRODUCTION-READY CLAIM.
NOT A VMX BACKEND IMPLEMENTATION WORK ORDER.
```

## Already Closed / Must Not Reopen

- No-effect/disabled equivalence.
- Stage B runtime admission boundary.
- Secure memory and migration fail-closed policy.
- Evidence visibility and host-owned quarantine.
- I/O shared-buffer-only admission.
- Hypercall admitted-denied backend closure.
- Layer 2 runtime descriptor/grant discipline.
- VMX compatibility deny/projection matrix.
- Nested design fence.
- Phase 10 and Post-Phase10 release/proof gates.

## Required Code/Doc Anchors

- All files in `HybridCPU_ISE/docs/ref2/SecureComputerefactoringNew/`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Plan/11-test-and-conformance-master-plan.md`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Plan/12-phasing-and-pr-breakdown.md`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Plan2/14-securecompute-open-decision-backlog.md`
- `Documentation/SecureCompute WhiteBook/SecureCompute HybridCPU-v2 WhiteBook.md`
- `HybridCPU_ISE/docs/ref2/Risks/Архитектурный-ревизор SecureCompute HybridCPU-v2.md`
- `HybridCPU_ISE/docs/ref2/SecureComputerefactoringNew/22_external_audit_risks_and_readiness_matrix.md`
- `HybridCPU_ISE.Tests/SecureComputeRefactoring/SecureComputePhase10ReleaseGateTests.cs`
- `HybridCPU_ISE.Tests/VmxRefactoring/SecureComputeVmxPhase10ReleaseGateTests.cs`

## Work Items

- Require the following activation checklist:
  - no-effect theorem for absent/disabled/ordinary operations;
  - no compiler/ISA/VLIW emission change;
  - materialized root descriptor and required subdescriptors;
  - runtime admission through `RuntimeBoundaryAdmissionService`;
  - monotonic grant validation with neutral owner and current epoch;
  - measurement/evidence visibility classification;
  - host-owned evidence quarantine;
  - private/shared/measured memory policy;
  - secure I/O explicit shared-buffer admission only;
  - hypercall/trap states split between recognition, backend execution, completion and retire;
  - migration payload rejection of host/VMCS/compat/raw-secret/active-pointer authority;
  - migration/checkpoint restore policy keeps guest-visible state, migration-serializable evidence, recomputed-after-restore host evidence and compatibility projection values separated; restore validation does not serialize, import or publish host-owned evidence, VMCS projection metadata, compatibility projection metadata, raw measurement secrets, raw sealing keys, active host pointers, completion records or compatibility projection values as guest/runtime authority;
  - nested child intent, parent-child monotonicity and nested checkpoint remain design-fence evidence only;
  - `SecureNestedDomainAdmissionPolicy`, `AllowedDesignFence`, `AllowedNoEffect`, child intent descriptors and nested projection/checkpoint services remain design-fence/no-effect/admission surfaces, not implementation of nested SecureCompute execution;
  - VMX/VMCS/VmxCaps deny/projection proof;
  - nested execution excluded unless separately approved;
  - nested evidence, telemetry and checkpoint facts remain quarantined from guest/runtime authority, nested backend execution and production activation evidence;
  - Phase 20 RFC/ADR, neutral backend owner, typed execution model, capability/evidence policy, completion fence and retire publication implemented;
  - Phase 20 RFC/ADR approval, `ProofChainAccepted`, owner descriptor materialization and subphase labels (`20A`-`20E`) remain insufficient for typed execution request/result, backend execution, completion record, retire publication, nested execution or production activation until implemented in code with tests;
  - negative conformance for every denied authority shortcut.
- Add audit-derived blockers:
  - schema entry does not equal current readable VMREAD value;
  - `GuestCr0`/`GuestCr4` remain denied until neutral privileged execution-state owner RFC and implementation;
  - compatibility-control fields remain denied until neutral frozen value contract exists;
  - all VMCS writes remain denied;
  - `VmcsFieldProjectionSchema` entry presence, schema `ReadOnly`/`CanRead`, generated aliases and `VmcsReadOnlyValueProjectionService` presence do not equal current readable VMREAD values or SecureCompute authority;
  - a neutral privileged execution-state owner RFC does not open `GuestCr0`/`GuestCr4` until owner semantics, value source, visibility policy, migration classification and conformance tests are implemented;
  - route/fence classes do not equal publication permission;
  - `RuntimeOwnedPublication` is blocked before real neutral backend owner;
  - completion publication, retire publication, route authorization, completion records and production activation remain separate states and do not substitute for each other;
  - Stream/Lane6/L7 bounded contours do not become SecureCompute or virtualization authority;
  - secure I/O shared-buffer descriptors and hypercall shared-buffer arguments do not become raw pointer admission, host/device pointer authority, VMX/VMCALL authority, backend execution proof or production activation evidence;
  - `AllowedAdmittedDenied`, VMCALL decode/projection, `TrapDecision`, `VmExitReason.VmCall`, route descriptors and publication fences do not become backend execution, completion publication, retire publication or production activation evidence;
  - nested child intent, parent-child monotonicity and nested checkpoint do not become nested backend execution, mutable nested secure state, VMCS12/VMCS02/Shadow VMCS authority or migration authority;
  - nested evidence/telemetry/checkpoint facts do not become guest/runtime authority, nested backend execution, mutable nested secure state or production activation evidence;
  - `SecureBackendOwnerRfcGate`, `AllowedProofOnlyNoExecution` and proof-only/no-effect evidence do not become secure backend execution, completion publication, retire publication or production activation claims;
  - approved RFC/ADR state, `ProofChainAccepted`, `SecureBackendOwnerDescriptor`, Phase 20 subphase labels, typed-execution vocabulary, completion-record vocabulary and retire-publication vocabulary do not become backend execution, nested execution, completion/retire publication or production activation evidence;
  - measurement/debug/attestation visibility does not become runtime authority, VMREAD authority, migration authority, completion publication or production activation evidence;
  - memory/private-domain descriptors, host-inspection metadata, policy-sealed checkpoint payload contracts and runtime dirty/migration classes do not become hardware tags, CHERI-like ISA/memory semantics, VMX EPT/VPID/NPT authority, VMREAD/VMWRITE authority, migration authority or production activation evidence;
  - private descriptors do not become hardware tags, shared descriptors do not become raw pointer admission, measured descriptors do not become production activation evidence, and policy-sealed checkpoint payload contracts do not become CHERI sealing, sealed capabilities, pointer-level authority or tag/provenance migration format;
  - EPT/VPID/NPT/VMX artifacts and VMREAD schema/value projections do not become SecureCompute memory authority;
  - repo-relative paths are normalized for executable checks.
- Add release-gate wording that blocks any product claim while a checklist item is open.
- Keep Plan2 open decisions quarantined.

## Explicit Non-Goals

- No activation by checklist text alone.
- No production claim based on tests, telemetry, evidence or documentation without matching runtime code.
- No secure backend success before Phase 20 implementation.
- No nested SecureCompute execution under general activation.
- No VMX/VMCS/VmxCaps authority exception.
- No plan-derived work order for VMCALL backend, VMCS writes or SecureCompute activation.

## Done Criteria

- Every checklist item has a code/doc/test anchor.
- Every forbidden regression has a negative test or source guard.
- Positive secure backend runtime execution is either implemented through Phase 20 or explicitly blocked.
- Release-gate tests fail on product claims not backed by the checklist.
- Audit risk matrix is present and referenced.

## Required Tests / Static Checks

- `rg -n "production-ready SecureCompute|feature-complete SecureCompute|VMX activates SecureCompute|VMX owns SecureCompute|VmxCaps grants SecureCompute|secure VMCS|CHERI ISA|tagged memory|capability-aware LOAD|capability-aware STORE|capability-aware FETCH" HybridCPU_ISE/docs/ref2/SecureComputerefactoringNew --glob "*.md"`
- `rg -n "SecureComputeDomainDescriptor|RuntimeBoundaryAdmissionService|SecureMemoryDomainDescriptor|SecureEvidencePolicy|SecureMigrationDescriptor|SecureIoHypercallAdmissionPolicy|SecureGrantAuthorityPolicy|SecureComputeCompatibilityBoundaryMatrixPolicy|SecureBackendOwnerRfcGate|AllowedProofOnlyNoExecution" HybridCPU_ISE/docs/ref2/SecureComputerefactoringNew --glob "*.md"`
- `rg -n "positive secure backend|backend execution|completion publication|retire publication|AllowedAdmittedDenied|proof-only" HybridCPU_ISE/docs/ref2/SecureComputerefactoringNew --glob "*.md"`
- `git diff --check -- HybridCPU_ISE/docs/ref2/SecureComputerefactoringNew`
- `rg -n "GuestCr0|GuestCr4|VmcsFieldProjectionSchema|VmcsReadOnlyValueProjectionService|CompatibilityControlDescriptor|TrapCompletionRoute|TrapCompletionPublicationFence|DmaStreamComputeDescriptorParser|ExecutionEnabled" HybridCPU_ISE/docs/ref2/SecureComputerefactoringNew --glob "*.md"`
- `SecureComputePhase10ReleaseGateTests` route/fence wording guard for `RuntimeOwnedPublication`, `TrapCompletionRouteDescriptor`, `TrapCompletionRouteService` and `TrapCompletionPublicationFence` over the `SecureComputerefactoringNew` corpus.
- `SecureComputePhase10ReleaseGateTests` publication-matrix wording guard for proof-only owner, admitted-denied hypercall, VMCALL projection, internal backend result, completion fence, retire fence, completion records and production activation over the `SecureComputerefactoringNew` corpus.
- `SecureComputePhase10ReleaseGateTests` VMREAD/schema wording guard for `GuestCr0`, `GuestCr4`, `VmcsFieldProjectionSchema`, `VmcsReadOnlyValueProjectionService` and `CompatibilityControlDescriptor` over the `SecureComputerefactoringNew` corpus.
- `VmxControlLikeVmReadDenialTests` guard proving `GuestCr0`/`GuestCr4` generated read-only schema entries and `VmcsReadOnlyValueProjectionService` presence still return denied current-value projection.
- `SecureComputePhase10ReleaseGateTests` Stream/Lane6/Lane7 wording guard for `DmaStreamComputeDescriptorParser.ExecutionEnabled` and bounded contours over the `SecureComputerefactoringNew` corpus.
- `SecureComputePhase10ReleaseGateTests` secure I/O wording/source guard for shared-buffer descriptors, hypercall shared-buffer arguments, raw pointer denial, VMX/VMCALL denial and backend execution denial over the `SecureComputerefactoringNew` corpus and secure I/O/memory sources.
- `SecureComputePhase10ReleaseGateTests` hypercall/trap recognition wording/source guard for `AllowedAdmittedDenied`, VMCALL decode/projection, `TrapDecision`, `VmExitReason.VmCall`, route descriptors and publication fences over the `SecureComputerefactoringNew` corpus and hypercall/trap admission sources.
- `SecureComputePhase10ReleaseGateTests` migration/checkpoint wording guard for host-owned evidence, VMCS projection metadata, compatibility projection metadata, raw measurement secrets, active host pointers and raw sealing keys over the `SecureComputerefactoringNew` corpus.
- `SecureComputePhase10ReleaseGateTests` restore/publication wording guard proving restore validation and migration-serializable evidence do not become guest/runtime authority, completion publication, retire publication, compatibility projection value publication or production activation evidence.
- `SecureComputePhase10ReleaseGateTests` nested design-fence wording/source guard for child intent, parent-child monotonicity, nested checkpoint, VMCS12/VMCS02 and Shadow VMCS over the `SecureComputerefactoringNew` corpus and neutral nested sources.
- `SecureComputePhase10ReleaseGateTests` backend-owner proof wording/source guard for `SecureBackendOwnerRfcGate`, `AllowedProofOnlyNoExecution`, proof-only/no-effect evidence and backend-owner wording over the `SecureComputerefactoringNew` corpus and backend owner/hypercall sources.
- `SecureComputePhase10ReleaseGateTests` measurement/debug/attestation wording/source guard for guest-visible evidence, debug traces, attestation facts, host-owned evidence and recomputed evidence over the `SecureComputerefactoringNew` corpus and secure evidence/measurement/checkpoint sources.
- `SecureComputePhase10ReleaseGateTests` memory/private-domain wording/source guard for private/shared/measured descriptors, host-inspection metadata, policy-sealed checkpoint payload contracts and runtime dirty/migration classes over the `SecureComputerefactoringNew` corpus, secure memory sources and migration-sealed payload sources.
- `SecureMigrationPolicyTests` source guard proving forbidden checkpoint payload classes remain denied and non-serializable.

## Residual Risk

The plan remains non-activating. Activation remains blocked until future code proves neutral backend execution semantics, publication semantics and conformance. The immediate admitted-denied publication-bit ambiguity is hardened in code/tests; the remaining risk is future wording or route/fence infrastructure being overread as permission.

The external audit adds another high-signal risk: generated/read-only projection infrastructure and bounded Stream/Lane6/L7 execution can be overclaimed as runtime authority. The release gate must treat those as evidence surfaces or separate bounded contours only.

## Next Phase Dependency

No automatic next phase opens from this file. The next valid step is either documentation release-gate tests for this directory or a separate Phase 20 RFC/ADR implementation proposal.
