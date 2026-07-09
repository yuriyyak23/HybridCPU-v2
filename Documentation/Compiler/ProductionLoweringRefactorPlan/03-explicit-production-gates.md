# 03 — Explicit production gate model

## Goal

Introduce a typed production gate model that is separate from helper/parser/descriptor evidence and separate from runtime authority.

This phase may add API/types in a later implementation PR, but in the current docs branch it only defines the design target.

## Design target

A production gate is not a runtime permission. It is a compiler-side precondition that says the compiler is allowed to construct a contour-specific production package while still declaring which runtime authority remains pending.

Suggested model:

```csharp
public sealed record CompilerProductionLoweringContext(
    CompilerTargetProfile TargetProfile,
    string ProducerSurface,
    CompilerProductionLoweringProfile ProductionProfile);

public sealed record CompilerProductionLoweringProfile(
    string Name,
    bool EnablesProductionLowering,
    IReadOnlySet<ExecutionContourKind> EnabledContours,
    IReadOnlySet<string> EnabledGateIds);

public sealed record CompilerProductionLoweringGateResult(
    ExecutionContourKind ContourKind,
    bool IsProductionGateSatisfied,
    IReadOnlyList<string> SatisfiedGates,
    IReadOnlyList<string> MissingGates,
    CompilerRuntimeAuthorityDependency RuntimeAuthorityStillRequired,
    string Reason);
```

## Required gate families

Every contour-specific production provider must check:

1. **Profile gate** — target profile explicitly enables production lowering.
2. **Contour gate** — the exact contour is enabled; no fallback contour is allowed.
3. **Intent gate** — classifier coverage is complete for the operation shape.
4. **Artifact gate** — required carrier/sideband/descriptor/facts envelopes are present.
5. **Runtime dependency gate** — runtime Legality A/B and any contour-specific runtime gates remain declared.
6. **No-fallback gate** — `NoFallbackProof` exists and forbids cross-contour fallback.
7. **Parity gate** — positive golden artifact and ISE parity tests exist.
8. **Telemetry/evidence gate** — package emits complete evidence and telemetry.

## Gate result must not imply

A satisfied compiler production gate does **not** imply:

- runtime legality;
- runtime execution;
- architectural publication;
- memory/register commit;
- retire;
- guest/domain authority;
- descriptor/token/certificate authority.

## Production package status

The current `CompilerProductionLoweringStatus` should gain a value with semantics similar to:

```text
ProductionCarrierPackageRuntimeAuthorityPending
```

This value means:

- the compiler produced a contour-specific production package;
- the package is not helper-only or parser-only;
- runtime Legality A/B and execution are still pending;
- publication/commit/retire remain runtime-owned;
- no descriptor or sideband is promoted to execution authority.

## Merge gate

- New gate types are additive.
- Existing shell provider behavior is unchanged.
- Existing helper/parser/descriptor adapters remain non-production.
- No contour can become production-enabled simply because `CompilerTargetProfile.AllowsCarrierEmission` is true.

## Rollback

Disable or remove production gate types before any provider consumes them. No runtime ABI impact.
