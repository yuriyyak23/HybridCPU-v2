# Phase 06 — Typed-Slot Facts and Runtime Legality Bridge

## Назначение

Сделать typed-slot facts и runtime bridge формальными, но не смешивать их с runtime legality. Compiler может подготовить structural agreement, но окончательный `LegalityDecision` остается за runtime.

This phase must align with the existing runtime-owned `CompilerContract` / `CompilerTypedSlotPolicyMode` vocabulary. The compiler may observe or carry a policy mode value, but it must not own runtime typed-slot policy.

## Основной принцип

```text
typed-slot facts == compiler/runtime structural agreement input
typed-slot facts != runtime legality
typed-slot facts != execution authorization
bridge ingress compatibility != execution readiness
```

Missing typed-slot facts under compatibility mode are weaker than validated facts, not stronger.

## Runtime policy ownership rule

The runtime/ISE side owns typed-slot policy and contract version compatibility. Compiler-side bridge envelopes may include:

- producer compiler contract version;
- runtime contract version observed at build/test time;
- observed `CompilerTypedSlotPolicyMode` value;
- typed-slot facts and structural agreement;
- evidence and diagnostics.

They must not include:

- final `LegalityDecision`;
- Stage A decision;
- Stage B decision;
- execution ready bit;
- commit/retire status;
- memory/register publication authority.

## Предлагаемые типы

### `TypedSlotFactsEnvelope`

```csharp
public sealed record TypedSlotFactsEnvelope(
    TypedSlotBundleFacts Facts,
    CompilerTypedSlotPolicyMode RuntimePolicyModeObserved,
    TypedSlotFactStaging Staging,
    IrAdmissibilityAgreementEnvelope Agreement,
    bool StructuralEvidenceOnly,
    bool RuntimeLegalityStillRequired,
    CompilerCoreResultHeader Header);
```

`StructuralEvidenceOnly` must always be `true` for compiler-produced facts.

### `TypedSlotFactStaging`

```csharp
public enum TypedSlotFactStaging
{
    MissingCompatibility,
    PresentUnvalidated,
    PresentValidated,
    PresentQuarantined,
    RejectedByRuntimeBridge,
    FutureRequiredForAdmission
}
```

`FutureRequiredForAdmission` is documentation/future seam only unless runtime explicitly selects such policy. It must not be used by compiler to enforce runtime admission.

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
    int ProducerCompilerContractVersion,
    int RuntimeContractVersionObservedAtBuild,
    CompilerTypedSlotPolicyMode RuntimePolicyModeObserved,
    VliwCarrierEnvelope? Carrier,
    CompilerSidebandEnvelope? Sideband,
    DescriptorEnvelope? Descriptor,
    TypedSlotFactsEnvelope? TypedSlotFacts,
    IrAdmissibilityAgreementEnvelope? StructuralAgreement,
    CompilerEvidenceEnvelope Evidence,
    bool RequiresRuntimeLegalityA,
    bool RequiresRuntimeLegalityB,
    CompilerCoreResultHeader Header);
```

`RequiresRuntimeLegalityA` and `RequiresRuntimeLegalityB` must be true for executable carrier candidates. For no-emission/parser/helper-only outputs they must be represented through `RuntimeAuthorityDependency` and decision kind, not omitted silently.

### `BridgeAcceptanceReport`

```csharp
public enum BridgeIngressStatus
{
    Unknown,
    BridgeIngressAccepted,
    BridgeIngressRejected,
    VersionRejected,
    AgreementFailure,
    SidebandRejected,
    DescriptorRejected,
    TypedSlotFactsRejected,
    Quarantined,
    CompatibilityAcceptedMissingFacts,
    CompatibilityRecordedWithoutValidation
}

public sealed record BridgeAcceptanceReport(
    BridgeIngressStatus Status,
    bool RuntimeLegalityAStillRequired,
    bool RuntimeLegalityBStillRequired,
    bool RuntimeCommitStillRequired,
    bool RuntimeRetireStillRequired,
    bool RuntimePublicationStillRequired,
    string Reason);
```

Prefer `BridgeIngressStatus` over `BridgeAcceptanceStatus`: bare `Accepted` is too easy to confuse with runtime execution readiness.

Forbidden statuses:

```text
RuntimeLegal
ExecutionReady
CanExecute
Committed
Retired
PublishedArchitecturalState
```

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

Это bridge ingress compatibility, а не runtime execution.

## Compatibility mode semantics

If current runtime policy allows missing typed-slot facts:

```text
missing facts -> compatibility path only
present validated facts -> stronger structural evidence
present mismatch -> quarantine/reject depending on runtime policy
```

Missing facts must not be treated as stronger or equivalent to validated agreement. The bridge report must preserve the distinction.

## Tasks

### 1. Обернуть текущие typed-slot facts

Если текущие facts находятся в validation-only/compatibility mode, API должен сохранять этот staging как часть результата.

### 2. Разделить structural admissibility и dynamic legality

Ввести отдельные имена:

- `CompilerStructuralAdmissionDecision`;
- `CompilerStructuralBundleAdmissionResult`;
- `CompilerStructuralPlacementReport`;
- `IrAdmissibilityAgreement`;
- `RuntimeLegalityARequired`;
- `RuntimeLegalityBRequired`;
- `RuntimeLegalityDecision` only in runtime-owned boundary.

### 3. Версионировать bridge

`CompilerContractView` должен включать:

- producer compiler contract version;
- observed runtime contract version;
- typed-slot policy mode;
- supported carrier shape;
- supported sideband envelope version;
- supported descriptor ABI versions;
- known future-gated contours.

### 4. Добавить quarantine path

Если runtime bridge видит structural mismatch, stale version, unknown contour или invalid facts, результат должен быть `Quarantined` или `BridgeIngressRejected`, но не silent fallback.

### 5. Add Stage A/B ownership tests

Add tests that ensure:

```text
BridgeIngressAccepted -> RuntimeLegalityAStillRequired == true
BridgeIngressAccepted -> RuntimeLegalityBStillRequired == true
StructurallyAdmissible -> not RuntimeLegal
TypedSlotFacts present -> not RuntimeLegal
TypedSlotFacts missing compatibility -> not stronger authority
```

## Deliverables

- `TypedSlotFactsEnvelope`.
- `TypedSlotFactStaging`.
- `IrAdmissibilityAgreement`.
- `IrAdmissibilityAgreementEnvelope`.
- `RuntimeBridgeEnvelope`.
- `BridgeAcceptanceReport` with `BridgeIngressStatus`.
- `ICompilerRuntimeBridge`.
- Tests on stale contract version.
- Tests on typed-slot mismatch.
- Tests on Stage A/B runtime ownership.
- Tests on missing facts compatibility being weaker than validated facts.

Recommended namespace layout:

```text
HybridCPU.Compiler.Core.IR.Bridge
HybridCPU.Compiler.Core.IR.Artifacts
```

## Acceptance criteria

Фаза завершена, если:

1. typed-slot facts могут отсутствовать без изменения legacy compatibility path, если текущая runtime policy это разрешает;
2. missing facts are recorded as compatibility-only and weaker than validated facts;
3. typed-slot facts при наличии проверяются;
4. mismatch дает diagnostic/quarantine/reject;
5. bridge ingress acceptance не выдается за runtime legality;
6. logs показывают, что Stage A/Stage B остаются runtime-owned;
7. compiler bridge types carry observed runtime policy values but do not own policy.

Machine-checkable gates:

```text
[ ] No bridge status contains RuntimeLegal/ExecutionReady/Committed/Retired.
[ ] BridgeIngressAccepted always reports runtime Legality A/B still required for executable candidates.
[ ] StructurallyAdmissible cannot be assigned to RuntimeLegalityDecision.
[ ] Missing typed-slot facts under compatibility policy do not strengthen authority.
[ ] Runtime policy mode is observed/reference-only, not compiler-owned.
```

## Non-goals

- Не переносить Stage A/Stage B в compiler.
- Не делать compiler source of truth для legality.
- Не добавлять runtime retire/commit в bridge.
- Не менять contract version без отдельного migration PR.
- Не превращать `RequiredForAdmission` future seam в selectable compiler policy.

## Риски

Главный риск — назвать bridge acceptance словом `Legal`. Это сломает архитектурный смысл HybridCPU. Bridge может принять пакет как структурно пригодный для runtime рассмотрения, но только runtime решает legality и execution.

Второй риск — продублировать runtime typed-slot policy в compiler and diverge. Compiler must observe/reference runtime-owned policy, not own it.
