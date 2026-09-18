# 19 — Structured FSP donor evidence and certificate ABI

## Goal and motivation

Define a versioned, structured compiler-to-runtime donor evidence ABI that can be rejected safely and never substitutes for runtime legality. This phase specifies and tests the contract; it does not emit or consume it in production.

**Status:** `Planned`.

## Confirmed starting state

- Compiler lowering publishes `VliwBundleAnnotations` with typed-slot structural facts, safety-mask diagnostics and an advisory `StealabilityHint`.
- Runtime FSP builds and validates `BundleResourceCertificate4Way` with per-VT register read/write groups and shared resources.
- `CompilerRuntimeBridge` states runtime class admission, legality, publication, commit and retire are still required.
- No structured compiler donor-evidence envelope exists. Runtime epoch/generation/replay state exists under runtime owners and must not be serialized as compiler-minted freshness.

## In scope

- Static compiler schema for donor identity, evidence kind, resource summary, model/profile provenance and program/build/model/bundle/instruction identity.
- Separate runtime-only validation context for current epoch, generation, replay phase and owner/domain.
- Canonical serialization/hash and fail-closed validation rules.
- Compiler/runtime neutral fixtures and threat/compatibility model.
- Explicit claim taxonomy distinguishing hint, preflight evidence and runtime validation result.

## Explicit non-goals

- No compiler emission, runtime adoption, fast path or bypass.
- No compiler-owned lane choice, PRF allocation, replay, publication, commit or retire state.
- No compatibility API removal and no VMX/SecureCompute/device activation.

## Canonical types/components

Proposed `FspDonorStaticEnvelopeV1`, `DonorEvidenceKind`, `CompilerResourceSummaryV1`, canonical encoder/decoder; proposed runtime-owned `RuntimeEvidenceValidationContextV1` is never emitted by the compiler. Runtime-owned `BundleResourceCertificate4Way` remains distinct.

## Target architecture and data flow

```text
compiler schedule/model facts
 -> canonical static donor evidence payload
 -> version + program/build/model/bundle/instruction identity
 -> serialized static envelope (no runtime epoch/generation)
 -> [future runtime ingress]
 -> trusted loader/runtime attaches current epoch/generation/replay/owner context
 -> decode/schema/static identity + runtime freshness checks
 -> full normal runtime validation remains mandatory
```

Allowed claims are evidence/hints such as predicted resource groups and stealability class. Forbidden claims include “legal to issue”, chosen physical lane, completion, publication, commit or retire authorization.

## Invariants and authority boundaries

- Missing, unknown-version, malformed or static-identity-mismatched evidence fails closed to the normal path.
- Only trusted runtime/loader code binds the static envelope to current epoch/generation/replay/owner state; compiler bytes contain no authoritative runtime freshness.
- Evidence can reduce future recomputation only after runtime validates equivalent live facts; it cannot add legality.
- Stage A admission and Stage B materialization remain runtime-separated.
- Replay identity and runtime generation own freshness; compiler profile never proves legality.

## Dependencies

Phases 03, 07 and 18 verified.

## Implementation backlog

1. Inventory current annotation and runtime certificate fields with owners and lifetime.
2. Define allowed/forbidden claim matrix and version negotiation.
3. Specify fixed-width/endian canonical encoding, maximum sizes and unknown-field behavior.
4. Bind only static program/build/model, bundle/instruction, contour/domain and owner-VT claims in compiler bytes.
5. Define evidence kinds (`HintOnly`, `CompilerStructuralPreflight`) and prohibit a compiler `RuntimeValidated` kind.
6. Define a physically separate runtime validation context and specify stale/replay/cross-contour rejection taxonomy there.
7. Provide golden bytes and cross-component parser fixtures.
8. Threat-model forged, truncated, collision, downgrade and confused-deputy inputs.
9. Obtain compiler/runtime/SafetyVerifier owner review.
10. Freeze V1 before Phase 20 emission.

## Migration and compatibility strategy

V1 is additive beside current annotations. Unknown or absent evidence preserves the exact normal runtime path. Existing compatibility APIs stay until a separately accepted compatibility-window decision.

## Tests

### Positive/property/determinism

- Golden encode/decode for every evidence kind and maximum legal resource set.
- Canonical field ordering and byte-identical output.
- Decode/encode stability and independent compiler/runtime fixture agreement.

### Negative

- Unknown version/kind, malformed lengths, duplicate fields, wrong program/bundle/model digest, cross-contour donor and forged authority claim.
- Runtime-context tests separately cover stale epoch/generation/replay and owner/domain mismatch; compiler fixtures cannot set those fields.

### Static

- Schema contains no physical-lane authorization, commit/retire/publication flag or mutable runtime PRF state.
- Compiler cannot construct a runtime-validated result.

## Benchmarks and KPI

- ABI payload <=256 bytes/bundle default and decode p95 <=1 microsecond in microbenchmark (measurement target, not release authority).
- 100% malformed/stale/cross-contour fixtures rejected; zero divergence from normal validation verdict.
- Report projected donor candidates, predicted accept/reject reasons and payload bytes without changing runtime cycles.

## Diagnostics and telemetry

Static envelope version/kind/payload/model/program/bundle digests; separately, runtime context epoch/generation/replay/owner result and stable rejection reason. Do not log architectural register contents or secrets.

## Bounded-search, timeout and fallback policy

No search. Parser uses fixed maximum payload/field counts and checked arithmetic. Any bound or decode failure discards evidence and selects the ordinary validation path.

## Risks and forbidden shortcuts

- No certificate wording that implies runtime authorization.
- No epoch/generation/replay field in the compiler-produced envelope and no freshness minted from compiler data.
- No cross-contour fallback, silent downgrade or “unknown means accept”.

## Rollback / kill switch

V1 remains unused; deleting/disabling the schema integration has no codegen/runtime effect.

## Acceptance and merge gates

- Joint compiler/runtime/SafetyVerifier authority review.
- Golden, fuzz/property, freshness and downgrade tests pass.
- Allowed/forbidden claim matrix and compatibility policy are explicit.
- Phase 20 cannot start before V1 is frozen.

## Status criteria

- `implemented`: schema, canonical codec and fixtures exist.
- `verified`: owner review and adversarial/cross-component tests pass.
- `default-enabled`: not applicable in contract-only phase.
- `release-authorized`: ABI publication only; no emission, runtime adoption or fast path.

## Residual work / next gate

Phase 20 may emit the static V1 envelope behind a compiler flag. Runtime binding/consumption waits for Phase 21 authorization.
