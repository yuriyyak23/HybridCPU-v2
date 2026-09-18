# Phase 46 exact GuestCr3/EptPointer/Vpid/Cr3TargetCount VMREAD scalar delivery

Status: closed exact production composition, default disabled.

The accepted decision
`D2-HV-VMREAD-SCALAR-DELIVERY-V1-GUEST-CR3-EPTP-VPID-CR3TC-0003`
remains a governance constraint, not runtime authority. The only source owner is
the existing `MemoryDomainDescriptor` through canonical `MemoryDomainRuntime`;
the value source is a materialized `MemoryDomainReadOnlyTranslationView`.

The exact production route is:

`VMREAD` canonical ingress → accepted exact D2 → atomic live MemoryDomain
owner/address-space/generation/field/value capture → opaque single-use
`VmReadScalarResultReceipt` → canonical PRF/rename/writeback →
`RetireRecord.RegisterWrite` → `RetireCoordinator`.

Only `GuestCr3`, `EptPointer`, `Vpid`, and `Cr3TargetCount` are in this profile.
`GuestCr3` comes from the canonical address-space root. `EptPointer` additionally
requires owned and valid second-stage translation. `Vpid` additionally requires
enabled tagging and a non-zero tag. `Cr3TargetCount` comes from the canonical
target count. Failure is field-specific and does not enable another field.

Freshness is the runtime-owned, non-zero current `AddressSpaceGeneration`.
Caller-supplied views or generations are never freshness authority. Owner,
domain, address space, generation, field, and scalar value are captured
atomically relative to source replacement. Ordinary source change after a
successful capture does not make retire re-read the source. Replay, squash,
restore, exact-profile disable, or revocation invalidates outstanding receipts;
squash before retire has zero architectural effect.

Activation is default disabled. Rollback disables the exact binding and
invalidates outstanding receipts. Migration is `DrainOnly`; receipts, seals,
physical destinations, and VMREAD output are not serialized.

There is no compatibility/frontend authority, VMCS scalar/backing-store source,
direct architectural register write, `VmxRetireEffect` authority, VMCALL
D2/O1/E2-E7/E5/E6 reuse, backend/trap completion, VMWRITE, broad VMREAD
activation, compiler change, or adjacent-field activation.

Implementation subjects:

- P46-A exact MemoryDomain composition: `356c49f9d6384ec8bbb34c8f6a72a793447b7c3a`, tree `19d2a4378a28d4295570610ebf3f1461e4fc705e`.
- P46-B canonical pipeline integration: `6560d27386e78ee91fed00cdb8bd6af2a752b8ad`, tree `ea960cd29c9c60f4186ec3f6ac4b7c8fc7163af1`.
- Clean verification basis: `25453f76858875ccc183dc70155734948c96f741`, tree `ae49d8f89becd9296c973f8a79ebe3659459cdfe`.

No later VMREAD field group is opened by this closure.

## 2026-06-11 Audit Contract

- File name: `46_guest_cr3_eptp_vpid_cr3_target_count_vmread_scalar_delivery_production.md`
- Purpose: Record the exact Phase 46 production composition and its clean-subject boundary.
- Status: Closed exact production composition; default disabled.
- Scope: `GuestCr3`, `EptPointer`, `Vpid`, and `Cr3TargetCount` scalar delivery only.
- No-goals: Broad VMREAD, VMWRITE, backend/completion authority, compiler changes, or adjacent owners and fields.
- Code anchors: MemoryDomain atomic source capture, exact D2 resolver, VMREAD scalar receipt, canonical scheduler/writeback/retire path.
- Authority owner: Existing `MemoryDomainDescriptor` and canonical `MemoryDomainRuntime`.
- Required RFC/ADR: Accepted immutable Phase 45 SpecV2 and later AcceptanceRecordV2.
- Acceptance criteria: Atomic owner/address-space/generation/value capture, exact-once receipt, zero effect before retire, canonical `RetireCoordinator` commit.
- Tests/static scans: Focused field/source/receipt/race tests, full VMX/SecureCompute matrices, Release build, forbidden scans, and caller reachability.
- Risks: Generic routing, stale receipt reuse, incomplete generation coverage, or compatibility metadata becoming authority.
- Next-gate dependency: A new field group requires a separate bounded E0/D2 authorization.

Owner map completeness is recorded as: field/operation, owner, value source, capability policy, evidence class, migration class, denial reason.
