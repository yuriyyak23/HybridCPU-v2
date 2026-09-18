# 13 — Loop canonicalization, distance DAG and HybridCPU MII family

## Goal

Create an architecture-correct loop representation and proven initiation-interval lower bounds before implementing modulo scheduling.

## Required model

- Canonical preheader/header/latch/exits and induction/PHI representation.
- Loop-carried dependencies with explicit iteration distance `d >= 0`, latency and dependence kind.
- Conservative memory-carried distances unless statically proven otherwise.
- Loop value lifetimes and pressure estimates from Phase 07.
- Stable loop/edge IDs and a version stamp tying every distance/MII proof to the exact Canonical IR, target contract and machine-resource model used to derive it.
- Explicit unsupported/unknown disposition for irreducible control, unmodelled calls, atomics/fences, exceptional exits and any side effect whose inter-iteration ordering cannot be proven.

## MII components

Compute and report `RecMII`, `SlotMII`, `PRFReadPortMII`, `PRFWritePortMII`, `RegisterGroupMII`, `MemoryBankMII`, `MemoryChannelMII`, `Lane6MII`, `Lane7MII` and `CertificateMII`.

Each component is a typed result:

```text
MiiComponentResult =
    Proven(value, proofInputs, targetDigest)
  | NotApplicable(reason)
  | Unknown(reason)
  | Unsupported(reason)
```

`Unknown`/`Unsupported` are never encoded as zero and cannot be omitted from diagnostics. If a required component cannot be proven conservatively, the modulo path is ineligible or must use a separately proven conservative bound; it cannot proceed with an optimistic lower bound.

```text
ProvenLowerBoundII = max(all Proven MII components required by the loop)
ChosenII >= ProvenLowerBoundII
```

This inequality is necessary, not sufficient: exact modulo placement/resource feasibility remains required.

## Component-proof requirements

- `RecMII`: derived from loop-carried dependence cycles using latency and iteration distance; distance-zero intra-iteration edges remain ordinary precedence constraints.
- `SlotMII`: counts only issue classes/slot domains that share a proven capacity under the W=8 carrier model.
- PRF/group MII: derived only from stable Phase 03/08A architectural capacities, never from live PRF/rename occupancy.
- Bank/channel MII: exact only for statically proven mapping sets; possible/unknown mappings cannot be treated as guaranteed parallel capacity.
- `Lane6MII`/`Lane7MII`: use their special execution contours/serialization rules explicitly rather than treating every lane as symmetric.
- `CertificateMII`: represents only a stable compiler-visible structural certificate capacity; it must not serialize or predict runtime `LegalityDecision`/admission tokens.

## Mutation rule

Any loop transform, spill insertion, address rewrite, VT reassignment or target/model-version change invalidates affected distance and MII proofs. Phase 14/16/20 must request recomputation rather than patching a stale `ChosenII`.

## Diagnostics

For every rejected or raised II, emit the binding component(s), contributing ops/edges, numerator/capacity calculation where applicable, proof precision and target/model digests. Distinguish `lower-bound-too-high`, `unknown-required-resource`, `unsupported-loop-semantics` and later `placement-infeasible-at-II`.

## Tests / acceptance

Synthetic recurrences, multi-distance edges, bank/channel pressure, PRF ports/groups, lane6/lane7 and certificate bottlenecks must produce independently checkable lower bounds. Negative tests cover irreducible loops, unsupported side exits, unknown resource mappings, zero/negative distance mistakes, stale proof versions and profile attempts to alter a proof.

Cross-check bounded kernels against the Phase 15 exact oracle: the oracle may find the true optimum above the lower bound, but it must never find a feasible schedule below any component declared `Proven`.

**Acceptance:** all qualified loops have deterministic distance DAGs and independently checkable, versioned MII proof records; unknown required facts fail closed; no scheduler yet changes emitted code.
