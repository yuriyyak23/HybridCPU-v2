# 20 — FSP certificate emit-only

## Goal and motivation

Emit frozen V1 donor evidence alongside existing annotations for offline comparison while runtime behavior remains exactly unchanged.

**Status:** `Planned`.

## Confirmed starting state

- Current compiler emits annotations and advisory stealability facts but no structured V1 certificate.
- Current runtime must not consume an ABI that has not completed Phase 19 review.
- Existing exact probe/carrier work is default off and does not authorize a new donor fast path.

## In scope

- Compiler-side construction and serialization of the Phase 19 static envelope.
- Emit-only storage/transport, offline decoder and comparison with runtime-observed outcomes.
- Size, determinism, stale-binding and coverage telemetry.

## Explicit non-goals

- No runtime behavior change, acceptance shortcut, lane selection or SafetyVerifier bypass.
- No assertion that an emitted candidate is executable/stealable.
- No simultaneous Phase 21/22 implementation.

## Canonical types/components

`HybridCpuBundleBuilder`, `HybridCpuBundleLowerer`, `VliwBundleAnnotations` extension or separately versioned sidecar, Phase 19 codec and `CompilerRuntimeBridge` documentation.

## Target architecture and data flow

```text
materialized compiler bundle + shared resource facts
 -> V1 HintOnly/CompilerStructuralPreflight envelope
 -> emit sidecar/annotation under feature flag
 -> runtime ignores it
 -> offline tool joins runtime logs by identity
 -> prediction/coverage/staleness report
```

## Invariants and authority boundaries

- Emission never changes bundle membership, slots, machine bytes or runtime decisions.
- No compiler field represents live runtime readiness, physical lane selection, PRF ownership or commit state.
- Compiler output contains no epoch/generation/replay placeholder. A trusted runtime/loader may later create a separate validation context; the static envelope is never self-fresh.
- No cross-contour evidence reuse.

## Dependencies

Phase 19 verified and ABI frozen.

## Implementation backlog

1. Select additive sidecar/annotation transport with explicit size/lifetime ownership.
2. Build evidence only from canonical scheduled/materialized facts.
3. Add program/bundle/model identity and provenance.
4. Keep emitted VLIW bytes identical; prove with before/after goldens.
5. Add offline decoder and join keys for runtime telemetry.
6. Count candidate/evidence coverage, projected conflicts and stale/missing bindings.
7. Add flag default off, then consider default-on emit-only after overhead review.
8. Document that runtime ignores V1 in this phase.

## Migration and compatibility strategy

Additive versioned metadata; absent/unknown evidence is normal. Existing consumers that ignore the sidecar remain compatible. No compatibility API is removed.

## Tests

### Positive/property/determinism

- Eligible bundles produce stable V1 bytes; decoded facts match compiler resource summaries.
- Compiler machine code and schedule are byte-identical with flag on/off.
- Same input/profile produces identical evidence.

### Negative

- Unsupported facts, oversize payload, missing identity, model mismatch and serialization failure omit evidence with a reason and preserve normal output.

### Static

- Runtime scheduler/ingress code has no V1 decision branch in this phase.
- No authority-bearing fields or runtime certificate construction in compiler.

## Benchmarks and KPI

- Report `FSP donor candidates`, evidence emitted/omitted and Phase 18 static suitability/value classes. Offline joins may report accept/fallback/stale/reject only with explicit runtime numerators/denominators.
- Zero schedule/runtime cycle, IPC, memory-stall or verdict delta with flag on/off.
- Compile-time p95 <=1.02, peak memory <=1.02 and artifact growth <=1% or separately budgeted sidecar.
- 100% byte determinism and candidate attribution.

## Diagnostics and telemetry

Per-bundle eligibility/omit reason, evidence kind/version/size/digest, predicted register-group/bank/channel conflicts, lane6/lane7 pressure and join identity.

## Bounded-search, timeout and fallback policy

Evidence construction is O(bundle width) with fixed W=8 and fixed payload cap. Encoding failure/overflow omits the envelope; compilation and normal runtime path continue deterministically.

## Risks and forbidden shortcuts

- No runtime reader “for testing” that affects decisions.
- No schedule mutation to manufacture more donors in this phase.
- No treating predicted accept as actual runtime legality.

## Rollback / kill switch

Disable emission flag; existing bundles/annotations and runtime behavior remain unchanged.

## Acceptance and merge gates

- Machine-byte/codegen equivalence, determinism, payload and overhead gates pass.
- Static proof/runtime tests show V1 is ignored for decisions.
- Offline reports distinguish prediction from runtime fact.

## Status criteria

- `implemented`: flag-controlled emitter and offline decoder exist.
- `verified`: zero-behavior-delta and overhead/evidence tests pass.
- `default-enabled`: emit-only may default on after Phase 24 compiler+evidence review, still ignored by runtime.
- `release-authorized`: compiler metadata emission only.

## Residual work / next gate

Phase 21 requires separate runtime-owner authorization to bind runtime freshness, parse and measure V1 without a fast path.
