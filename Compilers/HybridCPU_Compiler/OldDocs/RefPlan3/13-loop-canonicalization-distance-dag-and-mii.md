# 13 — Loop canonicalization, distance dependences, HybridCPU MII and stage bound

## Goal and motivation

Create a correctness-neutral loop representation, auditable lower bounds on initiation interval and
a finite, witnessed upper bound on the scheduling horizon before attempting modulo scheduling. A
loop is eligible only when control, memory and carried-dependence evidence is explicit.

**Status:** `Planned`.

## Confirmed starting state

- `HybridCpuProgramDependencyAnalyzer` exposes inter-block carried edges, but `HybridCpuLocalListScheduler` schedules each basic block independently.
- `ParallelRegionDetector` recognizes a narrow parallel-for shape; it is not a scheduling-region or general loop-canonicalization owner.
- No `LoopCanonicalizer`, distance dependence graph, `RecMII`, `SlotMII`, `PortMII`, `RegGroupMII`,
  `MemMII`, `ChosenII` or modulo stage-bound implementation exists.
- Existing hot-loop and loop-phase scheduler options are profitability heuristics and default off in the canonical compiler.

## In scope

- Natural-loop discovery, reducibility checks, preheader/latch/exit normalization as an analysis-first representation.
- Distance-labelled register, memory, control and serialization dependences.
- HybridCPU lower-bound family and reasoned `ChosenII`.
- For each selected candidate II, an analysis-only finite stage/horizon bound derived from the
  versioned difference-constraint graph by Floyd-Warshall or an equivalent deterministic APSP
  algorithm.
- Eligibility/rejection diagnostics and a frozen loop corpus.

## Explicit non-goals

- No code motion or CFG mutation in the first slice.
- No modulo schedule generation, unrolling, speculative alias proof or profile legality.
- No reuse of `ParallelRegionDetector` as a generic loop owner.

## Canonical types/components

`IrNaturalLoop`, `IrCanonicalLoopView`, `IrLoopDependenceEdge` (`latency`, `distance`, `kind`,
evidence), `IrLoopDependenceGraph`, `HybridCpuLoopMiiReport`,
`HybridCpuModuloStageBoundAnalyzer`, `ModuloStageBoundReport` (`InitiationInterval`,
`MaxOperationSeparation`, `MaxStageCount`, `Witness`, `BoundKind`), and adapters to
`IHybridCpuMachineResourceModel` and `IrSchedulingRegion`.

## Target architecture and data flow

```text
CFG + dominance + explicit effects
 -> natural-loop discovery/reducibility
 -> canonical analysis view
 -> distance dependence graph
 -> RecMII / SlotMII / PortMII / RegGroupMII / MemMII
 -> ChosenII=max(proven lower bounds)
 -> candidate-II difference-constraint graph
 -> deterministic APSP -> finite MaxStageCount + witness
 -> eligibility + evidence digest
```

Unknown alias, effect or distance remains a conservative edge or rejects the loop; it is never erased by profile frequency.
`ChosenII` and `MaxStageCount(II)` are deliberately different facts: the former is a lower bound on
the initiation interval, while the latter bounds the finite search horizon for a selected II. The
stage-bound analyzer uses the same versioned recurrence inequalities as Phase 14, checked integer
arithmetic and explicit infinity/inconsistency dispositions; it does not silently substitute an
unbounded constant.

For every dependence `u -> v` with latency `l` and iteration distance `d`, the canonical lower
constraint `start(v)-start(u) >= l-II*d` is represented in the upper-bound graph by the reverse
edge `v -> u` with checked weight `d*II-l`. Deterministic APSP plus the versioned anchor constraints
produces witnessed pairwise upper bounds. If the required separations are not finite, the analyzer
returns `UnsupportedOrUnknown`; otherwise the report projects the maximum bounded time separation
to `MaxStageCount` using the report schema's explicit modulo-cycle/stage convention.

## Invariants and authority boundaries

- Distance zero/positive semantics and latency units are versioned and deterministic.
- `ChosenII` is a lower bound, not a promise of runtime issue or FSP acceptance.
- A stage bound is a compiler search bound, not a proof of placement, emitted-code correctness or
  runtime issue.
- SystemSingleton, serialization, hard-pinned lane and no-cross-contour rules remain hard constraints.
- Compiler models scheduling resources only; PRF allocation, replay, commit and retire remain runtime-owned.

## Dependencies

Phases 03, 04, 05 and 08 verified.

Implementation may land in two evidence-separated slices without weakening the phase gate:

- `13A` depends on Phases 03, 04 and 08 and owns natural-loop IR, the distance DAG, recurrence/
  topology bounds and selected-II stage-bound analysis.
- `13B` additionally depends on Phase 05 and adds pressure/register-aware bounds and the lifetime
  facts later consumed by modulo value-versioning analysis.

Phase 13 is `Verified` only when both required slices and their combined deterministic report pass;
the split shortens the analysis path but does not authorize Phase 14 from partial evidence.

## Implementation backlog

1. Define loop IDs and stable block/edge ordering from CFG/dominator evidence.
2. Build an analysis-only canonical view; record why irreducible, multi-entry or exceptional loops are rejected.
3. Convert register dependences to `(latency,distance)` edges; preserve anti/output edges unless optional Phase 06 proves a safe local allocation and rename.
4. Consume Phase 08 proof-labelled memory/affine relations; `MayAlias` remains a carried edge or eligibility rejection.
5. Compute `RecMII` from recurrence cycles with checked arithmetic and a witness.
6. Compute slot/class, PRF port, register-group, memory/bank and channel lower bounds from the shared resource model.
7. Emit every MII and the resource/witness determining `ChosenII`.
8. For each candidate II, construct the corresponding difference-constraint graph and compute
   deterministic all-pairs bounds with Floyd-Warshall or an equivalent APSP implementation.
9. Derive a finite conservative `MaxOperationSeparation` and `MaxStageCount`; retain a constraint-
   path witness and return `UnsupportedOrUnknown` on inconsistency, unboundedness or overflow.
10. Cross-check tiny graphs and stage bounds against exhaustive enumeration and, when available in
    CI/research, Phase 12 oracle bounds; production analysis never loads that backend.
11. Freeze representative reducible, irreducible, alias-unknown, singleton-resource and multi-stage
    loops.
12. Add metrics without changing emitted schedules.

## Migration and compatibility strategy

Analysis is additive and default emit/compare-only. Existing block scheduling remains byte-identical when loop analysis is disabled or rejects a loop. Schema versions and model digests make stored reports non-authoritative when stale.

## Tests

### Positive/property/determinism

- Single/multiple recurrence, distance >1, mixed class/port/register-group/memory bounds.
- `ChosenII >=` every component MII; increasing a demand cannot reduce its resource lower bound.
- Finite stage bounds match exhaustive tiny-loop horizons for each tested II; increasing the
  permitted horizon cannot invalidate an already valid witness.
- Reordered dictionary/enumeration input yields identical loop IDs and report bytes.

### Negative

- Irreducible/multi-entry loops, unknown alias/effect, invalid distance, zero-distance positive-
  latency cycles, inconsistent/unbounded stage constraints, overflow and unsupported control.
- Profile claims independence but canonical effects do not: reject or retain the edge.

### Static

- No runtime authority type or `ParallelRegionDetector` mutation.
- No CFG/code emission from analysis-only entry points.

## Benchmarks and KPI

- Report `RecMII / SlotMII / PortMII / RegGroupMII / MemMII / ChosenII` and the selected-II
  `MaxStageCount` for 100% of accepted corpus loops.
- 100% agreement with exhaustive tiny-loop lower bounds and horizon bounds; every rejection has a
  stable reason and witness when applicable.
- Analysis compile-time p95 <=5% and peak-memory <=5% over Phase 00 when enabled; zero emitted-byte change.
- Record candidate loop trip/hotness only as profitability context.

## Diagnostics and telemetry

Loop/header/latch IDs, eligibility, edge counts by kind/distance, all MII values and witnesses,
candidate II, difference-constraint/APSP digest, maximum operation separation, maximum stage count,
bound witness/kind, alias uncertainty, pressure estimates, model/profile digests and rejection reason.

## Bounded-search, timeout and fallback policy

Natural-loop, MII and stage-bound analysis has caps of 256 blocks, 4096 ops and 16384 edges per
loop. APSP work is bounded by those structural counters and checked matrix dimensions. Cap,
unbounded constraint or overflow yields `UnsupportedOrUnknown`, and normal block scheduling
continues.

## Risks and forbidden shortcuts

- Do not label a heuristic bound exact without a witness/proof category.
- Do not use an arbitrary stage cap in place of a proved conservative bound.
- Do not infer legality from profile or dynamic non-conflict observations.
- Do not mutate CFG before analysis-only equivalence and rollback exist.

## Rollback / kill switch

Disable loop analysis/reporting; block schedules, lowering and runtime ingress remain unchanged.

## Acceptance and merge gates

- Frozen loop classification, MII and stage-bound golden corpus.
- Exhaustive lower-bound/horizon cross-check, determinism and budget tests pass; optional Phase 12
  reports are qualification evidence, not a production prerequisite.
- Zero codegen delta and no unsupported loop silently accepted.
- Compiler/runtime authority review confirms lower bounds are non-authoritative evidence.

## Status criteria

- `implemented`: canonical loop view, distance DAG, all MII reports and selected-II stage-bound
  reports exist.
- `verified`: proofs/cross-checks, corpus and overhead gates pass.
- `default-enabled`: analysis/telemetry only after overhead qualification.
- `release-authorized`: does not authorize modulo codegen.

## Residual work / next gate

Phase 14 consumes only verified loop graphs, MII evidence and finite selected-II stage bounds;
transformations wait for Phases 15–16.
