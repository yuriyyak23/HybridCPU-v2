# Phase 03 — IR Intent and Execution Contour Classification

## Назначение

Отделить смысл операции от способа ее переноса в runtime. IR должен сначала получить semantic intent, затем execution contour, и только потом lowering decision. Это предотвращает смешение scalar VLIW, Stream, MatrixTile, DSC, L7-SDC, VMX projection и SecureCompute admission.

## Целевой pipeline

```text
IR node / helper call / frontend directive
  -> CompilerSemanticIntent
  -> CompilerExecutionContourSelection
  -> CompilerLoweringDecision
  -> CompilerEmissionPackage
```

`Semantic intent` и `execution contour` must remain separate objects. Intent describes what the operation means. Contour describes which scoped transport/lowering route is allowed. Combining them in one record invites hidden fallback and authority leakage.

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
    NativeVliwBranchControl,
    StreamEngineVector,
    MatrixTileHelperOnly,
    DmaStreamComputeLane6,
    L7SdcLane7,
    VmxProjectionOnly,
    SecureComputePolicyAdmissionOnly,
    ParserOnly,
    NoEmission,
    FutureGated,
    UnknownRejected
}
```

### `CompilerSemanticIntent`

```csharp
public sealed record CompilerSemanticIntent(
    SemanticIntentKind Kind,
    string OpcodeFamily,
    bool RequiresDescriptor,
    bool RequiresSideband,
    bool RequiresToken,
    bool RequiresRuntimeLegality,
    bool IsCompatibilityProjection,
    bool IsPolicyAdmissionOnly,
    bool IsHelperAbiOnly,
    bool IsParserOnly,
    string Reason);
```

This record must not contain `PreferredContour`, `MayPublishMemory`, `MayPublishRegisterState`, `CompilerMayEmitCarrier` or `CompilerMayEmitDescriptor`. Those are contour/lowering/output questions, not semantic intent.

### `CompilerExecutionContourSelection`

```csharp
public sealed record CompilerExecutionContourSelection(
    ExecutionContourKind Contour,
    bool IsKnownContour,
    bool IsProviderAvailable,
    bool IsEmissionForbidden,
    bool RequiresSideband,
    bool RequiresDescriptor,
    bool RequiresRuntimeLegalityA,
    bool RequiresRuntimeLegalityB,
    SidebandRequirement SidebandRequirement,
    CompilerRuntimeAuthorityDependency RuntimeDependency,
    string SelectionReason,
    IReadOnlyList<string> MissingInputs);
```

Contour selection is not a lowering decision. It must not create carrier, sideband, descriptor, typed-slot facts or evidence other than a selection diagnostic.

### Compatibility note for old `SemanticIntentClassification`

The old combined shape:

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

is deprecated for the refactor plan. If used as a temporary adapter, it must be internal-only and must be translated into `CompilerSemanticIntent` + `CompilerExecutionContourSelection` before any public decision object is emitted.

## Mapping rules

### Scalar / LoadStore / Branch

- `ScalarAlu` maps to `NativeVliwScalar`.
- `LoadStore` maps to `NativeVliwLoadStore`.
- `BranchControl` maps to `NativeVliwBranchControl`.
- These contours may produce native VLIW carrier emission.
- They require runtime Legality A/B.
- They do not require descriptor by default.
- Hazard/resource/slot success is structural evidence only.
- Branch/control-flow lane placement cannot be described as publication, commit or retire.

### VectorStream

- May lead to `StreamEngineVector` only if current target/profile explicitly supports the scoped path.
- Vector helper/recovery success is helper/transport recognition, not broad production vector compiler success.
- Unsupported vector shape/dtype/predicate/stride must produce reject/no-emission/future-gated decision, not scalar fallback.
- Evidence about contour selection is required.

### MatrixTile

- Current допустимый смысл: helper-only ABI для ограниченного набора MatrixTile операций.
- Supported helper ops return `HelperOnlyDecision` or carrier decision with `HelperAbiOnly` production status.
- Unsupported dtype/shape/layout/accumulator policy must fail closed.
- Нельзя превращать MatrixTile helper success в general matrix compiler, GEMM lowering or production lowering.
- Rejected MatrixTile must not fallback to scalar/vector/Stream.

### DmaStreamCompute

- Leads to lane6 DSC only for current scoped DSC contour.
- DSC2/parser-only must remain `ParserOnly` or `FutureGated`.
- Descriptor success is transport ABI evidence only.
- Missing commit/publication gate prevents any compiler publication claim.
- Rejected DSC must not fallback to L7, Stream or scalar.

### ExternalAcceleratorCommand / L7-SDC

- Leads to lane7 L7-SDC only with descriptor sideband.
- `ACCEL_SUBMIT` without descriptor sideband must be `UnknownRejected`/typed reject, not empty annotation fallback.
- Descriptorless submit запрещен.
- Token/capability observation is evidence only.
- Rejected submit has no fallback to DSC/Stream/scalar.

### VmxCompatibilityProjection

- Only projection/ABI vocabulary/no-emission.
- No VMX backend emission.
- VMCS is not state owner.
- VmxCaps is not capability authority.

### SecureComputeAdmission

- Only policy/admission/evidence.
- No secure backend execution claim.
- No nested/retire/publication claim from compiler.
- Host-owned evidence must never enter guest/domain architectural state.

### ParserOnly / NoEmission / FutureGated / UnknownRejected

- `ParserOnly`: parser recognized syntax or descriptor shape, but no emission or production lowering is allowed.
- `NoEmission`: emission is architecturally forbidden, e.g. VMX projection or SecureCompute policy/admission.
- `FutureGated`: a concept exists but required gates are absent.
- `UnknownRejected`: contour cannot be selected safely; default scalar fallback is forbidden.

## Tasks

### 1. Ввести intent classifier

Добавить слой, который принимает текущий IR/helper/directive и возвращает `CompilerSemanticIntent` без emission.

### 2. Ввести contour selector

Контур выбирается отдельно от intent. Один intent может иметь несколько теоретических контуров, but current capability/observation matrix must choose exactly one result:

- supported scoped contour;
- parser-only;
- helper-only;
- no-emission;
- future-gated;
- compile-time reject.

The selector must output `CompilerExecutionContourSelection`, not `CompilerLoweringDecision`.

### 3. Запретить implicit fallback

Если contour выбран и отвергнут, fallback возможен только при явном `FallbackPolicy`, который по умолчанию должен быть `Forbidden`.

Cross-contour fallback is forbidden unless explicitly represented as a new semantic decision and reviewed in Phase 04/07. Same-contour structural placement retry may be allowed only when it does not change:

```text
semantic intent
execution contour
sideband requirement
descriptor requirement
emission class
authority class
runtime dependency
```

### 4. Привязать telemetry

Каждая classification должна писать audit-friendly reason:

```text
intent=MatrixTile contour=MatrixTileHelperOnly reason=unsupported accumulator policy -> rejected
intent=L7Sdc contour=L7SdcLane7 reason=missing descriptor -> rejected
intent=VMX contour=VmxProjectionOnly reason=projection-only -> no emission
```

Telemetry must include both `intent.kind` and `contour.kind`; one field is insufficient.

## Deliverables

- `SemanticIntentKind`.
- `ExecutionContourKind` with `NativeVliwBranchControl` and `UnknownRejected`.
- `CompilerSemanticIntent`.
- `CompilerExecutionContourSelection`.
- Deprecated adapter note for old `SemanticIntentClassification`.
- `ICompilerIntentClassifier`.
- `IExecutionContourSelector`.
- Negative tests на запрет смешения контуров.

Recommended namespace layout:

```text
HybridCPU.Compiler.Core.IR.Intent
HybridCPU.Compiler.Core.IR.Contours
```

## Acceptance criteria

Фаза завершена, если каждый путь lowering сначала проходит через intent/contour classification и в логах можно увидеть, почему операция попала именно в этот contour.

Additional acceptance checks:

```text
[ ] No public API exposes combined SemanticIntentClassification as the final classification result.
[ ] Intent classifier does not emit carrier/sideband/descriptor/facts.
[ ] Contour selector does not lower or emit.
[ ] Unknown contour -> UnknownRejected/FutureGated, never scalar fallback.
[ ] Scalar/load-store/branch are distinct native contours.
[ ] VMX and SecureCompute classifications always lead to NoEmission/projection/admission-only decisions.
```

## Non-goals

- Не писать production lowering.
- Не менять runtime contour implementation.
- Не добавлять новые ISA semantics.
- Не расширять MatrixTile за пределы текущего helper ABI.
- Не превращать L7-SDC в универсальный accelerator compiler.

## Риски

Главный риск — попытаться упростить классификацию до `Opcode -> Lane`. Для HybridCPU этого недостаточно: один и тот же carrier slot не объясняет authority, publication, descriptor/token lifecycle и runtime legality. Поэтому intent и contour должны быть отдельными сущностями.

Второй риск — оставить helper recovery paths (`TryRecoverFromInstruction`) как de facto lowering decisions. Recovery success must feed intent/contour classification and Phase 04 decisions; it must not be consumed directly as production lowering.
