# 07 — Extended basic-block region formation and scheduling

## Goal and motivation

Extend the verified BB-only region contract to single-entry EBBs with explicit boundary liveness and conservative cross-block motion.

**Status:** `Planned`.

## Confirmed starting state

- Current production scheduling is per basic block; the inter-block dependency graph is not a motion authorization.
- Phase 04 supplies the region abstraction and Phase 05 supplies boundary liveness/pressure.
- Phase 06 allocation is optional and cannot be required for EBB correctness.

## In scope

- Deterministic maximal single-entry EBB formation over simple fallthrough chains.
- Cross-block scheduling with explicit dependences, liveness, control/exception/effect barriers and pressure budgets.
- Exact fallback to constituent BB schedules.

## Explicit non-goals

- No guarded diamonds, predication, speculative loads/stores, loop modulo scheduling or CFG duplication.
- No cross-contour/domain/owner region and no dependence inference from profile.

## Canonical types/components

`IrSchedulingRegion` with `ExtendedBasicBlock` kind, proposed `HybridCpuEbbRegionFormer`, Phase 05 liveness, dependency graph, joint scheduler and existing exact materializer.

## Target architecture and data flow

```text
CFG + BB regions + liveness/dependences/resources
 -> deterministic EBB candidate chain
 -> legality/budget proof
 -> region DAG + bounded joint scheduling
 -> existing placement/materialization
 -> adopt or concatenate original BB schedules
```

## Invariants and authority boundaries

- Single entry; side exits and block identity remain explicit.
- Traps, barriers, system/control, serialization, volatile/atomic/unknown memory and hard pins constrain motion.
- `BundleFormer` never repairs a region schedule.
- Runtime legality and retirement remain unchanged.

## Dependencies

Phases 05 and 06 verified; Phase 06 may be disabled at runtime/default policy.

## Implementation backlog

1. Define stable EBB eligibility and maximal-chain ordering.
2. Compute entry/side-exit/live-out boundaries and pressure envelope.
3. Project intra/inter-block dependencies into one region DAG.
4. Prohibit motion across unresolved effect, trap, memory, contour/domain/VT or serialization boundaries.
5. Schedule with Phase 02 search and Phase 01/03 resources.
6. Preserve source/relocation/cache identity for every moved instruction.
7. Validate side-exit state/effect traces.
8. Fall back to the exact captured BB schedules on any rejection/cap.

## Migration and compatibility strategy

BB-only remains default and fallback. EBB begins shadow-only, then allowlisted. No public/serialized compatibility API removal.

## Tests

### Positive/property/determinism

- Straight-line chains, legal latency hiding and boundary live values.
- CFG-generated reference execution/effect equality and 100-run determinism.
- Phase 06 enabled/disabled both produce legal EBB or identical fallback.

### Negative

- Multiple entry, side-effect/trap/unknown memory, cross-contour/domain, system/serialization and pressure-cap cases.

### Static

- No guarded/hyperblock transform.
- Every cross-block move has a dependence/liveness/effect witness.

## Benchmarks and KPI

- Representative EBB `schedule_cycles` geomean <=0.99x BB baseline; no workload >1.01x.
- Bundle count <=1.00x, pressure within Phase 05 limits, code growth zero.
- Compile time/memory p95 <=1.08x; cap/fallback rates reported.

## Diagnostics and telemetry

Region blocks/edges, motion count, boundary liveness, pressure, cycles/bundles before/after, search states/prunes and fallback reason.

## Bounded-search, timeout and fallback policy

No wall-clock selection. Max 8 blocks, 128 instructions, 512 dependencies and inherited Phase 02 state caps. Limit deterministically concatenates original BB schedules.

## Risks and forbidden shortcuts

- No treating the inter-block graph as sufficient motion proof.
- No hidden CFG repair or cross-contour fallback.
- No requiring optional renaming for correctness.

## Rollback / kill switch

Disable EBB former and use verified BB-only regions/schedules.

## Acceptance and merge gates

- Side-exit/effect/reference tests and deterministic fallback pass.
- Cycle/compile/pressure gates pass on frozen corpus.
- Relocation/cache/source identity audit complete.

## Status criteria

- `implemented`: EBB former/scheduler/fallback exist.
- `verified`: correctness, determinism and KPI gates pass.
- `default-enabled`: only for qualified simple chains.
- `release-authorized`: compiler motion scope only.

## Residual work / next gate

Phase 08 refines memory dependences; Phase 09 audits whether guarded scheduling is representable by the ISA contract.
