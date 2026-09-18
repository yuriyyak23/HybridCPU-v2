# 12 — Exact scheduler oracle for small regions

## Goal and motivation

Provide an exact, bounded research/test service that proves or bounds optimal schedule cycles/bundles for small regions and explains production divergence. It measures heuristic quality; it is not a mandatory production dependency.

**Status:** `Planned`.

## Confirmed starting state

- No `ExactScheduleOracle` exists.
- Runtime `ShadowOracleSchedulerTests` concern runtime/FSP behavior, not optimal compiler scheduling.
- Current placement search is exact only after membership and does not prove temporal optimality.

## In scope

- Exact small-region temporal+resource model using the same dependence/resource/placement contracts.
- Optimality results: `Optimal`, `BoundedGap`, `Unsat`, `Timeout/Unknown`, never conflated.
- Production/oracle comparison, first divergence and dominant limiting constraints.
- Offline CI/research invocation.

## Explicit non-goals

- No solver on every production function.
- No oracle-generated production code in this phase.
- No duplicate machine model or runtime legality encoding.
- No “timeout means optimal” claim.

## Canonical types/components

- `IExactScheduleOracle`, `ExactScheduleOracleRequestV1`, `ExactScheduleOracleResultV1`, proof/unsat diagnostics.
- Adapter from `IrSchedulingRegion`, dependence graph and `IHybridCpuMachineResourceModel`.
- Solver-neutral `ConstraintProblemV1`, stable `ConstraintId` and `IConstraintSolverBackend`.
- Solver backends isolated behind an optional test/research assembly or process boundary; oracle
  schemas contain no Z3/CVC5 AST or backend-native identity.

## Target architecture and data flow

```text
small frozen region + exact model digest
 -> lower-bound computation
 -> solver-neutral ConstraintProblemV1
 -> optional IConstraintSolverBackend
 -> exact bounded temporal/membership/placement solve
 -> validated schedule or proof status
 -> compare production cycles/bundles/membership
 -> optimality_gap + first_divergence + dominant constraints
```

Every solver model is replay-validated by the normal compiler legality and existing placement machinery.

## Invariants and authority boundaries

- Oracle feasibility must be a subset of normal compiler structural feasibility; runtime legality remains stricter/live.
- Solver order, seeds and options are pinned for determinism.
- Backend-independent constraint IDs, result dispositions and validation are canonical; a backend
  cannot redefine machine-resource truth.
- Production builds do not require the solver package/runtime.
- Profile is excluded from exact legality; optional profitability comparison is labelled separately.

## Dependencies

Phases 03 and 04 verified. Phase 08 memory refinement may enrich an oracle request, but is not required for the initial straight-line oracle.

## Implementation backlog

1. Define `ConstraintProblemV1`, stable constraint IDs, checked coefficients and backend-neutral
   statuses before choosing an optional solver.
2. Define `IConstraintSolverBackend`; choose Z3, CVC5 or another optional backend only after
   licence, deterministic and CI availability review, and retain exhaustive tiny reference solving.
3. Define exact request/result/proof schema and statuses without backend-native objects.
4. Encode dependence latency, W=8 membership, existing placement, class/structural/register/port/bank known constraints.
5. Minimize cycles first, bundles second, then stable tie-break; do not optimize width ahead of cycles.
6. Validate every returned schedule through normal compiler checkers/materializer.
7. Compute first divergence and alternative blocking reason.
8. Extract unsat/conflict explanations as stable constraint-ID sets grouped by resource; label
   non-minimal cores and preserve the backend-reported core separately.
9. Populate frozen <=20-op corpus from synthetic and real small regions.
10. Add production-vs-oracle CI report without changing production output.
11. Keep all production schedulers independent of this backend; Phase 14 consumes oracle reports only in tests/qualification.

## Migration and compatibility

- Optional tooling; no production API/ABI change.
- Missing solver produces `Unavailable`, not a skipped passing optimality claim.
- Oracle result version binds input/model/options hashes.

## Tests

### Positive/property/determinism

- Known optimal chains, independent width, hard pins, mixed resources and greedy counterexamples.
- Returned schedule passes standard legality/materialization and matches exhaustive reference for tiny cases.
- Fixed seed/options produce identical proof/result bytes.
- Z3/CVC5/exhaustive backends, when enabled for the same tiny problem, agree on canonical
  disposition and replay-validated optimum even if backend proof payloads differ.

### Negative

- Unsat hard constraints, stale model digest, unsupported unknown facts, timeout, solver crash, invalid model and forged returned schedule.

### Static

- Production compiler project has no hard solver dependency.
- Public oracle/report schemas contain no backend AST types.
- Oracle cannot publish or emit production packages.
- Runtime authority types are not constructed.

## Benchmarks and KPI

- Oracle coverage: 100% frozen regions <=12 ops; >=90% <=20 ops finish within research budget.
- Production median optimality gap <=1 cycle and p90 relative gap <=10% before default-enabling later scheduler changes.
- Every mismatch has `first_divergence_cycle`; every unknown has reason/time/state count.
- Oracle wall time initial cap 2 seconds/region in CI and 30 seconds offline; these do not affect production output.

## Diagnostics and telemetry

Bounds, optimum/proof status, solve states/time/memory, gap cycles/bundles, first divergence,
canonical conflicting constraint IDs/ops/resources, backend identity/version/options, model/input
digest and validation outcome.

## Bounded-search, timeout and fallback

Default limits: <=20 ops, <=8 cycles above lower bound, 2 seconds CI, 512 MiB per worker. Timeout -> `Unknown`; production schedule remains unchanged. Exact exhaustive fallback for <=10 ops may cross-check the solver.

## Risks and forbidden shortcuts

- No monolithic whole-function constraint solve.
- No solver-specific resource truth fork.
- No optimality claim without validated proof/result and complete model version.

## Rollback / kill switch

Remove/disable oracle job; production compiler artifacts are unaffected.

## Acceptance and merge gates

- Optional dependency/licence review.
- Cross-validation with exhaustive tiny cases and normal materializer.
- Determinism, timeout/unknown and proof-status tests.
- Baseline gap report archived; no production codegen change.

## Status criteria

- `implemented`: optional oracle and report exist.
- `verified`: exhaustive cross-check and frozen-corpus coverage pass.
- `default-enabled`: only as CI/research reporting, never required production codegen.
- `release-authorized`: tooling release does not authorize solver-produced binaries.

## Residual work / next gate

Phases 02, 07, 13 and 14 use oracle results as qualification evidence when available. None has a production dependency on the oracle backend.
