# Phase 08 - Subdescriptor Materialization And Completeness

## Goal

Define completeness rules for SecureCompute subdescriptors. An active root descriptor is not enough; enabled secure operations require the relevant subdescriptor to be present, materialized, bound to the neutral domain/address-space context and classified by policy.

## Current Code Baseline

The root descriptor aggregates `SecureMemoryDomainDescriptor`, `DomainMeasurementDescriptor` inputs through admission, `SecureEvidencePolicy`, `SecureMigrationDescriptor`, `SecureIoDomainDescriptor`, `SecureHypercallDescriptor`, `SecureDebugPolicy` and `SecureCompatibilityProjectionPolicy`. Tests already cover missing measurement, missing memory policy, domain tag mismatch, address-space mismatch and disabled/no-effect cases.

## Already Closed / Must Not Reopen

- Missing required subdescriptor denies secure operations.
- Ordinary operations remain no-effect when SecureCompute is absent or disabled.
- Measurement, memory, evidence, migration, I/O, hypercall, debug and compatibility policies are neutral runtime surfaces, not VMX-owned surfaces.
- Subdescriptor completeness must not imply backend execution.

## Required Code/Doc Anchors

- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Descriptors/Memory/SecureMemoryDomainDescriptor.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Descriptors/Measurement/DomainMeasurementDescriptor.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Policies/Evidence/SecureEvidencePolicy.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Descriptors/Migration/SecureMigrationDescriptor.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Descriptors/Io/SecureIoDomainDescriptor.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Descriptors/Hypercalls/SecureHypercallDescriptor.cs`
- `HybridCPU_ISE.Tests/SecureComputeRefactoring/SecureRuntimeBoundaryAdmissionHookTests.cs`

## Work Items

- For each subdescriptor, document materialized, disabled, missing and stale-epoch outcomes.
- Require binding to neutral `DomainTag` and `AddressSpaceTag` where applicable.
- Require policy digests and measurement digests to match descriptor policy.
- Route incomplete subdescriptor behavior to fail-closed, not partial execution.

## Explicit Non-Goals

- No VMX/VMCS/VmxCaps materialization path.
- No compatibility projection as subdescriptor completeness proof.
- No evidence/test-only artifact as materialized policy.
- No backend execution opened by descriptor completeness.

## Done Criteria

- Every subdescriptor has a completeness rule.
- Missing policy denial is documented for secure operations only.
- Disabled/no-effect remains independent from subdescriptor presence.

## Required Tests / Static Checks

- `SecureRuntimeBoundaryAdmissionHookTests`
- `SecureMemoryDomainPolicyTests`
- `SecureMeasurementEvidencePolicyTests`
- `SecureMigrationPolicyTests`
- `SecureIoHypercallPolicyTests`

## Residual Risk

The root descriptor can look complete while a specific operation still lacks required subpolicy. Phase 09 must keep operation-class-specific admission strict.

## Next Phase Dependency

Phase 09 consumes the subdescriptor completeness rules in the runtime admission boundary.
