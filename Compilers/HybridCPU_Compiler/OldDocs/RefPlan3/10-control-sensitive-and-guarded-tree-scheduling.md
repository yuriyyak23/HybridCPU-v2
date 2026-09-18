# 10 — Control-sensitive motion and capability-gated guarded-tree scheduling

## Goal and motivation

Improve scheduling across decision trees without assuming general predication. A mandatory speculation-free mode preserves control/effect order; guarded motion is an optional subset enabled only by Phase 09 proven capability rows.

**Status:** `Planned`.

## Confirmed starting state

- EBB scheduling stops before guarded diamonds/trees.
- `PredicateMask` exists for vector masking, but general control guard semantics are not established.
- Memory aliasing and guarded traps/stores are central legality constraints, not profile decisions.

## In scope

- Speculation-free control-sensitive scheduling that reorders only within dominance/control-equivalent segments.
- Optional `IrGuardIdentity`/guarded dependence for Phase 09 `ProvenSupported` opcode subset.
- Guard-preserving lowering/evidence only when the existing encoding/runtime semantics are proven.

## Explicit non-goals

- No new predication ISA, speculative loads/stores/traps, if-conversion of unsupported opcodes or hyperblock duplication.
- No profile-derived guard probability as legality.

## Canonical types/components

Proposed `ControlSensitiveSchedulingRegion`, optional `IrGuardIdentity`, `GuardedDependence`; Phase 07 EBB, Phase 08 memory and Phase 09 capability matrix.

## Target architecture and data flow

```text
decision-tree CFG + EBB/liveness/memory evidence + capability matrix
 -> partition control-equivalent segments
 -> speculation-free scheduling always available
 -> optional proven guard subset scheduling
 -> existing exact placement/materialization
 -> effect/control/reference validation
 -> EBB fallback
```

## Invariants and authority boundaries

- Unsupported/unresolved opcode is never moved under a synthesized guard.
- Traps, stores, atomics, system effects and MayAlias remain ordered unless an explicit proven contract permits motion.
- Guard identity is preserved in encoded/runtime-authoritative form, not compiler-only annotations.
- Runtime legality/execution/commit remains final authority.

## Dependencies

Phases 07, 08 and 09 verified.

## Implementation backlog

1. Define control-equivalence segments and speculation-free motion rules.
2. Build decision-tree region without CFG duplication.
3. Apply dependency/liveness/memory barriers across segments.
4. Add optional guard identity only for capability-matrix supported rows.
5. Rebuild dependencies/resources after any guarded representation change.
6. Preserve relocation/cache/replay identity and validate lower/decode round-trip.
7. Compare path-weighted profitability after static correctness.
8. Fall back to EBB schedules on any unsupported row or budget cap.

## Migration and compatibility strategy

Speculation-free mode ships first and default off. Guarded subset is a separate feature flag and can remain permanently disabled. No new encoded guard ABI is introduced here.

## Tests

### Positive/property/determinism

- Diamonds/trees with control-equivalent pure operations; supported vector-predicate subset if Phase 09 permits.
- Per-path reference/effect trace, decode round-trip and deterministic output.

### Negative

- Unsupported predicate row, store/trap/atomic/system/MayAlias, guard-definition dominance failure, cache/relocation identity mismatch and cap.

### Static

- Guarded path requires `ProvenSupported` capability.
- No profile-to-legality or compiler-only guard serialization.

## Benchmarks and KPI

- Speculation-free representative cycles <=1.00x EBB baseline and no correctness delta.
- Guarded subset, if enabled: geomean cycles <=0.99x, no workload >1.01x, code growth zero.
- Compile time/memory p95 <=1.08x; guard rejection reasons fully accounted.

## Diagnostics and telemetry

Region/path/segment IDs, capability rows, moved operations, memory/control blockers, path cycles, pressure, search states/prunes and fallback.

## Bounded-search, timeout and fallback policy

No production wall-clock cutoff. Max 16 blocks, 128 ops, 32 paths and 4096 search states; deterministic limit returns constituent EBB schedules. CI watchdog cannot select code.

## Risks and forbidden shortcuts

- No assumption that `PredicateMask` is general predication.
- No speculative hyperblock behavior hidden in control-sensitive scheduling.
- No guard repair in `BundleFormer`.

## Rollback / kill switch

Independent switches disable guarded subset or all tree scheduling and restore EBB schedules.

## Acceptance and merge gates

- Phase 09 decision enforced by static/dataflow tests.
- Per-path/effect/exception/memory properties pass.
- Determinism, budget and cycle gates pass separately for each mode.

## Status criteria

- `implemented`: speculation-free tree scheduler and optional capability adapter exist.
- `verified`: control/effect properties and KPI gates pass.
- `default-enabled`: speculation-free subset only after qualification; guarded subset separately decided.
- `release-authorized`: no ISA extension or runtime authority.

## Residual work / next gate

Phase 11 proceeds only if Phase 09 proves sufficient general predication; otherwise it becomes a separate ISA-extension proposal.
