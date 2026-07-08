# Phase 04 — Lowering Decision API

## Назначение

Заменить неявные `TryLower`/`bool success`/helper-return paths на typed decision API. Цель — сделать невозможной ситуацию, где compiler quietly falls back из одного контура в другой или представляет parser/helper success как production lowering.

`CompilerLoweringDecision` должен быть semantic firewall, а не wrapper над `bool`, `Try*`, `HasLegalAssignment`, `Success`, `Valid`, `Accepted` или exception-as-control-flow.

## Required enums

### `CompilerLoweringDecisionKind`

```csharp
public enum CompilerLoweringDecisionKind
{
    EmitCarrier,
    Reject,
    ParserOnly,
    HelperOnly,
    NoEmission,
    FutureGated
}
```

### `CompilerEmissionClass`

```csharp
public enum CompilerEmissionClass
{
    NoEmission,
    CarrierOnly,
    CarrierWithOptionalSideband,
    CarrierWithRequiredSideband,
    DescriptorOnly,
    SidebandOnly,
    EvidenceOnly,
    CompatibilityProjectionOnly
}
```

### `CompilerProductionLoweringStatus`

```csharp
public enum CompilerProductionLoweringStatus
{
    NotProductionLowering,
    HelperAbiOnly,
    ParserOnly,
    DiagnosticOnly,
    ProductionCandidateRequiresRuntimeLegality,
    ProductionAllowedByExplicitCompilerGate
}
```

`ProductionAllowedByExplicitCompilerGate` is forbidden unless Phase 01 inventory identifies an existing supported production path and Phase 09 has negative tests for every missing gate. For current disputed contours, prefer `HelperAbiOnly`, `ParserOnly`, `DiagnosticOnly`, `NotProductionLowering` or `ProductionCandidateRequiresRuntimeLegality`.

## Центральный тип

```csharp
public abstract record CompilerLoweringDecision
{
    public required CompilerLoweringDecisionKind DecisionKind { get; init; }
    public required SemanticIntentKind IntentKind { get; init; }
    public required ExecutionContourKind ContourKind { get; init; }
    public required CompilerAuthorityClass AuthorityClass { get; init; }
    public required CompilerAuthoritySourceKind AuthoritySourceKind { get; init; }
    public required CompilerEvidenceClass EvidenceClass { get; init; }
    public required CompilerExecutionClaim ExecutionClaim { get; init; }
    public required CompilerPublicationClass PublicationClass { get; init; }
    public required CompilerEmissionClass EmissionClass { get; init; }
    public required CompilerProductionLoweringStatus ProductionLoweringStatus { get; init; }
    public required CompilerRuntimeAuthorityDependency RuntimeAuthorityDependency { get; init; }
    public required NoFallbackProof NoFallbackProof { get; init; }
    public required IReadOnlyList<CompilerArtifactKind> ProducedArtifacts { get; init; }
    public required IReadOnlyList<CompilerArtifactKind> RequiredArtifacts { get; init; }
    public required IReadOnlyList<CompilerRejectReason> RejectReasons { get; init; }
    public required LegacyApiTranslation? LegacyTranslation { get; init; }
    public required string Reason { get; init; }
}
```

The base type intentionally carries fields that feel redundant. This redundancy is the safety boundary: a caller should not need inference to know whether a result emitted a carrier, only parsed, only helped, or still requires runtime legality.

## Decision subtypes

### `EmitCarrierDecision`

```csharp
public sealed record EmitCarrierDecision : CompilerLoweringDecision
{
    public required CompilerEmissionPackage Package { get; init; }
}
```

Rules:

- `DecisionKind == EmitCarrier`.
- `EmissionClass` must not be `NoEmission`.
- `ProductionLoweringStatus` must not be `ProductionAllowedByExplicitCompilerGate` unless explicit gates exist.
- `RuntimeAuthorityDependency` must include runtime Legality A/B for executable candidates.
- Carrier emission does not imply execution, publication, commit or retire.

### `RejectAtCompileTimeDecision`

```csharp
public sealed record RejectAtCompileTimeDecision : CompilerLoweringDecision
{
    public required IReadOnlyList<CompilerRejectReason> Reasons { get; init; }
    public required bool IsFailClosed { get; init; }
}
```

Rules:

- rejection must record no-fallback proof;
- rejection must not silently retry another contour;
- rejection must include negative evidence.

### `ParserOnlyDecision`

```csharp
public sealed record ParserOnlyDecision : CompilerLoweringDecision
{
    public required DescriptorEnvelope? ParsedDescriptor { get; init; }
}
```

Parser success does not imply production lowering, execution, bridge acceptance or runtime legality.

### `HelperOnlyDecision`

```csharp
public sealed record HelperOnlyDecision : CompilerLoweringDecision
{
    public required string HelperAbiName { get; init; }
    public required IReadOnlyList<string> SupportedOperations { get; init; }
}
```

Helper success is a scoped ABI/helper result only. MatrixTile helper success is not a general matrix compiler and not production lowering.

### `NoEmissionDecision`

```csharp
public sealed record NoEmissionDecision : CompilerLoweringDecision
{
    public required string NoEmissionReason { get; init; }
}
```

Used for VMX projection-only, SecureCompute admission-only, diagnostics and policy/evidence-only outputs.

### `FutureGatedDecision`

```csharp
public sealed record FutureGatedDecision : CompilerLoweringDecision
{
    public required IReadOnlyList<string> RequiredGates { get; init; }
    public required IReadOnlyList<string> MissingGates { get; init; }
}
```

Future-gated is a rejection/no-emission class, not permission to fallback.

## `NoFallbackProof`

```csharp
public sealed record NoFallbackProof(
    Guid ProofId,
    ExecutionContourKind SourceContour,
    ExecutionContourKind? RejectedFallbackContour,
    FallbackPolicy Policy,
    bool SameContourStructuralRetryAllowed,
    bool CrossContourFallbackForbidden,
    IReadOnlyList<ExecutionContourKind> ProhibitedFallbacks,
    string Reason);
```

### `FallbackPolicy`

```csharp
public enum FallbackPolicy
{
    Forbidden,
    SameContourStructuralRetryOnly,
    ExplicitPolicyRequired,
    DiagnosticOnlyNoEmission
}
```

Same-contour structural placement retry is allowed only if it does not change semantic intent, contour, sideband requirement, descriptor requirement, emission class, authority class or runtime dependency. Cross-contour fallback is forbidden by default.

## Legacy adapter anti-wrapper guard

### `LegacyApiTranslation`

```csharp
public sealed record LegacyApiTranslation(
    string SourceFile,
    string SourceMember,
    string LegacyReturnShape,
    string LegacySuccessMeaning,
    string TypedReplacementMeaning,
    bool PreservesBehaviorOnly,
    bool StrengthensAuthority);
```

`StrengthensAuthority` must always be `false`. Tests must fail if an adapter maps `true`, `Success`, `Valid`, `Accepted`, `IsLegal` or `HasLegalAssignment` directly to execution, runtime legality, commit, retire, publication or production lowering.

Mandatory adapters from Phase 01:

```text
TryRecoverFromInstruction -> ParserOnlyDecision or HelperOnlyDecision, never ProductionAllowed
HasLegalAssignment -> structural placement evidence, never RuntimeLegal
IrBundleLegalityResult.Legal -> structural admission evidence, never RuntimeLegal
EmitAnnotationsForBundle -> SidebandOnly/CarrierWithSideband artifact, never authority
EmitFactsForBundle -> TypedSlotFacts evidence, never legality authority
ValidateRuntimeContractCompatibility -> version compatibility check, never execution readiness
```

## `CompilerRejectReason`

```csharp
public enum CompilerRejectReason
{
    Unknown,
    UnsupportedIntent,
    UnknownContour,
    ProviderUnavailable,
    MissingSideband,
    MissingDescriptor,
    DescriptorAbiViolation,
    TypedSlotFactsMismatch,
    StaleCompilerContract,
    RuntimeLegalityRequired,
    MatrixTileUnsupportedOperation,
    MatrixTileUnsupportedDType,
    MatrixTileUnsupportedShape,
    MatrixTileUnsupportedLayout,
    MatrixTileUnsupportedAccumulator,
    DscParserOnly,
    DscCommitGateMissing,
    L7DescriptorlessSubmitForbidden,
    VmxBackendEmissionForbidden,
    SecureComputeEmissionForbidden,
    CapabilityObservationNotAuthority,
    CrossContourFallbackForbidden,
    HelperSuccessNotProductionLowering,
    DescriptorSuccessNotAuthority
}
```

## API tasks

### 1. Replace bare success APIs at boundaries

New public compiler lowering boundaries must return `CompilerLoweringDecision`. Internal legacy paths may remain until migrated but must be wrapped before crossing the new handoff API.

### 2. Add compile-time/default constructors guards

Records should not have constructors that default to authority-like success. A missing field should be a compile error or explicit `Unknown`/`Rejected`/`NoEmission` value.

### 3. Add no-fallback proof propagation

Every decision must include `NoFallbackProof`, including success paths. Success still needs proof that it did not silently downgrade to another contour.

### 4. Add decision validation

Add validators or tests for impossible combinations:

```text
DecisionKind=NoEmission with EmissionClass!=NoEmission -> invalid
ParserOnly with ProductionAllowedByExplicitCompilerGate -> invalid
HelperOnly with RuntimeExecutionRequired -> invalid unless explicitly bridged as helper ABI evidence only
Reject with ProducedArtifacts containing carrier -> invalid unless diagnostic-only artifact
BridgeAccepted in CompilerLoweringDecision -> invalid; bridge acceptance is separate phase
```

## Deliverables

- `CompilerLoweringDecision` hierarchy.
- `CompilerLoweringDecisionKind`.
- `CompilerEmissionClass`.
- `CompilerProductionLoweringStatus`.
- `NoFallbackProof`.
- `FallbackPolicy`.
- `LegacyApiTranslation`.
- `CompilerRejectReason` expanded matrix.
- Decision validator tests.
- Legacy adapters for current `Try*`/`bool`/`IsLegal`/`HasLegalAssignment` paths.

Recommended namespace layout:

```text
HybridCPU.Compiler.Core.IR.Lowering
HybridCPU.Compiler.Core.IR.Diagnostics
```

## Acceptance criteria

Фаза завершена, если:

1. Новый lowering boundary never returns bare `bool`.
2. Every lowering outcome has decision kind, emission class, production lowering status, authority class, evidence class and runtime dependency.
3. Parser/helper success cannot be consumed as production lowering.
4. Descriptor ABI success cannot be consumed as execution authority.
5. Same-contour structural retries are distinguished from forbidden cross-contour fallbacks.
6. Legacy API adapters all have `LegacyApiTranslation` with `StrengthensAuthority == false`.
7. Negative tests fail on any direct `Success -> RuntimeLegal` or `HasLegalAssignment -> BridgeAccepted` mapping.

## Non-goals

- Не переписывать scheduler.
- Не добавлять production backends.
- Не менять runtime legality.
- Не превращать MatrixTile helper ABI в general matrix lowering.
- Не превращать parser result в runtime execution permission.

## Риски

Главный риск — сделать новый `CompilerLoweringDecision` как красивую оболочку над старым `bool success`. Поэтому decision must encode all safety-relevant distinctions as fields, not rely on caller convention.
