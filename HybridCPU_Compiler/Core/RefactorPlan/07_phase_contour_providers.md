# Phase 07 — Contour Lowering Providers

## Назначение

Разложить lowering logic по contour-specific providers. Цель — убрать универсальные helpers, которые скрыто смешивают Scalar VLIW, Stream, MatrixTile, DSC, L7-SDC, VMX и SecureCompute.

Provider registry must be a responsibility router, not a fallback router. A rejected contour is a result, not an invitation to try another contour.

## Split analyzer/provider model

Analysis and lowering must be separate roles. Analysis may inspect intent, target profile and current support gates; lowering may produce a decision only after analysis has produced a typed report.

### `IContourAnalyzer`

```csharp
public interface IContourAnalyzer
{
    ExecutionContourKind ContourKind { get; }
    ContourAnalysisReport Analyze(CompilerSemanticIntent intent, CompilerLoweringContext context);
}
```

### `IContourLoweringProvider`

```csharp
public interface IContourLoweringProvider
{
    ExecutionContourKind ContourKind { get; }
    CompilerCapabilityObservation ObserveCapability(CompilerTargetProfile target);
    CompilerLoweringDecision Lower(
        CompilerSemanticIntent intent,
        ContourAnalysisReport analysis,
        CompilerLoweringContext context);
}
```

`Analyze` must not emit carrier. `Lower` must not bypass `Analyze`. A provider must reject if it receives an intent/analysis pair for another contour.

## Capability observation model

Use `CompilerCapabilityObservation`, not `CompilerLoweringCapability`, unless authority source and runtime dependency are explicit. Capability-like data is evidence/observation by default.

```csharp
public enum CompilerCapabilityObservationState
{
    NoEmission,
    ParserOnly,
    HelperOnly,
    ValidationOnly,
    ScopedRuntimeContour,
    ProductionBlocked,
    ProductionCandidateRequiresRuntimeLegality,
    ProductionAllowedByExplicitCompilerGate,
    FutureGated
}

public sealed record CompilerCapabilityObservation(
    ExecutionContourKind ContourKind,
    CompilerCapabilityObservationState State,
    CompilerAuthoritySourceKind AuthoritySourceKind,
    CompilerRuntimeAuthorityDependency RuntimeAuthorityDependency,
    IReadOnlyList<string> RequiredGates,
    IReadOnlyList<string> MissingGates,
    string Reason);
```

`ProductionAllowedByExplicitCompilerGate` must be rare and requires explicit conformance gates plus negative tests. For current disputed contours, safe states are `HelperOnly`, `ParserOnly`, `ScopedRuntimeContour`, `ProductionBlocked`, `NoEmission` or `FutureGated`.

## `ContourAnalysisReport`

```csharp
public sealed record ContourAnalysisReport(
    ExecutionContourKind ContourKind,
    CompilerSemanticIntent Intent,
    CompilerCapabilityObservation CapabilityObservation,
    bool ProviderAvailable,
    bool RequiredSidebandPresent,
    bool RequiredDescriptorPresent,
    bool RuntimeLegalityRequired,
    IReadOnlyList<CompilerRejectReason> RejectReasons,
    CompilerEvidenceEnvelope Evidence,
    string Reason);
```

Analysis success is not lowering success. It is only a typed report consumed by Phase 04 lowering decisions.

## Providers

### `ScalarVliwLoweringProvider`

Ответственность:

- native VLIW scalar carrier construction;
- bundle shape preservation;
- optional sideband and typed-slot facts production where available;
- no runtime legality claim.

Запреты:

- не принимать Stream/MatrixTile/DSC/L7 intent через scalar fallback;
- не скрывать hazards как runtime legality;
- no publication/commit/retire claim.

### `LoadStoreVliwLoweringProvider`

Ответственность:

- native VLIW load/store carrier construction;
- explicit memory region evidence as compiler structural/effect evidence only;
- runtime Legality A/B dependency.

Запреты:

- no memory publication claim;
- no descriptor authority;
- no commit/retire claim.

### `BranchControlVliwLoweringProvider`

Ответственность:

- native branch/control-flow carrier construction;
- branch target evidence and structural lane placement facts;
- runtime Legality A/B dependency.

Запреты:

- no publication/commit/retire claim;
- no VMX projection fallback.

### `StreamVectorLoweringProvider`

Ответственность:

- только scoped StreamEngine path, если он разрешен target profile;
- явный fail-closed при unsupported vector shape/dtype/predicate/stride;
- evidence о contour selection.

Запреты:

- no hidden scalar fallback;
- no claim of broad vector compiler;
- helper/transport recovery success is not production lowering.

### `MatrixTileLoweringProvider`

Ответственность:

- helper-only ABI для текущих поддержанных MatrixTile операций;
- явная проверка operation/dtype/shape/layout/accumulator policy;
- typed `HelperOnlyDecision` или `RejectAtCompileTimeDecision`.

Запреты:

- no general matrix compiler;
- no GEMM/LLM/FP4 claim unless separate gates are implemented;
- no scalar/stream fallback after MatrixTile rejection;
- helper success is not production lowering.

### `DmaStreamComputeLoweringProvider`

Ответственность:

- lane6 DSC scoped contour;
- DSC1 descriptor ABI construction when supported;
- parser-only/future-gated distinction for unsupported DSC phases;
- explicit commit/publication requirements as runtime-owned.

Запреты:

- no DSC2 executable claim if only parser exists;
- no memory publication claim from compiler;
- no fallback to L7/Stream/scalar;
- descriptor success is not authority.

### `L7SdcLoweringProvider`

Ответственность:

- lane7 SDC command handling;
- descriptor sideband requirement;
- token lifecycle evidence as evidence only;
- explicit `L7DescriptorMissing` / `L7DescriptorlessSubmitForbidden` rejection.

Запреты:

- no descriptorless submit;
- no universal accelerator compiler;
- no fallback to DSC after rejected submit;
- capability observation is not authority.

### `VmxProjectionLoweringProvider`

Ответственность:

- compatibility vocabulary/projection only;
- `NoEmissionDecision` for backend emission attempts;
- evidence that VMX is not source of truth.

Запреты:

- no VMX backend;
- no VMCS state ownership;
- no VmxCaps authority;
- no carrier emission.

### `SecureComputeAdmissionLoweringProvider`

Ответственность:

- policy/admission/evidence carrier only;
- `NoEmissionDecision` for secure backend execution attempts;
- explicit separation of admission and execution.

Запреты:

- no secure backend execution;
- no nested domain execution claim;
- no retire/publication claim;
- host-owned evidence cannot enter guest/domain architectural state.

## Provider registry

```csharp
public interface IContourLoweringProviderRegistry
{
    IContourAnalyzer ResolveAnalyzer(ExecutionContourKind contourKind);
    IContourLoweringProvider ResolveProvider(ExecutionContourKind contourKind);
    IReadOnlyList<CompilerCapabilityObservation> ObserveAll(CompilerTargetProfile target);
}
```

Registry must fail closed on unknown contour.

```text
Unknown contour -> UnknownRejected/FutureGated, no scalar fallback.
Provider failure -> reject unless explicit same-contour retry policy is present and recorded.
Same-contour placement search fallback is allowed only if it does not change semantic intent, contour, sideband requirement, descriptor requirement, emission class, authority class or runtime dependency.
```

## Tasks

### 1. Create provider shells

Добавить пустые provider shells, которые сначала вызывают legacy code через adapters. Shells must return typed decisions, not raw helper results.

### 2. Move decision ownership into providers

Каждый provider должен возвращать `CompilerLoweringDecision`, а не private helper-specific result.

### 3. Add contour-specific negative tests

Минимум:

- MatrixTile unsupported dtype -> reject, no fallback.
- MatrixTile helper success -> helper ABI only, no production lowering.
- Stream/vector unsupported shape -> reject/no-emission, no scalar fallback.
- DSC2 parser-only -> no executable carrier.
- DSC rejected -> no L7/Stream/scalar fallback.
- L7 submit without descriptor -> reject.
- L7 rejected submit -> no DSC fallback.
- VMX operation -> projection/no-emission.
- SecureCompute admission -> no secure backend execution.
- Unknown contour -> unknown rejected/future gated, no scalar fallback.

### 4. Deprecate universal lowering helpers

Все helpers, которые принимают широкий generic intent и сами выбирают путь, должны быть помечены как legacy или превращены в dispatcher over providers.

### 5. Add provider anti-fallback audit

For every provider rejection, evidence must record:

```text
provider.kind
intent.kind
contour.kind
reject.reason
fallback.policy
fallback.proof_id
prohibited_fallbacks
```

## Deliverables

- `IContourAnalyzer`.
- `IContourLoweringProvider`.
- `CompilerCapabilityObservation`.
- `ContourAnalysisReport`.
- Provider registry.
- Provider shells for each contour.
- Legacy adapters.
- Contour-specific negative tests.
- Anti-fallback evidence snapshots.

Recommended namespace layout:

```text
HybridCPU.Compiler.Core.IR.Contours
HybridCPU.Compiler.Core.IR.Lowering
```

## Acceptance criteria

Фаза завершена, если ни один contour не может быть выбран неявно внутри generic helper без audit trail, а каждый отказ возвращает typed decision с reason и no-fallback proof.

Machine-checkable gates:

```text
[ ] Analyzer and lowering provider are separate interfaces.
[ ] Unknown contour fails closed.
[ ] Capability observation cannot be consumed as authority.
[ ] Provider failure does not invoke another provider unless an explicit policy records same-contour retry.
[ ] MatrixTile/vector helper recovery success is not production lowering.
[ ] VMX and SecureCompute providers only return NoEmission/projection/admission decisions.
```

## Non-goals

- Не реализовывать missing backend paths.
- Не расширять opcode set.
- Не менять runtime implementation.
- Не делать universal accelerator provider.
- Не превращать provider registry в fallback-router.

## Риски

Главный риск — сделать registry удобным fallback-router. Registry должен быть маршрутизатором ответственности, а не механизмом спасения rejected lowering. Если contour отвергнут, это результат, а не приглашение попробовать другой contour.

Второй риск — назвать observation capability. All capability-like outputs must remain observations unless explicit runtime-owned authority source and dependency are recorded.
