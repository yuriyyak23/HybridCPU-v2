# Phase 07 - Domain Descriptor Materialization

## Goal

Define activation-safe materialization for the root neutral secure-domain descriptor. A `SecureComputeDomainDescriptor` is active only when enabled and materialized with a neutral domain binding; descriptor existence alone is not activation.

## Current Code Baseline

`SecureComputeDomainDescriptor` stores `DomainTag`, `SecurityLevel`, measurement/private-memory requirements, host inspection policy, evidence policy, migration policy, I/O policy, hypercall policy, debug policy and compatibility projection policy. `IsMaterialized` requires `DomainTag != 0`; `IsActive` requires enabled security level and materialization.

`DomainRuntimeContext` carries neutral `DomainTag` and `AddressSpaceTag`. `RuntimeBoundaryAdmissionService` denies enabled secure operations when the secure descriptor does not match the neutral runtime domain tag.

## Already Closed / Must Not Reopen

- `SecureComputeDomainDescriptor` remains the root neutral descriptor.
- Materialization must not come from VMX, VMCS, VMREAD, VMWRITE, VmxCaps, test evidence or telemetry.
- Unmaterialized enabled descriptor must fail closed for secure operations.
- Ordinary operations remain no-effect.

## Required Code/Doc Anchors

- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Descriptors/Domain/SecureComputeDomainDescriptor.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/Services/DomainRuntimeContext.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Services/RuntimeBoundaryAdmissionService.cs`
- `HybridCPU_ISE.Tests/SecureComputeRefactoring/SecureComputeDomainDescriptorNoEffectTests.cs`
- `HybridCPU_ISE.Tests/SecureComputeRefactoring/SecureRuntimeBoundaryAdmissionHookTests.cs`

## Work Items

- Document the descriptor materialization source as neutral runtime owner only.
- Require `DomainTag` binding proof before secure-domain admission.
- Keep descriptor materialization separate from backend owner materialization in Phase 20.
- Add this phase to activation release checklist.

## Explicit Non-Goals

- No VMX-derived materialization.
- No VMCS field as secure domain identity.
- No VmxCaps bit as descriptor materialization.
- No positive backend execution based only on active descriptor state.

## Done Criteria

- Active descriptor definition is unambiguous.
- Enabled-unmaterialized secure operation denial is preserved.
- Domain tag mismatch denial is preserved.
- Descriptor materialization is a prerequisite, not activation completion.

## Required Tests / Static Checks

- `RuntimeBoundaryAdmission_EnabledUnmaterializedDescriptorFailsClosedForSecureOperation`
- `RuntimeBoundaryAdmission_ActiveDescriptorMustMatchNeutralRuntimeDomainTag`
- VMX denial guard tests proving compatibility surfaces cannot materialize the descriptor.

## Residual Risk

Future compatibility projection work might try to expose materialization state as authority. Phase 18 must keep any projection read-only and non-authoritative.

## Next Phase Dependency

Phase 08 covers subdescriptor completeness after the root descriptor materialization rules are fixed.
