# 05 — Authority, capability, schema and provenance contracts

## Goal

Create versioned cross-layer contracts before multiple frontends and runtime-facing evidence multiply compatibility surfaces.

## Current-code reality and reuse

Reuse `CompilerAuthorityTaxonomy`, `CompilerStructuralAuthorityQuarantine`, `CompilerProductionLoweringGate`, `CompilerRuntimeBridge`, `CompilerBenchmarkEvidenceV1`, machine-description/topology digests and current ABI-specific compiler contracts. This phase consolidates; it must not duplicate or weaken them.

## Required contracts

- `CompilerFeatureSet` / capability descriptor: frontend features, ISA contours, scheduling transforms, evidence schema versions and managed-safety level.
- Versioned ABI/evidence envelope with producer version, target architecture revision, machine/topology digest and required consumer capabilities.
- Deterministic build provenance: source commit/tree, toolchain versions, frontend version, options, DataLayout/ABI version and profile hash/absence.
- Compatibility matrix and explicit migration rules (`Compatible`, `NeedsMigration`, `Unsupported`, `Unknown`).
- Unknown future fields may be ignored only where schema declares them non-semantic; unknown legality-relevant versions fail closed.
- Every schema declares **purpose**, **authority class**, **producer**, **consumer**, **compatibility range**, **semantic/non-semantic fields** and whether migration is lossless. Evidence schemas may describe facts; they cannot silently become command/permission schemas.
- Feature/capability descriptors distinguish `Supported`, `Unsupported`, `ExperimentalDefaultOff`, `RequiredByInput` and `Unknown`; absence is never interpreted as permission.

## Authority rules

No compiler schema field may mean runtime freshness, owner validity, replay validity, `LegalityDecision`, execution permission, publication, commit or retire permission. Runtime-attached epoch/generation/freshness state is never compiler-produced.

Compiler evidence is immutable build-time evidence. Runtime may attach its own validation/result envelope, but the two namespaces and signatures/digests must remain distinguishable so runtime state cannot be round-tripped back as compiler-created proof.

Profile-derived fields are explicitly tagged `ProfitabilityOnly`; they are rejected from legality/evidence locations that can affect alias safety, Stage A/B admission, replay/freshness or execution permission.

## Migration / compatibility requirements

1. Major-version mismatch on any legality-relevant ABI/evidence/capability schema => deterministic `UnsupportedVersion` before emission/execution handoff.
2. Minor-version forward compatibility is allowed only for fields declared non-semantic by the older consumer.
3. Migration tools produce a new provenance record; they never preserve the old producer identity as if bytes were unchanged.
4. Cached schedules/evidence are invalidated on target revision, machine/topology digest, ABI schema, compiler feature set or deterministic-option mismatch.
5. A capability required by Canonical IR must be validated at the frontend/core boundary and again before final lowering; adapters cannot assume downstream support.

## Tests / acceptance

- Static tests reject authority-bearing names/flows in compiler evidence contracts.
- Golden round-trip and version-skew tests cover N, N-1 where intentionally supported and unknown-major rejection.
- Same build inputs produce identical provenance/evidence bytes.
- Profile removal changes profitability metadata only, never legality fields.
- Negative tests inject runtime epoch/freshness/owner/replay fields into compiler envelopes and require rejection/quarantine.
- Cache-key tests prove every semantics-affecting digest/version invalidates stale products.

**Acceptance:** every later frontend/backend phase can name an explicit compatible contract instead of relying on assembly identity or undocumented metadata, and no version migration can elevate compiler evidence into runtime authority.
