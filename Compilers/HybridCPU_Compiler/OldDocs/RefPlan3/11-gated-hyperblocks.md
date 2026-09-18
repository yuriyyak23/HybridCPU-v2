# 11 — Predicated hyperblocks behind an ISA-capability gate

## Goal and motivation

Evaluate bounded if-conversion/hyperblock formation only when an existing, end-to-end predication contract is proven. Otherwise record `DeferredToIsaExtension` and do no compiler implementation.

**Status:** `Planned — capability gated`.

## Confirmed starting state

- No first-class general control guard is present in the normalized annotation/dependence contract.
- Existing `PredicateMask` supports vector masking but does not by itself prove guarded scalar/store/trap semantics.
- Hyperblocks also require identity rebuild for relocation, annotations, decoder/replay/cache and boundary liveness.

## In scope

- Capability-gate decision and, only if passed, bounded if-conversion for proven opcode/effect classes.
- Predicate pressure, code-growth, speculation and identity budgets.
- Rebuilt CFG/dependencies/liveness/memory/resources and deterministic EBB/tree fallback.

## Explicit non-goals

- No new ISA encoding or runtime predicate semantics.
- No speculative unknown memory, stores, atomics, traps, system effects or unresolved opcodes.
- No implementation when Phase 09 concludes an ISA extension is required.

## Canonical types/components

`PredicateCapabilityMatrixV1`, proposed `IrHyperblockRegion`, `HyperblockFormationDecisionV1`, Phase 08 memory, Phase 10 guarded/control-sensitive regions, lowerer/decoder identity contracts.

## Target architecture and data flow

```text
Phase 09 capability decision
 -> unsupported/extension-needed: DeferredToIsaExtension, stop
 -> supported subset + Phase 10 tree
 -> bounded if-conversion candidate
 -> rebuild CFG/DAG/liveness/memory/resources/identity
 -> schedule/materialize/lower/decode/reference validate
 -> adopt or deterministic tree/EBB fallback
```

## Invariants and authority boundaries

- Capability gate precedes candidate formation.
- Guard-false architectural effects match ISA semantics exactly.
- Runtime legality/replay/commit/retire authority is unchanged.
- Profile affects profitability only after static equivalence.

## Dependencies

Phases 05, 08, 09 and 10 verified, plus a Phase 09 decision that existing ISA capability is sufficient.

## Implementation backlog

1. Consume and pin capability-matrix version/decision.
2. Define eligible CFG shapes/opcodes/effects and reject all unresolved rows.
3. Form isolated candidates with explicit predicate defs/uses.
4. Rebuild relocation, annotations, decoder/replay/cache identity.
5. Recompute memory dependences, pressure and resources.
6. Schedule with bounded joint search and validate every path/effect.
7. Enforce predicate pressure, code growth and speculation budgets.
8. Archive `DeferredToIsaExtension` evidence if capability gate fails.

## Migration and compatibility strategy

Default off. No encoded ABI change. Capability failure produces no code path, not a partial workaround. Existing EBB/tree output is the fallback and compatibility baseline.

## Tests

### Positive/property/determinism

- Only proven predicate-capable pure/control shapes; path/effect/reference equivalence; stable output.

### Negative

- Capability absent/stale, unsupported opcode, trap/store/atomic/system/MayAlias, predicate pressure, code-growth and identity rebuild failure.

### Static

- Hyperblock construction is unreachable without the capability decision.
- No new predicate encoding or runtime owner call.

## Benchmarks and KPI

- If implemented: representative runtime-cycle geomean <=0.98x qualified tree baseline, no workload >1.01x.
- Default per-function code growth <=5%, hard cap 15%; pressure within Phase 05 cap.
- Compile time/memory p95 <=1.10x; zero semantic/effect divergence.

## Diagnostics and telemetry

Capability decision/version, candidates/accepted/rejected, predicate count/pressure, duplicated ops, code growth, cycles, identity rebuild and fallback.

## Bounded-search, timeout and fallback policy

No wall-clock selection. Max 8 blocks, 64 ops, 2 duplication alternatives/edge and 4096 states. Limit returns Phase 10/07 schedule in stable order.

## Risks and forbidden shortcuts

- No compiler-only emulation of missing ISA predication.
- No benchmark-based capability claim.
- No speculative hyperblock before memory/pressure/guard baselines.

## Rollback / kill switch

Disable hyperblock former; use control-sensitive tree/EBB schedules.

## Acceptance and merge gates

- Explicit capability decision and ISA/runtime owner review.
- Identity, per-path, exception/effect and negative tests pass.
- Independent code-growth/pressure/cycles and rollback gates.

## Status criteria

- `implemented`: only if capability gate passes and bounded former exists.
- `verified`: end-to-end semantics and budgets pass.
- `default-enabled`: separate Phase 24 release decision.
- `release-authorized`: compiler transformation only; missing capability means deferred, not authorized.

## Residual work / next gate

If deferred, create a separate ISA-extension project outside RefPlan3. No later production phase depends on hyperblocks.
