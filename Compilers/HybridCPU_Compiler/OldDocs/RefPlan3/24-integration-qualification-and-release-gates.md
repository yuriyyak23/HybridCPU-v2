# 24 — Integration, performance qualification, rollback and release gates

## Goal and motivation

Integrate only independently verified slices, qualify representative correctness/performance/compile budgets, and make default/release decisions reversible and evidence-bound.

**Status:** `Planned`.

## Confirmed starting state

- Phase 00 has a clean pinned verification subject; no later working tree or phase inherits that qualification automatically.
- Established canonical dispatch/provider parity and authority boundaries remain inputs; RefPlan3 must not duplicate them.
- Candidate benchmark artifacts are hypotheses until reproduced on the exact subject of the phase that consumes them.
- Runtime FSP phases 21–22 have external authority gates independent of compiler scheduling release.

## In scope

- Slice-by-slice integration order, feature flags, compatibility matrix and evidence manifest.
- Correctness, determinism, performance, compile-time/memory and code-growth qualification.
- Canary, rollback drills and release decision records.
- Independent dispositions for scheduler, regions, loops, transformations, VT-aware scheduling, VDSA intent and each FSP stage.
- Separate release profiles: `CompilerOnly`, `CompilerPlusStaticFspEvidence`, `CompilerPlusStaticAssistIntent`, and externally authorized `RuntimeEvidenceMeasurement`/`RuntimeEvidenceReuse`.

## Explicit non-goals

- No automatic default enable because code merged or a benchmark improved.
- No bundling different authority/correctness scopes into one approval.
- No compatibility API removal without an accepted compatibility-window decision.
- No broad runtime/device activation.

## Canonical types/components

`RefPlan3EvidenceManifestV1`, feature-policy matrix, Phase 00 corpus/metrics, CI/scripts, compiler diagnostics, runtime performance/correctness harnesses and `RefPlan3StatusV1.json`.

## Target architecture and data flow

```text
immutable baseline + phase evidence
 -> dependency/gate validation
 -> feature-isolated correctness and determinism
 -> A/B performance + compile/code budgets
 -> authority and compatibility review
 -> canary + rollback drill
 -> per-feature default/release decision
 -> signed/archived evidence manifest
```

No aggregate score can override a correctness, authority or representative regression failure.

## Invariants and authority boundaries

- Same compiler input/profile/options/model produces byte-identical schedules, bundles, evidence and diagnostics.
- Correctness and authority gates precede cycle profitability; width/occupancy never stand alone.
- Runtime phases 21/22 require their own owner decisions; compiler release cannot imply them.
- SafetyVerifier, Stage A/B, replay, backend, completion, publication, commit and retire ownership remains unchanged.

## Dependencies

Integration may qualify independently verified slices; Phase 00 evidence is mandatory for every profile. `CompilerOnly` may consume verified Phases 01–18 while excluding optional capability-gated transforms. `CompilerPlusStaticFspEvidence` additionally requires Phases 19–20. Static assist intent separately requires Phase 23. Phases 21–22 are external runtime profiles and are never implied dependencies of compiler release. Phase 12 oracle results are qualification inputs, not production dependencies.

## Implementation backlog

1. Freeze clean candidate SHA, toolchain/runtime versions, corpus hashes and configuration.
2. Generate a manifest mapping each release profile and enabled feature to implementation/verified/default/release status and evidence.
3. Run static authority/API/symbol and dependency checks.
4. Run unit, negative, property, determinism, differential and reference-execution suites.
5. Run compile-time/memory/code-growth budgets on cold and warm builds.
6. Run repeated representative performance trials; archive raw distributions, not only summaries.
7. Compare production vs optional oracle exports and record optimality gaps/first divergence without loading the solver in production.
8. For modulo candidates, bind Phase 13 stage-bound evidence, joint schedule/placement witness,
   value-versioning disposition and scheduler/expander ownership scan into the release manifest.
9. Test every feature flag singly and supported combinations in dependency order.
10. Drill rollback/canary and stale certificate/model/profile behavior.
11. Record per-feature accept/reject/defer decision; update status JSON only from evidence.

## Migration and compatibility strategy

Default flags progress `off -> shadow -> allowlist -> qualified default`. Serialized formats are versioned and additive. Old artifacts/runtime paths remain accepted during the documented compatibility window. Each feature has an independently removable path to the Phase 00 scheduler/runtime baseline.

## Tests

### Positive/property/determinism

- Full representative compiler/runtime suite, fixed/randomized IR properties, replay and repeated parallel-build byte comparison.
- Supported flag combinations preserve dependency ordering and output equivalence.
- Modulo qualification replays the exact prescribed placement after CFG expansion and proves the
  emitted prologue/kernel/epilogue against rebuilt CFG/dependence/resource analyses.

### Negative

- Stale/missing/corrupt profile/model/certificate/stage bound, deterministic counter-budget
  exhaustion, unsupported regions/loops/value versioning, exact-placement witness mismatch,
  injected oracle/fast-path mismatch and rollback mid-canary. CI watchdog expiry fails/invalidates
  a run and never selects alternate code.

### Static

- No compiler-to-backend/completion/commit/retire bypass; no profile legality path.
- Existing canonical provider/dispatch/parity contracts remain intact.
- Compatibility APIs cannot be removed without a recorded window decision.
- Constraint scheduler has no CFG/relocation mutation surface; loop expander has no solver-backend
  authority; BundleFormer has no modulo repair path.

## Benchmarks and KPI

- Primary release gate: representative geomean runtime cycles <=0.98 for the qualified scheduling train, no workload >1.01, and zero correctness-neutrality failures.
- `schedule_cycles` geomean <=0.99 and bundle count <=1.00 versus frozen baseline; single-/packed-VT width reported as secondary.
- Oracle median `optimality_gap_cycles <=1`, p90 relative gap <=10%, with `first_divergence_cycle` for all mismatches.
- Compile-time p95 <=1.10, peak memory <=1.15; default module code growth <=5%.
- Report ready-window/search states/prunes/cap hits, PRF ports, predicted register conflicts,
  bank/channel conflicts, lane6/lane7 pressure, all MIIs, selected-II stage bound/actual stages,
  placement-witness digest, value-versioning disposition, VT fairness/complementarity, FSP outcomes
  and runtime IPC/stalls.
- Separate foreground structural/certificate conflicts from donor/FSP rejects; report reject/reclaim-attempt, accepted/eligible-donor, successful injects, useful reclaimed slots and cycles saved.
- Physical lane realization must remain `1.0` and width drops `0` on current qualified packed profiles unless a separately reviewed counterexample proves the metric definition changed rather than backend quality.
- Phase-specific stricter gates remain binding; aggregate wins cannot hide an individual >threshold regression.

## Diagnostics and telemetry

Base/candidate SHA, dirty flag, toolchain/runtime/config/profile/model/evidence hashes, feature matrix, every required KPI distribution, fallback/cap reasons, authority decision IDs and rollback state.

## Bounded-search, timeout and fallback policy

Production limits are deterministic states/cuts/candidates/nodes/iterations only. CI records every cap hit/fallback; representative cap-hit thresholds must pass. Elapsed time is diagnostic/watchdog-only and cannot select emitted code. A counter limit never triggers an unbounded retry or solver requirement and always chooses the phase-defined deterministic fallback.

## Risks and forbidden shortcuts

- No release from dirty/unattributed artifacts or historical test totals.
- No average-only result hiding tail regressions; no width-only claim.
- No combined authorization for `CompilerOnly`, static FSP/assist evidence, runtime measurement or runtime FSP reuse.
- No deleting fallback/compatibility paths before rollback and window decisions.
- No modulo release from temporal feasibility alone, an arbitrary stage cap, unproved rotating-
  register/MVE assumptions or post-hoc placement repair.

## Rollback / kill switch

Feature/profile matrix can restore Phase 00 behavior by dependency layer. Release must prove rollback without cache/artifact corruption; FSP emit, assist-intent emit, runtime measure and fast-path switches are independent.

## Acceptance and merge gates

- Clean reproducible evidence manifest and all phase-specific merge gates pass.
- No representative correctness failure or regression beyond threshold.
- Deterministic output and fallback/rollback drills pass.
- Architecture/runtime owners sign only the scopes they own; unresolved external blockers are listed.

## Status criteria

- `implemented`: integration harness, manifest and feature policy exist.
- `verified`: clean full-matrix qualification and rollback drill pass.
- `default-enabled`: assigned separately to each qualified feature, never to the plan as a whole.
- `release-authorized`: recorded per feature/profile/scope with SHA and evidence; Phase 22 needs its own runtime authorization.

## Residual work / next gate

Deferred compatibility removal, broad VMX/SecureCompute/device work and any FSP authority expansion require separate approved plans. Failed KPI slices remain off and retain deterministic fallback.
