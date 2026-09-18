# Phase 43 exact GuestPc/GuestSp/GuestFlags VMREAD scalar delivery

Status: closed exact production composition, default disabled.

The accepted decision `D2-HV-VMREAD-SCALAR-DELIVERY-V1-GUEST-PC-SP-FLAGS-0002`
is resolved as a governance constraint only. Runtime authority remains with the
existing `ExecutionDomainDescriptor` and canonical `ExecutionDomainRuntime`.

The production path is deliberately exact:

`VMREAD` canonical ingress → accepted exact D2 → atomic materialized
`ExecutionDomainReadOnlyStateView` value/owner/source-epoch capture → opaque
single-use `VmReadScalarResultReceipt` → canonical PRF/rename/writeback →
`RetireRecord.RegisterWrite` → `RetireCoordinator`.

Only `GuestPc`, `GuestSp`, and `GuestFlags` are in this profile. The source epoch
is issued by the runtime, is non-zero, and changes on source replacement,
re-materialization, restore rebind, and ordinary rebind. A caller-supplied epoch
is never an input to capture. A successful capture freezes the scalar value;
ordinary later source replacement does not cause retire to re-read the source.

The receipt is bound to the canonical E1 attempt, accepted D2, field, source
owner and epoch, value, destination, VT/owner context/domain, replay epoch,
restore generation, and exact profile generation. It is single-use. Replay,
squash, restore, disable, or revocation invalidates outstanding receipts.
Squash before retire has zero architectural effect.

There is no VMCS scalar/backing-store source, direct architectural register
write, VMX retire-effect authority, VMCALL E2-E7/E5/E6 reuse, backend/trap
completion, VMWRITE, broad VMREAD activation, compiler change, or adjacent field
activation. Receipts and runtime seals are not migration state. Migration is
`DrainOnly`.

Implementation subjects:

- P43-A source-epoch gate: `21ca5bece0d17fdb69f4ecd4bbd1a7c9ef61f141`, tree `2040f157bdbcad3576c9abec6e82c91aff62a2cf`.
- P43-B production composition: `ee5627c022a8f0922fd2477b54a53a06b9b2aa71`, tree `5f097109564255f2a0628a48ead983837625bada`.

No later VMREAD field group is opened by this closure.

## 2026-06-11 Audit Contract

- File name: `44_guest_pc_sp_flags_vmread_scalar_delivery_production.md`
- Purpose: Record the exact Phase 43 production composition and clean-subject boundary.
- Status: Closed exact production composition; default disabled.
- Scope: `GuestPc`, `GuestSp`, and `GuestFlags` scalar delivery only.
- No-goals: Broad VMREAD, VMWRITE, backend/completion authority, or adjacent owners and fields.
- Code anchors: ExecutionDomain source-epoch gate, exact D2 resolver, VMREAD scalar receipt, canonical scheduler/writeback/retire path.
- Authority owner: Existing `ExecutionDomainDescriptor` and canonical `ExecutionDomainRuntime`.
- Required RFC/ADR: Accepted immutable Phase 43 SpecV2 and later AcceptanceRecordV2.
- Acceptance criteria: Atomic owner/value/epoch capture, attempt-bound exact-once receipt, zero effect before retire, canonical `RetireCoordinator` commit.
- Tests/static scans: Focused source/receipt/race tests, full VMX/SecureCompute matrices, Release build, forbidden scans, and caller reachability.
- Risks: Accidental generic VMREAD routing, stale receipt reuse, or compatibility metadata becoming authority.
- Next-gate dependency: A new field group requires a separate bounded E0/D2 authorization.

Owner map completeness is recorded as: field/operation, owner, value source, capability policy, evidence class, migration class, denial reason.
