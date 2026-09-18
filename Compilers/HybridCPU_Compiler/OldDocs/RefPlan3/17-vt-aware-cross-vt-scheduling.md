# 17 — VT-aware and bounded cross-VT compiler scheduling

## Goal and motivation

Make virtual-SMT ownership, fairness and complementary resource shaping first-class compiler policy while preserving runtime FSP/replay/admission authority.

**Status:** `Planned`.

## Confirmed starting state

- `IrInstruction` carries `VirtualThreadId`; dependency keys are VT-qualified with a conservative cross-VT memory/register guard.
- `HybridCpuMultithreadedCompiler` creates four per-VT contexts and compiles canonical thread programs separately; its compatibility output round-robins encoded instructions.
- Scheduler has an optional profile-backed `UseVtAwareBackendPressureTieBreaks`, but no joint `VtSchedulingContext`, fairness/starvation or complementarity search.
- The untracked packed benchmark snapshot reports four IR VTs and 32 cross-VT cycle groups/bundles, demonstrating that mixed-VT program inputs also reach the scheduler. This is historical evidence to reproduce, not release proof.

## In scope

- `VtSchedulingContext`: owner VT, domain/contour compatibility, ready debt/fairness, resource complementarity, register-group overlap and donor suitability inputs.
- Shadow comparison for existing mixed-VT programs and a separately gated joint coordinator for canonical per-VT schedules.
- Deterministic fairness/starvation bounds and unchanged runtime owner checks.

## Explicit non-goals

- No runtime VT selection, slot stealing, replay, PRF allocation or issue authority.
- No cross-domain/contour fallback, barrier weakening or profile legality.
- No removal of VT-local canonical API/compatibility interleaver.

## Canonical types/components

Proposed `VtSchedulingContext`, `VtReadyDebt`, `CrossVtComplementarityScore`, `HybridCpuCrossVtCycleCoordinator`; current per-VT contexts, joint cycle search, resource/memory models and barriers are inputs.

## Target architecture and data flow

```text
ready candidates partitioned by owner VT
 + domain/contour/barrier/dependence facts
 + per-VT resource/pressure/donor summaries
 -> deterministic VT admission/fairness window
 -> bounded cross-VT complementary CycleGroup alternatives
 -> existing exact placement/materialization
 -> runtime performs unchanged Stage A/B/FSP/replay
```

## Invariants and authority boundaries

- Owner VT remains attached to every instruction/evidence record.
- Fairness is compile-policy ordering; it cannot override dependence, barrier, domain, contour or resource legality.
- Runtime virtual SMT, FSP/replay, lane materialization and retirement remain authoritative.
- Same input/profile/options yields identical VT admission sequence; profile affects cost only.

## Dependencies

Phases 02, 03, 04, 05 and 08 verified.

## Implementation backlog

1. Reproduce and classify current mixed-VT inputs and four canonical per-VT compile path.
2. Define context/owner/domain/contour/barrier and stable per-VT ready queues.
3. Add fairness debt with a finite starvation bound that never overrides hard constraints.
4. Score cross-VT complementarity by slot/resource/PRF/group/bank/channel overlap.
5. Feed donor suitability as a labelled profitability field for Phase 18, not legality.
6. Shadow current mixed-VT scheduler before coordinating separately compiled programs.
7. Add bounded multi-VT alternatives to Phase 02 search using existing placement.
8. Preserve original per-VT programs and deterministic VT-local fallback.
9. Compare runtime FSP/certificate outcomes with denominator-correct metrics.

## Migration and compatibility strategy

Existing VT-local compilation and compatibility interleaving remain default. Mixed-VT shaping begins shadow-only; joint coordination is a separate default-off feature and never changes external API without a compatibility decision.

## Tests

### Positive/property/determinism

- Four-VT ready sets, complementary classes, fairness debt, barriers and same-domain mixed bundles.
- Identical admission sequence/fingerprints over repeated runs and permuted container order.
- Runtime/full reference execution retains owner VT and outputs.

### Negative

- Cross-domain/contour, barrier/dependence, incompatible register group, starvation-cap blocked by hard legality, stale profile and missing owner.

### Static

- No runtime scheduler/PRF/replay/retire owner mutation.
- Canonical VT-local API and compatibility path remain.

## Benchmarks and KPI

- Report per-VT schedule cycles, issued share, max ready debt/starvation distance, cross-VT groups, complementarity and group overlap.
- Packed-VT representative runtime cycles <=0.98x VT-local baseline, IPC non-regressing, physical lane realization `1.0`, width drops `0`.
- No VT violates declared fairness bound when a legal candidate exists; compile time/memory p95 <=1.10x.
- FSP metrics use attempts/eligibility denominators defined in Phase 18.

## Diagnostics and telemetry

Owner VT, ready counts/debt, chosen VT set, hard blockers, complementarity components, group/bank/channel overlap, donor class, cycles and fallback.

## Bounded-search, timeout and fallback policy

No wall-clock selection. Four VTs maximum, top 4 candidates/VT, 64 VT-set candidates/cycle and inherited Phase 02 4096-state cap. Counter limit falls back to stable VT-local order/interleaver.

## Risks and forbidden shortcuts

- No fairness rule may create illegal issue or cross-contour fallback.
- No treating runtime donor acceptance probability as legality.
- No replacing virtual SMT runtime ownership with compiler grouping.

## Rollback / kill switch

Disable cross-VT coordinator and retain current mixed-program scheduler or canonical VT-local programs/interleaver.

## Acceptance and merge gates

- Owner/domain/barrier/fairness properties and deterministic fallback pass.
- Reproduced packed corpus and denominator-correct runtime comparison archived.
- Cycles/IPC/lane-realization/width-drop/compile-resource gates pass.

## Status criteria

- `implemented`: context, shadow metrics and bounded coordinator exist.
- `verified`: ownership/fairness/correctness/performance gates pass.
- `default-enabled`: per input contract after Phase 24; VT-local API remains supported.
- `release-authorized`: compiler shaping only, no runtime VT/FSP authority.

## Residual work / next gate

Phase 18 unifies donor-quality profitability; Phase 23 may use VT context for advisory VDSA/donor-prefetch intent.
