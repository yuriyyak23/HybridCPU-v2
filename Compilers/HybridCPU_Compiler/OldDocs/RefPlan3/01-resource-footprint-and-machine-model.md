# 01 — `IrResourceFootprint`, cycle state and unified machine model

## Goal and motivation

Create one compiler-owned, versioned description of static instruction demand and per-cycle reserved capacity. Replace duplicated/count-by-scan feasibility logic with composable `IrResourceFootprint`, `HybridCpuCycleResourceState` and `IHybridCpuMachineResourceModel` contracts while preserving all current decisions in shadow mode.

**Status:** `Verified` on implementation subject
`e47bc14c908d51ba039ab285ab97e59a9f7590a6`; evidence is in
`evidence/2026-08-25-phase01-resource-model-e47bc14/`.

## Confirmed starting state

- Slot masks come from `HybridCpuSlotModel`; W=8 is explicit.
- Class counts come from `HybridCpuClassCapacityChecker`; structural units come from `HybridCpuStructuralResourceModel`.
- `HybridCpuInstructionLegalityChecker` repeatedly rescans whole candidate lists for pair hazards, capacities, structural units and slot existence.
- Runtime has a richer canonical physical taxonomy in `SlotClassLaneMap` and a live `SlotClassCapacityState`, but those runtime objects cannot be imported as compiler authority.
- No compiler symbols named `IrResourceFootprint`, `HybridCpuCycleResourceState` or `MachineResourceModel` exist.

## Pre-implementation readiness audit (2026-08-19)

This audit is documentation-only. Phase 01 implementation remains gated by the clean-subject
Phase 00 `verified` transition.

- The production model must own an immutable, versioned compiler machine-description snapshot
  and digest. `SlotClassLaneMap` is a read-only parity oracle for tests, not an object imported as
  compiler decision authority.
- Shadow evaluation must report the existing checker decision and `ModelDecision` separately, plus explicit
  mismatch/fallback reason and provenance. It must reproduce the actual production result, including
  the current single-instruction fast path, rather than silently correcting it.
- Per-resource reservation is not proof of W=8 placement. Candidate-level feasibility remains
  delegated to the existing exact `HybridCpuSlotModel` search; an incremental state may return
  `RequiresExactPlacement`, but must not replace exact placement with greedy reservation.
- Current compiler and runtime taxonomies are not fully isomorphic. In particular,
  `IrResourceClass` has no matrix-tile stream class; matrix-tile memory currently enters the
  compiler through generic `LoadStore`, while runtime uses `SlotClass.MatrixTileStreamClass` on
  lane 6. Phase 01 must record this as `Unknown`/unsupported coverage or existing-checker fallback in shadow
  mode. Making it a new hard constraint is a behavior change and is outside this phase.
- `HybridCpuClassCapacityChecker` does not count `MatrixTileStreamClass` or `Unclassified`;
  `HybridCpuStructuralResourceModel.GetCapacity` currently defaults an unrecognized resource to
  capacity one; and candidate legality mixes pairwise structural checks with aggregate summaries.
  The new model must preserve these existing outcomes in its comparison channel while exposing
  unknown facts explicitly in its evidence channel.

The first admissible implementation slice, after Phase 00 is verified on a clean pinned subject,
is therefore contracts and extraction only: versioned machine-description key, provenance and
precision enums, immutable footprint extraction, deterministic fingerprints, and default-off
shadow diagnostics. It must not reserve resources, alter legality, or change scheduling yet.

## In scope

- Static per-instruction footprint extraction for existing compiler facts.
- Immutable target/topology description and mutable-per-search cycle state.
- `CanReserve`, `Reserve`, `IncrementalCost` separation.
- Shadow parity with current slot/class/structural legality.
- Generated/parity checks against canonical W=8 class topology.

## Explicit non-goals

- No cycle membership change, new packer or runtime policy change.
- No register-group/PRF/bank/channel hard constraints yet; Phase 03 adds them.
- No profile-derived legality.
- No compiler creation of PRF, rename, FSP, replay, commit or retire state.
- No provider/contour/API expansion.

## Canonical types/components

- New `IrResourceFootprint` immutable record: slot mask, required `SlotClass`, binding kind/pin, structural-unit counts, serialization/control flags, memory effect/precision and evidence provenance.
- New `HybridCpuCycleResourceState`: occupied compiler slots, class usage, aliased-lane usage, structural usage, serialization occupant, reserved instruction IDs.
- New `IHybridCpuMachineResourceModel` and one default implementation composed from `HybridCpuSlotModel`, `HybridCpuClassCapacityChecker`, `HybridCpuStructuralResourceModel`, `IrSlotClassMapping` and opcode semantics.
- Existing `IrInstructionAnnotation` remains the canonical attached instruction fact set; footprint is a derived query result, not a second mutable truth.

## Target architecture and data flow

```text
IrInstruction + canonical opcode/annotation facts
  -> GetResourceFootprint(instruction)
  -> IrResourceFootprint(provenance=StaticCanonical)
  -> state.CanReserve(footprint, static dependency facts)
  -> state.Reserve(footprint)
  -> optional IncrementalCost(state, footprint, profitability context)
```

Hard feasibility and cost use distinct result types. A profile object cannot be passed to `CanReserve`. The model exposes compiler structural evidence only.

## Invariants and authority boundaries

- W=8 and runtime class topology are versioned inputs; hard pins, SystemSingleton/lane7 alias, lane6 singleton and exclusive-cycle serialization cannot be weakened.
- Compiler early slot masks and runtime class masks remain distinguishable fields; no silent coordinate conflation.
- `Reserve` is deterministic and side-effect-free outside the search-local state.
- Runtime Stage A/B and `SafetyVerifier` still re-evaluate live legality.
- Unknown resource fact is `Unknown`, not zero/free.

## Dependencies

Phase 00 verified baseline and metrics.

## Implementation backlog

1. Define a versioned machine-description key: W, slot masks, class capacities/aliases, structural-unit capacities and contract digest.
2. Define footprint provenance (`StaticCanonical`, `ConservativeFallback`, `Unknown`) and explicit exact/possible/unknown fields.
3. Implement extraction from `IrInstructionAnnotation` and opcode semantics without profile input.
4. Implement copyable cycle state with checked counters/bitsets; reject overflow and duplicate reservation.
5. Implement `CanReserve` with structured reason codes matching current hazard categories without claiming runtime reject identity.
6. Implement `Reserve` only after successful feasibility; make invalid mutation impossible through public API.
7. Implement `IncrementalCost` as a separate profitability service with a stable lexicographic tuple.
8. Run shadow evaluation beside current `AnalyzeCandidateBundle`; record any disagreement with instruction IDs and fact provenance.
9. Add a single adapter so the existing legality checker can consume model summaries without changing its public compatibility APIs.
10. Document generator ownership if W=8 topology is generated; otherwise add a parity test against `SlotClassLaneMap` rather than copying magic numbers.

## Migration and compatibility

- First merge is shadow-only and default-off.
- Current legality checker remains the decision source until exhaustive parity passes.
- Shadow parity is evaluated over the explicitly supported/default-enabled footprint domain.
  Unsupported or unknown facts are counted and fall back to the existing checker; they are never
  converted to zero demand to manufacture agreement.
- Existing public compatibility slot-search APIs remain and delegate as before; no compatibility deletion.
- New schemas use additive versioning and stable enum numeric values.

## Tests

### Positive/property/determinism

- Exhaustive class-count combinations within W=8 and property-generated slot masks.
- Reservation order independence for commutative independent footprints; stable ordered diagnostics otherwise.
- State usage equals fold of individual footprints.
- 100 repeated runs produce identical state/fingerprint/reasons.

### Negative

- >8 candidates, no slot, branch+system alias, two lane6 singleton claims, exclusive-cycle sharing, structural-unit overflow, unknown class, duplicate reserve and counter overflow.
- Profile objects cannot affect or enter feasibility APIs.

### Static

- No dependency from machine-model feasibility namespaces to telemetry profile readers.
- No authority vocabulary (`ExecutionReady`, commit/retire permission) in compiler results.
- Compatibility API declarations remain.

## Benchmarks and KPI

- Shadow disagreement with current compiler: target `0` across exhaustive W<=8 corpus and representative inputs.
- Footprint extraction coverage: `100%` of scheduled instructions; unknown footprint rate `0` for the default-enabled opcode set.
- Added compile time p95 `<=3%`, memory p95 `<=5%` in shadow mode; regression threshold `5%/8%`.
- Emit `peak class usage`, structural usage, lane6/lane7 pressure and footprint-provenance counts; `schedule_cycles`, bundles and output bytes must be exactly unchanged.

## Diagnostics and telemetry

Structured `CompilerResourceDecisionV1`: instruction ID, resource key, requested/used/capacity, fact precision/provenance, current-state digest, old/new decision and mismatch kind.

## Bounded-search, timeout and fallback

No combinatorial search is added. Footprint extraction is O(resources) with fixed-size W=8 state. Unknown/unsupported facts use the current conservative legality path. Any shadow mismatch disables model consumption and retains the existing checker.

## Risks and forbidden shortcuts

- Do not copy runtime mutable state or call runtime admission from the compiler.
- Do not treat compiler-internal slot indices as runtime physical lane authority.
- Do not encode profile probability as capacity.
- Do not remove the exact slot machinery or replace it with greedy reservation.

## Rollback / kill switch

`UseUnifiedMachineResourceModel=false` removes all decision impact. Shadow telemetry can be independently disabled. Existing legality remains the fallback.

## Acceptance and merge gates

- Exact parity and deterministic diagnostics on the frozen corpus.
- W=8 topology/parity tests pass, including SystemSingleton, lane6 and lane7 aliases.
- No emitted schedule/bundle/image/annotation delta.
- Overhead thresholds and all compiler/runtime authority-negative tests pass.

## Status criteria

- `implemented`: types, extraction, state and shadow adapter exist.
- `verified`: exhaustive/representative parity and budgets pass.
- `default-enabled`: model may become the compiler structural query source only after dual-run parity over two qualification runs; search behavior still unchanged.
- `release-authorized`: named compiler release evidence only; no runtime authority follows.

## Residual work / next gate

Phase 02 may consume the base footprint/state for bounded joint membership search. Phase 03 must add richer topology without changing this hard-vs-heuristic separation.
