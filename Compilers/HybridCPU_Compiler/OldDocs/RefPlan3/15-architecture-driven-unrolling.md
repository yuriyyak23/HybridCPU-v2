# 15 — Architecture-driven loop unrolling

## Goal and motivation

Select bounded unroll factors from measured recurrence/resource/pressure behavior, not generic width or hotness alone, and adopt only when cycles improve within code-growth and register budgets.

**Status:** `Planned`.

## Confirmed starting state

- No architecture-driven unroll planner is present in the audited compiler pipeline.
- Phase 13 loop/MII evidence does not exist yet; unrolling before it would guess at recurrence and resource bottlenecks.
- Candidate benchmark width is already about 4.46–4.56 for packed VT while memory stalls dominate; occupancy alone is not an adequate objective.

## In scope

- Factors `{1,2,4,8}` capped by loop/body budgets.
- Remainder handling, dependence/value remapping and interaction with modulo scheduling.
- Profitability from cycles, MII relief, code growth, register pressure, speculation and compile cost.
- Shadow estimates before codegen adoption.

## Explicit non-goals

- No arbitrary factor search, profile-derived legality, vector semantic rewrite, fusion/unroll-and-jam or runtime fast path.
- No transformation of irreducible/unknown-effect loops.

## Canonical types/components

`HybridCpuUnrollPlanner`, `UnrollCandidateV1`, `UnrollProfitabilityReportV1`, canonical loop/dependence graph, resource model, pressure estimator and modulo/block schedulers.

## Target architecture and data flow

```text
verified loop + baseline schedule/MII/profile profitability
 -> enumerate bounded factors
 -> clone/remap in an isolated candidate IR
 -> rebuild dependence/resource/pressure evidence
 -> schedule + validate candidate
 -> lexicographic cycles/code-growth/pressure cost
 -> adopt or keep factor 1
```

## Invariants and authority boundaries

- Clone/remap preserves program order where dependences require it, exits, side effects and exact remainder semantics.
- Profile chooses whether to spend compile/code budget, never legality.
- W=8 topology, SystemSingleton, serialization, hard pins and no-cross-contour constraints apply after transformation.
- Runtime still validates every emitted bundle/certificate.

## Dependencies

Phase 13 verified. Phase 14 modulo scheduling is an optional candidate scheduler, not a prerequisite: factor candidates must also support the verified non-modulo block/region path.

## Implementation backlog

1. Define factor candidate and rejection taxonomy.
2. Build isolated deterministic cloning/remapping with source-map provenance.
3. Generate peeled/remainder path with equivalence checks.
4. Recompute distance DAG, all MIIs and pressure for every candidate.
5. Schedule through the normal block/modulo path; never reuse stale placement evidence.
6. Estimate dynamic cycles from trip distribution only after static correctness succeeds.
7. Apply default budgets: body <=64 ops, transformed <=256 ops, factor <=8, code growth <=15%, pressure <= configured cap.
8. Adopt only for >=2% estimated cycle improvement and non-worse correctness/authority class.
9. Add shadow mode and per-loop diagnostics.
10. Freeze transformed IR/schedule goldens for selected loops.

## Migration and compatibility strategy

Default factor remains 1. First release runs estimates only; codegen is allowlisted and default-off. Any rejection or deterministic work-budget exhaustion returns the original loop IR and schedule without compatibility API removal.

## Tests

### Positive/property/determinism

- Exact/misaligned trip counts, factor/remnant combinations, carried values, multiple exits rejected or normalized.
- Transformed/reference execution equivalence and dependence validation.
- Identical profile/input yields identical factor and bytes.

### Negative

- Side-effect/exception ordering, unknown trip behavior, overflow, high pressure, hard-pin/resource collision, code-growth and compile-budget rejection.

### Static

- No profile fact enters dependence or legality construction.
- No runtime PRF/rename/commit/retire mutation.

## Benchmarks and KPI

- Report factor, schedule cycles, bundles, all MIIs before/after, code growth, peak pressure, compile time/memory and runtime cycles/IPC/memory stalls.
- Qualified transformed loops: estimated and measured cycles improve >=2%; representative suite geomean cycles <=0.99 and no workload >1.01.
- Default code growth <=15% per loop and <=5% module; compile p95/memory <=1.10 over the Phase 13 non-transform baseline.
- Width/occupancy remain secondary diagnostics.

## Diagnostics and telemetry

Factors considered, reject reasons, MII deltas, pressure/live-range deltas, schedule/oracle gap, remainder cost, profile confidence, code growth and measured-vs-estimated cycles.

## Bounded-search, timeout and fallback policy

At most four factors, 256 transformed nodes/factor, 4096 remap/dependence operations and inherited scheduler state/iteration limits per loop. Evaluate in numeric order; the first counter limit returns factor 1. Production output never branches on elapsed time. A CI watchdog may fail the run without selecting code.

## Risks and forbidden shortcuts

- No “wider bundle means profitable” rule.
- No factor explosion, stale dependence reuse or pressure-budget waiver for hot code.
- No profile proof of trip count or alias independence.

## Rollback / kill switch

Global/per-loop flag forces factor 1; transformed provenance permits exact attribution.

## Acceptance and merge gates

- Semantic/property/determinism and resource-validation suites pass.
- Code-growth, compile-resource and runtime-cycle gates pass on frozen representative workloads.
- Remainder/exception behavior is independently reviewed.

## Status criteria

- `implemented`: bounded planner and isolated transform exist.
- `verified`: equivalence, budget and performance gates pass.
- `default-enabled`: only for qualified loop classes after Phase 24.
- `release-authorized`: limited compiler transform; no runtime authority claim.

## Residual work / next gate

Phase 16 considers fusion and unroll-and-jam as separate transformations with stronger gates.
