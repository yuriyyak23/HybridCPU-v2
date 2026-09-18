# Phase 62 — neutral compatibility-control policy owner construction

Status: `CLOSED IMPLEMENTATION / VMREAD COMPOSITION DENIED`.

## 2026-06-11 Audit Contract

- File name: `62_compatibility_control_policy_owner_production_construction.md`.
- Purpose: make the neutral compatibility-control policy owner-issued,
  production-constructible, freshness-bound, and consumed by a real neutral path.
- Status: implemented; verification and provenance recorded by the machine status.
- Scope: `CompatibilityControlPolicyOwner`, immutable snapshot identity/freshness,
  core construction, interrupt-routing consumer, restore/rebind/replacement lifecycle.
- No-goals: no VMREAD production composition, activation, other controls, VMWRITE,
  VMCALL, nested, SecureCompute, compiler, memory/IOMMU/device/lane/stream.
- Code anchors: `CompatibilityControlPolicyOwner.cs`, `CpuCorePlatformContext.cs`,
  `CPU_Core.StateData.cs`, `CPU_Core.InterruptDispatch.cs`, restore/reset paths.
- Authority owner: the neutral `CompatibilityControlPolicyOwner`; descriptor,
  compatibility frontend, VMX/VMCS and mapping artifacts have no runtime authority.
- Required RFC/ADR: repository-owner bounded neutral-owner authorization supplied.
- Acceptance criteria: explicit construction, non-zero identity/generation, sealed
  current snapshot, stale/foreign/domain denial, real consumer, lifecycle invalidation.
- Tests/static scans: construction/default, forged/stale/cross-domain, consumer,
  replace/rebind/restore/owner replacement, repeated races and forbidden scans.
- Risks: caller-created descriptor authority, stale snapshot acceptance, inferred
  domain identity, or compatibility frontend becoming policy source.
- Next-gate dependency: separately authorized frozen ABI decision, then Phase 61 E0
  repeated from current production source; scalar delivery remains separately gated.

Owner map completeness: field/operation = neutral event routing policy; owner =
`CompatibilityControlPolicyOwner`; value source = issuer-sealed immutable snapshot;
capability policy = none; evidence class = neutral runtime state; migration class =
recomputed after restore; denial reason = absent/stale/foreign/domain mismatch.

Required owner-map columns remain explicit: field/operation, owner, value source, capability policy, evidence class, migration class, denial reason.

Verification is green: focused `72/72`; the lifecycle race passed five runs of
`1/1`; worktree VMX `585/585`; worktree SecureCompute `324/324`; clean staged
VMX `567/567`; clean staged SecureCompute `313/313`; hook-free Release `0`
warnings and `0` errors.

## Canonical model

`CpuCorePlatformContext` carries an explicit immutable construction profile.
`CPU_Core` is the production constructor and creates exactly one owner when the
profile is present. Absence preserves existing behavior and creates no implicit
policy or compatibility activation.

The snapshot binds stable owner identity, owner epoch, non-zero domain identity,
and non-zero policy generation under a private issuer seal. Replace, domain
rebind, restore, and architectural-state replacement issue a new snapshot and
make every earlier snapshot stale.

Production `DispatchInterrupt` is the first neutral consumer. When the owner is
configured, it requires a current same-domain snapshot and the three neutral
predicates for runtime trap policy, neutral trap result, and publication fence
before calling the existing dispatcher. Neither VMX nor VMREAD participates.

## Verdict

The Phase 61 production-construction/freshness blocker is closed. This phase
does not establish a compatibility bit map and does not open VMREAD composition.
