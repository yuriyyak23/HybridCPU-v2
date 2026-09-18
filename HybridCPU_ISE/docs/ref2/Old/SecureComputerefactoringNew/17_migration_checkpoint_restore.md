# Phase 17 - Migration Checkpoint Restore

## Goal

Define activation-grade checkpoint/restore policy. Migration may carry guest-visible policy/state only when explicitly classified; it must reject host-owned evidence, VMCS metadata, compatibility projection metadata, raw keys, raw measurement secrets and active host pointers.

## Current Code Baseline

`SecureMigrationDescriptor` carries migration mode, private-memory policy, policy epoch, guest-visible evidence allowance, compatibility projection metadata allowance, measurement restore policy and grant restore policy. `SecureCheckpointPayloadPolicy` denies host-owned evidence, scheduler evidence, backend binding evidence, native token evidence, debug traces, VMCS projection metadata, compatibility projection metadata, raw measurement secrets, active host pointers and raw sealing keys. `SecureMigrationAdmissionPolicy` validates epochs, measurement restore and grant restore.

Payload-class policy matrix:

| Payload class | Checkpoint classification | Restore/publication consequence |
|---|---|---|
| `GuestVisibleState` | policy-allowed guest-visible state | may be restored only after migration admission; not completion publication, retire publication or activation evidence |
| `SecurePolicyDescriptor` | policy descriptor state | descriptor metadata only; not runtime authority |
| `SecureSharedMemory` | shared-memory descriptor state | descriptor/range state only; not raw pointer restore |
| `SecurePrivateMemory` | denied unless sealed and encrypted payload contract is complete | storage validation only; not raw sealing key, CHERI sealing or pointer-level authority |
| `HostOwnedEvidence`, `SchedulerEvidence`, `BackendBindingEvidence`, `NativeTokenEvidence` | denied host-owned/recomputed evidence | rebuild after restore; never deserialize or publish as guest/runtime authority |
| `DebugTrace` | denied debug visibility | not guest-visible secure state |
| `VmcsProjectionMetadata` | denied VMCS projection metadata | not SecureCompute checkpoint, restore or VMREAD/VMWRITE authority |
| `CompatibilityProjectionMetadata` | denied compatibility projection metadata | not migration authority or restored guest/runtime state |
| `RawMeasurementSecret` | denied raw secret | never serializable checkpoint payload |
| `ActiveHostPointer` | denied active host pointer | never restored or trusted as portable state |
| `RawSealingKey` | denied raw key | never serializable checkpoint payload |

Restore validation is admission-only. `AdmitRestore` validates policy epoch, measurement revalidation/reattestation, grant provenance and private-memory policy; it does not publish guest-visible evidence, completion publication, retire publication, compatibility projection values or production activation. Host-owned and recomputed evidence must be rebuilt from host runtime state after restore, not deserialized from checkpoint payloads or reused as guest/runtime authority.

## Already Closed / Must Not Reopen

- Host-owned evidence is not guest state.
- VMCS projection metadata is not SecureCompute checkpoint authority.
- Compatibility projection metadata is not migration authority.
- Private secure memory is denied unless sealed/encrypted payload contract is complete.
- Migration-serializable evidence is not guest-visible publication, completion publication, retire publication, runtime authority or production activation evidence.
- Recomputed-after-restore evidence is host-side rebuild input only, not serialized guest/runtime authority.
- Current migration remains fail-closed/policy-defined, not production live migration.

## Required Code/Doc Anchors

- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Descriptors/Migration/SecureMigrationDescriptor.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Checkpoint/SecureCheckpointPayloadPolicy.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Policies/Migration/SecureMigrationAdmissionPolicy.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Plan/06-layer1-secure-migration-plan.md`
- `Documentation/Virtualization WhiteBook/16_Current_State_And_Closure_Matrix.md`
- `HybridCPU_ISE.Tests/SecureComputeRefactoring/SecureMigrationPolicyTests.cs`
- `HybridCPU_ISE.Tests/VmxRefactoring/VmxMigrationEvidenceRecomputedCompatibilityFieldTests.cs`

## Work Items

- Maintain a payload-class table for guest-visible, migration-serializable, recomputed-after-restore and denied classes.
- Require restore to rebuild host-owned evidence instead of deserializing it.
- Require policy epoch, measurement and grant validation before restore admission.
- Keep future tag/provenance migration format in Plan2/RFC quarantine.

## Explicit Non-Goals

- No host-owned evidence serialization.
- No VMCS or compatibility metadata authority.
- No raw sealing key or raw measurement secret payload.
- No active host pointer restore.
- No restore validation as completion publication, retire publication or production activation evidence.
- No compatibility projection value, completion record or host-owned evidence import as checkpoint/restore authority.
- No production live migration claim.

## Done Criteria

- Every secure payload class maps to allowed or denied policy outcome.
- Restore validation rejects stale epoch, stale measurement and stale grant.
- Host evidence rebuild is required before guest-visible publication.
- Phase 21 checklist includes migration/evidence non-leak proof.
- Restore admission is separated from publication and production activation evidence.

## Required Tests / Static Checks

- `SecureMigrationPolicyTests`
- `SecureEvidencePublicationPolicyTests`
- `VmxMigrationEvidenceRecomputedCompatibilityFieldTests`
- `SecureComputePhase10ReleaseGateTests` migration/checkpoint wording guard over `SecureComputerefactoringNew`.
- Source scans for VMCS/compat metadata authority in migration files.
- Source guard proving host-owned evidence, VMCS projection metadata, compatibility projection metadata, raw measurement secrets, active host pointers and raw sealing keys are denied payload classes, not serializable migration authority.
- Source guard proving restore validation does not expose completion publication, retire publication, compatibility projection values, completion records or production activation authority.

## Residual Risk

Guest-visible evidence and migration-serializable evidence can drift together. Keep the classes separate and require explicit migration classification.

## Next Phase Dependency

Phase 18 locks compatibility projection boundaries before release-gate checklist assembly.
