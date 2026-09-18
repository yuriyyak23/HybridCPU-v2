# 23 — VDSA and donor-prefetch advisory planning

## Goal and motivation

Add a separate memory-latency-hiding optimisation family that identifies useful donor-prefetch/VDSA opportunities and can emit advisory intent without constructing or authorizing runtime assists.

**Status:** `Planned`.

## Confirmed starting state

- Runtime already defines `AssistKind.DonorPrefetch`, `Ldsa`, `Vdsa`, carrier tuples, donor-source identity and assist admission/execution paths.
- Runtime VDSA maps to stream-register prefetch on lane 6; runtime owns assist tuple validation, lane placement, replay/epoch/domain checks and non-retiring execution.
- No compiler source references `AssistKind`, `DonorPrefetch` or `Vdsa`; RefPlan3 previously omitted a compiler planner.
- Historical representative workloads are memory-stall dominated, but must be reproduced in Phase 00.

## In scope

- Load criticality/latency exposure, reusable value/access seed and donor-prefetch/VDSA eligibility.
- Bandwidth, bank/channel, lane6, VT/domain/owner, replay-risk and code/compile budgets.
- `CompilerAssistIntentV1` static advisory envelope and emit-only/offline evaluation.
- Mapping to existing runtime assist taxonomy as a compatibility description, never direct micro-op construction.

## Explicit non-goals

- No runtime assist injection, queueing, lane choice, execution, replay or retire authority.
- No FSP certificate reuse as an assist authorization and no compiler call to `AssistRuntime`/`AssistMicroOp`.
- No profile promotion of `MayAlias`, no cross-domain/contour donor and no broad runtime activation.

## Canonical types/components

Proposed `HybridCpuAssistIntentPlanner`, `CompilerAssistIntentV1`, `AssistProfitabilityReportV1`; Phase 08 memory relations, Phase 13 loop/criticality facts, Phase 17 VT context and Phase 03 lane6/bank/channel resources. Runtime assist taxonomy is read-only compatibility evidence.

## Target architecture and data flow

```text
memory dependence/bank model + loop/profile profitability + VT context
 -> load criticality and latency exposure
 -> donor-prefetch/VDSA eligibility
 -> bandwidth/lane6/replay/owner/domain budgets
 -> bounded candidate ranking
 -> static CompilerAssistIntentV1 (emit-only; runtime ignores)
 -> offline join with runtime assist/memory telemetry
```

## Invariants and authority boundaries

- Static memory/owner/domain/contour facts determine eligibility; profile only estimates benefit.
- Compiler intent contains no runtime epoch/generation/replay freshness and no chosen physical lane.
- Runtime assist owners must revalidate every field before any future use.
- Existing foreground scheduling and FSP evidence are unaffected in the emit-only phase.

## Dependencies

Phases 03, 08, 13 and 17 verified.

## Implementation backlog

1. Inventory existing runtime assist tuple/source contracts without importing runtime authority types into compiler.
2. Define static candidate/intent schema and allowed/forbidden claims.
3. Compute load criticality and exposed latency from dependence/schedule evidence.
4. Prove seed/access reuse and `NoAlias`/distance requirements through Phase 08.
5. Enforce bandwidth, bank/channel, lane6, owner/domain/contour and replay-risk budgets.
6. Rank a fixed candidate set by expected cycles saved; stable tie-break.
7. Emit intent to a versioned sidecar only; runtime ignores it.
8. Join offline with assist eligibility/injection, memory stalls and cycle telemetry.
9. Require a separate runtime-owner plan before shadow consumption or assist injection.

## Migration and compatibility strategy

Planner starts analysis-only; static intent emission is a second default-off slice. Existing artifacts/runtime ignore the sidecar. Runtime consumption is not part of this phase and requires a separate authorization/ABI review.

## Tests

### Positive/property/determinism

- Critical-load chains, affine loop accesses, legal same/cross-VT donor candidates and stable ranking/bytes.
- Emission on/off leaves code/schedules/runtime traces identical.

### Negative

- `MayAlias`, volatile/atomic/system/trapping access, insufficient distance, cross-domain/contour, lane6/bandwidth saturation, stale profile/model and unsupported runtime tuple.

### Static

- Compiler has no dependency on `AssistRuntime`, `AssistMicroOp` or live runtime freshness.
- No FSP certificate field authorizes an assist.

## Benchmarks and KPI

- Report candidates/eligible/emitted, donor source class, criticality, expected cycles saved, bandwidth/bank/channel/lane6 pressure and reject reason.
- Emit-only: zero code/schedule/runtime cycles/IPC/memory-stall delta; compile time/memory p95 <=1.05x; sidecar growth <=1%.
- Future runtime shadow proposal must target memory stalls and total cycles, not candidate count; physical lane realization `1.0` and width drops `0` remain protected.

## Diagnostics and telemetry

Access/load/loop/VT identity, alias/distance witness, criticality/latency exposure, donor class, budgets, expected value, static intent digest and offline actual outcome.

## Bounded-search, timeout and fallback policy

No production wall-clock cutoff. Max 128 memory ops/region, 256 donor candidates, 4096 candidate pairs and 64 emitted intents/function. Counter limit emits no intent for remaining candidates in stable order; code output is unchanged.

## Risks and forbidden shortcuts

- No memory-stall claim from historical/unreproduced logs.
- No runtime assist construction or lane6 authority in compiler.
- No MayAlias-to-NoAlias via profile and no cross-contour fallback.

## Rollback / kill switch

Disable analysis/emission independently; runtime and emitted code remain unchanged.

## Acceptance and merge gates

- Static memory/owner/tuple properties and deterministic budgets pass.
- Code/schedule/runtime trace equivalence for emission on/off.
- Compiler/runtime authority review approves only static emit-only claims.

## Status criteria

- `implemented`: bounded planner and optional static sidecar emitter exist.
- `verified`: eligibility, determinism, no-effect and overhead gates pass.
- `default-enabled`: analysis/emit-only only after Phase 24 compiler+evidence profile.
- `release-authorized`: static advisory intent only; no runtime assist consumption/injection.

## Residual work / next gate

Runtime shadow validation and any VDSA/donor-prefetch activation require a separate authorized plan; they are not implied by FSP Phases 21–22.
