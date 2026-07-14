# Phase 05 — Carrier, Sideband and Descriptor ABI Separation

## Назначение

Развести разные продукты компиляции:

1. carrier bytes / carrier objects;
2. sideband envelope;
3. descriptor ABI payload;
4. typed-slot facts;
5. structural agreement;
6. evidence envelope;
7. runtime bridge envelope.

Эта фаза нужна, чтобы descriptor preservation, sideband projection, typed-slot facts и carrier emission не выглядели как одно и то же действие.

## Целевое правило

```text
carrier construction != sideband preservation != descriptor ABI construction != typed-slot facts != bridge acceptance != runtime execution
```

## Compatibility requirement for current Core

Current `HybridCpuCompiledProgram` already aggregates schedule, bundle layout, lowered bundles, program image, contract version, optional emission base address, `IrAdmissibilityAgreement` and lowered bundle annotations. This phase must not abruptly erase that shape. It must introduce a compatibility adapter that projects the current aggregate into separated envelopes without strengthening authority.

Required adapter principle:

```text
HybridCpuCompiledProgram -> CompilerEmissionPackage
```

This adapter is a product split, not a new execution authorization step.

## Proposed package type

### `CompilerEmissionPackage`

```csharp
public sealed record CompilerEmissionPackage(
    CompilerPackageIdentity Identity,
    VliwCarrierEnvelope? Carrier,
    CompilerSidebandEnvelope? Sideband,
    DescriptorEnvelope? Descriptor,
    TypedSlotFactsEnvelope? TypedSlotFacts,
    IrAdmissibilityAgreementEnvelope? StructuralAgreement,
    RuntimeBridgeEnvelope? RuntimeBridgeInput,
    CompilerEvidenceEnvelope Evidence,
    CompilerArtifactSeparationProof SeparationProof,
    CompilerCoreResultHeader Header);
```

### `CompilerPackageIdentity`

```csharp
public sealed record CompilerPackageIdentity(
    Guid PackageId,
    int CompilerContractVersion,
    SemanticIntentKind IntentKind,
    ExecutionContourKind ContourKind,
    string ProducerSurface,
    string Reason);
```

### `CompilerArtifactSeparationProof`

```csharp
public sealed record CompilerArtifactSeparationProof(
    bool CarrierSeparatedFromSideband,
    bool DescriptorSeparatedFromAuthority,
    bool TypedSlotFactsSeparatedFromLegality,
    bool EvidenceSeparatedFromProductionLowering,
    bool BridgeSeparatedFromExecution,
    IReadOnlyList<string> Notes);
```

### `CompilerArtifactKind`

```csharp
public enum CompilerArtifactKind
{
    VliwCarrierImage,
    VliwBundleAnnotations,
    DescriptorAbiPayload,
    TypedSlotFacts,
    IrAdmissibilityAgreement,
    RuntimeBridgeEnvelope,
    CompilerEvidenceEnvelope,
    TelemetrySnapshot
}
```

### `SidebandRequirement`

```csharp
public enum SidebandRequirement
{
    Forbidden,
    OptionalCompatibility,
    RequiredForTransport,
    RequiredForDescriptorSubmit,
    RequiredForBridgeValidation
}
```

## Предлагаемые envelope типы

### `VliwCarrierEnvelope`

```csharp
public sealed record VliwCarrierEnvelope(
    VliwCarrierImage Image,
    CompilerEmissionClass EmissionClass,
    CompilerCoreResultHeader Header);
```

### `VliwCarrierImage`

```csharp
public sealed record VliwCarrierImage(
    int BundleWidth,
    int SlotSizeBytes,
    int BundleSizeBytes,
    IReadOnlyList<CarrierSlotImage> Slots,
    byte[]? SerializedImage,
    CompilerCoreResultHeader Header);
```

Нельзя включать runtime-owned state в carrier image. Carrier may be fetch-ready, but fetch-ready is not execution-ready and not publication-ready.

### `CompilerSidebandEnvelope`

```csharp
public sealed record CompilerSidebandEnvelope(
    IReadOnlyList<InstructionSlotMetadata> SlotMetadata,
    SidebandRequirement Requirement,
    SidebandPreservationClass PreservationClass,
    bool IsEmptyCompatibilitySideband,
    CompilerCoreResultHeader Header);
```

Sideband должен быть bounded, explicit и проверяемым. Отсутствие sideband для contour, где он обязателен, должно давать typed reject.

`VliwBundleAnnotations.Empty` or equivalent compatibility path must never strengthen authority. Missing optional sideband can mean compatibility-only carrier path; it does not prove correctness.

### `DescriptorEnvelope`

```csharp
public sealed record DescriptorEnvelope(
    ExecutionContourKind ContourKind,
    DescriptorAbiStatus Status,
    object? Descriptor,
    SidebandRequirement SidebandRequirement,
    IReadOnlyList<string> AbiWarnings,
    CompilerCoreResultHeader Header);
```

`DescriptorEnvelope` не должен называться `ExecutableDescriptor`.

Forbidden fields inside `DescriptorEnvelope`:

```text
Executable
CanExecute
IsLegal
RuntimeLegal
Commit
Retire
PublicationAuthority
CapabilityAuthority
```

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

`ValidTransportDescriptor` означает только корректность переносимого ABI-представления. It does not imply runtime legality, execution, memory publication, register publication, commit or retire.

### `IrAdmissibilityAgreementEnvelope`

```csharp
public sealed record IrAdmissibilityAgreementEnvelope(
    IrAdmissibilityAgreement Agreement,
    CompilerEvidenceClass EvidenceClass,
    bool RuntimeLegalityStillRequired,
    CompilerCoreResultHeader Header);
```

Structural agreement is not runtime Legality A/B.

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

## Adapter from current aggregate

Add a compatibility adapter:

```csharp
public interface ICompiledProgramEnvelopeAdapter
{
    CompilerEmissionPackage Project(HybridCpuCompiledProgram compiledProgram, CompilerArtifactProjectionOptions options);
}
```

Adapter rules:

- `LoweredBundles` and `ProgramImage` become `VliwCarrierEnvelope`.
- `LoweredBundleAnnotations` become `CompilerSidebandEnvelope`.
- `AdmissibilityAgreement` becomes `IrAdmissibilityAgreementEnvelope`.
- Descriptors copied through annotations remain `DescriptorAbiPayload` / sideband evidence only.
- `EmissionBaseAddress` is compiler product metadata; it is not runtime publication, commit or retire.
- Missing annotations for optional scalar carrier are compatibility-only, not stronger correctness.
- Missing descriptor sideband for L7 submit is reject, not empty annotations fallback.

## Контурные правила

### Native VLIW scalar/load-store/branch

- Carrier may be emitted with optional sideband.
- No descriptor is required by default.
- Typed-slot facts are structural evidence only.
- Runtime Legality A/B required for executable candidates.
- Carrier image does not publish architectural memory/register state.

### DSC / lane6

- Descriptor обязателен only for supported scoped DSC path.
- DSC2 должен оставаться `ParserOnlyDescriptor` или `FutureGatedDescriptor`.
- Descriptor success не разрешает memory publication.
- Commit/publication только через runtime path.
- Lane6 descriptor on non-DSC opcode is rejected.

### L7-SDC / lane7

- `ACCEL_SUBMIT` без descriptor sideband должен давать `RejectedDescriptor` or `L7DescriptorlessSubmitForbidden`.
- Descriptorless submit запрещен.
- После rejected submit не допускается fallback в DSC/Stream/scalar.
- Token/capability evidence is evidence only.

### MatrixTile

- Descriptor может быть отсутствующим, если текущий путь helper-only.
- Helper ABI должен явно перечислить поддержанные операции.
- Unsupported dtype/shape/layout/accumulator policy — reject, не scalar fallback.
- Helper descriptor success is not production lowering.

### Stream/vector

- Vector/Stream sideband requirements must be explicit.
- Helper/transport recovery is not production vector backend success.
- Unsupported shape/stride/predicate must reject or no-emit, not scalar fallback.

### VMX

- Descriptor/sideband могут использоваться только как compatibility/projection vocabulary.
- Любой backend/executable descriptor — reject.
- VMCS is not state owner.
- VmxCaps is not authority.

### SecureCompute

- Descriptor может быть policy/admission/evidence carrier.
- Никакого secure backend execution claim.
- Host-owned evidence must not enter guest/domain architectural state.

## Tasks

### 1. Разделить result objects

Текущие места, где один объект одновременно содержит slot bytes, metadata и descriptor, нужно обернуть в `CompilerEmissionPackage`, но внутри пакета сохранить разные envelope.

### 2. Ввести envelope validators

Каждый envelope должен иметь validator:

- `ICarrierImageValidator`;
- `ISidebandEnvelopeValidator`;
- `IDescriptorEnvelopeValidator`;
- `ITypedSlotFactsEnvelopeValidator`;
- `IEmissionPackageSeparationValidator`.

Validator не выдает runtime legality. Он выдает structural/ABI evidence.

### 3. Ввести explicit missing sideband errors

Для L7-SDC и DSC paths отсутствие descriptor/sideband должно быть отдельной ошибкой, а не generic failure.

### 4. Запретить descriptor authority leakage

Ввести grep/analyzer checks на имена:

- `ExecutableDescriptor`;
- `DescriptorCapability`;
- `DescriptorAuthority`;
- `DescriptorCommit`;
- `DescriptorRetire`;
- `CanExecuteDescriptor`;
- `DescriptorIsLegal`.

### 5. Add artifact separation tests

Tests must verify that carrier, sideband, descriptor, typed-slot facts, structural agreement, bridge envelope and evidence cannot be substituted for each other.

## Deliverables

- `CompilerEmissionPackage`.
- `CompilerPackageIdentity`.
- `CompilerArtifactKind`.
- `CompilerArtifactSeparationProof`.
- `SidebandRequirement`.
- `VliwCarrierEnvelope` / `VliwCarrierImage`.
- `CompilerSidebandEnvelope`.
- `DescriptorEnvelope`.
- `DescriptorAbiStatus`.
- `IrAdmissibilityAgreementEnvelope`.
- `IDescriptorAbiBuilder<TIntent,TDescriptor>`.
- Envelope validators.
- `ICompiledProgramEnvelopeAdapter` for current `HybridCpuCompiledProgram`.
- Negative tests for descriptor/sideband absence.

Recommended namespace layout:

```text
HybridCPU.Compiler.Core.IR.Artifacts
HybridCPU.Compiler.Core.IR.Bridge
```

## Acceptance criteria

Фаза завершена, если caller может отличить:

- carrier создан;
- sideband сохранен;
- descriptor построен;
- descriptor только parser-only;
- descriptor rejected;
- typed-slot facts emitted;
- structural agreement emitted;
- evidence emitted;
- bridge envelope prepared;
- emission запрещен;
- runtime authority still required.

Additional machine checks:

```text
[ ] DescriptorAbiStatus.ValidTransportDescriptor cannot be consumed as execution authority.
[ ] Empty sideband compatibility cannot strengthen authority.
[ ] L7 descriptorless submit cannot be represented as carrier-only success.
[ ] HybridCpuCompiledProgram adapter preserves all existing products but separates envelopes.
[ ] EmissionBaseAddress is not treated as runtime publication/commit/retire.
```

## Non-goals

- Не менять формат runtime descriptors без отдельного runtime PR.
- Не добавлять production backend lowering.
- Не добавлять descriptorless L7 submit.
- Не превращать descriptor в capability authority.
- Не удалять `HybridCpuCompiledProgram` without compatibility adapter.

## Риски

Главный риск — оставить descriptor как “магический объект”, по которому caller предполагает исполнение. После этой фазы descriptor должен быть скучным ABI-переносчиком: он может быть корректным, но сам по себе ничего не исполняет и не публикует.

Второй риск — split envelope сломает существующие consumers или усилит missing sideband semantics. Compatibility adapter must preserve behavior only and never strengthen authority.
