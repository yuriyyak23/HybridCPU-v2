# Phase 08 — Evidence Envelope and Telemetry

## Назначение

Сделать compiler evidence наблюдаемым, переносимым и проверяемым, но не превращать его в authority. Telemetry должна объяснять, почему compiler выбрал contour, почему отказал, какие gates отсутствуют и где runtime authority still required.

## Основной принцип

```text
compiler evidence explains compiler decisions
compiler evidence does not authorize runtime execution
compiler evidence != production lowering
compiler telemetry != runtime publication
```

Evidence must be structured enough for tests. Free-form strings are diagnostics, not the safety boundary.

## Evidence ownership and authority semantics

### `EvidenceOwnershipDomain`

```csharp
public enum EvidenceOwnershipDomain
{
    CompilerHostOwned,
    RuntimeObserved,
    TestHarnessOwned,
    GuestVisibleForbidden,
    DomainArchitecturalStateForbidden
}
```

### `EvidenceAuthoritySemantics`

```csharp
public enum EvidenceAuthoritySemantics
{
    EvidenceOnly,
    DiagnosticOnly,
    CompatibilityObservation,
    RuntimePolicyReferenceOnly,
    ForbiddenAsAuthority
}
```

Host-owned evidence must not enter guest/domain architectural state. This must be a validator rule, not only a comment.

## Evidence envelope

```csharp
public sealed record CompilerEvidenceEnvelope(
    Guid EvidenceId,
    CompilerContractView ContractView,
    SemanticIntentKind IntentKind,
    ExecutionContourKind ContourKind,
    CompilerLoweringDecisionSummary DecisionSummary,
    EvidenceOwnershipDomain OwnershipDomain,
    EvidenceAuthoritySemantics AuthoritySemantics,
    IReadOnlyList<CompilerEvidenceRecord> Records,
    IReadOnlyList<string> MissingGates,
    bool RuntimeLegalityAStillRequired,
    bool RuntimeLegalityBStillRequired,
    bool RuntimeCommitStillRequired,
    bool RuntimeRetireStillRequired,
    bool RuntimePublicationStillRequired,
    string Reason);
```

### `CompilerEvidenceRecord`

```csharp
public sealed record CompilerEvidenceRecord(
    CompilerEvidenceClass EvidenceClass,
    EvidenceOwnershipDomain OwnershipDomain,
    EvidenceAuthoritySemantics AuthoritySemantics,
    string Source,
    string Statement,
    bool IsAuthority,
    string AuthorityBoundary);
```

For compiler-generated records `IsAuthority` must normally be `false`, except for narrow compiler-internal structural statements such as `StructuralPlacementEvidence`. Even then it must not become runtime authority.

## Required telemetry fields

Каждая lowering попытка должна логировать structured keys:

```text
intent.kind
contour.kind
capability.observation_state
decision.kind
emission.class
production_lowering.status
authority.class
authority.source_kind
evidence.class
evidence.ownership_domain
evidence.authority_semantics
runtime_dependency
runtime_legality_a.required
runtime_legality_b.required
runtime_commit.required
runtime_retire.required
runtime_publication.required
sideband.requirement
descriptor.abi_status
typed_slot.policy_mode
typed_slot.staging
reject.reason
fallback.policy
fallback.attempted
fallback.proof_id
legacy_translation.source_file
legacy_translation.source_member
missing_gates
```

Telemetry must contain both `intent.kind` and `contour.kind`; contour-only logs are insufficient.

## Decision summary

```csharp
public sealed record CompilerLoweringDecisionSummary(
    CompilerLoweringDecisionKind DecisionKind,
    CompilerEmissionClass EmissionClass,
    CompilerProductionLoweringStatus ProductionLoweringStatus,
    CompilerRejectReason? RejectReason,
    CompilerExecutionClaim ExecutionClaim,
    CompilerPublicationClass PublicationClass,
    CompilerRuntimeAuthorityDependency RuntimeAuthorityDependency,
    bool CarrierEmitted,
    bool SidebandEmitted,
    bool DescriptorEmitted,
    bool TypedSlotFactsEmitted,
    bool StructuralAgreementEmitted,
    bool EvidenceEmitted,
    bool BridgeEnvelopePrepared,
    bool ProductionLoweringClaimed);
```

`ProductionLoweringClaimed` must be false for all future-gated/limited contours until a separate production gate has been implemented and tested.

## Negative evidence requirements

Rejections must be as evidence-rich as successful emissions. The following cases require negative evidence records:

- unsupported MatrixTile operation;
- unsupported MatrixTile dtype/shape/layout/accumulator;
- vector unsupported shape/dtype/stride/predicate;
- missing L7 descriptor;
- descriptorless L7 submit;
- lane6 descriptor on non-DSC opcode;
- simultaneous lane6 and lane7 descriptors on one instruction;
- VMX no-emission;
- SecureCompute no-backend;
- DSC2 parser-only;
- typed-slot mismatch;
- stale compiler contract;
- unknown contour;
- cross-contour fallback attempt;
- helper success not production lowering;
- descriptor success not authority.

## Audit snapshots

For complex decisions, create compact snapshots suitable for unit tests:

```text
intent -> contour -> capability observation -> decision kind -> emission class -> descriptor status -> typed-slot facts -> bridge status -> runtime dependency -> fallback proof
```

Example snapshot fields:

```text
intent=MatrixTile
contour=MatrixTileHelperOnly
decision=Reject
reject=MatrixTileUnsupportedDType
fallback=Forbidden
production_lowering=NotProductionLowering
runtime_legality_a=required_if_emitted
runtime_legality_b=required_if_emitted
```

## Tasks

### 1. Встроить evidence в decision API

Каждый `CompilerLoweringDecision` должен иметь evidence envelope или ссылку на evidence builder.

### 2. Нормализовать telemetry keys

Запретить свободный текст как единственный источник диагностики. Свободный текст остается в `Reason`, но ключевые поля должны быть structured.

### 3. Добавить negative evidence

Отказы должны быть так же подробно доказуемы, как успехи.

### 4. Добавить audit snapshots

Snapshot должен быть пригоден для unit tests and regression comparison.

### 5. Add evidence isolation validator

Validator must fail if compiler/host-owned evidence is projected into guest-visible or domain architectural state fields.

## Deliverables

- `CompilerEvidenceEnvelope`.
- `CompilerEvidenceRecord`.
- `EvidenceOwnershipDomain`.
- `EvidenceAuthoritySemantics`.
- `CompilerLoweringDecisionSummary`.
- Structured telemetry keys.
- Snapshot serializer for tests.
- Evidence isolation validator.
- Negative evidence tests.

Recommended namespace layout:

```text
HybridCPU.Compiler.Core.IR.Evidence
HybridCPU.Compiler.Core.IR.Telemetry
```

## Acceptance criteria

Фаза завершена, если по одному telemetry/evidence record можно понять:

1. что compiler пытался сделать;
2. почему выбрал этот contour;
3. почему emission был разрешен или запрещен;
4. какие gates отсутствовали;
5. почему runtime legality still required;
6. почему это не production lowering;
7. кто owns evidence;
8. почему evidence не является authority;
9. почему host-owned evidence не попало в guest/domain architectural state.

Machine-checkable gates:

```text
[ ] Evidence contains ownership domain.
[ ] Evidence contains authority semantics.
[ ] Telemetry contains decision.kind, contour.kind, authority.class, evidence.class, emission.class, production_lowering.status and fallback proof id.
[ ] Host-owned evidence cannot be written to guest/domain architectural state.
[ ] Negative paths produce evidence, not only exceptions/free-form strings.
[ ] Capability observation is logged as observation, not authority.
```

## Non-goals

- Не делать evidence certificate authority.
- Не смешивать runtime evidence и compiler evidence.
- Не записывать host-owned evidence в guest/domain architectural state.
- Не добавлять retire/publication telemetry от имени compiler.
- Не использовать telemetry as policy owner.

## Риски

Главный риск — превратить telemetry в набор красивых строк без проверяемой структуры. Для HybridCPU telemetry является частью safety story: она должна доказывать fail-closed behavior и границы authority.

Второй риск — advisory telemetry/profile/certificate-like data может незаметно стать policy. Evidence must say whether it is diagnostic, compatibility observation or runtime-policy-reference-only, and all compiler-generated evidence must remain forbidden as runtime authority.
