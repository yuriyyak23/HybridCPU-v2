# Phase 66 — exact PinBasedControls production release

Status: `EXACT RELEASE DECISION / DEFAULT DISABLED / NO ADJACENT AUTHORITY`.

## 2026-06-11 Audit Contract

- File name: `66_pin_based_controls_exact_production_release.md`.
- Purpose: release exactly the Phase 65 read-only PinBasedControls profile.
- Status: release decision bound to a separate clean release candidate.
- Scope: exactly `VmcsField.PinBasedControls` VMREAD scalar delivery.
- No-goals: adjacent fields, hot reactivation, VMWRITE or broad activation.
- Code anchors: `CpuCorePlatformContext.cs`, Phase 65 composition and canonical retire.
- Authority owner: `CompatibilityControlPolicyOwner` only.
- Required RFC/ADR: repository-owner bounded Phase 66 release authorization.
- Acceptance criteria: immutable construction, default off, rollback and clean SHA binding.
- Tests/static scans: identity forgery, domain mismatch, rollback, races and full matrices.
- Risks: evidence/configuration becoming authority or bare boolean activation returning.
- Next-gate dependency: later non-self-referential evidence, then no open pool.

Owner map completeness remains explicit: field/operation, owner, value source, capability policy, evidence class, migration class, denial reason.

## Exact release identity

The release reuses without semantic change:

- `ABI-HV-VMX8-PIN-BASED-CONTROLS-0001`;
- `D2-HV-VMREAD-PIN-BASED-CONTROLS-0005`;
- `D2-HV-VMREAD-PIN-BASED-CONTROLS-SCALAR-0006`.

The reviewed Phase 65 implementation is subject
`bcb10489762c4a560813be8efac299408d8f2906`, tree
`e5db97f9dcee4cab0d6114fcc85a00f305339011`. The separate clean release
candidate is subject `b424a224afd07146080a0f174fc3e12eec0a043a`, tree
`f12dbaa96e356531ea5840e31f7137767a3cae8a`.

## Construction, authority and rollback

`PinBasedControlsVmReadProductionConstructionProfile` is the only production
activation request. It validates all three exact decision identities, the
reviewed Phase 65 SHA/tree and the same non-zero domain as the canonical
`CompatibilityControlPolicyConstructionProfile`. The existing internal
construction flag is derived from that profile; no bare boolean ingress remains.
Absent the exact profile, behavior remains disabled.

The construction profile, release record, deployment configuration, VMX/VMCS,
VMREAD opcode and mapper grant no runtime authority. Runtime value authority
remains exclusively in the current owner/domain/policy-generation-bound
`CompatibilityControlPolicyOwner` snapshot.

Rollback uses `DisablePinBasedControlsVmReadProfile`, whose composition lock
linearizes disable with profile-generation advance. Outstanding receipts fail
speculative and retire validation. There is no enable API: reactivation requires
new explicit core construction. Migration remains `DrainOnly`; receipts and
compatibility values are not serialized.

## Exclusions and exit

`ProcBasedControls`, `ExitControls`, `EntryControls`,
`SecondaryProcControls`, all adjacent VMREAD fields, VMWRITE, new VMCALL,
nested, SecureCompute, compiler, memory/IOMMU/DMA/device/lane/stream and broad
virtualization activation remain denied.

After later evidence closure, `NextOpenPool=None`. No next field or operation is
selected; it requires a new, separate owner authorization.

## Final release evidence

The release decision subject is
`5b9e7353e7bb86afa355e41b7666c7b1cebb5e9e`, tree
`6640b30475eca09de28b22835cd48f5a36a44b9c`. Its later non-self-referential
clean-source evidence is
`evidence/2026-08-13-phase66-pin-based-controls-production-release-clean-evidence.json`.
The evidence is immutable audit material and is never a production runtime input.
