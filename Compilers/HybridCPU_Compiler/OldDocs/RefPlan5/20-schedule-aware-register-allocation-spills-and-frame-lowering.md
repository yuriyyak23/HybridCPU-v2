# 20 — Schedule-aware register allocation, spills and frame lowering

## Goal

Perform final physical/group allocation only after schedule-changing region/loop/VT/FSP/VDSA planning, avoiding the RefPlan4 inversion where carrier allocation occurred too early.

## Dependencies

Phase 07 virtual values/pressure, Phase 08B full calling-convention/stack contract, Phase 03 topology/resources and the selected verified schedule/loop/VT/FSP/VDSA plans from Phases 12–19. Allocation cannot invent missing architectural register classes or stack rules.

## Required work

- Allocate Phase 07 virtual values only to architecture-defined allocatable registers/classes/groups from Phase 08A/08B while respecting fixed/precolored operands and special-state exclusions.
- Make allocation aware of scheduled lifetimes, PRF read/write ports and group conflicts from Phase 03.
- Deterministic coalescing, spill-cost model, spill/reload insertion and revalidation.
- Implement calling-convention preservation, frame objects, stack alignment, stack probing where required, prolog/epilog and frame-index lowering from Phase 08B.
- Recompute dependencies/resources after spill insertion.
- Recompute **all affected loop/region facts** after any spill/reload/rematerialization/frame mutation: memory dependencies, distance DAG, applicable MII components, liveness/pressure, resource reservations and exact W=8 placement. A spill is a real memory operation, not an allocator-private annotation.
- Treat rematerialization as code generation subject to the same side-effect/fault/resource legality as ordinary instructions.
- Produce a final allocation/frame witness tied to target/ABI/model digests and selected schedule identity.

## Avoiding a scheduler↔RA cycle

Scheduling uses conservative pressure bounds, not final assignments. RA consumes a selected schedule. If allocation fails or spill insertion invalidates resource/placement/modulo feasibility, allow only a bounded deterministic repair protocol:

```text
schedule candidate
 -> allocation attempt
 -> if needed insert verified spill/remat plan
 -> rebuild affected DAG/MII/liveness/resources/placement
 -> validate
 -> bounded reschedule stage
 -> retry allocation
 -> success OR deterministic fallback/failure
```

The maximum repair stages, allocation candidates and spill alternatives are fixed work-count bounds recorded in provenance. No wall-clock cutoff participates in emitted-code choice. No unbounded scheduler↔allocator fixed-point iteration is allowed.

For modulo kernels, a spill that changes loop memory/resource/dependence facts invalidates the Phase 14 kernel witness and may raise `ChosenII`; Phase 20 cannot force the spill into a stale kernel schedule merely to complete allocation.

## Physical-register ownership boundary

Compiler physical register assignment is **architectural encoding/ABI state** only. It does not allocate runtime PRF entries, rename tags, free-list entries, scoreboard state or commit resources. The allocator consumes only stable architectural register/group contracts, never live backend state or runtime telemetry as legality.

Runtime still owns rename/physical backing, hazards, replay/freshness, execution, publication, commit and retire.

## Frame/lowering contract

- Every frame object has deterministic size/alignment/lifetime/offset identity.
- Spill slots obey target stack alignment/addressing limits and participate in alias/dependence modeling with other stack objects.
- Callee-save/caller-save rules are validated against the versioned calling convention.
- Unsupported dynamic stack allocation, probing, unwind interaction or special-contour call behavior fails closed until its Phase 08/25 contract exists.
- Debug/source mapping for inserted spills/prolog/epilog uses synthetic origin records linked to the responsible source value/instruction without pretending those instructions originated in user code.

## Tests / acceptance

High-pressure loops, calls, fixed/special registers, VT groups, spills near branches, lane6/lane7 and modulo lifetimes. Validate frame ABI and schedule after spills.

Add adversarial tests where allocation forces a spill into a bank/channel bottleneck, breaks a previously feasible modulo II, collides with a register group/PRF port budget, or requires unsupported stack addressing. Verify the bounded repair either re-schedules/re-raises II with fresh proofs or selects the conservative fallback; it may not let BundleFormer/encoder repair the inconsistency.

Static tests forbid allocator references to runtime PRF/rename/free-list/commit state. Determinism tests perturb candidate/container order and require identical assignment/spill/frame results.

**Acceptance:** allocation is deterministic, ABI-correct and resource-aware; every code-mutating allocation step rebuilds affected proofs; bounded repair terminates with explicit reason and safe fallback; final schedule + exact W=8 placement + allocation/frame witness agree before lowering.
