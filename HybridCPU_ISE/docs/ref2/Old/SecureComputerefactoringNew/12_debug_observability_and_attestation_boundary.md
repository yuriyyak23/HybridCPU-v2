# Phase 12 - Debug Observability And Attestation Boundary

## Goal

Keep debug, observability and attestation surfaces separate from runtime authority. These surfaces may expose approved facts only through explicit visibility policy, never through VMCS storage, VmxCaps grants, VMREAD mutation, test-only data or host-owned evidence leakage.

## Current Code Baseline

SecureCompute has `SecureDebugPolicy`, `SecureEvidencePolicy`, `DomainMeasurementDescriptor` and evidence publication policies. The WhiteBook prefers neutral debug/attestation API placement for future secure visibility aliases. VMX schema aliases are denied by default unless a neutral owner, read-only source, secure visibility policy, migration classification and conformance proof exist.

## Already Closed / Must Not Reopen

- Host-owned evidence remains quarantined from guest-visible debug paths.
- Debug traces are denied as migration guest state by `SecureCheckpointPayloadPolicy`.
- Debug traces and attestation facts are visibility surfaces, not runtime authority, VMREAD authority, migration authority or production activation evidence.
- Compatibility projection cannot become attestation authority.
- VMREAD of secure-sensitive fields remains denied unless the full neutral proof chain exists.

## Required Code/Doc Anchors

- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Policies/HostInspection/`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Policies/Evidence/SecureEvidencePolicy.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Policies/Evidence/SecureEvidencePublicationPolicy.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Checkpoint/SecureCheckpointPayloadPolicy.cs`
- `Documentation/SecureCompute WhiteBook/SecureCompute HybridCPU-v2 WhiteBook.md`
- `HybridCPU_ISE.Tests/SecureComputeRefactoring/SecureEvidencePublicationPolicyTests.cs`
- `HybridCPU_ISE.Tests/VmxRefactoring/SecureComputeVmxPhase8BoundaryMatrixTests.cs`

## Work Items

- Prefer a separate neutral debug/attestation API for future secure visibility aliases.
- Require explicit evidence class and migration class for any future debug/attestation exposure.
- Keep VMX aliases denied by default and read-only if ever admitted.
- Keep debug traces out of checkpoint and migration payload authority.

## Explicit Non-Goals

- No VMCS field as secure attestation state.
- No VmxCaps bit as secure visibility grant.
- No debug-only evidence as runtime authority.
- No debug trace, attestation output or test telemetry as completion publication or activation evidence.
- No test telemetry as attestation owner.

## Done Criteria

- Debug/observability paths are classified as denied, debug-only, guest-visible or host-owned quarantined.
- Any compatibility alias is read-only, migration-classified and conformance-proven.
- Release gate rejects product claims based only on debug/attestation output.

## Required Tests / Static Checks

- Evidence publication tests.
- Secure checkpoint payload tests for debug trace denial.
- VMX Phase 8 read visibility matrix tests.
- `SecureComputePhase10ReleaseGateTests` debug/attestation wording/source guard over `SecureComputerefactoringNew` and secure evidence/measurement/checkpoint sources.
- Source scans for VMCS/VmxCaps authority in secure evidence/debug files.

## Residual Risk

Debug/attestation APIs often become accidental product proof. Keep them classified as visibility surfaces, not activation proof.

## Next Phase Dependency

Phase 13 applies evidence and debug visibility rules to private/shared/measured memory policy.
