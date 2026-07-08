# Phase 06 — Typed-Slot Facts and Runtime Legality Bridge

## Назначение

Сделать typed-slot facts и runtime bridge формальными, но не смешивать их с runtime legality. Compiler может подготовить structural agreement, но окончательный `LegalityDecision` остается за runtime.

## Основной принцип

```text
typed-slot facts == compiler/runtime structural agreement input
typed-slot facts != runtime legality
typed-slot facts != execution authorization
```

## Предлагаемые типы

### `TypedSlotFactsEnvelope`

```csharp
public sealed record TypedSlotFactsEnvelope(
    TypedSlotBundleFacts Facts,
    CompilerTypedSlotPolicy Policy,
    TypedSlotFactStaging Staging,
    IrAdmissibilityAgreement Agreement,
    CompilerCoreResultHeader Header);
```

### `IrAdmissibilityAgreement`

```csharp
public sealed record IrAdmissibilityAgreement(
    bool StructurallyAdmissible,
    IReadOnlyList<string> RequiredSlotClasses,
    IReadOnlyList<string> PinningConstraints,
    IReadOnlyList<string> DynamicRuntimeGates,
    string Reason);
```

`StructurallyAdmissible == true` не означает, что runtime Stage A/Stage B примут bundle.

### `RuntimeBridgeEnvelope`

```csharp
public sealed record RuntimeBridgeEnvelope(
    CompilerContractView ContractView,
    VliwCarrierImage? Carrier,
    CompilerSidebandEnvelope? Sideband,
    DescriptorEnvelope? Descriptor,
    TypedSlotFactsEnvelope? TypedSlotFacts,
    CompilerEvidenceEnvelope Evidence,
    CompilerCoreResultHeader Header);
```

### `BridgeAcceptanceReport`

```csharp
public enum BridgeAcceptanceStatus
{
    Unknown,
    BridgeAccepted,
    BridgeRejected,
    VersionRejected,
    AgreementFailure,
    SidebandRejected,
    DescriptorRejected,
    TypedSlotFactsRejected,
    Quarantined
}

public sealed record BridgeAcceptanceReport(
    BridgeAcceptanceStatus Status,
    bool RuntimeLegalityStillRequired,
    string Reason);
```

Запрещены статусы `RuntimeLegal`, `ExecutionReady`, `Committed`, `Retired`.

## Bridge API

```csharp
public interface ICompilerRuntimeBridge
{
    BridgeAcceptanceReport DeclareCompilerContractVersion(CompilerContractView contract);
    BridgeAcceptanceReport AcceptSideband(CompilerSidebandEnvelope sideband);
    BridgeAcceptanceReport AcceptDescriptor(DescriptorEnvelope descriptor);
    BridgeAcceptanceReport AcceptTypedSlotFacts(TypedSlotFactsEnvelope facts);
    BridgeAcceptanceReport AcceptEmissionPackage(CompilerEmissionPackage package);
}
```

Это bridge acceptance, а не runtime execution.

## Tasks

### 1. Обернуть текущие typed-slot facts

Если текущие facts находятся в validation-only/compatibility mode, API должен сохранять этот staging как часть результата.

### 2. Разделить structural admissibility и dynamic legality

Ввести отдельные имена:

- `CompilerStructuralAdmissionDecision`;
- `IrAdmissibilityAgreement`;
- `RuntimeLegalityRequired`;
- `RuntimeLegalityDecision` только в runtime-owned boundary.

### 3. Версионировать bridge

`CompilerContractView` должен включать:

- contract version;
- typed-slot policy mode;
- supported carrier shape;
- supported sideband envelope version;
- supported descriptor ABI versions;
- known future-gated contours.

### 4. Добавить quarantine path

Если runtime bridge видит structural mismatch, stale version, unknown contour или invalid facts, результат должен быть `Quarantined` или `BridgeRejected`, но не silent fallback.

## Deliverables

- `TypedSlotFactsEnvelope`.
- `IrAdmissibilityAgreement`.
- `RuntimeBridgeEnvelope`.
- `BridgeAcceptanceReport`.
- `ICompilerRuntimeBridge`.
- Тесты на stale contract version.
- Тесты на typed-slot mismatch.

## Acceptance criteria

Фаза завершена, если:

1. typed-slot facts могут отсутствовать без изменения legacy compatibility path, если текущая политика это разрешает;
2. typed-slot facts при наличии проверяются;
3. mismatch дает diagnostic/quarantine;
4. bridge acceptance не выдается за runtime legality;
5. logs показывают, что Stage A/Stage B остаются runtime-owned.

## Non-goals

- Не переносить Stage A/Stage B в compiler.
- Не делать compiler source of truth для legality.
- Не добавлять runtime retire/commit в bridge.
- Не менять contract version без отдельного migration PR.

## Риски

Главный риск — назвать bridge acceptance словом `Legal`. Это сломает архитектурный смысл HybridCPU. Bridge может принять пакет как структурно пригодный для runtime рассмотрения, но только runtime решает legality и execution.