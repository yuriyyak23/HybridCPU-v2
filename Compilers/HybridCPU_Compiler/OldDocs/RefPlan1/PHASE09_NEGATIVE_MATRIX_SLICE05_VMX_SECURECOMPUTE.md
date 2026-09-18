# Phase 09 Negative Matrix Slice 05 - VMX and SecureCompute

Status: implemented as focused compiler-core negative gates.

## Scope

This slice freezes VMX and SecureCompute no-emission boundaries before caller migration:

- VMX backend emission request rejects as `VmxBackendEmissionForbidden`;
- VMX remains projection/no-emission and cannot own VMCS state;
- SecureCompute backend execution request rejects as `SecureComputeEmissionForbidden`;
- SecureCompute remains policy/admission/evidence-only and cannot emit secure backend execution;
- VMX/SecureCompute negative evidence remains host/compiler evidence only;
- no carrier, descriptor, bridge or production package is prepared for these backend requests.

## Tests

Added `CompilerPhase09VmxSecureComputeNegativeMatrixTests`:

- `VmxBackendEmissionRequestRejectsWithNoEmissionAndNoVmcsOwnership`
- `SecureComputeBackendExecutionRequestRejectsWithPolicyAdmissionEvidenceOnly`
- `ProjectionAndAdmissionEvidenceSnapshotsRemainHostOwnedEvidenceOnly`
- `VmxAndSecureComputeDoNotPrepareCarrierDescriptorBridgeOrProductionPackage`

## Authority Notes

- VMX compatibility projection is not backend execution and not VMCS ownership.
- SecureCompute admission is not secure backend execution.
- Evidence snapshots use `EvidenceOnly` semantics with forbidden fallback.
- Negative decisions produce no carrier, descriptor, typed-slot facts, runtime bridge input or production lowering claim.

## Historical Next Task

The fallback/evidence cleanup matrix listed here has since been implemented.
For the current open compiler work, use:

- `PHASE09_REMAINING_COMPILER_WORK.md`
- `CONTINUATION_PROMPT_PHASE09.md`
