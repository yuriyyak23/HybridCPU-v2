# Phase 19 - Nested Domain Design Fence

## Goal

Preserve nested SecureCompute as a design fence only. Nested child intent, parent/child monotonicity and nested checkpoint policy can be validated, but no nested SecureCompute backend execution or mutable nested secure state is opened by this plan.

## Current Code Baseline

`SecureChildDomainIntentDescriptor` records parent and child domain tags, requested security level, requested authority bounds, derivation record and intent state. `SecureNestedDomainAdmissionPolicy` returns `AllowedNoEffect` or `AllowedDesignFence`, and denies missing child intent owner, missing parent descriptor, stale epoch, child policy expansion, host evidence leakage, projection expansion, VMCS12/VMCS02 authority and mutable Shadow VMCS authority.

## Already Closed / Must Not Reopen

- Nested path is design-fence only.
- Nested child intent is not nested backend execution authority.
- Parent-child monotonicity is proof of bounded policy only, not permission to open mutable nested secure state.
- Nested checkpoint is not migration authority, nested backend execution, VMCS12/VMCS02 authority or mutable Shadow VMCS authority.
- `SecureNestedDomainAdmissionPolicy`, `AllowedDesignFence`, `AllowedNoEffect`, child intent descriptors and nested projection/checkpoint services are design-fence/no-effect/admission surfaces, not implementation of nested SecureCompute execution.
- Child policy cannot widen parent authority.
- Host evidence cannot leak parent-to-child or child-to-parent.
- Shadow VMCS, VMCS12 and VMCS02 remain compatibility structures, not nested secure authority.
- Activation of non-nested SecureCompute does not imply nested readiness.
- Nested evidence, telemetry and checkpoint facts are not guest/runtime authority, nested backend execution or production activation evidence.

## Required Code/Doc Anchors

- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Descriptors/Nested/SecureChildDomainIntentDescriptor.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Policies/Nested/SecureNestedDomainAdmissionPolicy.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Plan/10-nested-secure-domain-plan.md`
- `HybridCPU_ISE.Tests/SecureComputeRefactoring/SecureNestedDomainDesignFenceTests.cs`
- `HybridCPU_ISE.Tests/VmxRefactoring/SecureComputeVmxPhase9NestedFenceTests.cs`

## Work Items

- Keep nested admission result names non-executional.
- Require neutral child-intent owner for any future nested work.
- Keep nested compatibility projection within parent policy.
- Keep nested checkpoint payload authority denied for VMCS12/VMCS02 and mutable Shadow VMCS.
- Keep nested evidence/telemetry/checkpoint facts quarantined as facts only; they cannot be imported as guest/runtime authority or production activation evidence.

## Explicit Non-Goals

- No nested SecureCompute backend execution.
- No mutable nested secure runtime state.
- No Shadow VMCS authority.
- No VMCS12/VMCS02 authority payload.
- No nested checkpoint as migration authority.
- No nested readiness implied by general activation.

## Done Criteria

- Nested tests prove design-fence status.
- Child authority is monotonic and bounded by parent.
- Nested projection and migration cannot widen parent policy.
- Phase 21 requires separate nested RFC/ADR before nested execution.

## Required Tests / Static Checks

- `SecureNestedDomainDesignFenceTests`
- `SecureComputeVmxPhase9NestedFenceTests`
- VMX nested composition evidence tests.
- `SecureComputePhase10ReleaseGateTests` nested design-fence wording/source guard over `SecureComputerefactoringNew` and neutral nested/Shadow VMCS sources.
- Source scans for VMCS12/VMCS02/Shadow VMCS authority in secure nested files.

## Residual Risk

General activation language can accidentally imply nested activation. Phase 21 must explicitly exclude nested execution unless a separate nested plan exists.

## Next Phase Dependency

Phase 20 defines the only permitted path toward positive secure backend runtime execution, still behind RFC/ADR.
