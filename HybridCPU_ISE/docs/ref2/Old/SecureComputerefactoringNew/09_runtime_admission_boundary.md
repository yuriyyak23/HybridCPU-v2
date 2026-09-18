# Phase 09 - Runtime Admission Boundary

## Goal

Make Stage B runtime admission the only SecureCompute decision boundary. Secure checks must be performed through neutral runtime descriptors and policies, not through Stage A legality, VMX compatibility frontend, VMCS fields, VmxCaps bits, telemetry or tests.

## Current Code Baseline

`RuntimeBoundaryAdmissionService` validates domain boundary, typed capability grant, evidence policy, frontend mutation denial and runtime authority. It then runs secure admission only for non-ordinary `SecureDomainOperationClass` values with an enabled secure descriptor. It checks root descriptor materialization, neutral domain tag binding, secure memory address-space binding and optional secure memory access policy.

## Already Closed / Must Not Reopen

- Secure checks belong to Stage B/runtime admission, not Stage A.
- Compatibility frontends cannot directly mutate authoritative runtime state.
- Secure memory policy runs only after opt-in secure-domain operation classification.
- Ordinary operations are not over-denied by active descriptors.

## Required Code/Doc Anchors

- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Services/RuntimeBoundaryAdmissionService.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Admission/SecureDomainAdmissionService.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Policies/Memory/SecureMemoryAdmissionPolicy.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/Services/DomainRuntimeContext.cs`
- `HybridCPU_ISE.Tests/SecureComputeRefactoring/SecureRuntimeBoundaryAdmissionHookTests.cs`
- `HybridCPU_ISE.Tests/VmxRefactoring/RuntimeBoundaryAdmissionTests.cs`

## Work Items

- Declare `RuntimeBoundaryAdmissionService` the canonical activation boundary.
- Prohibit alternate authority paths through VMX, VMCS, VmxCaps, direct evidence publication or migration payload.
- Require secure operation classification for any secure policy evaluation.
- Ensure future backend execution in Phase 20 enters through this admission boundary.

## Explicit Non-Goals

- No direct backend owner bypass.
- No secure operation classification inferred from VMX opcode names.
- No Stage A decoder rejection of ordinary code because SecureCompute exists.
- No compatibility projection write mutation.

## Done Criteria

- All secure operation paths name a neutral runtime owner and admission request.
- Missing owner, missing policy, stale epoch and forbidden authority paths deny.
- Positive secure backend runtime execution remains blocked until Phase 20.

## Required Tests / Static Checks

- `SecureRuntimeBoundaryAdmissionHookTests`
- `RuntimeBoundaryAdmissionTests`
- Source scan for SecureCompute imports in Stage A decoder/projector files.
- VMX tests proving compatibility frontend authoritative mutation is denied.

## Residual Risk

Future helper APIs may call policy objects directly and bypass runtime admission. Phase 21 must require source guards for direct-bypass patterns before activation.

## Next Phase Dependency

Phase 10 defines the grant authority discipline consumed by runtime admission.
