# 14 — Bounded constraint modulo scheduling

## Goal and motivation

Generate deterministic modulo schedules for a narrow set of hot, canonical loops using bounded
difference constraints and exact W=8 placement witnesses. Temporal membership and physical
placement form one validated solution even when solved by deterministic constraint/refinement
steps.

**Status:** `Planned`.

## Confirmed starting state

- No modulo scheduler exists; current schedules are per-basic-block.
- Phase 12 is an optional oracle/research service and is not a production dependency.
- Existing exact slot machinery can validate/materialize a fixed cycle membership; it must be reused.
- Phase 13 must provide a finite, witnessed `MaxStageCount` for every attempted II.

## In scope

- Eligible single-latch reducible loops from Phase 13 using Phase 08 memory-distance evidence.
- SDC recurrence constraints, `II` search from `ChosenII`, deterministic resource-conflict cuts and
  bounded backtracking within the Phase 13 stage bound.
- One immutable `IrModuloSchedule` containing operation modulo cycle, stage and exact physical-slot
  witness plus kernel-cycle membership/witnesses.
- A fail-closed modulo value-versioning eligibility check before any CFG transformation.
- A separate prologue/kernel/epilogue CFG expander and deterministic fallback.
- Profile-guided invocation only after correctness-neutral eligibility is proven.

## Explicit non-goals

- No whole-function or unbounded constraint solve.
- No speculative alias removal, hyperblock formation, unrolling/fusion, FSP fast path or runtime authority.
- No mandatory external solver in the default production toolchain.
- No rotating-register assumption, implicit modulo variable expansion or use of runtime rename as a
  compiler correctness proof.

## Canonical types/components

`HybridCpuConstraintModuloScheduler`, `ModuloScheduleRequestV1`, `IrModuloSchedule`
(`InitiationInterval`, `StageCount`, operation `ModuloCycle/Stage/PhysicalSlot`, kernel-cycle
membership and `PlacementWitness`), `ModuloConstraintId`, `ModuloConflictCut`,
`ModuloScheduleFailure`, `ModuloValueVersioningRequirement`
(`NoneRequired`, `StaticallyAllocatable`, `Unsupported`),
`HybridCpuModuloValueVersioningChecker`, `HybridCpuModuloLoopExpander`, Phase 13 graph/MII/stage-
bound reports, Phase 01/03 resource model and existing exact placement APIs.

## Target architecture and data flow

```text
verified hot canonical loop + distance DAG + MII report
 -> for II = ChosenII..maxII
 -> verified MaxStageCount(II)
 -> deterministic temporal candidate within finite horizon
 -> exact W=8 validation for every kernel cycle
      failure -> stable constraint-ID nogood/cut -> temporal retry
      success -> membership + exact placement witness in IrModuloSchedule
 -> ModuloValueVersioningRequirement
      Unsupported -> unchanged block schedule
      NoneRequired / separately proven StaticallyAllocatable -> continue
 -> HybridCpuModuloLoopExpander
      prologue + kernel + epilogue CFG
 -> rebuild CFG/dependencies/resources and validate
 -> compare cost with block schedule
 -> adopt only if lexicographically better and all gates pass
```

Cost is lexicographic: legality/completeness, runtime-authority compatibility, estimated cycles, code growth/pressure/speculation, bundles, stable tie-break. Width is diagnostic, not primary.

## Invariants and authority boundaries

- Every recurrence satisfies `start(v)-start(u) >= latency-II*distance`.
- Temporal feasibility is insufficient by itself: every accepted kernel cycle has an exact W=8
  placement witness in the same final `IrModuloSchedule`.
- Stage A class admission and Stage B materialization remain separate; compiler placement does not select runtime authority outcomes.
- `BundleFormer` verifies/materializes the prescribed membership and placement witness. It must not
  search for a semantically different placement, move an operation to another cycle or repair an
  infeasible modulo membership.
- `HybridCpuConstraintModuloScheduler` owns scheduling mathematics only; it does not mutate CFG,
  labels, branches, relocation or lowering state.
- `HybridCpuModuloLoopExpander` alone owns prologue/kernel/epilogue CFG construction and must rebuild
  dependence/resource analyses before normal lowering.
- The first production eligibility admits `NoneRequired` only. `StaticallyAllocatable` remains
  unsupported until a separate Phase 05/06 allocation proof is versioned and qualified.
- Profile selects candidates/profitability only; it cannot remove edges or constraints.

## Dependencies

Phases 03, 08 and 13 verified, including the selected-II finite stage bound. Phase 05/06 evidence is
required before enabling `StaticallyAllocatable`; it is not needed for the initial
`NoneRequired`-only subset. Phase 12 may supply qualification results but no production assembly or
invocation depends on it.

## Implementation backlog

1. Define immutable request/result, stable `ModuloConstraintId`, eligibility and failure taxonomy.
2. Implement checked-integer SDC feasibility with stable node/constraint order and the exact Phase 13
   recurrence convention.
3. Start at `ChosenII`; increment deterministically to `min(ChosenII+8, 32)` and require the pinned
   `MaxStageCount(II)` before solving.
4. Validate modulo resource state by slot/class, structural unit, PRF ports, register groups,
   bank/channel and hard pin.
5. Convert the first deterministic conflict or exact-placement failure into a versioned constraint-
   ID nogood/cut; never learn profile-derived legality cuts.
6. Return each successful exact slot assignment as part of the final kernel-cycle witness, not as a
   later packer suggestion.
7. Compute `ModuloValueVersioningRequirement` from overlapping iteration lifetimes and proven
   architectural versions. Reject `Unsupported`; initially reject `StaticallyAllocatable` pending a
   separate allocation proof.
8. Implement `HybridCpuModuloLoopExpander` separately: consume only a validated schedule, construct
   prologue/kernel/epilogue, rebuild CFG/dependences/resources, then invoke normal validation,
   bundling, lowering and relocation.
9. Compare with the unchanged block schedule; reject on pressure/code-growth budget.
10. Add shadow mode and consume exported Phase 12 oracle results in tests when available; missing
    oracle is `Unavailable`, never a production fallback trigger.
11. Add per-loop kill switch and global feature flag.

## Migration and compatibility strategy

Ship shadow-only, then default-off codegen for an allowlisted corpus. Unsupported, deterministic work-budget exhaustion or validation failure returns the original block schedule byte-for-byte. No serialized ABI change is required until separately versioned metadata is needed.

## Tests

### Positive/property/determinism

- Recurrences, resource collisions, successful deterministic cuts, prescribed placement witnesses,
  prologue/kernel/epilogue value flow.
- Every emitted kernel satisfies Phase 13 edges and shared resource validation.
- Every materialized slot equals the `IrModuloSchedule` witness; BundleFormer performs no alternate
  search or repair.
- `NoneRequired` loops expand correctly; later `StaticallyAllocatable` cases require an explicit
  allocation proof and versioned carrier map.
- Same input/profile/model/options gives identical schedule and diagnostics.

### Negative

- Unsatisfied recurrence, missing/unbounded stage bound, `MayAlias` eligibility rejection,
  SystemSingleton collision, hard-pin conflict, exact-placement failure, register/port/bank
  overflow, value-versioning `Unsupported`/unproved `StaticallyAllocatable`, pressure/code-growth
  limit, state/cut/iteration exhaustion and invalid solver result.
- Reject stale graph/model/profile digests fail closed.

### Static

- No compiler dependency on runtime commit/retire/backend owners.
- No required production dependency on the Phase 12 oracle/solver.
- Constraint scheduler has no CFG mutation/relocation APIs; loop expander has no constraint-backend
  ownership.
- BundleFormer has no modulo repair path.

## Benchmarks and KPI

- Report `ChosenII`, achieved II, `MaxStageCount`, actual stage count, kernel `schedule_cycles`,
  bundle count, prologue/epilogue cycles, value-versioning disposition, code growth, pressure,
  states/cuts and fallback reason.
- Tiny accepted loops: achieved II equals oracle optimum for >=90%; all remaining gaps explained.
- Qualified hot-loop subset: geomean runtime cycles <=0.97 and no representative workload >1.01; correctness output identical.
- Compile-time p95 <=1.15 and peak memory <=1.15 with feature enabled; cap-hit <=2% of eligible loops.

## Diagnostics and telemetry

All MIIs, tried IIs, stage bounds/witnesses, canonical constraint IDs, deterministic cuts/nogoods by
resource, placement-witness digests, value-versioning requirement/proof identity, states/iterations
and observed diagnostic time/memory, achieved II, first divergence from oracle, CFG-expansion
validation, pressure/code-growth rejection and fallback. Time is telemetry only.

## Bounded-search, timeout and fallback policy

Default eligibility <=128 ops and <=512 dependence edges; max 9 II attempts, 4096 constraint states,
256 cuts, 65536 SDC relaxations and 32 conflict rounds per loop, all inside the proved stage bound.
The first deterministic counter limit uses the original block schedule. Production code contains no
elapsed-time branch. A CI/watchdog wall-clock limit may abort and fail/mark the test, but cannot
select emitted code.

## Risks and forbidden shortcuts

- No monolithic whole-function constraint solve or unbounded cut loop.
- No width-first adoption and no profile legality.
- No silently weakened recurrence/resource model to reach a lower II.
- No temporal-only acceptance, post-hoc packer repair, implicit register rotation, runtime-rename
  proof or solver-owned CFG transformation.

## Rollback / kill switch

Global/per-loop disable restores the captured block schedule; metadata marks which path produced each result.

## Acceptance and merge gates

- Reference interpreter/output equivalence and normal legality/materialization pass.
- Joint membership/exact-placement witness validation, stage-bound cross-check, value-versioning
  fail-closed and solver/expander ownership tests pass.
- Optional oracle comparison plus mandatory determinism, counter-budget/fallback and adversarial pressure tests pass.
- Hot-loop performance, compile-resource and no-regression gates pass before default enable.
- Authority review confirms unchanged runtime ingress/validation.

## Status criteria

- `implemented`: bounded constraint scheduler, exact joint witnesses, value-versioning checker,
  separate loop expander and shadow comparison exist.
- `verified`: correctness, oracle-gap and budget gates pass on the frozen corpus.
- `default-enabled`: only for qualified eligible loops after Phase 24 review.
- `release-authorized`: requires release gate; never authorizes runtime FSP/retire behavior.

## Residual work / next gate

Phases 15–16 evaluate architecture transformations. Phase 15 can also schedule unrolled candidates through the non-modulo path and does not require this feature to be enabled.
