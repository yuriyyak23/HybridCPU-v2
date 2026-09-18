# Phase 64 — exact PinBasedControls repeated E0 and bounded D2

Status: `E0 GREEN / D2 ACCEPTED / NO PRODUCTION COMPOSITION`.

## 2026-06-11 Audit Contract

- File name: `64_pin_based_controls_vmread_e0_d2.md`.
- Purpose: repeat Phase 61 E0 from the real Phase 62/63 graph and, only after
  green E0, materialize the authorized immutable exact D2 mapping.
- Status: E0 verification boundary; production composition remains denied.
- Scope: exactly `VmcsField.PinBasedControls`.
- No-goals: no VMREAD receipt/delivery, activation, adjacent fields, VMWRITE,
  VMX/VMCS authority, compiler, SecureCompute, nested or neighboring subsystems.
- Code anchors: `CompatibilityControlPolicyOwner.cs`, canonical CPU construction,
  neutral interrupt consumer and the Phase 63 frozen ABI decision.
- Authority owner: `CompatibilityControlPolicyOwner`; D2 is projection only.
- Required RFC/ADR: repository-owner Phase 61 E0/D2 authorization supplied.
- Acceptance criteria: production construction and consumption, current exact
  owner/domain/generation source, whole-field equivalence and explicit denial.
- Tests/static scans: construction/reachability, all supported combinations,
  unsupported bits, absent/stale/foreign/cross-domain and frontend absence.
- Risks: mistaking optional construction for implicit activation or mapping
  absent/stale state to zero.
- Next-gate dependency: D2 only after green E0; scalar delivery separately authorized.

Owner map completeness: field/operation = `VmcsField.PinBasedControls`; owner =
`CompatibilityControlPolicyOwner`; value source = current owner-issued snapshot;
capability policy = exact field/default disabled; evidence class = owner state plus
frozen ABI; migration class = recomputed; denial reason = absent/stale/foreign,
cross-domain or semantic mismatch.

Required owner-map columns remain explicit: field/operation, owner, value source, capability policy, evidence class, migration class, denial reason.

## E0 verdict and D2 materialization

The repeated E0 is green. It proves the explicit platform-context construction
path into `CPU_Core`, owner-issued identity/epoch/domain/non-zero generation,
the production neutral interrupt consumer, lifecycle freshness and all seven
non-empty combinations of the exact three neutral predicates. Absent, stale,
foreign-owner and cross-domain sources deny without a value.

Only after that gate, `Phase64PinBasedControlsVmReadProjectionD2` materializes
the immutable exact mapping. A granted result carries a present `ulong`; denial
carries no value, preserving absent versus zero. The mapper has no frontend
caller, grants no runtime authority and authorizes no production composition.

`PinBasedControlsVmReadDecisionSpecV2.json` and its digest-bound
`PinBasedControlsVmReadAcceptanceRecordV2.json` freeze the one-field decision.
Production VMREAD composition, scalar delivery and activation remain denied.

Verification is green: focused E0 `68/68`; focused D2 boundary `71/71`;
freshness race five runs of `1/1`; worktree VMX `593/593`; worktree
SecureCompute `324/324`; hook-free Release `0` warnings and `0` errors.
Clean staged verification is VMX `575/575`, SecureCompute `313/313`, and
hook-free Release `0` warnings / `0` errors.
