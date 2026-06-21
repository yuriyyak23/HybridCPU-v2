
Ниже — системная постановка **Compiler API** для HybridCPU-v2. Я исхожу из текущей архитектурной линии репозитория: активный frontend является native-VLIW-only; носитель — фиксированная 8-slot / 256-byte `VLIW_Bundle`; typed-slot scheduling имеет Stage A / Stage B; `LegalityDecision` остаётся runtime-owned authority; typed-slot facts сейчас находятся в режиме `ValidationOnly` / `CompatibilityValidation`, а `CompilerBackendLoweringContract` прямо блокирует production lowering за пределами текущих ограниченных DSC1 Phase 06 и L7 Phase 08 / 08A контуров. ([GitHub](https://github.com/yuriyyak23/HybridCPU-v2))

## 1. Главный вывод

**Compiler API нужен не как один метод `Compile()`**, а как явная система границ:

```text
semantic intent
  -> contour classification
  -> capability-gated lowering decision
  -> carrier emission
  -> sideband / descriptor emission
  -> typed-slot facts
  -> compiler evidence
  -> runtime admission handoff
```

При этом Compiler API **не должен** возвращать результат вида:

```csharp
CompiledExecutableOperation
```

потому что это смешивает carrier, descriptor, execution и authority.

Правильный верхнеуровневый продукт API:

```csharp
CompilerEmissionPackage
```

где отдельно представлены:

```csharp
VliwCarrierImage
CompilerSidebandEnvelope
DescriptorEnvelope
TypedSlotBundleFacts
IrAdmissibilityAgreement
LoweringDecision
ConformanceGateReport
EvidenceEnvelope
```

То есть API должен говорить:

> «Я построил структурно согласованный носитель и сопроводительные факты для такого-то execution contour, при таких-то capability gates; runtime обязан принять или отвергнуть это самостоятельно».

А не:

> «Я скомпилировал операцию, значит она допустима к исполнению».

---

## 2. Текущая проблема Compiler API

Сейчас компилятор выглядит скорее как **compiler/runtime handoff layer**, чем как зрелый backend. Это не недостаток само по себе; это нормальное состояние для HybridCPU, где runtime сохраняет право последнего допуска.

Но существующая архитектурная опасность такова:

```text
compiler API слишком легко станет удобным фасадом,
который случайно начнёт скрывать различия между:
carrier,
sideband,
descriptor,
typed-slot facts,
runtime legality,
token lifecycle,
commit,
retire,
evidence,
production lowering.
```

Это особенно опасно для:

```text
MatrixTile
DmaStreamCompute
L7-SDC
VMX projection
SecureCompute
Vector / Stream
```

потому что именно там есть много «почти готовых» поверхностей: парсер, descriptor ABI, helper, sideband, telemetry, fake/test backend, token, status, replay evidence. Но ни одна из них сама по себе не является execution authority.

README прямо фиксирует, что descriptor/sideband preservation не является production executable lowering, а будущий lowering требует реализации runtime semantics, publication, ordering/conflict semantics, cache protocol, положительных и отрицательных тестов, compiler conformance и документационной миграции. ([GitHub](https://github.com/yuriyyak23/HybridCPU-v2))

---

## 3. Требуемая форма Compiler API

Я бы разделил Compiler API на семь слоёв.

### 3.1. Session API

```csharp
public sealed class HybridCpuCompilerSession
{
    public CompilerContractView Contract { get; }
    public CompilerTargetProfile Target { get; }
    public CompilerCapabilitySet Capabilities { get; }

    public CompilerAnalysisResult Analyze(CompilerModule module);
    public CompilerEmissionPackage Emit(CompilerEmitRequest request);
}
```

`HybridCpuCompilerSession` не должен быть «магическим компилятором всего». Это контекст, связанный с:

```text
CompilerContract.Version
CompilerTypedSlotPolicy
BackendLoweringCapability
RuntimeContourCapability
NoFallbackPolicy
EvidencePolicy
```

С учётом текущего `CompilerContract.Version == 6` и режима `CompatibilityValidation`, API обязан уметь явно сказать: «typed-slot facts emitted and validated as compatibility evidence, not mandatory admission substrate». ([GitHub](https://github.com/yuriyyak23/HybridCPU-v2))

---

### 3.2. Intent API

Нужен слой, который классифицирует не opcode, а **semantic intent**.

```csharp
public enum SemanticIntentKind
{
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

И результат:

```csharp
public sealed record SemanticIntentClassification(
    SemanticIntentKind Kind,
    ExecutionContour Contour,
    IntentEvidence Evidence,
    bool IsExecutableIntent,
    bool RequiresDescriptor,
    bool RequiresRuntimeToken,
    bool MayPublishMemory,
    bool MayWriteArchitecturalRegister);
```

Ключевой момент: `SemanticIntentKind.MatrixTile` не должен автоматически означать «можно lower-ить в VectorALU». `SemanticIntentKind.ExternalAcceleratorCommand` не должен автоматически означать «можно исполнять через L7». `SemanticIntentKind.VmxCompatibilityProjection` не должен означать VMX backend execution.

---

### 3.3. Contour API

Нужен отдельный тип `ExecutionContour`, потому что в HybridCPU именно контур является смысловым владельцем исполнения.

```csharp
public enum ExecutionContourKind
{
    NativeVliwScalar,
    NativeVliwLsu,
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

Каждый contour должен иметь описание полномочий:

```csharp
public sealed record ExecutionContourAuthority(
    ExecutionContourKind Kind,
    bool CompilerMayEmitCarrier,
    bool CompilerMayEmitDescriptor,
    bool CompilerMayEmitTypedSlotFacts,
    bool RuntimeLegalityRequired,
    bool RuntimeTokenRequired,
    bool CommitRequiredForPublication,
    bool RetireRequiredForRegisterWriteback,
    bool ProductionLoweringAllowed);
```

Для текущего состояния проекта это примерно так:

| Контур | Compiler carrier | Descriptor | Production lowering | Runtime authority |
|---|---:|---:|---:|---:|
| Native VLIW scalar/LSU/branch | да | обычно нет | ограниченно да | обязательно |
| Vector / Stream | да, в рамках реализованного пути | sideband по необходимости | только реализованный stream/vector | обязательно |
| MatrixTile | helper-only / scoped | да, если есть ABI | не как общий backend | обязательно |
| DSC lane6 | да, только DSC1 Phase 06 | `DmaStreamComputeDescriptor` | только текущий DSC1 contour | обязательно |
| L7-SDC lane7 | да, только scoped `ACCEL_*` | `AcceleratorCommandDescriptor` | только Phase 08 / 08A | обязательно |
| VMX | projection-only | да, как compatibility evidence | нет | runtime deny/projection |
| SecureCompute | policy/admission-only | да | нет production secure backend | runtime policy/admission |

Это соответствует текущей документации: DSC descriptors проходят через `InstructionSlotMetadata.DmaStreamComputeDescriptor`, L7 descriptors — через `InstructionSlotMetadata.AcceleratorCommandDescriptor`, а `DecodedBundleTransportProjector` может создать lane6/lane7 carrier только при наличии чистого sideband; сама projection не является исполнением. ([GitHub](https://github.com/yuriyyak23/HybridCPU-v2))

---

### 3.4. Lowering Decision API

Самое важное расширение: **Compiler API должен возвращать не только success/fail, а typed lowering decision**.

```csharp
public abstract record CompilerLoweringDecision;

public sealed record EmitCarrierDecision(
    ExecutionContourKind Contour,
    VliwCarrierImage Carrier,
    CompilerSidebandEnvelope Sideband,
    TypedSlotBundleFacts? Facts,
    DescriptorEnvelope? Descriptor,
    LoweringEvidence Evidence) : CompilerLoweringDecision;

public sealed record RejectAtCompileTimeDecision(
    ExecutionContourKind RequestedContour,
    CompilerRejectReason Reason,
    NoFallbackProof NoFallback) : CompilerLoweringDecision;

public sealed record ParserOnlyDecision(
    DescriptorEnvelope Descriptor,
    string Reason) : CompilerLoweringDecision;

public sealed record HelperOnlyDecision(
    DescriptorEnvelope Descriptor,
    string HelperSurface,
    string NonProductionBoundary) : CompilerLoweringDecision;

public sealed record FutureGatedDecision(
    ExecutionContourKind Contour,
    IReadOnlyList<ConformanceGate> MissingGates) : CompilerLoweringDecision;

public sealed record NoEmissionDecision(
    string Reason) : CompilerLoweringDecision;
```

Это принципиально лучше, чем:

```csharp
bool TryLower(...)
```

Потому что `TryLower` провоцирует скрытые fallback-пути:

```text
MatrixTile unsupported -> VectorALU
L7 rejected -> DSC
DSC rejected -> StreamEngine
VMX recognized -> VMX execution
SecureCompute admitted -> secure backend execution
```

Для HybridCPU нужен результат, который сохраняет причину отказа и доказывает отсутствие fallback:

```csharp
public sealed record NoFallbackProof(
    SemanticIntentKind OriginalIntent,
    ExecutionContourKind RequestedContour,
    IReadOnlyList<ExecutionContourKind> ForbiddenFallbacks,
    string Explanation);
```

---

## 4. Compiler API должен быть contour-aware

Сейчас главный архитектурный риск — плоский backend API:

```csharp
CompileInstruction(...)
CompileBundle(...)
EmitOpcode(...)
LowerOperation(...)
```

Такой API недостаточен. Он не несёт сведений о контуре исполнения.

Нужен API вида:

```csharp
public interface IContourLoweringProvider
{
    ExecutionContourKind Contour { get; }

    CompilerLoweringCapability Capability { get; }

    CompilerLoweringDecision TryLower(
        SemanticIntent intent,
        CompilerLoweringContext context);
}
```

И отдельные реализации:

```csharp
ScalarVliwLoweringProvider
StreamVectorLoweringProvider
MatrixTileLoweringProvider
DmaStreamComputeLoweringProvider
L7SdcLoweringProvider
VmxProjectionLoweringProvider
SecureComputeAdmissionLoweringProvider
```

Но эти providers не должны иметь равные полномочия.

Например:

```text
MatrixTileLoweringProvider
  может вернуть HelperOnlyDecision,
  может вернуть FutureGatedDecision,
  но не должен silently return VectorALU lowering.

VmxProjectionLoweringProvider
  может вернуть compatibility projection,
  но не VMX backend execution.

SecureComputeAdmissionLoweringProvider
  может построить policy/admission descriptor,
  но не secure execution package.
```

---

## 5. Typed-slot API: facts, agreement, не authority

Текущий `TypedSlotBundleFacts` должен быть выставлен в Compiler API как отдельный продукт, а не как «часть инструкции».

```csharp
public sealed record CompilerTypedSlotFactsProduct(
    TypedSlotBundleFacts Facts,
    IrAdmissibilityAgreement Agreement,
    CompilerTypedSlotPolicy Policy,
    TypedSlotFactStaging Staging);
```

API должен явно различать:

```text
FactsGenerated
FactsAcceptedByBridge
FactsAgreementChecked
RuntimeLegalityAdmitted
LaneMaterialized
Retired
```

Это разные состояния.

Текущий README фиксирует, что typed-slot facts могут передаваться и проверяться, но отсутствующие/default facts остаются совместимыми с canonical runtime execution; runtime legality остаётся final authority; stronger stages вроде `RequiredForAdmission` — будущая лексика, не текущий runtime behavior. ([GitHub](https://github.com/yuriyyak23/HybridCPU-v2))

Поэтому API не должен иметь метод:

```csharp
bool IsRuntimeLegal(TypedSlotBundleFacts facts);
```

Нужен другой метод:

```csharp
TypedSlotAgreementReport VerifyStructuralAgreement(
    VliwCarrierImage carrier,
    TypedSlotBundleFacts facts);
```

Семантика: agreement report — evidence/quarantine/diagnostic surface, не authority.

---

## 6. Bundle API: carrier отдельно от sideband

Носитель должен быть неизменяемой 256-byte сущностью:

```csharp
public sealed record VliwCarrierImage(
    byte[] Bundle256,
    IReadOnlyList<VliwSlotImage> Slots);
```

Sideband должен быть отдельным:

```csharp
public sealed record CompilerSidebandEnvelope(
    IReadOnlyList<InstructionSlotMetadata> SlotMetadata,
    BundleMetadata? BundleMetadata,
    ReplayAnchor? ReplayAnchor,
    CompilerSourceMap? SourceMap);
```

Запрещённый дизайн:

```csharp
public sealed class CompiledBundle
{
    public VLIW_Bundle Bundle;
    public bool IsExecutable;
    public bool IsLegal;
    public bool WillCommit;
}
```

Правильный дизайн:

```csharp
public sealed class CompilerEmissionPackage
{
    public VliwCarrierImage Carrier { get; init; }
    public CompilerSidebandEnvelope Sideband { get; init; }
    public DescriptorEnvelope? Descriptor { get; init; }
    public CompilerTypedSlotFactsProduct? TypedSlotFacts { get; init; }
    public CompilerLoweringDecision Decision { get; init; }
    public ConformanceGateReport GateReport { get; init; }
}
```

---

## 7. Descriptor ABI API

Нужно создать отдельный API для descriptor ABI, но с жёсткой семантикой:

```csharp
public interface IDescriptorAbiBuilder<TIntent, TDescriptor>
{
    DescriptorBuildResult<TDescriptor> BuildDescriptor(
        TIntent intent,
        DescriptorAbiContext context);
}
```

Результат:

```csharp
public sealed record DescriptorBuildResult<TDescriptor>(
    TDescriptor? Descriptor,
    DescriptorAbiStatus Status,
    DescriptorEvidence Evidence,
    IReadOnlyList<DescriptorAbiViolation> Violations);
```

`DescriptorAbiStatus`:

```csharp
ValidTransportDescriptor
ParserOnlyDescriptor
HelperOnlyDescriptor
RejectedDescriptor
FutureGatedDescriptor
```

Не должно быть статуса:

```text
ExecutableDescriptor
```

Потому что descriptor не исполняет сам себя.

Для L7-SDC это особенно важно: текущая документация говорит, что `ACCEL_SUBMIT` требует typed, guard-accepted `AcceleratorCommandDescriptor`; descriptorless submit fail-closed; token handles, telemetry, descriptor hashes, status words и backend APIs являются evidence/helper surfaces внутри scoped contour, не универсальной authority. ([GitHub](https://github.com/yuriyyak23/HybridCPU-v2))

---

## 8. Capability API и conformance gates

`CompilerBackendLoweringCapability` и `CompilerLoweringCapabilityState` следует сделать центральными для API.

```csharp
public sealed record CompilerLoweringCapability(
    ExecutionContourKind Contour,
    CompilerLoweringCapabilityState State,
    IReadOnlyList<ConformanceGate> RequiredGates,
    IReadOnlyList<ConformanceGate> SatisfiedGates,
    IReadOnlyList<ConformanceGate> MissingGates);
```

Где `CompilerLoweringCapabilityState`:

```csharp
NoEmission
ParserOnly
HelperOnly
ValidationOnly
ScopedRuntimeContour
ProductionBlocked
ProductionAllowed
FutureGated
```

Для текущей архитектуры:

```text
DSC1 Phase 06 -> ScopedRuntimeContour, not broad production lowering
L7 Phase 08 / 08A -> ScopedRuntimeContour, not universal accelerator protocol
VMX -> ProjectionOnly / NoEmission for backend execution
SecureCompute -> PolicyAdmissionOnly / NoEmission for secure backend execution
MatrixTile -> HelperOnly or scoped, not universal lowering
DSC2 -> ParserOnly / FutureGated
```

---

## 9. No-fallback policy в API

Нужен обязательный объект:

```csharp
public sealed record CompilerNoFallbackPolicy(
    bool ForbidCrossContourFallback,
    bool ForbidRuntimeRejectionFallback,
    bool ForbidDescriptorToHelperFallback,
    bool ForbidMatrixTileToVectorFallback,
    bool ForbidL7ToDscFallback,
    bool ForbidVmxToNativeExecutionFallback,
    bool ForbidSecureComputeToPlainExecutionFallback);
```

И каждый `CompilerLoweringDecision` должен содержать:

```csharp
NoFallbackProof
```

Это нужно не для красоты, а для предотвращения самой опасной ошибки: когда API ради удобства пользователя «всё же скомпилирует как-нибудь».

Для HybridCPU правильный отказ — это полноценный результат компиляции:

```text
Rejected at compiler boundary
because requested contour is unsupported
and fallback is architecturally forbidden.
```

---

## 10. Runtime bridge API

`ProcessorCompilerBridge.AcceptTypedSlotFacts` и родственные bridge-поверхности должны быть представлены как **ingress**, а не как runtime approval.

API-слой можно оформить так:

```csharp
public interface ICompilerRuntimeBridge
{
    BridgeAcceptanceReport DeclareCompilerContractVersion(int version);

    BridgeAcceptanceReport AcceptSideband(
        VliwCarrierImage carrier,
        CompilerSidebandEnvelope sideband);

    BridgeAcceptanceReport AcceptTypedSlotFacts(
        VliwCarrierImage carrier,
        TypedSlotBundleFacts facts,
        IrAdmissibilityAgreement agreement);
}
```

Но имена результатов должны быть осторожными:

```text
BridgeAccepted
BridgeRejected
AgreementFailure
Quarantined
VersionRejected
```

Не:

```text
RuntimeLegal
ExecutionReady
Admitted
Committed
```

Потому что runtime admission происходит позже, через scheduler / legality service / token / commit-retire path.

---

## 11. API для runtime preview: только negative-safe

Можно добавить «preflight» API, но он не должен имитировать runtime.

```csharp
public interface ICompilerPreflightService
{
    CompilerPreflightReport CheckStructuralAdmissibility(
        CompilerEmissionPackage package);
}
```

Разрешённая семантика:

```text
если preflight отверг — компилятор не должен emit;
если preflight принял — это не гарантия runtime admission.
```

Это совпадает с текущей логикой: `StructurallyAdmissible` означает, что compiler preflight прошёл, но не обещает отсутствие runtime dynamic rejects; dynamic class exhaustion, scoreboard, bank pressure, speculation budget и assist backpressure остаются runtime-state outcomes. ([GitHub](https://github.com/yuriyyak23/HybridCPU-v2))

---

## 12. API для evidence

Нужно формализовать evidence как отдельный продукт:

```csharp
public sealed record CompilerEvidenceEnvelope(
    CompilerContractEvidence Contract,
    StructuralEvidence Structural,
    SidebandPreservationEvidence Sideband,
    DescriptorEvidence? Descriptor,
    TypedSlotAgreementEvidence? TypedSlot,
    LoweringGateEvidence Lowering,
    NoFallbackProof NoFallback);
```

Но `EvidenceEnvelope` не должен содержать:

```csharp
bool IsAuthoritative
bool IsExecuted
bool IsCommitted
```

Evidence объясняет, диагностирует, помогает replay/audit, но не становится authority. README прямо разделяет evidence и authority: telemetry, token, capability, certificate и descriptor evidence могут быть observation/binding input, но не закрывают executable gate сами по себе. ([GitHub](https://github.com/yuriyyak23/HybridCPU-v2))

---

## 13. Предлагаемая структура namespace

Я бы разнёс Compiler API так:

```text
HybridCPU_Compiler.API
  Contract
    CompilerContractView
    CompilerApiVersion
    CompilerCompatibilityProfile

  Sessions
    HybridCpuCompilerSession
    CompilerSessionOptions
    CompilerTargetProfile

  Intent
    SemanticIntent
    SemanticIntentClassification
    SemanticIntentKind

  Contours
    ExecutionContour
    ExecutionContourKind
    ExecutionContourAuthority
    ContourLoweringProvider

  Lowering
    CompilerLoweringDecision
    CompilerLoweringCapability
    CompilerLoweringCapabilityState
    CompilerBackendLoweringContract
    CompilerNoFallbackPolicy

  Carrier
    VliwCarrierImage
    VliwSlotImage
    BundleEmissionResult

  Sideband
    CompilerSidebandEnvelope
    InstructionSlotMetadata
    BundleSidebandMetadata

  Descriptors
    DescriptorEnvelope
    DmaStreamComputeDescriptorProduct
    AcceleratorCommandDescriptorProduct
    MatrixTileDescriptorProduct
    SecureComputeDescriptorProduct
    VmxProjectionDescriptorProduct

  TypedSlots
    CompilerTypedSlotFactsProduct
    TypedSlotBundleFacts
    IrAdmissibilityAgreement
    TypedSlotAgreementReport

  Bridge
    ICompilerRuntimeBridge
    BridgeAcceptanceReport

  Evidence
    CompilerEvidenceEnvelope
    ConformanceGateReport
    NoFallbackProof
```

Главная идея: API-структура должна физически мешать разработчику спутать carrier, sideband, descriptor и authority.

---

## 14. Специальные требования по контурам

### MatrixTile

Compiler API должен разрешать:

```text
MatrixTile intent classification
MatrixTile descriptor construction
MatrixTile helper-only result
MatrixTile compile-time rejection
```

Но запрещать:

```text
MatrixTile unsupported -> VectorALU fallback
MatrixTile unsupported -> DSC fallback
MatrixTile helper success -> production lowering claim
```

API-результат:

```csharp
HelperOnlyDecision(
    Contour: MatrixTileHelperOnly,
    NonProductionBoundary: "MatrixTile helper ABI is not general production lowering")
```

---

### DSC lane6

Compiler API может emit DSC carrier только при выполнении capability gates:

```text
Descriptor ABI valid
lane6 DmaStreamClass hard-pinned
DSC1 Phase 06 supported
no DSC2 expansion
no StreamEngine fallback
no Vector fallback
no L7 fallback
```

При unsupported footprint:

```csharp
FutureGatedDecision(DmaStreamComputeLane6, MissingGates: ...)
```

или

```csharp
RejectAtCompileTimeDecision(..., NoFallbackProof)
```

---

### L7-SDC lane7

Compiler API должен разделять:

```text
ACCEL_QUERY_CAPS
ACCEL_SUBMIT
ACCEL_POLL
ACCEL_WAIT
ACCEL_CANCEL
ACCEL_FENCE
ACCEL_STATUS
```

и не должен считать `ACCEL_SUBMIT` исполнением.

Для `ACCEL_SUBMIT` API обязан требовать:

```text
clean carrier
sideband descriptor
typed descriptor ABI
owner/domain binding evidence
normalized footprint evidence
no descriptorless submit
```

Но runtime guard, token lifecycle, backend staging, commit и conditional `rd` writeback остаются runtime-owned.

---

### VMX

Compiler API должен иметь только:

```text
VmxProjectionOnly
VmxNoEmission
VmxCompatibilityEvidence
```

Не должно быть:

```text
VmxBackendExecutionDecision
```

VMX opcode recognition — это parser/projection compatibility, не разрешение VMX backend execution.

---

### SecureCompute

Compiler API может строить:

```text
SecureComputeDomainDescriptor
policy/admission package
measurement/evidence envelope
```

Но не:

```text
secure backend executable package
secure retire package
nested secure execution package
```

`SecureCompute policy admitted` не должно становиться `secure backend execution allowed`.

---

## 15. Минимальный набор новых публичных API

Для ближайшего этапа я бы добавил не весь большой слой сразу, а шесть обязательных типов.

### 15.1. `CompilerEmissionPackage`

Центральный результат компилятора.

```csharp
public sealed record CompilerEmissionPackage(
    VliwCarrierImage Carrier,
    CompilerSidebandEnvelope Sideband,
    CompilerLoweringDecision LoweringDecision,
    CompilerTypedSlotFactsProduct? TypedSlotFacts,
    DescriptorEnvelope? Descriptor,
    CompilerEvidenceEnvelope Evidence);
```

### 15.2. `ExecutionContourKind`

Без него API останется opcode-centric.

### 15.3. `CompilerLoweringDecision`

Вместо `bool TryLower`.

### 15.4. `CompilerLoweringCapability`

Чтобы не размазывать future-gated/parser-only/helper-only состояния по исключениям и комментариям.

### 15.5. `CompilerNoFallbackPolicy`

Чтобы архитектурный запрет fallback был исполнимым API-контрактом.

### 15.6. `ConformanceGateReport`

Чтобы production lowering нельзя было объявить «по факту наличия helper/descriptor/parser».

---

## 16. Проверочные инварианты для тестов Compiler API

Нужно добавить тесты не только на успешный emit, но и на отказ.

Обязательные negative tests:

```text
MatrixTile unsupported does not lower to VectorALU
DSC2 descriptor parses but does not emit executable DSC carrier
L7 descriptorless ACCEL_SUBMIT is compile-time or bridge rejection
L7 rejected submit does not fallback to lane6 DSC
VMX opcode recognized does not create VMX execution package
SecureCompute descriptor admitted does not create secure backend execution package
TypedSlot facts mismatch produces agreement/quarantine evidence, not runtime authority
Missing typed-slot facts remain compatible under CompatibilityValidation
CompilerContract stale version is rejected fail-closed
```

Текущая документация уже поддерживает такую дисциплину: stale producer version rejected, sideband descriptor emission is not production lowering, and canonical opcodes must execute correctly with missing/default compiler metadata. ([GitHub](https://github.com/yuriyyak23/HybridCPU-v2))

---

## 17. Итоговая архитектурная формула

Compiler API HybridCPU-v2 должен быть не «API генерации инструкций», а **API ответственного разграничения полномочий**.

Его правильная формула:

```text
Compiler API =
  semantic intent classifier
  + contour-aware lowering selector
  + no-fallback decision engine
  + carrier emitter
  + sideband / descriptor ABI builder
  + typed-slot facts producer
  + structural agreement reporter
  + conformance gate reporter
  + evidence packager

Compiler API ≠
  runtime legality
  token authority
  memory publication
  register retire
  backend execution proof
  SecureCompute execution admission
  VMX execution admission
  production lowering proof by sideband preservation
```

Главное изменение, которое я бы внёс в проект: **сделать `CompilerLoweringDecision` и `ExecutionContourKind` центральными публичными сущностями Compiler API**. После этого все остальные API — descriptor builders, typed-slot facts, bridge ingress, conformance gates — естественно встанут вокруг них и перестанут подменять authority.