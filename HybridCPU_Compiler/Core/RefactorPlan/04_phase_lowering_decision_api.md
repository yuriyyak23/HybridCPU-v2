# Phase 04 — Lowering Decision API

## Назначение

Заменить неявные `TryLower`/`bool success`/helper-return paths на typed decision API. Цель — сделать невозможной ситуацию, где compiler quietly falls back из одного контура в другой или представляет parser/helper success как production lowering.

## Центральный тип

```csharp
public abstract record CompilerLoweringDecision
{
    public required SemanticIntentKind IntentKind { get; init; }
    public required ExecutionContourKind ContourKind { get; init; }
    public required CompilerAuthorityClass AuthorityClass { get; init; }
    public required CompilerEvidenceClass EvidenceClass { get; init; }
    public required CompilerExecutionClaim ExecutionClaim { get; init; }
    public required NoFallbackProof NoFallbackProof { get; init; }
    public required string Reason { get; init; }
}
```

## Decision variants

### `EmitCarrierDecision`

Используется только там, где compiler вправе создать carrier bytes.

```csharp
public sealed record EmitCarrierDecision : CompilerLoweringDecision
{
    public required VliwCarrierImage CarrierImage { get; init; }
    public CompilerSidebandEnvelope? Sideband { get; init; }
    public DescriptorEnvelope? Descriptor { get; init; }
    public TypedSlotFactsEnvelope? TypedSlotFacts { get; init; }
}
```

Это не runtime legality и не execution claim.

### `RejectAtCompileTimeDecision`

Явный fail-closed результат.

```csharp
public sealed record RejectAtCompileTimeDecision : CompilerLoweringDecision
{
    public required CompilerRejectReason RejectReason { get; init; }
}
```

### `ParserOnlyDecision`

Операция распознана, но не исполнима через compiler emission.

```csharp
public sealed record ParserOnlyDecision : CompilerLoweringDecision;
```

### `HelperOnlyDecision`

Для scoped helper ABI, например ограниченного MatrixTile helper path.

```csharp
public sealed record HelperOnlyDecision : CompilerLoweringDecision
{
    public required string HelperAbiName { get; init; }
    public required IReadOnlyList<string> SupportedOperations { get; init; }
}
```

### `NoEmissionDecision`

Для VMX projection, SecureCompute policy/admission и иных случаев, где emission запрещен.

```csharp
public sealed record NoEmissionDecision : CompilerLoweringDecision;
```

### `FutureGatedDecision`

Для DSC2, broad accelerator protocol, unsupported backend paths и прочих заявленных, но закрытых направлений.

```csharp
public sealed record FutureGatedDecision : CompilerLoweringDecision
{
    public required IReadOnlyList<string> MissingGates { get; init; }
}
```

## No fallback policy

```csharp
public enum CompilerFallbackPolicy
{
    Forbidden,
    ExplicitlyAllowedForEquivalentScalarSemantics,
    DiagnosticOnly
}

public sealed record NoFallbackProof(
    CompilerFallbackPolicy Policy,
    bool WasFallbackAttempted,
    bool WasFallbackRejected,
    string Reason);
```

По умолчанию `Forbidden`. Любое implicit fallback поведение должно быть удалено или завернуто в explicit decision.

## Reject reasons

```csharp
public enum CompilerRejectReason
{
    Unknown,
    UnsupportedIntent,
    UnsupportedContour,
    DescriptorRequired,
    DescriptorAbiViolation,
    TypedSlotTopologyViolation,
    RuntimeAuthorityRequired,
    FallbackForbidden,
    VmxBackendEmissionForbidden,
    SecureComputeEmissionForbidden,
    MatrixTileOperationUnsupported,
    MatrixTileShapePolicyUnsupported,
    MatrixTileDtypePolicyUnsupported,
    MatrixTileAccumulatorPolicyUnsupported,
    DscProductionLoweringBlocked,
    DscParserOnly,
    L7DescriptorMissing,
    L7DescriptorlessSubmitForbidden,
    CapabilityGateMissing,
    FutureGated
}
```

## Tasks

### 1. Найти все lowering entrypoints

Из фазы 1 взять все методы, которые сейчас создают carrier/descriptor/sideband/facts или обещают lowering success.

### 2. Обернуть legacy methods

Сначала не переписывать алгоритмы. Создать adapter layer:

```text
legacy result -> CompilerLoweringDecision
```

### 3. Удалить ambiguous success из новых API

Новый код не должен возвращать `bool` как основной outcome. `bool` допустим только как техническое поле внутри typed decision.

### 4. Включить fail-closed по умолчанию

Если contour не может быть доказанно выбран, решение должно быть:

- `RejectAtCompileTimeDecision`, если intent недопустим;
- `ParserOnlyDecision`, если распознавание есть, emission нет;
- `FutureGatedDecision`, если путь заявлен, но закрыт;
- `NoEmissionDecision`, если emission архитектурно запрещен.

## Deliverables

- `CompilerLoweringDecision` hierarchy.
- `CompilerRejectReason`.
- `CompilerFallbackPolicy`.
- `NoFallbackProof`.
- Adapters над текущими lowering helpers.
- Отрицательные тесты на отсутствие implicit fallback.

## Acceptance criteria

Фаза завершена, если любой lowering path возвращает typed decision, а не голый `bool`, `null`, exception-as-control-flow или helper-specific success.

## Non-goals

- Не менять runtime legality.
- Не добавлять production backend lowering.
- Не расширять ISA.
- Не добавлять fallback ради прохождения тестов.

## Риски

Главный риск — сделать decision API слишком тонкой оберткой над старым поведением. Typed decision должен менять семантику интерфейса: caller обязан видеть, что произошло — emit, reject, parser-only, helper-only, no-emission или future-gated.