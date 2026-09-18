# Phase 03 - Existing Baseline Inventory

## Goal

Create an activation-oriented inventory of what is already implemented, what is shell/no-effect, what is fail-closed, what is proof-only or admitted-denied, and what remains future. This is not a reimplementation task.

## Current Code Baseline

Implemented or bounded baseline anchors include:

- `SecureComputeDomainDescriptor` with `Disabled`, `None -> Disabled`, `IsActive` and `IsNoEffect`.
- Optional `DomainRuntimeContext.SecureCompute`, `DomainTag` and `AddressSpaceTag`.
- `RuntimeBoundaryAdmissionService` secure hook for non-ordinary `SecureDomainOperationClass`.
- `SecureMemoryAdmissionPolicy`, `SecureEvidencePolicy`, `SecureMigrationAdmissionPolicy`, `SecureCheckpointPayloadPolicy`.
- `SecureIoHypercallAdmissionPolicy` with `AllowedAdmittedDenied` and `DeniedBackendSuccessClosed`.
- `SecureBackendOwnerAdmissionPolicy` with `AllowedProofOnlyNoExecution`.
- VMX deny/projection matrix and no-emission contract.

## Already Closed / Must Not Reopen

- Phase 1 no-effect descriptor baseline.
- Phase 2 Stage B secure admission hook and neutral binding checks.
- Phase 3 secure memory fail-closed baseline.
- Phase 4/4.5 measurement/evidence positive policy admission without production attestation/transport.
- Phase 5 migration fail-closed policy.
- Phase 6 secure I/O shared-buffer admission and admitted-denied hypercall path.
- Phase 7 runtime descriptor/grant discipline.
- Phase 8 VMX deny/projection matrix.
- Phase 9 nested design fence.
- Phase 10 release gate and Post-Phase10 proof-only owner gate.

## Required Code/Doc Anchors

- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Plan/12-phasing-and-pr-breakdown.md`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Plan/11-test-and-conformance-master-plan.md`
- `Documentation/SecureCompute WhiteBook/SecureCompute HybridCPU-v2 WhiteBook.md`
- `HybridCPU_ISE.Tests/SecureComputeRefactoring/`
- `HybridCPU_ISE.Tests/VmxRefactoring/SecureComputeVmxPhase8BoundaryMatrixTests.cs`
- `HybridCPU_ISE.Tests/VmxRefactoring/SecureComputeVmxPhase10ReleaseGateTests.cs`

## Work Items

- Use this inventory as a checklist for every phase.
- Do not duplicate old Plan files; link to them as baseline evidence.
- Mark all current backend-owner success as closed only by proof-only denial.
- Mark secure hypercall as admitted-denied, not backend success.

## Explicit Non-Goals

- No conversion of Plan2 backlog items into implementation tasks.
- No broadening of fail-closed policy into allowed execution.
- No migration from evidence/test proof into runtime authority.

## Done Criteria

- Baseline classes are mapped to closure taxonomy.
- The inventory names every major code/test anchor required by later phases.
- Future work is isolated to Phase 20 or Plan2.

## Required Tests / Static Checks

- `rg -n "AllowedProofOnlyNoExecution|AllowedAdmittedDenied|DeniedBackendSuccessClosed" HybridCPU_ISE.Tests HybridCPU_ISE/CloseToHSL/Core`
- `rg -n "Phase 10|Post-Phase10|Plan2" HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Plan HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Plan2`

## Residual Risk

An inventory can age quickly. Each future code PR must update the inventory classification before claiming a phase moved from proof-only/admitted-denied to implemented.

## Next Phase Dependency

Phase 04 defines the activation gate using this inventory as its negative baseline.
