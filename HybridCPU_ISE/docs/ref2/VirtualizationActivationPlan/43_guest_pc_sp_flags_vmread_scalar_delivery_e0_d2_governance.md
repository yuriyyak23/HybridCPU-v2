# Phase 43 — GuestPc/GuestSp/GuestFlags VMREAD scalar-delivery E0/D2 governance

Status: immutable E0/SpecV2 and a later non-self-referential AcceptanceRecordV2 are materialized; production composition remains unauthorized.

## 2026-06-11 Audit Contract

- File name: `43_guest_pc_sp_flags_vmread_scalar_delivery_e0_d2_governance.md`.
- Purpose: freeze the exact owner-specific E0 and D2 governance package without production composition.
- Status: immutable spec and later non-self-referential acceptance are machine-materialized; runtime delivery is unauthorized.
- Scope: exact `GuestPc`/`GuestSp`/`GuestFlags` scalar delivery policy only.
- No-goals: no receipt implementation, runtime activation, adjacent field, backend, completion, VMWRITE or compiler change.
- Code anchors: `Phase43GuestPcSpFlagsVmReadScalarDeliveryE0Contract.cs`, `Phase43GuestPcSpFlagsVmReadScalarDeliveryDecisionSpecV2.cs`, `ExecutionDomainDescriptor.cs`, `ExecutionDomainReadOnlyStateView.cs`.
- Authority owner: existing neutral `ExecutionDomainDescriptor`; governance and VMCS metadata are not authority.
- Required RFC/ADR: the separate exact decision recorded here; earlier GuestCr decisions remain unchanged.
- Acceptance criteria: exact identity/fields/owner/source/evidence/domain/ABI/migration/rollback constraints with no side authority.
- Tests/static scans: Phase 43 spec, acceptance, provenance, negative and forbidden-shortcut tests plus the full VMX matrix.
- Risks: treating acceptance as runtime admission or issuing a receipt without atomic source-epoch binding.
- Next-gate dependency: separately authorized production composition after this clean accepted D2; no field group opens automatically.

The full owner-map columns are: field/operation, owner, value source, capability policy, evidence class, migration class, denial reason.

## Exact scope

- DecisionId: `D2-HV-VMREAD-SCALAR-DELIVERY-V1-GUEST-PC-SP-FLAGS-0002`.
- Namespace: `HybridCPU.VMREAD.ScalarDelivery.v1`.
- Operation: `DELIVER_GUEST_PC_SP_FLAGS_SCALAR_V1`.
- Fields: only `GuestPc` (0), `GuestSp` (1), and `GuestFlags` (2).
- Owner: existing neutral `ExecutionDomainDescriptor`.
- Values: only the corresponding materialized members of `ExecutionDomainReadOnlyStateView`.
- Result/effect: `ScalarU64ToDestinationRegister` / `ArchitecturalRegisterResultOnly`.
- Capability: `None`; VMCALL grants and capability bit 41 are unrelated.
- Migration: `DrainOnly`; receipts are not serialized.
- Activation: default disabled; rollback is `DisableBindingAndInvalidateOutstandingReceipts`.

## E0 result

The canonical ingress is `VmxCompatibilityAdmissionService.AdmitVmReadProjection`. Decode and frozen VMCS metadata remain compatibility concerns. Runtime admission remains the existing `ReadCompatibilityProjection` operation with `FullDomainRuntime`, capability `None`, and guest-visible compatibility evidence. Field projection then requires guest-architectural visibility and a materialized field in the immutable read-only execution-domain view. No VMCS scalar or backing-store fallback exists.

The existing projection carries `ExecutionDomainReadOnlyStateView.StateEpoch`, but it does not compare that value with a separately supplied current epoch. Therefore any later production composition must atomically bind and revalidate descriptor/view identity, source epoch, runtime domain/address space, restore generation, replay identity, profile generation, exact D2/field/value and destination before issuing an opaque single-use receipt. This governance record does not fill that gap and grants no runtime authority.

Future speculative delivery is restricted to canonical PRF/rename/writeback. Architectural commit is restricted to `RetireRecord.RegisterWrite` through the canonical `RetireCoordinator`. Squash before retire has zero architectural effect. Replay, squash, restore, source-epoch replacement, disable, revocation, or profile-generation change invalidate outstanding receipts.

## Explicit denials

No direct architectural write, VMCS/VMX writeback authority, `VmxRetireEffect` authority, VMCALL D2/O1/E2–E7 or E5/E6 reuse, trap/backend completion, VMWRITE, GuestCr semantic expansion, memory-owned fields, host aliases, `HostCr3`, compatibility controls, SecureCompute/nested/memory/IOMMU/I/O/device/lane/stream or compiler expansion is authorized.

The immutable SpecV2 is committed at `46fc02c26e54aea5d5a905e953c490340b1e338a`, tree `3df2dd90c043e44d30bf4d5ed42113d41814703f`, digest `e67ff2620ff6a1fd193b8303c5b6ae1d532e51241e6b3e405f3c6cedefe2d754`. The later AcceptanceRecordV2 binds those exact bytes with digest `1bc7ccf0df814538ff531572fa9e8872e61f081dd0fae25dfc4337d50d94b96a` and deliberately does not name its own containing commit. Its containing commit is `cf0a634f94e5d13c67cc4499635b66994abd57d9`, tree `c9b1f0b7ad07bc121c238db7be8e95228a8d3fab`; that provenance is recorded only by later verification. Production composition requires a separate subsequent bounded pool.
