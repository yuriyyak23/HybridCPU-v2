# Phase 05 - No-Effect And Disabled Equivalence

## Goal

Make absent, disabled, `None`, unmaterialized and ordinary-operation SecureCompute behavior a cross-cutting no-effect theorem. SecureCompute must be opt-in and must not change ordinary execution when absent or disabled.

## Current Code Baseline

`SecureComputeDomainDescriptor` normalizes `SecureComputeSecurityLevel.None` to `Disabled`. `IsActive` requires both enabled security level and materialized `DomainTag`; `IsNoEffect` is true when not active. `RuntimeBoundaryAdmissionService` invokes secure admission only when `SecureOperationClass` is not `Ordinary` and the descriptor is enabled.

`SecureComputeDomainDescriptorNoEffectTests` and `SecureRuntimeBoundaryAdmissionHookTests` cover absent, disabled, `None`, ordinary operation and unmaterialized fail-closed cases.

## Already Closed / Must Not Reopen

- `None` and `Disabled` must not diverge.
- Absent descriptor must preserve ordinary runtime behavior.
- Active descriptor must not over-deny ordinary operations.
- Stage A decoder/projector sources must not import SecureCompute policy.

## Required Code/Doc Anchors

- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Descriptors/Domain/SecureComputeDomainDescriptor.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/Services/DomainRuntimeContext.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Services/RuntimeBoundaryAdmissionService.cs`
- `HybridCPU_ISE.Tests/SecureComputeRefactoring/SecureComputeDomainDescriptorNoEffectTests.cs`
- `HybridCPU_ISE.Tests/SecureComputeRefactoring/SecureRuntimeBoundaryAdmissionHookTests.cs`

## Work Items

- Treat no-effect as a required invariant for runtime, compiler, VMX compatibility and migration phases.
- Keep enabled-but-unmaterialized descriptors fail-closed only for secure-domain operations.
- Add cross-links from no-effect tests to Phase 21 release checklist.
- Require any new SecureCompute descriptor to document disabled/no-effect behavior.

## Explicit Non-Goals

- No ordinary-operation denial caused only by SecureCompute absence.
- No default activation.
- No hidden secure checks in Stage A.
- No VMX compatibility bit that changes disabled/no-effect behavior.

## Done Criteria

- Every later phase states whether disabled/absent SecureCompute is no-effect.
- Test anchors cover absent, disabled, `None`, ordinary operation and enabled-unmaterialized denial.
- No-effect is represented as a release-gate prerequisite, not a best-effort note.

## Required Tests / Static Checks

- `SecureComputeDomainDescriptorNoEffectTests`
- `SecureRuntimeBoundaryAdmissionHookTests`
- Source scan proving Stage A decoder/projector sources do not require SecureCompute policy.

## Residual Risk

Future policy objects may accidentally run on ordinary operations. Runtime admission tests must remain focused on the `SecureDomainOperationClass.Ordinary` bypass.

## Next Phase Dependency

Phase 06 applies the same no-effect discipline to compiler, ISA and VLIW emission boundaries.
