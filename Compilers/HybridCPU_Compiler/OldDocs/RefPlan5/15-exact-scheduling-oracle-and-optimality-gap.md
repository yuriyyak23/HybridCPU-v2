# 15 — Exact scheduling oracle and optimality-gap framework

## Goal

Provide an independent exact reference for CI/research, including minimum-II proofs and measured production-scheduler optimality gaps.

## Design

Define a solver-neutral oracle model using the same dependency/resource/placement contract as production. A concrete SMT-solver or other exact backend is optional infrastructure. Model explicit cycles, modulo slots, loop-carried constraints, latencies/distances, W=8 placement, PRF/group/bank/channel, lane6/lane7, certificate and bounded lifetime/pressure constraints.

The oracle must be independent in **search algorithm** but not divergent in target semantics: it consumes the same versioned Canonical IR, target/machine model and resource definitions, then produces a witness that is revalidated by Core. Shared validators are allowed; shared heuristic decision code is not, otherwise the oracle cannot expose production-search mistakes.

## Solver status contract

Every query returns one of:

```text
SAT(witness)
UNSAT(proof-or-core-when-supported)
UNKNOWN(reason)
RESOURCE_LIMIT(limitKind, deterministicOrOperational)
INVALID_MODEL(reason)
```

A wall-clock watchdog, solver crash or unsupported solver feature is `UNKNOWN/RESOURCE_LIMIT`, never `UNSAT`. If deterministic solver limits are configured for reproducible CI comparisons, those limits and solver options are part of provenance.

## Minimum-II search

- Begin at the Phase 13 proven lower bound.
- Prove each lower II infeasible or record `Unknown`; do not claim a minimum II across an unresolved lower candidate.
- A reported `MinimumFeasibleII=N` requires SAT at N and proven UNSAT for every searched candidate below N down to the proven lower bound.
- Exact issue-slot assignment is part of the SAT witness, not a post-hoc heuristic pack.
- Register lifetime/pressure constraints used by the oracle must state whether they are exact physical-allocation constraints or conservative pre-allocation bounds; these categories cannot be mixed in an “optimal” claim.

## Separation from production

No production assembly or binary may depend on oracle availability, solver version, wall-clock timeout or solver result. CI/research may use wall-clock watchdogs but must label timeout as `Unknown`, never `Infeasible`.

Production must have a build/test configuration where all solver packages/native binaries are absent. Oracle results cannot be cached as production legality certificates.

## Outputs

- minimum feasible II, feasible-at-II witness, proven infeasible range, or bounded `Unknown`;
- infeasible-II/UNSAT diagnostics/core where supported;
- exact cycle + issue-slot/resource schedule witness;
- production-vs-oracle II gap, objective gap and reason taxonomy;
- production search work units versus oracle result;
- frozen adversarial corpus for regressions;
- full solver/model/target/provenance fingerprint.

## Optimality-gap KPI

Report at least:

```text
II_gap = ProductionII - OracleMinimumII
relative_II_gap = ProductionII / OracleMinimumII
objective_gap = ProductionObjective - OracleObjectiveAtComparableContract
```

Do not compare objectives with different legality/resource/lifetime contracts. Cases with oracle `Unknown` remain unscored rather than being counted as production-optimal.

## Tests / acceptance

Cross-check oracle witnesses with Core validators and exact W=8 placement. Inject intentionally relaxed constraints to ensure validator rejection. Track oracle-version/provenance.

Add tests where one constraint family is omitted in a deliberately broken oracle model and require the Core validator to reject its SAT witness. Add known UNSAT kernels for recurrence, slot, PRF/group, bank/channel, lane6/lane7 and certificate causes. Verify timeout/crash never becomes UNSAT.

**Acceptance:** CI can quantify production optimality gap on bounded loops/regions with trustworthy SAT/UNSAT/UNKNOWN semantics, while production builds and codegen remain completely solver-independent.
