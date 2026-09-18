# 04 — First-class SchedulingRegion abstraction in BB-only compatibility mode

## Goal and motivation

Introduce the canonical region boundary before liveness, pressure or renaming needs region closure. The first implementation represents exactly one existing basic block and must preserve current output byte-for-byte.

**Status:** `Planned`.

## Confirmed starting state

- Current dependence DAG and `HybridCpuLocalListScheduler` operate per `IrBasicBlock`.
- `HybridCpuProgramDependencyAnalyzer` also produces an inter-block graph, but there is no `IrSchedulingRegion` owner.
- Region ownership precedes every phase that requires region-wide def-use closure.
- `ParallelRegionDetector` is a narrow parallel-for analysis and is not a scheduling-region abstraction.

## In scope

- Immutable `IrSchedulingRegion` identity, ordered blocks/instructions, entry/exit edges, contour/domain/VT ownership and boundary value sets.
- `BasicBlockRegionFormer` that creates one region per current block.
- Adapters so current scheduler, metrics and bundler consume a region without behavior change.

## Explicit non-goals

- No EBB formation, cross-block motion, liveness optimization, renaming, predication or CFG mutation.
- No reuse of `ParallelRegionDetector` as the canonical region owner.

## Canonical types/components

Proposed `IrSchedulingRegion`, `IrRegionBoundary`, `IrRegionIdentityV1`, `BasicBlockRegionFormer`; existing `IrBasicBlock`, CFG, dependence graphs, scheduler and bundler remain the underlying truth.

## Target architecture and data flow

```text
IrProgram + CFG + block/inter-block dependences
 -> BasicBlockRegionFormer
 -> IrSchedulingRegion(one block, stable identity, boundaries)
 -> unchanged readiness/cycle selection/materialization
 -> schedule/bundle fingerprints identical to Phase 00
```

## Invariants and authority boundaries

- Region identity includes program, contour, domain and owner-VT scope; no cross-contour fallback.
- W=8, SystemSingleton, serialization and hard pins are unchanged.
- A region is compiler scheduling scope only; it grants no runtime execution/publication/retire authority.
- Input ordering and all maps/sets used for fingerprints are canonicalized.

## Dependencies

Phases 02 and 03 verified.

## Implementation backlog

1. Define versioned immutable region/boundary types and stable ordering.
2. Materialize one region per `IrBasicBlock` including CFG entry/exit and inter-block dependence references.
3. Add region-to-block compatibility adapter for DAG construction.
4. Route scheduling metrics through region identity without changing selection.
5. Add boundary use/def placeholders required by Phase 05.
6. Assert contour/domain/VT consistency and fail closed on mixed unsupported ownership.
7. Compare schedule, bundle, image and annotations against Phase 00.

## Migration and compatibility strategy

The BB adapter is the only default former. Existing public APIs remain unchanged; region-aware APIs are internal/additive. Disabling the region layer returns to direct per-block scheduling.

## Tests

### Positive/property/determinism

- One-to-one block/region mapping, entry/exit identity and instruction order.
- Random CFGs preserve block schedules and emitted bytes with the adapter on/off.
- Repeated runs produce identical region and artifact fingerprints.

### Negative

- Duplicate/missing block, inconsistent contour/domain/owner VT, malformed CFG edge and stale dependence graph.

### Static

- No EBB/guard/hyperblock formation or runtime owner dependency.
- `ParallelRegionDetector` is not referenced by the canonical region former.

## Benchmarks and KPI

- Exact Phase 00 equality for `schedule_cycles`, `bundle_count`, widths, bundle bytes and annotations.
- Compile time and memory p95 <=1.02x; region formation linear in blocks+edges.
- Every region has stable ID, boundary counts and fallback reason.

## Diagnostics and telemetry

Region ID/kind, block list, contour/domain/owner VT, entry/exit edges, boundary counts, fingerprints and rejection reason.

## Bounded-search, timeout and fallback policy

No search and no wall-clock cutoff. Deterministic structural cap: <=4096 blocks and <=65536 CFG edges per program; excess uses the existing direct BB path with a stable reason.

## Risks and forbidden shortcuts

- Do not hide EBB behavior in the compatibility adapter.
- Do not reinterpret mixed VT/domain/contour ownership.
- Do not change scheduler or placement policy while introducing the abstraction.

## Rollback / kill switch

Disable `BasicBlockRegionFormer` adapter and invoke the captured Phase 00 block path.

## Acceptance and merge gates

- Byte/fingerprint parity and 100-run determinism.
- Boundary and malformed-CFG properties pass.
- Static scan shows no motion or runtime authority edge.

## Status criteria

- `implemented`: BB-only region types/former/adapters exist.
- `verified`: parity, property and overhead gates pass.
- `default-enabled`: only after exact Phase 00 compatibility proof.
- `release-authorized`: abstraction only; no cross-block scheduling authority.

## Residual work / next gate

Phase 05 computes liveness/pressure over this stable scope; Phase 07 later forms EBBs.
