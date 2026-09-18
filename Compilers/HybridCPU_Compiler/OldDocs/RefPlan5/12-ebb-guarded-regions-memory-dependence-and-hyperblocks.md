# 12 — EBB/guarded regions, memory dependence and capability-gated hyperblocks

## Goal

Safely expand scheduling beyond one basic block while keeping control/speculation semantics explicit and conservative.

## Dependencies

Phase 06 `SchedulingRegion`, Phase 07 liveness/pressure, Phase 08A target capability/register facts, and Phase 01–03 resource/placement foundation. Region formation may be implemented earlier in shadow mode, but no motion may be enabled until the required semantic/capability facts exist.

## Required sequence

1. Form deterministic EBB candidates from the Phase 06 region abstraction.
2. Refine memory dependencies with a lattice such as `MustAlias/NoAlias/MayAlias/Unknown`, preserving conservative edges.
3. Audit predicate/speculation capabilities per opcode/side effect and target capability version.
4. Enable guarded-tree scheduling only where side exits and predicates preserve semantics.
5. Enable hyperblocks only behind an explicit architecture capability and profitability gate.
6. Carry control-dependence, guard and exception/fault witnesses into the schedule result so later phases can verify that motion remained legal.
7. After every region transform, rebuild/invalidate dependency, liveness/pressure, resource and placement-derived state before scheduling.

## Reuse

Reuse inter-block dependency analysis, Phase 01–03 resource model, bounded cycle composition and exact W=8 placement. Do not use `IrParallelRegion` decomposition as a scheduling proof. Exact W=8 placement remains the final structural witness for every emitted cycle; region scheduling cannot invent a second slot model.

## Guard/speculation legality

- A guard is a semantic control condition, not merely a branch-probability hint.
- Potentially trapping/faulting instructions cannot be hoisted across control unless the target explicitly guarantees non-faulting/speculative behavior for that operation and operands, or an equivalent verified compensation mechanism exists.
- Stores, atomics, volatile operations, fences, calls with unknown effects, system/serialization state and lane6/lane7 special contours require explicit movement rules; absence of a rule means no motion.
- Profile likelihood may decide whether an already-legal region is profitable; it cannot legalize a guard, alias relation or speculative execution.
- Hyperblock formation is `ExperimentalDefaultOff` until a target capability contract identifies the required predication/speculation semantics. Do not infer hyperblock support from the existence of VLIW slots or virtual SMT.

## Safety / fallback

Unknown alias, predicate or exception/fault behavior blocks motion. Loads/stores, atomics, lane6/lane7 operations, system state and serialization ops require explicit movement rules. Any region rejection falls back deterministically to BB-local scheduling.

A deterministic bounded region candidate limit controls production behavior. Wall-clock timeouts may abort a test/process but cannot select a different region or emitted schedule.

## Tests / acceptance

Positive EBB cases plus side-exit, may-alias, volatile/atomic, trap/fault, call, SystemSingleton, lane6/lane7 and unsupported-predicate negative controls. Region schedule must preserve dependency/control witnesses and BundleFormer membership immutability.

Add profile negative controls showing that extreme branch probabilities cannot change a forbidden motion into an allowed one. Add stale-witness tests: mutating the region/CFG after dependence analysis must invalidate the schedule request. Add generated differential tests comparing region execution semantics against BB fallback for bounded kernels.

**Acceptance:** default-off region scheduling shows deterministic benefit on qualified cases with zero semantic regressions; BB fallback is byte-identical to Phase 06 behavior; all emitted region cycles have shared exact W=8 placement witnesses and explicit control/memory legality provenance.
