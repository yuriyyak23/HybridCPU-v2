# 16 — Modulo expansion and architecture-driven loop transforms

## Goal

Materialize valid modulo schedules and add loop transformations driven by HybridCPU resource economics rather than generic unroll factors.

## Required work

- Generate prolog/kernel/epilog with correct virtual-value versioning, live-in/live-out mapping, side exits and final exit values.
- Treat the Phase 14 kernel witness as an input proof that must be revalidated after expansion; prolog/epilog generation cannot weaken dependencies or invent new slot assignments silently.
- Architecture-driven unrolling using MII, slot mix, PRF/group pressure, bank/channel pressure, lane6/lane7 occupancy, code-size growth and expected trip-count profitability.
- Loop fusion only with proven dependence/memory/control legality and profitable combined resource shape.
- Unroll-and-jam with explicit cross-iteration dependencies and pressure limits.
- Recompute dependence DAG, loop distances, all applicable MII components, liveness/pressure and exact placement after every transform; never reuse stale evidence.
- Re-run Canonical IR capability/side-effect validation after structural transforms that clone/move operations.
- Preserve source/debug origin chains through cloned prolog/kernel/epilog operations.

## Transform contract

Every transform produces:

```text
LoopTransformResult {
  TransformedLoop;
  LegalityProofRefs;
  InvalidatedAnalysisKinds;
  RecomputedMiiSummary;
  PressureDelta;
  CodeSizeDelta;
  ProfitabilityEvidence;
  FallbackLoopIdentity;
}
```

A transformation is not considered complete until its invalidated analyses have been recomputed. “Legality preserved by construction” must be backed by an explicit preservation rule and test suite; it is not a reason to keep stale DAG/MII/liveness objects.

## Prolog/kernel/epilog correctness gates

- Correct iteration mapping for every original operation and dependence distance.
- Correct PHI/value rotation and live-out selection.
- Correct zero-trip, one-trip and trip-count-less-than-pipeline-depth behavior.
- Side exits either have a verified compensation/exit protocol or make the loop ineligible.
- Memory/atomic/volatile/faulting semantics remain ordered exactly as required by Canonical IR/target rules.
- Expanded cycles are validated with the shared exact W=8 placement primitive before final acceptance.

## Profile policy

Profile data may rank legal transforms or estimate trip-count profitability. It cannot remove dependence edges, establish NoAlias or authorize speculation.

Unknown/missing profile data selects a deterministic static profitability policy or leaves the transform disabled; it never changes legality.

## Determinism / fallback

Candidate transforms, factors and retry stages are bounded and deterministically ordered. Failure returns the original canonical loop or a previously verified transform; no wall-clock selection.

The chosen unroll factor is bounded by explicit factors/candidates/code-growth/pressure limits recorded in provenance. A process timeout cannot decide between factors.

## Tests / acceptance

Remainders, zero/small trip counts, multiple exits, carried PHIs, memory alias, lane6/lane7 and high-pressure cases. Compare semantics against non-transformed loops and use Phase 15 oracle on bounded kernels.

Add mutation tests proving that a transform changes the loop analysis version and makes any pre-transform Phase 13/14 proof unusable. Add negative controls for fusion with MayAlias, excessive pressure, unsupported exceptional exits and stale kernel witnesses.

**Acceptance:** prolog/kernel/epilog validation passes independently; every accepted transform has freshly recomputed dependency/MII/liveness/pressure/placement facts; each enabled transform demonstrates KPI benefit with a deterministic kill switch and exact non-transformed fallback.
