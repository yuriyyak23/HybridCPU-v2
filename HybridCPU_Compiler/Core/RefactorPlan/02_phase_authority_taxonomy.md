# Phase 02 — Authority, Evidence and Publication Taxonomy

## Назначение

Ввести единую таксономию того, что compiler имеет право утверждать, а что остается исключительно за runtime. Эта фаза должна убрать неоднозначность между carrier construction, evidence production, runtime legality, execution, commit и retire.

## Базовый инвариант

```text
carrier != execution != publication != authority != commit != retire != evidence != production lowering
```

Любой новый API в `Core` должен явно показывать, на какой стороне этого инварианта он находится.

## Legacy vocabulary quarantine rule

Текущий Core уже содержит compiler-side термины `Legal`, `Legality`, `IsLegal`, `LegalSlots`, `HasLegalAssignment`. В рамках этого рефакторинга они считаются legacy structural-placement/admission vocabulary.

Нормативное правило:

```text
Compiler-side Legal*/IsLegal/Legality/HasLegalAssignment are structural placement/admission terms only.
They must be wrapped as CompilerStructuralAdmission* or CompilerStructuralPlacement* before new public APIs expose them.
They must never be exported as, converted to, or named like runtime LegalityDecision.
```

Любой adapter, который переносит старый `true`, `Success`, `Valid`, `Accepted`, `IsLegal` или `HasLegalAssignment`, обязан заполнить `LegacyApiTranslation` из фазы 4 и явно указать, что authority не усиливается.

## Новые понятия

### `CompilerAuthorityClass`

Предлагаемый enum:

```csharp
public enum CompilerAuthorityClass
{
    None,
    StructuralAgreement,
    StructuralAdmissionEvidence,
    StructuralPlacementEvidence,
    TransportConstruction,
    DescriptorAbiConstruction,
    TypedSlotFactProduction,
    CompilerEvidenceProduction,
    RuntimeBridgePreparation,
    RuntimeAuthorityRequired
}
```

Запрещено добавлять значения вида `Executable`, `RuntimeLegal`, `Committed`, `Retired` в compiler layer.

### `CompilerAuthoritySourceKind`

Authority class alone is not enough. Every result that has any compiler-side authority must also state the source of that limited authority.

```csharp
public enum CompilerAuthoritySourceKind
{
    None,
    CompilerStructuralModel,
    CompilerAbiValidator,
    CompilerCarrierSerializer,
    CompilerSidebandProjector,
    RuntimeContractObservation,
    RuntimeOwnedPolicyReference,
    TestOnlyHarness
}
```

`RuntimeOwnedPolicyReference` is only a pointer to a runtime-owned policy boundary. It is not permission to execute.

### `CompilerRuntimeAuthorityDependency`

```csharp
public enum CompilerRuntimeAuthorityDependency
{
    RuntimeLegalityARequired,
    RuntimeLegalityBRequired,
    RuntimeCommitRequired,
    RuntimeRetireRequired,
    RuntimePublicationRequired,
    RuntimeExecutionRequired,
    NoRuntimeActionBecauseNoEmission
}
```

Any carrier/descriptor/sideband/facts package that could eventually execute must include at least `RuntimeLegalityARequired` and `RuntimeLegalityBRequired`, unless the decision is `NoEmission`/`ParserOnly`/`HelperOnly` and carries `NoRuntimeActionBecauseNoEmission`.

### `CompilerEvidenceClass`

```csharp
public enum CompilerEvidenceClass
{
    NoEvidence,
    ParserEvidence,
    StructuralEvidence,
    StructuralAdmissionEvidence,
    StructuralPlacementEvidence,
    DescriptorAbiEvidence,
    TypedSlotEvidence,
    HazardSummaryEvidence,
    ResourceExpectationEvidence,
    RuntimeContractObservationEvidence,
    NoFallbackEvidence,
    NegativeGateEvidence
}
```

Evidence не является authority. Это должно быть отражено в именах типов и XML-doc.

### `CompilerPublicationClass`

```csharp
public enum CompilerPublicationClass
{
    NoPublication,
    CarrierBytesOnly,
    SidebandOnly,
    DescriptorOnly,
    FactsOnly,
    EvidenceOnly,
    RuntimeBridgeEnvelopeOnly
}
```

Compiler publication означает публикацию компиляторного продукта, а не memory/register publication архитектурного состояния.

For clarity, new APIs should prefer naming this `CompilerProductPublicationClass` or `CompilerPublicationClaimClass`; `PublicationClass` alone is too easy to confuse with runtime publication.

### `CompilerExecutionClaim`

```csharp
public enum CompilerExecutionClaim
{
    NoExecutionClaim,
    ParserOnly,
    HelperOnly,
    ScopedTestContourOnly,
    RuntimeExecutionRequired,
    ProductionExecutionForbidden
}
```

Ни один compiler result не должен утверждать execution completion.

## API правило

Каждый результат публичной операции Core должен иметь не меньше следующих полей:

```csharp
public sealed record CompilerCoreResultHeader(
    CompilerAuthorityClass AuthorityClass,
    CompilerAuthoritySourceKind AuthoritySourceKind,
    CompilerEvidenceClass EvidenceClass,
    CompilerPublicationClass PublicationClass,
    CompilerExecutionClaim ExecutionClaim,
    CompilerRuntimeAuthorityDependency RuntimeAuthorityDependency);
```

Для простых внутренних helpers это можно применять через wrapper/result adapter, а не через немедленную массовую перепись.

## Forbidden semantic conversions

Запрещены следующие conversion patterns:

```text
Compiler structural admissible -> Runtime legal
HasLegalAssignment -> Bridge accepted
DescriptorAbiStatus.ValidTransportDescriptor -> CanExecute
Helper success -> Production lowering
Parser success -> Production lowering
Capability observed -> Authority granted
BridgeAccepted -> ExecutionReady
Carrier emitted -> Published architectural state
Evidence generated -> Retired/Committed
```

Any such conversion must fail tests before behavior migration begins.

## Tasks

### 1. Ввести общий header результата

Добавить типы в новую область, например:

```text
Core/IR/Authority/
```

или в `Core/Support/Authority`, если типы используются шире IR.

Recommended namespace layout:

```text
HybridCPU.Compiler.Core.IR.Authority
HybridCPU.Compiler.Core.IR.Diagnostics
```

### 2. Привязать taxonomy к текущим результатам

Для каждого текущего метода из фазы 1 определить:

- `AuthorityClass`;
- `AuthoritySourceKind`;
- `EvidenceClass`;
- `PublicationClass`;
- `ExecutionClaim`;
- `RuntimeAuthorityDependency`;
- требуется ли runtime `LegalityDecision`;
- требуется ли token;
- требуется ли commit;
- требуется ли retire.

### 3. Ввести запрет на ambiguous success

Методы, возвращающие `bool`, `Success`, `Valid` или `Accepted`, должны быть помечены как legacy до фазы 4. Новый код обязан возвращать typed decision.

### 4. Зафиксировать authority vocabulary

В compiler code запрещены неуточненные имена:

- `Legal` без `CompilerStructural` или `Runtime`;
- `Executable` без `RuntimeRequired`;
- `Capability` без `AuthoritySource` or `Observation`;
- `Published` без указания compiler/runtime domain;
- `Commit`/`Retire` как compiler success;
- `Accepted` без `BridgeIngress` или `Compatibility` qualifier.

### 5. Define migration aliases

До удаления старых имен допустимы compatibility wrappers:

```text
IrBundleLegalityResult -> CompilerStructuralBundleAdmissionResult
IrCandidateBundleAnalysis.IsLegal -> IsStructurallyAdmissible
IrIssueSlotMask LegalSlots -> StructurallyAllowedSlots
HasLegalAssignment -> HasStructuralPlacement
EvaluateCandidateBundle -> AnalyzeStructuralCandidateBundle
EvaluateClusterPreparedLegality -> AnalyzeClusterPreparedStructuralAdmission
```

These names are recommendations. During migration, compatibility adapters may preserve old names internally with `[Obsolete]` and `LegacyApiTranslation` metadata.

## Deliverables

- Новые enum/record типы authority taxonomy.
- `CompilerAuthoritySourceKind`.
- `CompilerRuntimeAuthorityDependency`.
- `CompilerCoreResultHeader` with authority source and runtime dependency.
- Документ `AUTHORITY_TAXONOMY.md`.
- Список legacy API, которые должны быть заменены typed decisions.
- Список legacy structural legality terms and their wrappers.
- Analyzer или хотя бы grep-based check для запрещенных имен.

## Acceptance criteria

Фаза завершена, когда любой новый result object может быть проверен правилом:

```text
Does this object claim runtime authority, execution, architectural publication, commit or retire?
```

Правильный ответ для compiler layer: нет, только подготовка, перенос, структурное соглашение или evidence.

Additional acceptance checks:

```text
[ ] No new compiler public API exposes bare IsLegal/Legal/Valid/Success/Accepted.
[ ] Every structural admission/placement result uses structural naming.
[ ] Every capability result is called observation unless it names an authority source and runtime dependency.
[ ] RuntimeOwnedPolicyReference is never used as permission to execute.
[ ] CompilerCoreResultHeader exists or every new result carries equivalent fields.
```

## Negative examples

Запрещенный API:

```csharp
public bool IsLegal(Bundle bundle);
public Descriptor BuildExecutableDescriptor(Intent intent);
public Capability GetDscCapability();
public bool CommitLowering(LoweringPlan plan);
```

Допустимый API:

```csharp
public CompilerStructuralAdmissionDecision CheckStructuralAdmission(Bundle bundle);
public DescriptorBuildResult BuildTransportDescriptor(Intent intent);
public CompilerCapabilityObservation ObserveDscLoweringCapability(TargetProfile target);
public RuntimeBridgeEnvelope PrepareBridgeEnvelope(CompilerEmissionPackage package);
```

## Non-goals

- Не реализовывать runtime legality.
- Не менять runtime `LegalityDecision`.
- Не добавлять commit/retire simulation в compiler.
- Не превращать evidence envelope в certificate authority.

## Риски

Самый опасный риск — позволить taxonomy выглядеть как бюрократический слой, который ничего не меняет. На самом деле эта фаза должна стать машинно-проверяемым предохранителем от главной ошибки: переносимые compiler artifacts не должны превращаться в runtime authority через имена, helpers или успешный return code.

Второй риск — оставить старые compiler-side `Legal*` имена без quarantine. Тогда новые типы будут красивой оберткой над старым ambiguity.
