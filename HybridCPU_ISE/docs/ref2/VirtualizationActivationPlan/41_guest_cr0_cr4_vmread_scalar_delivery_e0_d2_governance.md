# Phase 41 — GuestCr0/GuestCr4 VMREAD scalar-delivery E0/D2 governance

Status: E0/D2 governance closed; production scalar delivery remains unimplemented and unauthorized by this record alone.

## 2026-06-11 Audit Contract

- File name: `41_guest_cr0_cr4_vmread_scalar_delivery_e0_d2_governance.md`.
- Purpose: define the exact scalar-delivery E0/D2 governance package without changing the earlier read-only projection decision.
- Status: immutable spec and later non-self-referential acceptance are materialized; runtime composition is not implemented here.
- Scope: exact `GuestCr0`/`GuestCr4` scalar delivery to one architectural destination through canonical speculative and retire contours.
- No-goals: no new state owner, VMCS value source, VMWRITE, direct architectural write, backend/trap completion shortcut, VMCALL reuse or adjacent-field expansion.
- Code anchors: `Phase41VmReadScalarDeliveryE0Contract.cs`, `Phase41VmReadScalarDeliveryDecisionSpecV2.cs`, `VmReadScalarDeliveryDecisionValidatorV2.cs`, `PrivilegedExecutionStateOwnerPolicy.cs`, `RetireCoordinator.cs`.
- Authority owner: existing neutral `PrivilegedExecutionStateDescriptor` / `PrivilegedExecutionStateOwnerPolicy`; governance and compatibility metadata remain non-authoritative.
- Required RFC/ADR: this separate exact scalar-delivery decision; the Phase 40 read-only projection D2 remains unchanged.
- Acceptance criteria: exact identity/result/effect/owner binding, attempt-bound opaque receipt requirement, canonical PRF/rename/writeback plus retire-only commit, DrainOnly and deterministic denials.
- Tests/static scans: Phase 41 spec/acceptance/negative tests plus guards against VMCS source, direct register writes, VMCALL E5/E6, backend/completion shortcuts and receipt replay.
- Risks: treating accepted governance as admission, reusing a receipt across replay, or bypassing canonical speculative delivery and retire ownership.
- Next-gate dependency: separately implemented production composition after clean focused/full gates; this document grants no runtime permission.

The full owner-map columns are: field/operation, owner, value source, capability policy, evidence class, migration class, denial reason.

## Scope

This phase defines a new governance decision for delivery of the already admitted exact
`GuestCr0`/`GuestCr4` read-only projection as one scalar architectural-register result.
It does not edit, supersede or broaden
`D2-HV-VMREAD-PROJECTION-V1-GUEST-CR0-CR4-0001`.

The new identity is:

- DecisionId: `D2-HV-VMREAD-SCALAR-DELIVERY-V1-GUEST-CR0-CR4-0001`;
- namespace: `HybridCPU.VMREAD.ScalarDelivery.v1`;
- operation: `DELIVER_GUEST_CR0_CR4_SCALAR_V1`;
- result ABI: `ScalarU64ToDestinationRegister`;
- effect: `ArchitecturalRegisterResultOnly`;
- migration: `DrainOnly`.

The exact earlier projection SpecDigest and AcceptanceDigest are immutable prerequisites.
They remain governance constraints and do not issue a scalar value or delivery receipt.

## E0 boundary

The only source owner remains `PrivilegedExecutionStateOwnerPolicy`, with field-local
values from `PrivilegedExecutionStateDescriptor.GuestCr0` and `.GuestCr4`. VMCS field
ids remain compatibility metadata.

The future production composition must bind one opaque `VmReadScalarResultReceipt` to
the live attempt, replay epoch, bundle, domain, address space, descriptor epoch, exact
field and nonzero architectural destination. The receipt is single-use evidence for one
canonical scalar carrier. It is not a capability, admission token, source owner, generic
VMREAD enable, VMX/VMCS writeback authority or retire grant.

Speculative delivery is restricted to the existing published destination dependency and
canonical PRF/rename/EX-MEM-WB scalar carrier. Architectural commit is restricted to a
WB-local `RetireRecord.RegisterWrite` consumed by the canonical `RetireCoordinator`.
Direct architectural register writes are forbidden.

Squash before precise retire must invalidate or consume the receipt and leave zero
architectural effect. A receipt cannot cross replay attempt, replay epoch, issuer
generation, squash, checkpoint or restore. Migration is drain-only; receipts are never
migration payload.

## Explicit denials

- no VMWRITE or underlying virtualization-state mutation;
- no VMCS scalar/backing-store result source;
- no `VmxRetireEffect.VmcsRead` authority;
- no VMCALL E5/E6 or `PROBE_NO_STATE_V1` D2/O1/E1–E7 reuse;
- no trap completion or compatibility frontend publication authority;
- no x0, missing destination, `GuestCr3`, host aliases, compatibility controls or any
  field outside `GuestCr0` and `GuestCr4`;
- no receipt reuse after replay, squash, revocation or restore.

## Current status

The immutable SpecV2 is materialized at commit
`bb2125226425eb341fd94cc67cc6bb26abf918fe`, tree
`e1c89c0132a3dbac8a20c3c5d4f385d27ba31322`, with canonical digest
`ccda8698dbeb3f6eef1b4f13e22a3fb7607e939f493138fb7e3373674e234309`.

A later non-self-referential AcceptanceRecordV2 binds those exact earlier bytes with
acceptance digest
`465a887c2918a762af6bf2039f98c294b2f601b1bb7b71512df3f95a9141b5d5`.
The acceptance is committed at
`44fc1c3daf7d9359ffb5e4b00ccfa35953670515`, tree
`202d937dda1d65789d2fe18fbc619ab52231a022`.
Accepted policy remains governance-only: it grants no runtime authority, creates no
receipt, and does not activate production scalar delivery.
