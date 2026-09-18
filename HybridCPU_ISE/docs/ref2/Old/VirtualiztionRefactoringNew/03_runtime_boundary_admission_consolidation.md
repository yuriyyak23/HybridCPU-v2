# Phase 03 - Runtime Boundary Admission Consolidation

## Goal

Confirm that every current and future VMX-compatible path enters through a neutral runtime admission boundary before owner lookup, value projection, backend decision, completion route, or publication.

## Activation Authority

This phase creates review material only. It does not authorize runtime activation, VMX backend execution, VMCS mutation, SecureCompute backend execution, or production publication behavior.

## Current Code Baseline

- `VmxCompatibilityAdmissionService.AdmitVmReadProjection(...)` calls `RuntimeBoundaryAdmissionService.Validate(...)` with `DomainRuntimeOperationKind.ReadCompatibilityProjection`.
- `VmxCompatibilityAdmissionService.AdmitVmCallTrapProjection(...)` calls `RuntimeBoundaryAdmissionService.Validate(...)` with `DomainRuntimeOperationKind.ProjectCompatibilityTrap`.
- `RuntimeBoundaryAdmissionService` joins domain boundary, capability boundary, evidence boundary, frontend mutation denial, optional SecureCompute admission, and root authority validation.
- Compatibility frontend authoritative mutation is rejected by `RuntimeBoundaryAdmissionService`.

## Already Closed / Must Not Reopen

- VMX decode/projection validation cannot bypass runtime admission.
- Compatibility aliases cannot directly mutate execution, memory, I/O, capability, evidence, migration, SecureCompute, completion, or retire state.
- Admission is not backend execution.
- Admission is not publication.

## Required Code/Doc Anchors

- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Services/RuntimeBoundaryAdmissionService.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Virtualization/Compatibility/Frontend/Handlers/VmxCompatibilityAdmissionService.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Virtualization/Compatibility/Frontend/Handlers/VmxCompatibilityAdmissionService.Traps.cs`
- `Documentation/Virtualization WhiteBook/12_Trap_Intercept_Completion_Retire.md`
- `Documentation/SecureCompute WhiteBook/SecureCompute HybridCPU-v2 WhiteBook.md`

## Work Items

- Enumerate current VMX-compatible ingress paths and their runtime operation kind.
- Record required `DomainBoundaryDescriptor`, capability requirement, and evidence requirement per path.
- Define a future-path template requiring decode, projection validation, runtime admission, owner lookup, policy decision, and publication fence.
- Specify that new surfaces must add denial tests before any positive path can be considered.
- Document the SecureCompute hook as opt-in for secure operation classes and no-effect for ordinary operations.

## Explicit Non-Goals

- Do not create a new runtime owner.
- Do not replace `RuntimeBoundaryAdmissionService`.
- Do not collapse runtime admission and backend admission into one step.
- Do not let VMX vocabulary become neutral runtime vocabulary.

## Done Criteria

- Every current VMX-compatible path has a documented runtime operation kind.
- Every future VMX-compatible path has an admission checklist.
- Denial by missing context, boundary, capability, evidence, frontend mutation, and runtime authority remains explicit.
- SecureCompute admission is described as neutral Stage B/runtime policy, not VMX mode.

## Required Tests / Static Checks

- Focused search for `DomainRuntimeOperationKind.ReadCompatibilityProjection`.
- Focused search for `DomainRuntimeOperationKind.ProjectCompatibilityTrap`.
- Existing runtime-boundary admission tests.
- Existing SecureCompute runtime-boundary hook tests.

## Residual Risk

The main risk is a future shortcut that treats successful admission as enough to publish completion or retire effects. That shortcut remains forbidden.

## External Audit Risk Update

Runtime admission must remain a precondition, not a permission. No VMX-compatible path may move from admission to completion record or retire effects without separate backend owner authorization, route authorization, `TrapCompletionPublicationFence`, and explicit retire publication permission.

## Next Phase Dependency

Phase 04 depends on the admission chain to classify VMREAD fields owner-by-owner.

