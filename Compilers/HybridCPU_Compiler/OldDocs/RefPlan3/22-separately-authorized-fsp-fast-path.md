# 22 — Separately authorized FSP evidence fast path

## Goal and motivation

Only after measured equivalence and a new authority decision, allow narrowly scoped reuse of validated evidence to reduce redundant runtime work without weakening live legality, lane materialization, replay or retire ownership.

**Status:** `Planned — externally gated; intentionally not authorized`.

## Confirmed starting state

- No compiler certificate currently authorizes a runtime fast path.
- The blocker register explicitly preserves runtime/SafetyVerifier authority and does not open broad runtime work.
- Phase 21 measurement, even if successful, grants no fast-path permission.

## In scope

- A separately reviewed, narrowly allowlisted optimization that reuses only fields proven equivalent and freshness-bound.
- Mandatory cheap live checks, full-path fallback, differential shadow sampling and instant kill switch.
- Security, performance and rollback qualification.

## Explicit non-goals

- No bypass of `SafetyVerifier`, backend, completion, publication, commit or retire.
- No compiler-owned lane/PRF/replay state, no stale acceptance, no cross-contour fallback.
- No broad VMX, SecureCompute, device or runtime activation.
- No implementation without an explicit named authorization decision.

## Canonical types/components

Runtime-owned evidence validator/adapter, normal Stage A/B and `SafetyVerifier` path, `BundleResourceCertificate4Way`, epoch/generation/replay owners, feature-policy allowlist and differential sampler. Compiler V1 remains input evidence only.

## Target architecture and data flow

```text
V1 evidence
 -> full schema/identity/epoch/generation validation
 -> mandatory live non-cacheable checks
 -> narrowly authorized reuse of equivalent derived facts
 -> ordinary Stage A authority + SafetyVerifier
 -> ordinary Stage B lane materialization
 -> execution/publication/commit/retire
 -> sampled full recomputation comparison
```

The exact reusable fact set must be named in the authorization; the default assumption is none.

## Invariants and authority boundaries

- Runtime may only use evidence to avoid recomputing an identical derived fact; it cannot accept an operation the normal path rejects.
- Any stale/unknown/mismatch/uncertainty selects the full path before admission.
- Live dynamic capacities, register groups/ports, banks/channels, replay and lane availability remain runtime checks.
- Failures never fall back across contours.

## Dependencies

Phase 21 verified, an archived representative measurement report, threat-model review, and a new explicit runtime/SafetyVerifier release authorization naming the reusable fields and scope.

## Implementation backlog

1. Stop unless authorization record and owner list exist.
2. Define fast-path equivalence theorem/argument for each reusable fact.
3. Add allowlist restricted by artifact/model/evidence version and runtime generation.
4. Preserve mandatory live checks and ordinary authority calls.
5. Implement fail-closed fallback before any state mutation.
6. Add sampled full recomputation and mismatch-triggered global disable.
7. Compare complete traces and adversarial replay/freshness behavior.
8. Measure saved validation work versus overhead.
9. Roll out canary -> limited default only through Phase 24 runtime-enhanced profile gates.

## Migration and compatibility strategy

Default remains full validation. Artifacts without V1 and old runtimes remain supported. Feature policy is additive and reversible; compatibility APIs remain untouched.

## Tests

### Positive/property/determinism

- Authorized evidence reuses only named facts and yields the same Stage A/B/retire trace as full validation.
- Differential sampling is deterministic under replay configuration and does not alter decisions.

### Negative

- Every freshness, contour, digest, live-resource, replay and mutation race; injected comparator mismatch disables the path and uses full validation.
- Compiler evidence proposes a lane or accept result: rejected/ignored.

### Static

- Call/dataflow proofs retain SafetyVerifier and all backend/completion/commit/retire owners.
- No compiler reference to runtime mutable PRF/replay state.

## Benchmarks and KPI

- Zero correctness or full-path verdict divergence across 100% differential corpus.
- Report donor candidates/fast accepts/full fallback/stale/reject, certificate rejects, mismatch count, validation CPU time and runtime cycles/IPC/stalls.
- Required benefit before wider rollout: validation-cost p50 reduction >=10% on eligible donors and representative runtime-cycle geomean <=0.995; no workload >1.005.
- Any unexplained differential mismatch is a release blocker and triggers disable.

## Diagnostics and telemetry

Authorization policy/version, evidence/freshness result, reused field set, mandatory live-check outcomes, fallback reason, sampled full result, mismatch and kill-switch state.

## Bounded-search, timeout and fallback policy

No search and no elapsed-time code-selection branch. Validation uses fixed schema/W=8 bounds, at most 64 decoded fields and 128 comparison operations per bundle; any uncertainty or work-budget limit takes the full path. A watchdog may disable/fail the feature globally but cannot choose a different path for one otherwise identical bundle. Differential sampling is bounded and cannot suppress normal checks.

## Risks and forbidden shortcuts

- No authorization by benchmark success or by compiler ownership claim.
- No simultaneous first ABI emission and fast-path enablement.
- No accepting stale data, cross-contour donors or incomplete live-state coverage.

## Rollback / kill switch

Runtime global and policy/allowlist switches immediately force the full path. Mismatch auto-disable is fail closed; no artifact rewrite is required.

## Acceptance and merge gates

- Explicit separate authority record exists and exactly matches implementation scope.
- Threat model, static authority audit, trace equivalence and fault injection pass.
- Differential mismatch count is zero; performance benefit exceeds overhead.
- Canary/rollback drill succeeds before limited enablement.

## Status criteria

- `implemented`: authorized narrow path and full fallback exist.
- `verified`: equivalence/security/performance/rollback gates pass.
- `default-enabled`: only for the named allowlist after Phase 24 runtime-enhanced release decision.
- `release-authorized`: only by the separate recorded runtime decision; this plan does not grant it.

## Residual work / next gate

Broader runtime, VMX, SecureCompute, device or lane activation remains explicitly out of scope and needs a new plan/authority pool.
