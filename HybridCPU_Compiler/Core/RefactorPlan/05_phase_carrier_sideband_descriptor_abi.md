# Phase 05 — Carrier, Sideband and Descriptor ABI Separation

## Назначение

Развести три разных продукта компиляции:

1. carrier bytes;
2. sideband envelope;
3. descriptor ABI payload.

Эта фаза нужна, чтобы descriptor preservation, sideband projection и carrier emission не выглядели как одно и то же действие.

## Целевое правило

```text
carrier construction != sideband preservation != descriptor ABI construction != runtime execution
```

## Предлагаемые типы

### `VliwCarrierImage`

```csharp
public sealed record VliwCarrierImage(
    int BundleWidth,
    int SlotSizeBytes,
    int BundleSizeBytes,
    IReadOnlyList<CarrierSlotImage> Slots,
    CompilerCoreResultHeader Header);
```

Нельзя включать runtime-owned state в carrier image.

### `CompilerSidebandEnvelope`

```csharp
public sealed record CompilerSidebandEnvelope(
    IReadOnlyList<InstructionSlotMetadata> SlotMetadata,
    SidebandPreservationClass PreservationClass,
    CompilerCoreResultHeader Header);
```

Sideband должен быть bounded, explicit и проверяемым. Отсутствие sideband для contour, где он обязателен, должно давать reject.

### `DescriptorEnvelope`

```csharp
public sealed record DescriptorEnvelope(
    ExecutionContourKind ContourKind,
    DescriptorAbiStatus Status,
    object Descriptor,
    IReadOnlyList<string> AbiWarnings,
    CompilerCoreResultHeader Header);
```

`DescriptorEnvelope` не должен называться `ExecutableDescriptor`.

### `DescriptorAbiStatus`

```csharp
public enum DescriptorAbiStatus
{
    None,
    ValidTransportDescriptor,
    ParserOnlyDescriptor,
    HelperOnlyDescriptor,
    RejectedDescriptor,
    FutureGatedDescriptor
}
```

`ValidTransportDescriptor` означает только корректность переносимого ABI-представления.

## Descriptor builder contract

```csharp
public interface IDescriptorAbiBuilder<TIntent, TDescriptor>
{
    DescriptorBuildResult<TDescriptor> Build(TIntent intent, CompilerTargetProfile target);
}

public sealed record DescriptorBuildResult<TDescriptor>(
    DescriptorAbiStatus Status,
    TDescriptor? Descriptor,
    CompilerRejectReason? RejectReason,
    IReadOnlyList<string> MissingGates,
    CompilerEvidenceClass EvidenceClass,
    string Reason);
```

## Контурные правила

### DSC / lane6

- Descriptor обязателен только для scoped DSC1 executable path.
- DSC2 должен оставаться `ParserOnlyDescriptor` или `FutureGatedDescriptor`.
- Descriptor success не разрешает memory publication.
- Commit/publication только через runtime path.

### L7-SDC / lane7

- `ACCEL_SUBMIT` без descriptor sideband должен давать `RejectedDescriptor`.
- Descriptorless submit запрещен.
- После rejected submit не допускается fallback в DSC/Stream/scalar.

### MatrixTile

- Descriptor может быть отсутствующим, если текущий путь helper-only.
- Helper ABI должен явно перечислить поддержанные операции.
- Unsupported dtype/shape/layout/accumulator policy — reject, не scalar fallback.

### VMX

- Descriptor/sideband могут использоваться только как compatibility/projection vocabulary.
- Любой backend/executable descriptor — reject.

### SecureCompute

- Descriptor может быть policy/admission/evidence carrier.
- Никакого secure backend execution claim.

## Tasks

### 1. Разделить result objects

Текущие места, где один объект одновременно содержит slot bytes, metadata и descriptor, нужно обернуть в `CompilerEmissionPackage`, но внутри пакета сохранить разные envelope.

### 2. Ввести envelope validators

Каждый envelope должен иметь validator:

- `ICarrierImageValidator`;
- `ISidebandEnvelopeValidator`;
- `IDescriptorEnvelopeValidator`.

Validator не выдает runtime legality. Он выдает structural/ABI evidence.

### 3. Ввести explicit missing sideband errors

Для L7-SDC и DSC paths отсутствие descriptor/sideband должно быть отдельной ошибкой, а не generic failure.

### 4. Запретить descriptor authority leakage

Ввести grep/analyzer checks на имена:

- `ExecutableDescriptor`;
- `DescriptorCapability`;
- `DescriptorAuthority`;
- `DescriptorCommit`;
- `DescriptorRetire`.

## Deliverables

- `VliwCarrierImage`.
- `CompilerSidebandEnvelope`.
- `DescriptorEnvelope`.
- `DescriptorAbiStatus`.
- `IDescriptorAbiBuilder<TIntent,TDescriptor>`.
- Envelope validators.
- Negative tests for descriptor/sideband absence.

## Acceptance criteria

Фаза завершена, если caller может отличить:

- carrier создан;
- sideband сохранен;
- descriptor построен;
- descriptor только parser-only;
- descriptor rejected;
- emission запрещен;
- runtime authority still required.

## Non-goals

- Не менять формат runtime descriptors без отдельного runtime PR.
- Не добавлять production backend lowering.
- Не добавлять descriptorless L7 submit.
- Не превращать descriptor в capability authority.

## Риски

Главный риск — оставить descriptor как “магический объект”, по которому caller предполагает исполнение. После этой фазы descriptor должен быть скучным ABI-переносчиком: он может быть корректным, но сам по себе ничего не исполняет и не публикует.