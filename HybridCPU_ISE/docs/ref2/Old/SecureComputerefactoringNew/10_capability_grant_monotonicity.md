# Phase 10 - Capability Grant Monotonicity

## Goal

Keep Layer 2 as a runtime descriptor/grant discipline based on provenance, bounds, epochs, revocation and monotonic derivation. It must not become a new ISA, tagged-memory model, guest scalar authority or compatibility projection shortcut.

## Current Code Baseline

`SecureGrantHandle` is an opaque runtime handle. `SecureGrantAuthorityPolicy` validates materialization source, provenance, authority bounds, epoch set, runtime owner materialization, capability scope and compatibility projection scope. `SecureAuthorityBounds` provides subset checks. `SecurePolicyDerivationRecord` binds parent/child policy digests, derivation rule and epoch. Tests deny guest scalar materialization and compatibility authority.

## Already Closed / Must Not Reopen

- Guest scalar values cannot materialize `SecureGrantHandle`.
- Compatibility projection cannot satisfy secure grant authority.
- VMX, VMCS, VMREAD, VMWRITE and VmxCaps are not grant sources.
- Nested child policy cannot widen parent authority.

## Required Code/Doc Anchors

- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Authority/SecureGrantHandle.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Authority/SecureGrantAuthorityPolicy.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Authority/SecureAuthorityBounds.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Authority/SecurePolicyDerivationRecord.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Plan/08-layer2-cheri-like-authority-discipline-plan.md`
- `HybridCPU_ISE.Tests/SecureComputeRefactoring/SecureAuthorityDisciplineTests.cs`

## Work Items

- Keep grant source list restricted to neutral runtime owner materialization paths.
- Require epoch checks for grants used by memory, I/O, hypercall, migration and nested flows.
- Require monotonic child derivation for nested descriptors.
- Ensure grants are evidence for admission, not backend execution by themselves.

## Explicit Non-Goals

- No CHERI-style ISA capability model in current phases.
- No guest scalar grant materialization.
- No VmxCaps or VMREAD grant alias.
- No grant-only activation without descriptor, evidence, completion and retire policy.

## Done Criteria

- Grant authority is bound to provenance, bounds and current epoch.
- Child grants cannot widen parent authority.
- Compatibility projection cannot satisfy secure authority.
- Grant validation appears as a prerequisite in Phase 20, not an execution result.

## Required Tests / Static Checks

- `SecureAuthorityDisciplineTests`
- `SecureNestedDomainDesignFenceTests`
- `SecureIoHypercallPolicyTests`
- Source scans proving authority files do not import VMX/VMCS/VmxCaps authority.

## Residual Risk

Wording such as "unforgeable" can be overread as hardware capability semantics. Documentation must say runtime descriptor handle, not pointer capability.

## Next Phase Dependency

Phase 11 uses the grant discipline to classify measurement and evidence visibility.
