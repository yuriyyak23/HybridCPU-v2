# 02 — Bounded joint CycleGroup composition plus existing exact placement

## Goal and motivation

Remove the confirmed greedy membership gap by comparing complete, feasible cycle alternatives and their exact placement quality before committing an `IrScheduleCycleGroup`. Reuse `HybridCpuSlotModel.SearchStructuralAssignments`; do not build a new packer.

**Status:** `Verified` default-off on implementation subject `f9cf700`; evidence is in
`evidence/2026-08-25-phase02-joint-cycle-f9cf700/`. Representative output is exact parity, so the
`<=0.99x` improvement target and default enablement remain unqualified.

## Confirmed starting state

- `BuildCycleGroup` repeatedly selects one `ReadyNodePriority` winner.
- Each append receives exact “some assignment exists” validation through `AnalyzeStructuralAssignment`.
- The choice is irreversible even if a different ready subset would reduce cycles.
- Exact placement enumeration/Pareto/transition selection occurs later in `HybridCpuBundleFormer`, which cannot repair membership.
- Canonical compilation falls back to program order when program >192 instructions or a block >48; search-specific telemetry is absent.

## In scope

- A bounded `HybridCpuCycleGroupSearch` over a deterministically preselected ready window.
- Complete alternative cost using hard feasibility, existing exact placement result and bounded lookahead summary.
- Immutable chosen membership plus optional placement witness for consistency checking/materialization reuse.
- Deterministic greedy fallback and adversarial/oracle comparison.

## Explicit non-goals

- No new exact slot packer; no changes to runtime Stage B.
- No infinite growth of `ReadyNodePriority`.
- No unbounded, wall-clock-driven or monolithic whole-function constraint search.
- No cross-block motion yet.
- No profile legality or FSP authority.

## Canonical types/components

- `IrCycleSearchRequest/Result/SummaryV1`, `IrCycleMembershipCandidate`, `IrCyclePlacementWitness`.
- `HybridCpuCycleGroupSearch` owned by scheduling.
- `IHybridCpuMachineResourceModel` from Phase 01.
- Existing `HybridCpuInstructionLegalityChecker` and `HybridCpuSlotModel.SearchStructuralAssignments` as feasibility/placement backends.
- `HybridCpuBundleFormer` consumes schedule and validates witness, but does not choose different members.

## Target architecture and data flow

```text
ready nodes (stable order)
 -> deterministic top-K window
 -> bounded subset/beam expansion, depth <= 8
 -> dependency + CycleResourceState feasibility
 -> SearchStructuralAssignments(existing masks)
 -> Pareto placement + lexicographic cycle cost
 -> stable winning membership/witness
 -> immutable IrScheduleCycleGroup
 -> BundleFormer validates/materializes same membership
```

Lexicographic comparison: feasibility; projected block makespan; critical-path release; unavoidable stall; placement quality/transition; resource pressure; register pressure placeholder; productive width; FSP profitability; instruction-index/slot vector tie-break.

## Invariants and authority boundaries

- Correctness and hard feasibility dominate every performance term.
- Same input/profile/options/model digest yields identical output independent of hash/dictionary enumeration.
- A placement witness is compiler structural evidence, not runtime lane permission.
- `BundleFormer` may recompute/validate exact placement, but any membership or cycle change is an error.
- SystemSingleton, serialization, hard pins and no-cross-contour rules remain hard.

## Dependencies

Phase 01 verified base resource model; Phase 00 corpus and metrics.

## Implementation backlog

1. Freeze search request ordering and stable candidate identity.
2. Add an adversarial corpus where greedy first choice blocks a better pair/set.
3. Implement top-K (`12` initial) from existing readiness priority only as a preselector.
4. Enumerate subsets with canonical include/exclude order, depth <=8, dominance and upper/lower-bound pruning.
5. Use incremental cycle state for cheap pruning; revalidate survivors with current legality checker.
6. Invoke existing exact placement search only for structurally viable alternatives; retain Pareto placement/witness.
7. Compute lexicographic cost and stable tie-break; never weighted-sum correctness with performance.
8. Add optional one-cycle successor lower bound; do not duplicate bundle pair/triplet global placement.
9. Define hard budgets: 4096 evaluated states/cycle, beam 64, top-K 12; make them versioned options.
10. On budget exhaustion, select the best fully validated candidate seen; if none, call the exact existing greedy path.
11. Pass witness/member hash to `BundleFormer`; assert no policy repair.
12. Shadow compare, then default-off A/B, then qualified enablement.

## Migration and compatibility

- `UseJointCycleComposition=false` is bit-identical existing behavior.
- Schedule/result schemas add optional search summary/witness fields.
- Current scheduler thresholds (192/48) remain until Phase 00 data justifies separate limits.
- No public compatibility surface removal.

## Tests

### Positive/property/determinism

- Greedy counterexamples reduce `schedule_cycles` or bundles as predeclared.
- Every selected subset is ready, legal, <=8 and has a materializable exact assignment.
- Permuting source collection insertion order cannot change output.
- 100-run schedule/image fingerprint equality.

### Negative

- Empty ready set, all alternatives illegal, hard-pinned collision, SystemSingleton sharing, serialization, state cap, placement backend failure and stale witness.
- Profile variants may change cost only when enabled; they cannot change the feasible alternative set.

### Static

- `HybridCpuBundleFormer` contains no split/reorder/reschedule fallback.
- Joint search calls existing `SearchStructuralAssignments`; no second assignment enumerator.
- Production path has explicit budgets and deterministic fallback.

## Benchmarks and KPI

- Adversarial suite: at least one case improves cycles by >=1 and none regresses cycles/bundles.
- Representative corpus: schedule-cycle geomean <=0.99x, no case >1.01x; bundle geomean <=1.00x.
- Search cap-hit rate <=1%; p95 states <=2048; hard maximum 4096; emit pruned counts/reasons.
- Compile time p95 <=1.10x and memory <=1.15x; regression thresholds 1.10x/1.15x.
- Width reported as secondary; no pass based solely on occupancy.
- Runtime cycles/IPC must be non-regressing within the README thresholds before default-enable.

## Diagnostics and telemetry

`ready_window_size`, `window_truncated`, states expanded/evaluated/pruned by reason, cap/fallback, winning membership/placement fingerprints, lexicographic cost fields, first baseline divergence cycle and projected makespan.

## Bounded-search, timeout and fallback

State/depth/candidate limits determine production output; no wall-clock cutoff participates in choice. A watchdog may abort/fail compilation as an operational error, but cannot select emitted code or a per-input fallback. Only the deterministic counter limit selects the existing-path fallback. Budget settings are part of the output fingerprint.

## Risks and forbidden shortcuts

- No new packer, heuristic-field accretion masquerading as search, or hidden BundleFormer repair.
- Do not cache placement across different model/profile/input digests.
- Do not allow an unvalidated partial alternative to win on timeout.

## Rollback / kill switch

Global and per-function `UseJointCycleComposition=false`; retain the existing scheduler. A cap-rate auto-disable may choose the existing path for a function only from deterministic input size/state criteria.

## Acceptance and merge gates

- Correctness/image/authority suites pass.
- Adversarial improvement and representative no-regression targets pass.
- Search bounds, fallback, determinism and witness/materializer agreement proven.
- Oracle metrics may be absent until Phase 12, but all frozen small cases must retain enough trace for later comparison; production never requires that backend.

## Status criteria

- `implemented`: bounded search and existing-path fallback compile and are default-off.
- `verified`: tests/KPIs/determinism/budget gates pass.
- `default-enabled`: representative cycle and compile-cost gates pass twice on attributable subjects.
- `release-authorized`: explicit compiler release record; no runtime fast path implied.

## Residual work / next gate

Phase 03 enriches feasibility/cost with register/PRF/bank/channel/certificate resources. Phase 05 enlarges the region only after pressure modelling.
