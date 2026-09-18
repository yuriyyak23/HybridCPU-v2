# Phase 63 — exact PinBasedControls frozen ABI decision

Status: `CLOSED GOVERNANCE DECISION / E0 REAUDIT REQUIRED / NO D2 / NO PRODUCTION COMPOSITION`.

## 2026-06-11 Audit Contract

- File name: `63_pin_based_controls_frozen_abi_decision.md`.
- Purpose: freeze exact HybridCPU VMX8 semantics for one read-only field.
- Status: governance decision closed; E0 re-audit and D2 remain separate.
- Scope: exactly `VmcsField.PinBasedControls`, bits 0 through 63.
- No-goals: no production mapper, VMREAD receipt/delivery, activation, adjacent
  fields, VMWRITE, nested, SecureCompute, compiler or neighboring subsystems.
- Code anchors: none; this boundary is machine-readable governance only.
- Authority owner: `CompatibilityControlPolicyOwner`; the ABI decision has none.
- Required RFC/ADR: repository-owner exact frozen-ABI authorization supplied.
- Acceptance criteria: every bit has an exact predicate or unsupported-zero
  disposition; absent/stale/foreign sources deny without zero fallback.
- Tests/static scans: exact bit enumeration, predicate names, zero semantics,
  D2 denial, production composition absence and adjacency denial.
- Risks: ordinal inference, unsupported-bit invention or premature D2 materialization.
- Next-gate dependency: repeat Phase 61 E0 from the real production graph.

Owner map completeness: field/operation = `VmcsField.PinBasedControls`; owner =
`CompatibilityControlPolicyOwner`; value source = current issuer-sealed snapshot;
capability policy = exact field only and default-disabled; evidence class = frozen
governance decision; migration class = recomputed projection; denial reason =
absent/stale/foreign source or semantic mismatch.

Required owner-map columns remain explicit: field/operation, owner, value source, capability policy, evidence class, migration class, denial reason.

## Boundary

This boundary freezes compatibility semantics for exactly
`VmcsField.PinBasedControls` in the HybridCPU VMX8 ABI. It does not construct a
runtime mapper, call a compatibility frontend, create a VMREAD receipt, or
grant VMX/VMCS authority.

The sole neutral owner is `CompatibilityControlPolicyOwner`; the sole candidate
value source is a current issuer-sealed `CompatibilityControlPolicySnapshot`
and its materialized `CompatibilityEventRoutingPolicy`.

## Whole-field decision

- bit 0 maps exactly to `RuntimeTrapPolicyRequired`;
- bit 1 maps exactly to `NeutralTrapResultRequired`;
- bit 2 maps exactly to `PublicationFenceRequired`;
- bits 3 through 63 are unsupported and must read zero;
- zero for bits 0 through 2 is legal only when the current neutral predicate is
  absent from an otherwise authentic current snapshot;
- absent, stale, foreign-owner or cross-domain source is denial, never a zero
  fallback.

The machine-readable decision is `PinBasedControlsFrozenAbiDecisionV1.json`.
Enum ordinals, schema membership, adjacency, raw VMCS state, caller-created
descriptors and frontend defaults are not evidence.

## Exit

The frozen decision closes the semantic-decision prerequisite only. Phase 61
E0 must now be repeated from the real production graph. D2 may be materialized
only after that repeated E0 is green. Scalar delivery, activation, adjacent
fields and VMWRITE remain denied.

Verification is green: focused `64/64`; the underlying freshness race passed
five runs of `1/1`; worktree VMX `587/587`; worktree SecureCompute `324/324`;
clean staged VMX `569/569`; clean staged SecureCompute `313/313`; hook-free
Release `0` warnings and `0` errors.
