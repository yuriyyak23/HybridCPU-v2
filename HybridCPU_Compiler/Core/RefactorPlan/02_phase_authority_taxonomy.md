# Phase 02 — Authority, Evidence and Publication Taxonomy

## Назначение

Ввести единую таксономию того, что compiler имеет право утверждать, а что остается исключительно за runtime. Эта фаза должна убрать неоднозначность между carrier construction, evidence production, runtime legality, execution, commit и retire.

## Базовый инвариант

```text
carrier != execution != publication != authority != commit != retire != evidence != production lowering
```

Любой новый API в `Core` должен явно показывать, на какой стороне этого инварианта он находится.

## Новые понятия

### `CompilerAuthorityClass`

Предлагаемый enum:

```csharp
public enum CompilerAuthorityClass
{
    None,
    StructuralAgreement,
    TransportConstruction,
    DescriptorAbiConstruction,
    TypedSlotFactProduction,
    CompilerEvidenceProduction,
    RuntimeBridgePreparation,
    RuntimeAuthorityRequired
}
```

Запрещено добавлять значения вида `Executable`, `RuntimeLegal`, `Committed`, `Retired` в compiler layer.

### `CompilerEvidenceClass`

```csharp
public enum CompilerEvidenceClass
{
    NoEvidence,
    ParserEvidence,
    StructuralEvidence,
    DescriptorAbiEvidence,
    TypedSlotEvidence,
    HazardSummaryEvidence,
    ResourceExpectationEvidence,
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
    RuntimeBridgeEnvelopeOnly
}
```

Compiler publication означает публикацию компиляторного продукта, а не memory/register publication архитектурного состояния.

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

Каждый результат публичной операции Core должен иметь не меньше трех полей:

```csharp
public sealed record CompilerCoreResultHeader(
    CompilerAuthorityClass AuthorityClass,
    CompilerEvidenceClass EvidenceClass,
    CompilerPublicationClass PublicationClass,
    CompilerExecutionClaim ExecutionClaim);
```

Для простых внутренних helpers это можно применять через wrapper/result adapter, а не через немедленную массовую перепись.

## Tasks

### 1. Ввести общий header результата

Добавить типы в новую область, например:

```text
Core/IR/Model/Authority/
```

или в `Core/Support/Authority`, если типы используются шире IR.

### 2. Привязать taxonomy к текущим результатам

Для каждого текущего метода из фазы 1 определить:

- `AuthorityClass`;
- `EvidenceClass`;
- `PublicationClass`;
- `ExecutionClaim`;
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
- `Capability` без `AuthoritySource`;
- `Published` без указания compiler/runtime domain;
- `Commit`/`Retire` как compiler success.

## Deliverables

- Новые enum/record типы authority taxonomy.
- Документ `AUTHORITY_TAXONOMY.md`.
- Список legacy API, которые должны быть заменены typed decisions.
- Analyzer или хотя бы grep-based check для запрещенных имен.

## Acceptance criteria

Фаза завершена, когда любой новый result object может быть проверен правилом:

```text
Does this object claim runtime authority, execution, architectural publication, commit or retire?
```

Правильный ответ для compiler layer: нет, только подготовка, перенос, структурное соглашение или evidence.

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