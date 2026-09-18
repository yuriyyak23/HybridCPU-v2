# Phase 11 - Measurement And Evidence Visibility

## Goal

Define measurement and evidence visibility as policy surfaces, not authority sources. Evidence must be explicitly classified as denied, guest-visible, migration-serializable, compatibility alias, recomputed-after-restore, debug-only or host-owned quarantined.

## Current Code Baseline

`DomainMeasurementDescriptor` binds measurement handle, state, debug class, policy digest, memory digest, runtime digest, evidence class, creator domain tag, parent measurement id and policy source hash. `SecureEvidencePolicy` and `SecureEvidencePublicationPolicy` narrow what may become guest-visible or compatibility-visible. `SecureCompletionPublicationFence` separates completion publication from retire publication.

## Already Closed / Must Not Reopen

- Host-owned evidence remains quarantined.
- Guest-visible evidence is not automatically migration-serializable.
- Guest-visible evidence is visibility only, not runtime authority, VMREAD authority, migration authority, completion publication or activation evidence.
- Compatibility alias evidence requires read-only compatibility projection policy.
- Recomputed-after-restore evidence cannot be reused as old guest-visible state.
- Host-owned and recomputed evidence remain quarantined from migration authority and production activation evidence.
- Evidence cannot activate, grant, materialize, checkpoint, migrate, publish or own SecureCompute authority by itself.

## Required Code/Doc Anchors

- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Descriptors/Measurement/DomainMeasurementDescriptor.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Policies/Measurement/SecureMeasurementAdmissionPolicy.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Policies/Evidence/SecureEvidencePolicy.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Policies/Evidence/SecureEvidencePublicationPolicy.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Publication/SecureCompletionPublicationFence.cs`
- `HybridCPU_ISE.Tests/SecureComputeRefactoring/SecureMeasurementEvidencePolicyTests.cs`
- `HybridCPU_ISE.Tests/SecureComputeRefactoring/SecureEvidencePublicationPolicyTests.cs`

## Work Items

- Maintain an evidence-class table in code/docs for denied, guest-visible, migration-serializable, compatibility alias, recomputed-after-restore, debug-only and host-owned quarantined classes.
- Require secure and neutral evidence policy approval before publication.
- Require migration classification before any migration serialization.
- Keep completion and retire publication distinct from evidence visibility.

## Explicit Non-Goals

- No guest visibility for host-owned evidence.
- No VMCS-visible or VmxCaps-visible secure evidence by default.
- No evidence publication treated as backend success.
- No evidence class as completion publication without an explicit completion fence.
- No migration payload generated only because evidence is guest-visible.

## Done Criteria

- Evidence classes are explicit in docs and tests.
- Host-owned evidence is denied from guest-visible/checkpoint paths.
- Compatibility alias evidence remains projection-only and read-only.
- Phase 16 owns publication semantics after evidence approval.

## Required Tests / Static Checks

- `SecureMeasurementEvidencePolicyTests`
- `SecureEvidencePublicationPolicyTests`
- `SecureMigrationPolicyTests`
- `SecureComputePhase10ReleaseGateTests` measurement/evidence wording/source guard over `SecureComputerefactoringNew` and secure evidence/measurement/checkpoint sources.
- VMX evidence/projection tests where compatibility aliases are involved.

## Residual Risk

Evidence can look like proof of execution. Phase 16 and Phase 20 must require typed backend result and publication fences before any success claim.

## Next Phase Dependency

Phase 12 applies the evidence visibility rules to debug, observability and attestation boundaries.
