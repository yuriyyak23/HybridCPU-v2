# Phase 03 — IR Intent and Execution Contour Classification

## Назначение

Отделить смысл операции от способа ее переноса в runtime. IR должен сначала получить semantic intent, затем execution contour, и только потом lowering decision. Это предотвращает смешение scalar VLIW, Stream, MatrixTile, DSC, L7-SDC, VMX projection и SecureCompute admission.

## Целевой pipeline

```text
IR node / helper call / frontend directive
  -> SemanticIntentClassification
  -> ExecutionContourSelection
  -> CompilerLoweringDecision
  -> CompilerEmissionPackage
```

## Предлагаемые типы

### `SemanticIntentKind`

```csharp
public enum SemanticIntentKind
{
    Unknown,
    ScalarAlu,
    LoadStore,
    BranchControl,
    VectorStream,
    MatrixTile,
    DmaStreamCompute,
    ExternalAcceleratorCommand,
    VmxCompatibilityProjection,
    SecureComputeAdmission,
    RuntimeAssist,
    NonExecutable
}
```

### `ExecutionContourKind`

```csharp
public enum ExecutionContourKind
{
    None,
    NativeVliwScalar,
    NativeVliwLoadStore,
    StreamEngineVector,
    MatrixTileHelperOnly,
    DmaStreamComputeLane6,
    L7SdcLane7,
    VmxProjectionOnly,
    SecureComputePolicyAdmissionOnly,
    ParserOnly,
    NoEmission,
    FutureGated
}
```

### `SemanticIntentClassification`

```csharp
public sealed record SemanticIntentClassification(
    SemanticIntentKind IntentKind,
    ExecutionContourKind PreferredContour,
    bool RequiresDescriptor,
    bool RequiresToken,
    bool RequiresRuntimeLegality,
    bool MayPublishMemory,
    bool MayPublishRegisterState,
    bool CompilerMayEmitCarrier,
    bool CompilerMayEmitDescriptor,
    string Reason);
```

Поля `MayPublishMemory` и `MayPublishRegisterState` описывают архитектурный потенциал runtime-контура, а не право compiler самостоятельно публиковать состояние.

## Mapping rules

### Scalar / LoadStore / Branch

- Может вести к native VLIW carrier emission.
- Требует runtime legality.
- Не получает descriptor по умолчанию.
- Не должен маскировать hazards как compiler legality.

### VectorStream

- Может вести к StreamEngine contour только если текущий backend явно поддерживает этот scoped path.
- Не должен fallback-иться в scalar VLIW после отказа contour.
- Должен сохранять evidence о выборе contour.

### MatrixTile

- Текущий допустимый смысл: helper-only ABI для ограниченного набора MatrixTile операций.
- Unsupported dtype/shape/layout/accumulator policy должны давать fail-closed.
- Нельзя превращать MatrixTile helper success в general matrix compiler или GEMM lowering.

### DmaStreamCompute

- Вести к lane6 DSC только для текущего DSC1 scoped contour.
- DSC2/parser-only должно оставаться parser-only.
- Descriptor success не является execution authority.
- Commit/publication требуют runtime path.

### ExternalAcceleratorCommand / L7-SDC

- Вести к lane7 L7-SDC только при наличии sideband descriptor.
- Descriptorless submit запрещен.
- Нельзя fallback-иться в DSC/Stream/scalar после отказа L7 submit.

### VmxCompatibilityProjection

- Только projection/ABI vocabulary/no-emission.
- Никакой VMX backend emission.
- VMCS не является state owner.

### SecureComputeAdmission

- Только policy/admission/evidence.
- Никакой secure backend execution claim.
- Никакой nested/retire/publication claim из compiler.

## Tasks

### 1. Ввести intent classifier

Добавить слой, который принимает текущий IR/helper/directive и возвращает `SemanticIntentClassification` без emission.

### 2. Ввести contour selector

Контур выбирается отдельно от intent. Один intent может иметь несколько теоретических контуров, но текущая capability matrix должна выбрать ровно один результат:

- supported scoped contour;
- parser-only;
- helper-only;
- no-emission;
- future-gated;
- compile-time reject.

### 3. Запретить implicit fallback

Если contour выбран и отвергнут, fallback возможен только при явном `FallbackPolicy`, который по умолчанию должен быть `Forbidden`.

### 4. Привязать telemetry

Каждая classification должна писать audit-friendly reason:

```text
intent=MatrixTile contour=MatrixTileHelperOnly reason=unsupported accumulator policy -> rejected
intent=L7Sdc contour=L7SdcLane7 reason=missing descriptor -> rejected
intent=VMX contour=VmxProjectionOnly reason=projection-only -> no emission
```

## Deliverables

- `SemanticIntentKind`.
- `ExecutionContourKind`.
- `SemanticIntentClassification`.
- `ICompilerIntentClassifier`.
- `IExecutionContourSelector`.
- Negative tests на запрет смешения контуров.

## Acceptance criteria

Фаза завершена, если каждый путь lowering сначала проходит через intent/contour classification и в логах можно увидеть, почему операция попала именно в этот contour.

## Non-goals

- Не писать production lowering.
- Не менять runtime contour implementation.
- Не добавлять новые ISA semantics.
- Не расширять MatrixTile за пределы текущего helper ABI.
- Не превращать L7-SDC в универсальный accelerator compiler.

## Риски

Главный риск — попытаться упростить классификацию до `Opcode -> Lane`. Для HybridCPU этого недостаточно: один и тот же carrier slot не объясняет authority, publication, descriptor/token lifecycle и runtime legality. Поэтому intent и contour должны быть отдельными сущностями.