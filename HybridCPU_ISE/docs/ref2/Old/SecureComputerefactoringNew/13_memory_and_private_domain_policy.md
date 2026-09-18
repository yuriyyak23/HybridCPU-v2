# Phase 13 - Memory And Private Domain Policy

## Goal

Keep secure memory policy neutral and runtime-owned. Private, shared, measured and runtime-mutable memory policies are descriptor/policy surfaces; they are not hardware memory tags, CHERI-style memory semantics, VMX EPT/VPID/NPT authority or capability-aware LOAD/STORE/FETCH behavior.

## Current Code Baseline

`SecureMemoryDomainDescriptor` models private/shared/measured/runtime-mutable regions, domain tag, address-space tag, policy epoch and DMA policy. `SecureMemoryAdmissionPolicy` enforces access and DMA rules. `RuntimeBoundaryAdmissionService` checks secure memory binding against the secure domain tag and neutral address-space tag.

## Already Closed / Must Not Reopen

- Private memory denies host reads and raw private DMA.
- Shared memory is allowed only through explicit shared-buffer descriptors, direction, owner, lifetime, evidence and typed grant.
- Measured memory requires measurement admission.
- Runtime-mutable regions require dirty policy and migration classification.
- Private/shared/measured memory descriptors, host-inspection metadata and runtime dirty/migration classes are neutral descriptor/runtime-admission facts, not hardware tags, tagged-memory semantics, CHERI-like ISA/memory semantics, VMX EPT/VPID/NPT authority, VMREAD/VMWRITE authority, migration authority or production activation evidence.
- Private memory descriptors are not hardware tags; shared memory descriptors are not raw pointer admission; measured memory descriptors are not production activation evidence.
- Host-inspection metadata is not VMREAD/VMWRITE authority; runtime-dirty classes and runtime-migration classes are not migration authority by themselves.
- Policy-sealed checkpoint payload contract is migration/storage validation only, not CHERI sealing, sealed capability, pointer-level authority or tag/provenance migration format.
- Ordinary LOAD/STORE/FETCH behavior is unchanged outside secure-domain admission.

## Required Code/Doc Anchors

- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Descriptors/Memory/SecureMemoryDomainDescriptor.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Policies/Memory/SecureMemoryAdmissionPolicy.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Services/RuntimeBoundaryAdmissionService.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Plan/03-layer1-secure-memory-plan.md`
- `HybridCPU_ISE.Tests/SecureComputeRefactoring/SecureMemoryDomainPolicyTests.cs`

## Work Items

- Keep memory policy as Stage B runtime admission.
- Require explicit shared-buffer-only admission for secure I/O/DMA paths.
- Tie measured memory to `DomainMeasurementDescriptor` and policy digest checks.
- Tie runtime-mutable private memory to migration and dirty classification.

## Explicit Non-Goals

- No tagged-memory semantics.
- No hardware memory tags, capability registers, capability-bearing operands, per-pointer provenance or tagged-memory migration format.
- No capability-aware LOAD/STORE/FETCH.
- No VMX EPT/VPID/NPT authority for SecureCompute memory policy.
- No EPT/VPID/NPT, VMREAD or VMWRITE as SecureCompute memory authority.
- No production activation evidence from memory descriptor or policy existence.
- No raw private pointer passage to I/O or hypercall backend.

## Done Criteria

- Private/shared/measured/runtime-mutable classes are documented and tested.
- Secure memory binding uses neutral domain/address-space tags.
- Private memory migration remains sealed/encrypted-contract-only or denied.
- Ordinary memory operations remain no-effect under absent/disabled SecureCompute.

## Required Tests / Static Checks

- `SecureMemoryDomainPolicyTests`
- `SecureRuntimeBoundaryAdmissionHookTests`
- `SecureMigrationPolicyTests`
- `SecureComputePhase10ReleaseGateTests` memory/private-domain wording/source guard over `SecureComputerefactoringNew`, secure memory sources and policy-sealed checkpoint payload sources.
- Source guards for no tagged-memory and no capability-aware LOAD/STORE/FETCH changes.

## Residual Risk

Shared-buffer admission can be overread as raw pointer admission. Phase 14 must keep secure I/O on explicit shared buffers and typed grants only.

## Next Phase Dependency

Phase 14 applies memory policy to secure I/O lane boundaries.
