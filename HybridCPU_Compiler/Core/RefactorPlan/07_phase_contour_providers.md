# Phase 07 — Contour Lowering Providers

## Назначение

Разложить lowering logic по contour-specific providers. Цель — убрать универсальные helpers, которые скрыто смешивают Scalar VLIW, Stream, MatrixTile, DSC, L7-SDC, VMX и SecureCompute.

## Базовая форма provider

```csharp
public interface IContourLoweringProvider
{
    ExecutionContourKind ContourKind { get; }
    CompilerLoweringCapability ObserveCapability(CompilerTargetProfile target);
    CompilerLoweringDecision Analyze(SemanticIntentClassification intent, CompilerLoweringContext context);
    CompilerLoweringDecision Lower(SemanticIntentClassification intent, CompilerLoweringContext context);
}
```

`Analyze` не должен emit-ить carrier. `Lower` не должен обходить `Analyze`.

## Capability model

```csharp
public enum CompilerLoweringCapabilityState
{
    NoEmission,
    ParserOnly,
    HelperOnly,
    ValidationOnly,
    ScopedRuntimeContour,
    ProductionBlocked,
    ProductionAllowed,
    FutureGated
}

public sealed record CompilerLoweringCapability(
    ExecutionContourKind ContourKind,
    CompilerLoweringCapabilityState State,
    IReadOnlyList<string> RequiredGates,
    IReadOnlyList<string> MissingGates,
    string Reason);
```

`ProductionAllowed` должен быть редким и требовать явных conformance gates. Для текущих спорных контуров безопасное состояние — `HelperOnly`, `ParserOnly`, `ScopedRuntimeContour`, `ProductionBlocked` или `NoEmission`.

## Providers

### `ScalarVliwLoweringProvider`

Ответственность:

- native VLIW scalar/load/store/branch carrier construction;
- bundle shape preservation;
- typed-slot facts production where available;
- no runtime legality claim.

Запреты:

- не принимать Stream/MatrixTile/DSC/L7 intent через scalar fallback;
- не скрывать hazards как compiler legality.

### `StreamVectorLoweringProvider`

Ответственность:

- только scoped StreamEngine path, если он разрешен target profile;
- явный fail-closed при unsupported vector shape;
- evidence о contour selection.

Запреты:

- no hidden scalar fallback;
- no claim of broad vector compiler.

### `MatrixTileLoweringProvider`

Ответственность:

- helper-only ABI для текущих поддержанных MatrixTile операций;
- явная проверка operation/dtype/shape/layout/accumulator policy;
- typed `HelperOnlyDecision` или `RejectAtCompileTimeDecision`.

Запреты:

- no general matrix compiler;
- no GEMM/LLM/FP4 claim unless separate gates are implemented;
- no scalar/stream fallback after MatrixTile rejection.

### `DmaStreamComputeLoweringProvider`

Ответственность:

- lane6 DSC scoped contour;
- DSC1 descriptor ABI construction;
- parser-only/future-gated distinction for unsupported DSC phases;
- explicit commit/publication requirements as runtime-owned.

Запреты:

- no DSC2 executable claim if only parser exists;
- no memory publication claim from compiler;
- no fallback to L7/Stream/scalar.

### `L7SdcLoweringProvider`

Ответственность:

- lane7 SDC command handling;
- descriptor sideband requirement;
- token lifecycle evidence as evidence only;
- explicit `L7DescriptorMissing` rejection.

Запреты:

- no descriptorless submit;
- no universal accelerator compiler;
- no fallback to DSC after rejected submit.

### `VmxProjectionLoweringProvider`

Ответственность:

- compatibility vocabulary/projection only;
- `NoEmissionDecision` for backend emission attempts;
- evidence that VMX is not source of truth.

Запреты:

- no VMX backend;
- no VMCS state ownership;
- no VmxCaps authority.

### `SecureComputeAdmissionLoweringProvider`

Ответственность:

- policy/admission/evidence carrier only;
- `NoEmissionDecision` for secure backend execution attempts;
- explicit separation of admission and execution.

Запреты:

- no secure backend execution;
- no nested domain execution claim;
- no retire/publication claim.

## Provider registry

```csharp
public interface IContourLoweringProviderRegistry
{
    IContourLoweringProvider Resolve(ExecutionContourKind contourKind);
    IReadOnlyList<CompilerLoweringCapability> ObserveAll(CompilerTargetProfile target);
}
```

Registry must fail closed on unknown contour.

## Tasks

### 1. Create provider shells

Добавить пустые provider shells, которые сначала вызывают legacy code через adapters.

### 2. Move decision ownership into providers

Каждый provider должен возвращать `CompilerLoweringDecision`, а не private helper-specific result.

### 3. Add contour-specific negative tests

Минимум:

- MatrixTile unsupported dtype -> reject, no fallback.
- DSC2 parser-only -> no executable carrier.
- L7 submit without descriptor -> reject.
- VMX operation -> projection/no-emission.
- SecureCompute admission -> no secure backend execution.

### 4. Deprecate universal lowering helpers

Все helpers, которые принимают широкий generic intent и сами выбирают путь, должны быть помечены как legacy или превращены в dispatcher over providers.

## Deliverables

- `IContourLoweringProvider`.
- `CompilerLoweringCapability`.
- Provider registry.
- Provider shells for each contour.
- Legacy adapters.
- Contour-specific negative tests.

## Acceptance criteria

Фаза завершена, если ни один contour не может быть выбран неявно внутри generic helper без audit trail, а каждый отказ возвращает typed decision с reason и no-fallback proof.

## Non-goals

- Не реализовывать missing backend paths.
- Не расширять opcode set.
- Не менять runtime implementation.
- Не делать universal accelerator provider.

## Риски

Главный риск — сделать registry удобным fallback-router. Registry должен быть маршрутизатором ответственности, а не механизмом спасения rejected lowering. Если contour отвергнут, это результат, а не приглашение попробовать другой contour.