# 19 — VDSA and donor-prefetch advisory planning

## Goal

Plan VDSA/donor-prefetch opportunities as fault-neutral advisory information derived from static schedule/loop/resource evidence.

## Required work

- Identify donor-prefetch candidates from loop/region memory behavior, bank/channel model, VT relation and Phase 18 donor-quality evidence.
- Record address-evidence precision, dependence relation, distance, expected reuse/latency benefit, resource pressure and target/model/schema provenance.
- Exclude volatile/atomic/device/system memory, synchronization-sensitive accesses, unknown-fault semantics, unknown address-space behavior and lane/contour cases without an explicit safe contract.
- Version the plan/evidence through Phase 05 schemas.
- Preserve the original architectural memory operation/dependence graph. VDSA/donor-prefetch hints are additional advisory opportunities; they cannot replace, satisfy or remove the program's required load/store/ordering edges.
- Recompute/invalidate advisory plans when loop transforms, VT planning, spills/address rewriting, target topology or profile profitability input changes.

## Fault-neutrality contract

A VDSA/donor-prefetch plan is eligible only when its consumer can ignore, reject or suppress the hint without changing architectural program semantics. If a candidate could introduce an architecturally visible exception, fault, ordering effect, device access or reference-liveness change, the plan is `Unsupported` until an explicit runtime/ISA contract makes that behavior safe.

For managed references, GC-moving objects or addresses requiring pinning/handles, Phase 19 must stay disabled until Phase 25 defines a managed-safe contract. A raw compiler-derived managed address is never sufficient runtime authority for donor prefetch.

## Authority and safety

Compiler planning never makes a prefetch architecturally required and never grants execution permission. Runtime owns current address validity, freshness, replay, resource admission, Stage A/B behavior and whether a donor operation may execute. Unknown safety facts mean no plan.

Compiler metadata cannot mint or cache runtime epochs, generations, ownership/freshness tokens, donor slot reservations, `LegalityDecision` or replay validity. Runtime may reject every advisory candidate and program correctness must remain identical.

## Profitability policy

Profile data may rank already-safe candidates using latency, reuse, cache/bank/channel behavior and interference estimates. It cannot establish alias safety, address validity, non-faulting behavior or dynamic donor availability. Missing profile selects a deterministic static ranking or leaves the feature disabled.

## Determinism / rollout

Bound candidate count/distance/stages deterministically. Start emit-only/default-off; compare predicted vs actual usefulness before enabling any consumer behavior.

Any runtime consumer fast path is a separate runtime change with SafetyVerifier/LegalityDecision/replay/freshness qualification. Schema mismatch causes evidence to be ignored/rejected and falls back to ordinary execution.

## Tests / acceptance

Alias, page/fault boundary, bank/channel, stale evidence, replay, managed-reference and side-effect negative controls. KPI is useful latency hidden minus resource/interference cost, not number of generated hints.

Add tests proving the program remains semantically equivalent when all VDSA evidence is removed, when runtime rejects all hints, and when candidate addresses become stale between compilation and execution. Profile perturbation may change ranking but never the safe candidate set.

**Acceptance:** advisory plans are deterministic/versioned, fault-neutral under declared semantics, preserve all original memory/dependence obligations, and never alter correctness when ignored or rejected by runtime.
