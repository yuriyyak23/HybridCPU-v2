# Phase 65 — exact PinBasedControls production scalar delivery

Status: `IMPLEMENTED / DEFAULT DISABLED / RELEASE NOT AUTHORIZED`.

## 2026-06-11 Audit Contract

- File name: `65_pin_based_controls_vmread_scalar_delivery.md`.
- Purpose: implement the separately authorized exact scalar delivery boundary.
- Status: production composition implemented, explicit/default-disabled, unreleased.
- Scope: exactly `VmcsField.PinBasedControls` read-only scalar delivery.
- No-goals: adjacent fields, release, broad activation or runtime authority.
- Code anchors: canonical CPU construction, lane materialization, exact
  composition, existing scalar receipt, PRF/writeback and retire.
- Authority owner: `CompatibilityControlPolicyOwner` only.
- Required RFC/ADR: repository-owner bounded scalar-delivery authorization.
- Acceptance criteria: exact source, freshness, denial, exact-once and rollback.
- Tests/static scans: positive/negative delivery, lifecycle, races and forbidden scans.
- Risks: treating compatibility mapping or opt-in configuration as authority.
- Next-gate dependency: separate exact production release authorization.

Owner map completeness remains explicit: field/operation, owner, value source, capability policy, evidence class, migration class, denial reason.

## Bounded decision

Repository-owner authorization opens production scalar delivery for exactly
`VmcsField.PinBasedControls`. It does not open any adjacent control field and
does not make VMX, VMCS, VMREAD, the frozen ABI decision or the D2 mapper a
runtime authority source.

The value source remains the current owner-issued
`CompatibilityControlPolicySnapshot` from
`CompatibilityControlPolicyOwner`. The Phase 64 mapper supplies the exact
read-only compatibility value. Missing, foreign, stale, cross-domain or
semantically incompatible state denies; no zero fallback or inference exists.

## Production contour

The explicit `CpuCorePlatformContext.EnablePinBasedControlsVmRead` construction
flag is default false. Enabling it without the canonical neutral policy owner
fails construction. Canonical lane materialization recognizes only
`VmcsField.PinBasedControls` and calls the bounded composition after live VMREAD
E1 admission and the canonical field-selector read.

The composition captures owner identity, owner epoch, exact domain, non-zero
policy generation, VMREAD attempt/issuer/bundle/replay identity, restore
generation, field and destination. It reuses the existing single-use
`VmReadScalarResultReceipt`; execution forwards the scalar through the existing
PRF/writeback path and precise retire consumes the receipt before emitting only
`RetireRecord.RegisterWrite` to `RetireCoordinator`.

Policy replace, domain rebind, architectural owner replacement, restore and the
profile kill-switch invalidate outstanding receipts. A duplicate retire consume
fails closed. The profile is recomputed/drain-only and serializes no receipt.

## Exclusions and next boundary

`ProcBasedControls`, `ExitControls`, `EntryControls`,
`SecondaryProcControls`, VMWRITE, new VMCALL, host aliases, nested,
SecureCompute, compiler, memory/IOMMU/device/lane/stream and broad activation
remain denied. Compatibility frontend and VMCS backing state are not called.

This phase implements the authorized production composition and delivery only.
It does not issue a production release claim. `NextOpenPool` returns to `None`;
a separate owner authorization is required for an exact PinBasedControls
production release/activation decision.

Verification is green: focused implementation `16/16`; focused Phase 64/65
and plan guards `80/80`; prepare/kill-switch race five runs of `1/1`; worktree
VMX `609/609`; worktree SecureCompute `324/324`; hook-free Release `0` warnings
and `0` errors; clean staged VMX `591/591`; clean staged SecureCompute
`313/313`; clean staged hook-free Release `0` warnings and `0` errors;
forbidden and ignored-source scans are clean.

The immutable subject is `bcb10489762c4a560813be8efac299408d8f2906`,
tree `e5db97f9dcee4cab0d6114fcc85a00f305339011`. Later non-self-referential
evidence is recorded in
`evidence/2026-08-13-phase65-pin-based-controls-scalar-delivery-clean-evidence.json`.
