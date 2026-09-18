# 17 — VT-aware and cross-VT scheduling

## Goal

Exploit the four-way virtual SMT architecture in compiler profitability/layout decisions without claiming dynamic lane or execution ownership.

## Required model

Represent VT identity/locality, shared vs per-VT register groups/resources, cross-VT contention and static opportunities to improve Stage A class admission / Stage B materialization. Scheduling may coordinate independent VT streams only where program/runtime contracts define their relationship.

The compiler model must explicitly distinguish:

```text
StaticVtAssignment / VtAffinity
StaticClassCompatibilityEvidence
StaticSlot/LaneCompatibilityEvidence
RuntimeStageAAdmission
RuntimeStageBLaneMaterialization
RuntimeExecution/Replay/Publication/Commit/Retire
```

Only the first three are compiler-plannable/evidence-bearing. The last three categories are runtime/backend-owned.

## Stage A / Stage B contract

- **Stage A class admission** is dynamic runtime/backend arbitration. Compiler class/resource predictions may reduce expected conflicts but cannot assert that a carrier/operation will be admitted.
- **Stage B lane materialization** selects/realizes execution lanes from an admitted class under live state. A compiler slot/contour witness proves static structural compatibility only; it is not a lane lease or execution token.
- The 8-slot VLIW carrier is a compiler-visible structural placement domain; 4-way virtual SMT is a runtime/backend execution topology. Do not collapse “slot number”, “lane”, “VT” and “runtime materialized execution contour” into one identity.
- Hard ISA pins remain hard constraints. Dynamic donor/reclamation mechanisms remain outside this phase.

## Cross-VT eligibility

Cross-VT scheduling is allowed only when the program/runtime contract explicitly establishes that the compared streams may be co-planned without changing language-visible ordering, synchronization, exception or memory-model semantics. Independent VTs must not be silently fused into one compiler scheduling region merely because hardware can execute four virtual contexts.

Shared resources use Phase 03/08A stable topology. Unknown dynamic capacities remain profitability/telemetry only. Atomics, fences, synchronization, TLS/managed-thread state and special lane6/lane7 contours require explicit cross-VT rules or block motion/coordination.

## Authority boundary

Compiler VT plans are static evidence. Runtime Stage A admission, Stage B lane materialization, scoreboard/rename/PRF state, replay/freshness, dynamic FSP, execution, publication, commit and retire remain authoritative. No compiler schedule may pin a dynamically owned lane unless the ISA contract already defines a hard pin.

Compiler metadata must never contain a runtime epoch/generation/owner token or imply that a previous Stage A/B observation remains fresh at execution time.

## Reuse / fallback

Reuse bounded joint composition, exact W=8 placement, topology model and region/loop schedulers. VT-local scheduling is the deterministic fallback and remains independently valid.

Cross-VT search uses deterministic bounded VT combinations/candidates/stages. Runtime telemetry may tune profitability policy offline through an explicitly versioned profile path, but cannot alter static legality or candidate membership.

## Tests / acceptance

Shared-resource collision, per-VT group separation, lane6/lane7, replay-sensitive and one-to-four active VT cases. Compare against VT-local baseline; width realization and runtime rejection categories are monitored separately from raw compiler bundle width.

Add negative tests proving: compiler Stage A prediction cannot create `LegalityDecision`; a static lane-compatible witness cannot bypass Stage B materialization; stale replay/freshness observations are never serialized into compiler output; unsupported cross-VT memory synchronization falls back to VT-local scheduling.

**Acceptance:** qualified cross-VT policies improve runtime KPIs without increasing legality/replay failures; all Stage A/B/runtime authority remains explicit and revalidated; the feature can be disabled with byte-stable VT-local fallback and no change to correctness.
