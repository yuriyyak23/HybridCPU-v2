# 16 — Loop fusion and unroll-and-jam profitability

## Goal and motivation

Evaluate fusion and unroll-and-jam only after single-loop scheduling and unrolling are measurable, with independent legality, code-growth, cache, pressure and speculation gates.

**Status:** `Planned`.

## Confirmed starting state

- No general compiler fusion or unroll-and-jam pass was found.
- Runtime profiles show memory stalls on selected artifacts, but do not prove fusion legality or benefit.
- These transformations change memory ordering and live ranges more broadly than Phase 15 and require separate rollback/qualification.

## In scope

- Adjacent canonical-loop fusion with proven compatible iteration spaces and dependences.
- Unroll-and-jam for tightly nested canonical loops with bounded factors.
- Rebuild of dependence, MII, pressure, scheduling and memory-locality evidence.
- Independent profitability reports and feature flags.

## Explicit non-goals

- No speculative aliasing, loop distribution/interchange, arbitrary polyhedral optimizer, profile legality or broad VMX/device activation.
- No combining fusion and unroll-and-jam in the first implementation slice.

## Canonical types/components

`HybridCpuLoopFusionPlanner`, `HybridCpuUnrollAndJamPlanner`, `LoopTransformLegalityReportV1`, `LoopTransformProfitabilityV1`; Phase 08 memory relations, Phase 13 loop graph, Phase 01/03 resources, Phase 05 pressure and Phase 14/15 schedulers.

## Target architecture and data flow

```text
canonical loop pair/nest
 -> static iteration/dependence/effect legality proof
 -> isolated transform candidate
 -> rebuild DAG/MII/resource/pressure/schedule
 -> memory/code-growth/speculation profitability
 -> reference equivalence + normal materialization
 -> separate adopt/rollback decision per transform
```

## Invariants and authority boundaries

- All cross-loop/nest dependences and observable exception/side-effect order are preserved.
- Unknown alias/effect is a legality rejection, not a profile opportunity.
- Runtime legality, FSP validation, completion, commit and retire are unchanged.
- Fusion and unroll-and-jam have distinct correctness/performance/enable gates.

## Dependencies

Phases 08, 13 and 15 verified. Phase 14 is optional for modulo candidates and Phase 12 oracle reports are qualification-only.

## Implementation backlog

1. Define versioned legality/profitability results with witnesses.
2. Implement fusion eligibility for identical proven iteration domains and simple exits.
3. Rebuild cross-loop dependences before any scheduling estimate.
4. Implement shadow fusion candidate, validate and compare cycles/memory/pressure.
5. Qualify fusion separately before starting unroll-and-jam codegen.
6. Define rectangular perfect-nest eligibility and factors `{2,4}` for unroll-and-jam.
7. Clone/remap nest candidates and rebuild all analyses.
8. Apply module/per-loop code-growth and peak-pressure budgets.
9. Measure cache/memory-stall benefit; reject estimated wins not reproduced on representative runs.
10. Add provenance, feature flags and frozen negative corpora.

## Migration and compatibility strategy

Both planners begin shadow-only and default off. Fusion may reach default independently; unroll-and-jam cannot inherit that authorization. Failure returns original loops byte-for-byte.

## Tests

### Positive/property/determinism

- Independent adjacent loops, producer-consumer legal fusion, rectangular nests and remainder cases.
- Reference execution/effect-trace equivalence, rebuilt dependence/resource validation and deterministic output.

### Negative

- Mismatched bounds/steps, cross-iteration dependence, `MayAlias`, volatile/atomic/system effects, exceptions, early exits, high pressure, code growth and deterministic work-budget exhaustion.

### Static

- No shared “hot implies legal” branch.
- Separate feature flags, telemetry names and release gates for both transforms.

## Benchmarks and KPI

- Per transform: schedule/runtime cycles, IPC, memory stalls, all MIIs, code growth, peak pressure, compile time/memory and oracle gap where eligible.
- Qualified workloads: runtime-cycle geomean <=0.98 for fusion or <=0.97 for unroll-and-jam; no representative workload >1.01.
- Module code growth <=5% default, per-transform compile p95/memory <=1.10, zero correctness/effect-trace divergence.
- Memory-stall reduction is supporting evidence, never a substitute for cycles.

## Diagnostics and telemetry

Legality witness/rejection, cross-loop edges, factors, MII and pressure deltas, memory-distance/locality estimate, schedule states, code growth, measured cycles/stalls and rollback path.

## Bounded-search, timeout and fallback policy

Fusion considers only the immediately adjacent canonical pair; unroll-and-jam considers two factors and nests depth <=2, combined candidate <=256 ops. Caps: 4096 dependence pairs, 4096 transform nodes and inherited scheduler state/iteration limits. A counter limit returns the unchanged original; production output never depends on elapsed time. CI may use a non-code-selecting watchdog.

## Risks and forbidden shortcuts

- No speculative fusion before proven dependence/alias evidence.
- No transformation stacking in initial qualification.
- No relaxing pressure, exception or code-growth budgets for benchmark wins.

## Rollback / kill switch

Independent global/per-loop switches restore original loops; transformation provenance identifies generated regions.

## Acceptance and merge gates

- Fusion correctness/performance gates complete before unroll-and-jam codegen begins.
- Negative alias/effect/exception corpus and determinism pass.
- Representative cycle and resource budgets pass; no regression hidden by width.

## Status criteria

- `implemented`: a named transform has isolated planner/codegen and fallback.
- `verified`: that transform's independent gates pass.
- `default-enabled`: per transform after Phase 24 review.
- `release-authorized`: scope-limited compiler transform only.

## Residual work / next gate

No further loop transformation is implied. Phase 24 decides qualification; VT/FSP/VDSA work proceeds through Phases 17–23.
