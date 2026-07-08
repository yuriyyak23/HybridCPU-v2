# Phase 08 — Evidence Envelope and Telemetry

## Назначение

Сделать compiler evidence наблюдаемым, переносимым и проверяемым, но не превращать его в authority. Telemetry должна объяснять, почему compiler выбрал contour, почему отказал, какие gates отсутствуют и где runtime authority still required.

## Основной принцип

```text
compiler evidence explains compiler decisions
compiler evidence does not authorize runtime execution
```

## Evidence envelope

```csharp
public sealed record CompilerEvidenceEnvelope(
    Guid EvidenceId,
    CompilerContractView ContractView,
    SemanticIntentKind IntentKind,
    ExecutionContourKind ContourKind,
    CompilerLoweringDecisionSummary DecisionSummary,
    IReadOnlyList<CompilerEvidenceRecord> Records,
    IReadOnlyList<string> MissingGates,
    bool RuntimeLegalityStillRequired,
    string Reason);
```

### `CompilerEvidenceRecord`

```csharp
public sealed record CompilerEvidenceRecord(
    CompilerEvidenceClass EvidenceClass,
    string Source,
    string Statement,
    bool IsAuthority,
    string AuthorityBoundary);
```

Для compiler-generated records `IsAuthority` обычно должен быть `false`, кроме узкого structural/compiler-authority смысла, например `StructuralAgreement`.

## Required telemetry fields

Каждая lowering попытка должна логировать:

- `intent.kind`;
- `contour.kind`;
- `capability.state`;
- `decision.kind`;
- `reject.reason`;
- `fallback.policy`;
- `fallback.attempted`;
- `descriptor.status`;
- `typed_slot.policy`;
- `typed_slot.staging`;
- `runtime_legality.required`;
- `production_lowering.allowed`;
- `missing_gates`.

## Decision summary

```csharp
public sealed record CompilerLoweringDecisionSummary(
    string DecisionKind,
    CompilerRejectReason? RejectReason,
    CompilerExecutionClaim ExecutionClaim,
    CompilerPublicationClass PublicationClass,
    bool CarrierEmitted,
    bool SidebandEmitted,
    bool DescriptorEmitted,
    bool TypedSlotFactsEmitted,
    bool ProductionLoweringClaimed);
```

`ProductionLoweringClaimed` должен быть `false` для всех текущих future-gated/limited contours, пока не пройдены отдельные production gates.

## Tasks

### 1. Встроить evidence в decision API

Каждый `CompilerLoweringDecision` должен иметь evidence envelope или ссылку на evidence builder.

### 2. Нормализовать telemetry keys

Запретить свободный текст как единственный источник диагностики. Свободный текст остается в `Reason`, но ключевые поля должны быть structured.

### 3. Добавить negative evidence

Отказы должны быть так же подробно доказуемы, как успехи:

- unsupported MatrixTile dtype;
- missing L7 descriptor;
- VMX no-emission;
- SecureCompute no-backend;
- DSC2 parser-only;
- typed-slot mismatch;
- stale compiler contract.

### 4. Добавить audit snapshots

Для сложных decisions сохранять compact snapshot:

```text
intent -> contour -> capability -> descriptor status -> typed-slot facts -> bridge status
```

Этот snapshot должен быть пригоден для unit tests.

## Deliverables

- `CompilerEvidenceEnvelope`.
- `CompilerEvidenceRecord`.
- `CompilerLoweringDecisionSummary`.
- Structured telemetry keys.
- Snapshot serializer for tests.
- Negative evidence tests.

## Acceptance criteria

Фаза завершена, если по одному telemetry/evidence record можно понять:

1. что compiler пытался сделать;
2. почему выбрал этот contour;
3. почему emission был разрешен или запрещен;
4. какие gates отсутствовали;
5. почему runtime legality still required;
6. почему это не production lowering.

## Non-goals

- Не делать evidence certificate authority.
- Не смешивать runtime evidence и compiler evidence.
- Не записывать host-owned evidence в guest/domain architectural state.
- Не добавлять retire/publication telemetry от имени compiler.

## Риски

Главный риск — превратить telemetry в набор красивых строк без проверяемой структуры. Для HybridCPU telemetry является частью safety story: она должна доказывать fail-closed behavior и границы authority.