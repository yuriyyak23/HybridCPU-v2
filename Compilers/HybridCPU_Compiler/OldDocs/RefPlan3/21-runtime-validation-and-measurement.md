# 21 — Runtime validation and measurement of FSP evidence

## Goal and motivation

With explicit runtime-owner authorization, parse V1 evidence, compare it to live runtime validation and measure usefulness while keeping the normal legality/execution path authoritative and behaviorally unchanged.

**Status:** `Planned — externally gated`.

## Confirmed starting state

- Runtime already owns Stage A class admission, `SafetyVerifier`/legality service, Stage B lane materialization, FSP/replay and `BundleResourceCertificate4Way` validation.
- The blocker register has no open pool authorizing broad runtime activation or compiler bypass.
- Phase 20 emission is not runtime-consumption authorization.

## In scope

- Runtime decode/freshness validation in shadow/measurement mode.
- Comparison of compiler predictions with live `BundleResourceCertificate4Way`, Stage A/B and rejection outcomes.
- Counters for candidates, match, stale, reject and fallback with zero decision influence.
- Fail-closed behavior and overhead qualification.

## Explicit non-goals

- No fast path, skipped `SafetyVerifier`, cached admission, compiler-selected lane or altered publication/commit/retire.
- No cross-contour donor fallback and no broad VMX/SecureCompute/device/lane activation.
- No use of profile as legality.

## Canonical types/components

Runtime ingress/cache/decoder, `MicroOpScheduler` Stage A/B path, `SafetyVerifier`, `BundleResourceCertificate4Way`, replay identity/epoch/generation owner, Phase 19 codec/runtime-validation context and Phase 18 telemetry taxonomy.

## Target architecture and data flow

```text
normal fetched bundle + optional V1 evidence
 -> bounded decode + identity/version/freshness check
 -> shadow comparison input only
 -> unchanged Stage A + SafetyVerifier + live resource certificate
 -> unchanged Stage B/execution/commit/retire
 -> compare predicted vs actual outcome
 -> telemetry; never feed decision
```

## Invariants and authority boundaries

- SafetyVerifier and runtime owners remain final authority on every operation.
- Stale/missing/malformed/cross-contour evidence is ignored and counted; normal validation always runs.
- Runtime epoch/generation, replay and live PRF/bank/channel state cannot be supplied by compiler.
- Measurement cannot change ordering, random seeds, timing decisions or diagnostic exception behavior.

## Dependencies

Phases 00 and 20 verified, plus a new explicit runtime/SafetyVerifier owner authorization. This is an external gate, not satisfied by this plan.

## Implementation backlog

1. Record signed/traceable scope authorization and named runtime owners.
2. Add bounded decoder at a non-authoritative observation point.
3. Decode static identity first, then have trusted runtime code bind and validate current contour/owner/domain/epoch/generation/replay context; never read freshness from compiler bytes.
4. Run the complete existing Stage A/SafetyVerifier/certificate/Stage B path unchanged.
5. Compare evidence fields with live facts after the decision.
6. Emit confusion matrix and reject/stale/fallback taxonomy.
7. Prove measurement on/off produces identical architectural and scheduling traces.
8. Measure runtime overhead; add sampling if needed without biased acceptance reporting.
9. Keep feature default off until the Phase 24 compiler+FSP-evidence profile review.

## Migration and compatibility strategy

Unknown/absent V1 follows the current path. The shadow decoder is versioned and removable. No compatibility API is removed and old artifacts remain valid.

## Tests

### Positive/property/determinism

- Matching evidence, normal runtime accept/reject, epoch/generation refresh and reproducible telemetry.
- Measurement on/off has identical issued lanes, replay, execution, publication, commit, retire and outputs.

### Negative

- Forged/stale/truncated/downgraded/wrong-contour evidence, register-group/port/bank/channel mismatch and runtime state changes after emission.
- Evidence says accept while SafetyVerifier rejects: runtime reject wins.

### Static

- Shadow result cannot reach admission/materialization decision branches.
- No bypass edge around SafetyVerifier/backend/completion/retire.

## Benchmarks and KPI

- Report eligible donors, reclaim attempts, accepts, successful injects, useful reclaimed slots, fallback/stale/reject, cycles saved and runtime certificate rejects by foreground versus donor/FSP reason. Preserve every numerator/denominator.
- 100% runtime verdict/trace equality measurement off/on; zero correctness divergence.
- Runtime cycle overhead <=0.5% geomean and no workload >1%; memory overhead <=1%.
- Evidence precision/recall and stale rate are reported, not used as authority gates.

## Diagnostics and telemetry

Evidence identity/version/freshness result, predicted and actual outcome, first mismatched resource, Stage A/Stage B reject class, replay generation and sampling status; no secret/register contents.

## Bounded-search, timeout and fallback policy

No search. Decode/comparison uses fixed payload and W=8 bounds. Any error or budget excess skips comparison and executes the unchanged normal path; telemetry records `fallback`.

## Risks and forbidden shortcuts

- No “high precision” justification for bypass.
- No shared mutable state between shadow result and admission.
- No fast path bundled into this phase.

## Rollback / kill switch

Disable runtime shadow decoder/telemetry; Phase 20 artifacts remain safely ignored.

## Acceptance and merge gates

- Prior explicit runtime authorization is recorded.
- Static/dataflow and trace-equivalence reviews prove no decision influence.
- Adversarial freshness/cross-contour tests and overhead gates pass.
- Measurement report covers representative workloads before any Phase 22 proposal.

## Status criteria

- `implemented`: authorized shadow decoder/comparator exists.
- `verified`: trace equivalence, security/freshness and overhead gates pass.
- `default-enabled`: measurement only after runtime release review.
- `release-authorized`: measurement scope only; no fast path authority.

## Residual work / next gate

Phase 22 is optional and requires a new, separately recorded authorization based on Phase 21 evidence.
