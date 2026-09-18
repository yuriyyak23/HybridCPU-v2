# 14 — Deterministic production modulo/software-pipeline scheduler

## Goal

Implement a production-capable software-pipelining scheduler that does not require an exact solver.

## Core formulation

For every operation define explicit `Cycle(op)` and a modulo issue-slot/placement witness. For dependence `u -> v` with latency `L` and iteration distance `d` enforce:

```text
Cycle(v) - Cycle(u) >= L - d * II
```

Resource reservations are modulo `II` and include W=8 heterogeneous slots, Phase 03 PRF/group/bank/channel resources, lane6/lane7 and certificate classes.

The production result is not merely a cycle number per op. It contains an independently checkable witness:

```text
ModuloScheduleWitness {
  II;
  Cycle(op);
  ModuloCycle(op) = Cycle(op) mod II;
  IssueSlot(op);          // or exact W=8 structural assignment identity
  ResourceReservations;
  DependenceProofRefs;
  MiiProofRefs;
  LifetimePressureSummary;
  TargetAndModelDigests;
}
```

## Algorithm

1. Start from Phase 13 proven lower bound.
2. Solve deterministic SDC/difference temporal constraints.
3. Lazily refine discrete resource conflicts and exact W=8 placement using the existing slot-search primitive.
4. Search II upward with explicit deterministic limits on II attempts, states, cuts, candidates and refinement iterations.
5. Integrate lifetime/register-pressure constraints; infeasible pressure may raise II or reject pipelining before Phase 20 allocation.
6. Emit a verified kernel schedule witness for Phase 16 expansion.
7. Re-run the shared exact `SearchStructuralAssignments`-based W=8 validation on every modulo cycle before the witness is accepted; a heuristic resource summary cannot substitute for exact placement.

No wall-clock timeout may select production output. Exhaustion returns deterministic `BudgetExhausted` and falls back to non-modulo scheduling.

## Deterministic failure taxonomy

Each II attempt returns one of:

- `Feasible`;
- `TemporalInfeasible`;
- `DiscreteResourceInfeasible`;
- `ExactPlacementInfeasible`;
- `LifetimePressureInfeasible`;
- `UnsupportedSemanticOrResourceFact`;
- `BudgetExhausted`.

`InfeasibleIIReason` must carry the binding edges/resources/slots/cuts where known. `BudgetExhausted` is not proof of infeasibility and must never be reported as UNSAT.

Production bounds are configuration/provenance values measured only in deterministic work units: II attempts, SDC relaxations, candidates, cuts, nodes, refinement stages and exact-placement states. A process watchdog may abort the compilation as an operational error but cannot choose a different emitted schedule.

## Pressure/lifetime rule

Pre-allocation pressure constraints are conservative feasibility/profitability guards, not physical-register authority. If Phase 20 later introduces spills or allocation constraints that mutate the loop, it must invalidate the kernel witness and request bounded repair/re-scheduling; it cannot silently keep a stale modulo proof.

## Roorda alignment

The phase incorporates explicit cycle/slot/resource variables, loop-carried latency/distance constraints, minimum-II search, register lifetime/pressure considerations, prolog/kernel/epilog handoff requirements and infeasible-II diagnostics as motivated by J.-W. Roorda, “Optimal Software Pipelining using an SMT-Solver”, while keeping the production algorithm solver-neutral.

The intended architecture remains:

```text
deterministic SDC/difference constraints
 -> lazy discrete resource/placement refinement
 -> optional SMT backend behind the same abstract problem
 -> exact solver oracle in Phase 15 for CI/research
```

Production codegen must not acquire an SMT runtime dependency.

## Tests / acceptance

Known feasible/infeasible loops, UNSAT-like reason categories, repeated determinism, resource saturation, pressure and placement counterexamples. Kernel witness is independently revalidated before emission.

Add counterexamples where temporal constraints are satisfiable but exact W=8 placement is impossible, and where a schedule is resource-feasible at one II but impossible because of lane6/lane7/certificate constraints. Repeated builds with different hash/container enumeration order must produce identical chosen II, witness and fallback reason.

**Acceptance:** production scheduler finds valid schedules within declared deterministic bounds; every accepted kernel has an independently validated exact W=8 witness; failure reasons distinguish proof from budget exhaustion; no SMT solver installation or wall-clock decision is required for production.
