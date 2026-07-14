# Phase 10 — Architectural Audit Addendum

Дата аудита: 2026-07-08.

Статус: normative hardening addendum для `HybridCPU_Compiler/Core/RefactorPlan`.

Этот файл не заменяет фазы `00`–`09`. Он фиксирует результат проверки плана против текущего `master` и добавляет обязательные усиления, без которых рефакторинг может превратиться в косметическую типизацию старой неоднозначности.

## Scope and source baseline

Проверенный refactor branch:

```text
docs/compiler-core-refactor-plan-20260708
```

Проверенный baseline текущего кода:

```text
master @ 3c868346d18d5281c35949c14adc7f84f59a2281
```

На момент аудита diff branch относительно `master` состоит только из Markdown-файлов в:

```text
HybridCPU_Compiler/Core/RefactorPlan
```

Это значит, что текущий план является planning-only и пока не меняет compiler/runtime behavior.

## Architectural verdict

План можно использовать как основу, но только при усилении следующих границ:

1. `CompilerLoweringDecision` должен стать semantic firewall, а не wrapper над `bool`, `Try*`, `HasLegalAssignment`, `Success` или exception-as-control-flow.
2. Все compiler-side слова `legal`, `accepted`, `valid`, `capability`, `emit`, `lower` должны иметь typed authority/evidence class или быть quarantined legacy names.
3. Structural admission, hazard clearance, slot assignment и typed-slot agreement должны быть переименованы/обернуты как compiler structural evidence, а не runtime legality.
4. Helper/parser success для MatrixTile, Stream/vector, DSC, L7-SDC, VMX и SecureCompute должен возвращать typed no-production-lowering decision unless explicit production gates exist.
5. Phase 09 negative matrix must be partially pulled forward before behavior migration. Tests cannot remain only at the end.

## Current Core grounding map

| Plan phase | Current code area | Observed current behavior | Missing hardening | Risk |
|---|---|---|---|---|
| 01 inventory | `Core/IR/Model/HybridCpuCompiledProgram.cs` | `HybridCpuCompiledProgram` already mixes schedule, bundle layout, lowered `VLIW_Bundle`, program image, contract version, optional emission address, `IrAdmissibilityAgreement`, and `VliwBundleAnnotations`. Comments correctly state annotation descriptor sideband is transport evidence and agreement is not runtime legality. | Inventory must list this as an existing aggregate that needs envelope split, not a blank-slate design. | High: new `CompilerEmissionPackage` may duplicate or obscure current artifact semantics. |
| 01 inventory | `Core/IR/Bundling/HybridCpuBundleLowerer.cs` | Lowering returns `IReadOnlyList<VLIW_Bundle>`, emits annotations and typed-slot facts as side channels, preserves MatrixTile/vector encoded helper instructions, and copies DSC/L7 descriptors into slot metadata. | Inventory must enumerate every `Lower*`, `Emit*`, `TryRecover*`, `TryGet*`, `HasLegalAssignment`, `IsLegal`, `LegalSlots` current surface. | High: legacy ambiguity can survive behind wrappers. |
| 02 authority taxonomy | `Core/IR/Hazards/HybridCpuInstructionLegalityChecker.cs` | Compiler currently uses `Legality`, `IsLegal`, `EvaluateCandidateBundle`, `EvaluateClusterPreparedLegality`, and `IrBundleLegalityResult` for compiler-side structural checks. | Add explicit migration exception: current compiler `Legality` terms are structural placement legality and must be renamed or wrapped. | High: compiler structural legality may be mistaken for runtime Legality A/B. |
| 02 authority taxonomy | `HybridCPU_ISE/CloseToRTL/Core/Contracts/CompilerContract.cs` | Runtime bridge already defines typed-slot policy modes, ingress actions, contract version and fail-closed mismatch check. | Phase 02/06 must reuse runtime-owned contract semantics without importing runtime authority into compiler. | Medium: duplicated policy vocabulary can diverge. |
| 03 intent/contours | `Core/IR/Construction/HybridCpuIrBuilder.cs` | IR builder recovers MatrixTile/vector helper plans via `TryRecoverFromInstruction`, validates explicit lane6/lane7 descriptor intent, and fails closed for descriptorless L7 `ACCEL_SUBMIT`. | Intent classifier must be inserted before recovery/lowering so failed recovery becomes typed decision, not `null` or exception. | High: parser/helper success remains hidden semantic claim. |
| 03 intent/contours | `Core/IR/Hazards/HybridCpuHazardModel.cs` | Hazard model classifies opcodes into scalar/load-store/control-flow/system/vector/DmaStream resource classes and exposes compiler-visible execution profiles. | Contours must distinguish resource class from execution contour and runtime authority. | Medium: `ExecutionProfile` name may imply execution authority. |
| 04 lowering decision | `Core/IR/Bundling/HybridCpuBundleFormer.cs` | Bundler uses `TryBundleProgramGlobally`, `TryMaterialize*`, `HasLegalAssignment`, and fallback from global lookahead to local materialization. | Distinguish same-contour structural placement fallback from forbidden cross-contour lowering fallback. Require `NoFallbackProof`. | High: hidden fallback can remain in placement code. |
| 05 carrier/sideband/descriptor ABI | `HybridCpuCompiledProgram` + `HybridCpuBundleLowerer` | Carrier image, annotations, descriptors, typed facts and agreement are currently adjacent but not separate envelopes. | Define compatibility adapter that projects current artifact into separated envelopes without claiming new authority. | High: envelope split can break existing artifact consumers or accidentally strengthen missing annotations. |
| 06 typed-slot bridge | `CompilerContract.cs` | Missing typed-slot facts are compatible under current policy; present facts can be validation/quarantine evidence; `RequiredForAdmission` is future and not runtime selectable. | Bridge envelope must encode runtime policy mode and must not treat missing facts compatibility as stronger correctness. | High: compatibility path could become de facto authority. |
| 07 providers | `HybridCpuBundleLowerer`, `HybridCpuIrBuilder`, helper lowerers | Current helper lowering/recovery is embedded in generic construction/lowering. | Providers need an adapter phase and a registry fail-closed only for contour dispatch, not for existing scalar placement. | Medium: provider registry can become cosmetic routing. |
| 08 telemetry/evidence | `HybridCpuBundleFormer` | Telemetry profile reader and certificate-aware tie-breaks already influence advisory placement. | Evidence must record advisory-vs-authoritative status for telemetry/certificates. | Medium: certificate/tie-break evidence can look like authority. |
| 09 tests | Existing compiler tests outside Core | Existing tests cover some no-emission and contract boundaries, but refactor plan tests are not yet attached to exact source entrypoints. | Add source-level negative tests mapped to every current ambiguous entrypoint. | High: migration can pass broad tests while preserving ambiguity. |

## Required strengthening by phase

### Phase 01 — Inventory and freeze

Add to deliverables:

```text
CURRENT_BEHAVIOR.md must include:
- current artifact aggregate map for HybridCpuCompiledProgram;
- current carrier/sideband/facts/agreement fields and ownership;
- current ambiguous names table with file path, symbol, return type, and replacement decision type;
- current exception-as-control-flow table;
- current same-contour structural fallback list;
- current cross-contour fallback list, expected empty.
```

Mandatory ambiguous surfaces to inventory:

```text
HybridCpuCompiledProgram.EmitVliwBundleImage
HybridCpuCompiledProgram.ValidateRuntimeContractCompatibility
HybridCpuBundleLowerer.LowerProgram
HybridCpuBundleLowerer.LowerBlock
HybridCpuBundleLowerer.LowerBundle
HybridCpuBundleLowerer.EmitAnnotationsForProgram
HybridCpuBundleLowerer.EmitFactsForBundle
HybridCpuIrBuilder.BuildProgram
HybridCpuIrBuilder.ValidateExplicitAcceleratorIntent
CompilerMatrixTileEmissionLowerer.TryRecoverFromInstruction
CompilerVectorTransferEmissionLowerer.TryRecoverFromInstruction
HybridCpuInstructionLegalityChecker.AnalyzeCandidateBundle
HybridCpuInstructionLegalityChecker.EvaluateCandidateBundle
HybridCpuInstructionLegalityChecker.EvaluateClusterPreparedLegality
HybridCpuBundleFormer.TryBundleProgramGlobally
HybridCpuBundleFormer.TryMaterializeBlockGlobalLookahead
HybridCpuBundleFormer.TryMaterializeAdjacentBundleTripletLookahead
HybridCpuBundleFormer.TryMaterializeAdjacentBundlePair
HybridCpuSlotModel.SearchAssignments
HybridCpuSlotModel.SearchProgramAssignments
```

### Phase 02 — Authority taxonomy

Add a compatibility renaming rule:

```text
Compiler-side `Legal*`, `IsLegal`, `Legality`, `HasLegalAssignment` are legacy structural-placement terms.
They must be wrapped as `CompilerStructuralAdmission*` or `CompilerStructuralPlacement*` before any new API exposes them.
They must never be exported as `LegalityDecision`.
```

Add required authority source fields:

```csharp
public enum CompilerAuthoritySourceKind
{
    None,
    CompilerStructuralModel,
    CompilerAbiValidator,
    RuntimeContractObservation,
    RuntimeOwnedPolicyReference,
    TestOnlyHarness
}

public enum CompilerRuntimeAuthorityDependency
{
    RuntimeLegalityARequired,
    RuntimeLegalityBRequired,
    RuntimeCommitRequired,
    RuntimeRetireRequired,
    RuntimePublicationRequired,
    NoRuntimeActionBecauseNoEmission
}
```

Rule: `RuntimeOwnedPolicyReference` is a pointer to a runtime-owned policy boundary, not permission to execute.

### Phase 03 — IR intent and contours

Split classification into two records:

```csharp
public sealed record CompilerSemanticIntent(
    SemanticIntentKind Kind,
    string OpcodeFamily,
    bool RequiresDescriptor,
    bool RequiresSideband,
    bool IsCompatibilityProjection,
    bool IsPolicyAdmissionOnly);

public sealed record CompilerExecutionContourSelection(
    ExecutionContourKind Contour,
    bool IsKnownContour,
    bool IsProviderAvailable,
    bool IsEmissionForbidden,
    string SelectionReason,
    IReadOnlyList<string> MissingInputs);
```

Rationale: intent is semantic; contour is transport/lowering route. Keeping both in one object invites hidden fallback.

Add explicit contour values:

```text
NativeVliwScalar
NativeVliwLoadStore
NativeVliwBranchControl
StreamEngineVector
MatrixTileHelperOnly
DmaStreamComputeLane6
L7SdcLane7
VmxProjectionOnly
SecureComputePolicyAdmissionOnly
ParserOnly
NoEmission
FutureGated
UnknownRejected
```

### Phase 04 — Lowering decision API

Strengthen `CompilerLoweringDecision` so every decision includes:

```csharp
public enum CompilerLoweringDecisionKind
{
    EmitCarrier,
    Reject,
    ParserOnly,
    HelperOnly,
    NoEmission,
    FutureGated
}

public enum CompilerEmissionClass
{
    NoEmission,
    CarrierOnly,
    CarrierWithOptionalSideband,
    CarrierWithRequiredSideband,
    DescriptorOnly,
    SidebandOnly,
    EvidenceOnly,
    CompatibilityProjectionOnly
}

public enum CompilerProductionLoweringStatus
{
    NotProductionLowering,
    HelperAbiOnly,
    ParserOnly,
    DiagnosticOnly,
    ProductionCandidateRequiresRuntimeLegality,
    ProductionAllowedByExplicitCompilerGate
}
```

Required base fields:

```csharp
public abstract record CompilerLoweringDecision
{
    public required CompilerLoweringDecisionKind DecisionKind { get; init; }
    public required SemanticIntentKind IntentKind { get; init; }
    public required ExecutionContourKind ContourKind { get; init; }
    public required CompilerAuthorityClass AuthorityClass { get; init; }
    public required CompilerEvidenceClass EvidenceClass { get; init; }
    public required CompilerEmissionClass EmissionClass { get; init; }
    public required CompilerProductionLoweringStatus ProductionLoweringStatus { get; init; }
    public required CompilerRuntimeAuthorityDependency RuntimeDependency { get; init; }
    public required NoFallbackProof NoFallbackProof { get; init; }
    public required IReadOnlyList<CompilerArtifactKind> ProducedArtifacts { get; init; }
    public required IReadOnlyList<CompilerArtifactKind> RequiredArtifacts { get; init; }
    public required IReadOnlyList<CompilerRejectReason> RejectReasons { get; init; }
    public required LegacyApiTranslation? LegacyTranslation { get; init; }
}
```

Add anti-wrapper guard:

```csharp
public sealed record LegacyApiTranslation(
    string SourceFile,
    string SourceMember,
    string LegacyReturnShape,
    string LegacySuccessMeaning,
    string TypedReplacementMeaning,
    bool PreservesBehaviorOnly,
    bool StrengthensAuthority);
```

`StrengthensAuthority` must always be false. Tests must fail if an adapter maps `true`/`Success`/`Valid` directly to execution, runtime legality, commit, retire or production lowering.

### Phase 05 — Carrier / sideband / descriptor ABI

Add a top-level package that replaces implicit adjacency:

```csharp
public sealed record CompilerEmissionPackage(
    CompilerPackageIdentity Identity,
    VliwCarrierEnvelope? Carrier,
    CompilerSidebandEnvelope? Sideband,
    DescriptorEnvelope? Descriptor,
    TypedSlotFactsEnvelope? TypedSlotFacts,
    CompilerEvidenceEnvelope Evidence,
    RuntimeBridgeEnvelope? RuntimeBridgeInput,
    CompilerArtifactSeparationProof SeparationProof);
```

Add artifact kind enum:

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

Add fail-closed sideband requirement:

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

Rules:

- `DescriptorAbiStatus.ValidTransportDescriptor` does not imply runtime legality.
- `DescriptorEnvelope` must not contain `Executable`, `CanExecute`, `IsLegal`, `Commit`, `Retire` fields.
- Missing sideband for scalar compatibility may be accepted as carrier-only.
- Missing sideband for L7 `ACCEL_SUBMIT` must be a typed reject, not empty annotations fallback.

### Phase 06 — Typed-slot and legality bridge

Bridge acceptance must include the active runtime policy mode by value, but must not become policy owner:

```csharp
public sealed record RuntimeBridgeEnvelope(
    int ProducerCompilerContractVersion,
    int RuntimeContractVersionObservedAtBuild,
    CompilerTypedSlotPolicyMode RuntimePolicyModeObserved,
    TypedSlotFactsEnvelope? TypedSlotFacts,
    IrAdmissibilityAgreementEnvelope? StructuralAgreement,
    DescriptorEnvelope? Descriptor,
    CompilerEvidenceEnvelope Evidence,
    bool RequiresRuntimeLegalityA,
    bool RequiresRuntimeLegalityB);
```

Add rule:

```text
Missing typed-slot facts under CompatibilityValidation are weaker than validated facts, not stronger.
Bridge acceptance is ingress compatibility only.
```

### Phase 07 — Contour providers

Split providers into analysis and lowering roles:

```csharp
public interface IContourAnalyzer
{
    ContourAnalysisReport Analyze(CompilerSemanticIntent intent, CompilerInputBundle input);
}

public interface IContourLoweringProvider
{
    ExecutionContourKind ContourKind { get; }
    CompilerLoweringDecision Lower(CompilerSemanticIntent intent, ContourAnalysisReport analysis, CompilerLoweringContext context);
}
```

Provider registry rule:

```text
Unknown contour -> UnknownRejected/FutureGated, no scalar fallback.
Provider failure -> reject unless an explicit same-contour retry policy is present and recorded.
Same-contour placement search fallback is allowed only if it does not change semantic intent, contour, sideband requirement or emission class.
```

### Phase 08 — Evidence and telemetry

Add evidence ownership and visibility:

```csharp
public enum EvidenceOwnershipDomain
{
    CompilerHostOwned,
    RuntimeObserved,
    TestHarnessOwned,
    GuestVisibleForbidden,
    DomainArchitecturalStateForbidden
}

public enum EvidenceAuthoritySemantics
{
    EvidenceOnly,
    DiagnosticOnly,
    CompatibilityObservation,
    RuntimePolicyReferenceOnly,
    ForbiddenAsAuthority
}
```

Telemetry must include:

```text
intent.kind
contour.kind
decision.kind
emission.class
production_lowering.status
authority.class
evidence.class
runtime_dependency
sideband.requirement
descriptor.abi_status
typed_slot.policy_mode
fallback.policy
fallback.proof_id
legacy_translation.source_member
```

### Phase 09 — Tests, migration and exit

Move a minimal negative gate before any behavior change:

```text
Phase 09A — Early negative gates
- stale compiler contract rejects bridge package;
- `HasLegalAssignment` cannot map to runtime legality;
- helper success cannot map to production lowering;
- descriptor ABI success cannot map to execution authority;
- L7 descriptorless submit remains fail-closed;
- VMX/SecureCompute cannot emit backend carrier.

Phase 09B — Migration/cleanup gates
- all legacy ambiguous APIs obsolete or removed;
- full contour negative matrix passes;
- telemetry/evidence snapshots are stable.
```

## API types that should be added to the plan

Recommended namespace layout:

```text
HybridCPU.Compiler.Core.IR.Authority
HybridCPU.Compiler.Core.IR.Intent
HybridCPU.Compiler.Core.IR.Lowering
HybridCPU.Compiler.Core.IR.Artifacts
HybridCPU.Compiler.Core.IR.Bridge
HybridCPU.Compiler.Core.IR.Contours
HybridCPU.Compiler.Core.IR.Evidence
HybridCPU.Compiler.Core.IR.Diagnostics
```

Recommended new records/enums:

```text
CompilerAuthoritySourceKind
CompilerRuntimeAuthorityDependency
CompilerLoweringDecisionKind
CompilerEmissionClass
CompilerProductionLoweringStatus
CompilerArtifactKind
SidebandRequirement
CompilerPackageIdentity
CompilerEmissionPackage
CompilerArtifactSeparationProof
CompilerSemanticIntent
CompilerExecutionContourSelection
ContourAnalysisReport
LegacyApiTranslation
CompilerStructuralAdmissionReport
CompilerStructuralPlacementReport
CompilerPlacementFallbackProof
BridgeRuntimePolicyObservation
EvidenceOwnershipDomain
EvidenceAuthoritySemantics
```

Recommended rename/wrapper targets:

```text
IrBundleLegalityResult -> CompilerStructuralBundleAdmissionResult
IrCandidateBundleAnalysis.IsLegal -> IsStructurallyAdmissible
IrIssueSlotMask LegalSlots -> StructurallyAllowedSlots
HasLegalAssignment -> HasStructuralPlacement
EvaluateCandidateBundle -> AnalyzeStructuralCandidateBundle
EvaluateClusterPreparedLegality -> AnalyzeClusterPreparedStructuralAdmission
```

These names are recommendations. During migration, compatibility adapters may preserve old names internally with `[Obsolete]` and `LegacyApiTranslation` metadata.

## Contour audit requirements

### Native VLIW scalar/load-store/branch

Required behavior:

- intent classification separates scalar ALU, load/store and branch/control-flow;
- output may include carrier and optional sideband;
- hazard/slot/resource success is structural evidence only;
- runtime Legality A/B, commit, retire and publication remain runtime-owned;
- no fallback to Stream/DSC/L7/MatrixTile;
- branch/control-flow lane placement cannot be described as publication or commit.

### StreamEngine/vector

Required behavior:

- vector helper recovery must return typed helper or carrier decision;
- vector encoded instruction preservation is not a production Stream backend claim;
- shape/dtype/predicate/stride mismatch must reject or no-emit, not scalar fallback;
- sideband requirement must be explicit.

### MatrixTile helper-only

Required behavior:

- supported helper ops return `HelperOnlyDecision` or a carrier decision with `HelperAbiOnly` production status;
- unsupported op/dtype/shape/layout/accumulator rejects;
- helper success does not claim general matrix compiler, production lowering, or runtime execution authority;
- rejected MatrixTile must not fallback to scalar/vector/Stream.

### DSC / lane6

Required behavior:

- lane6 descriptor is transport descriptor evidence until runtime;
- DSC2 parser-only stays parser-only;
- missing commit/publication gate prevents any publication claim;
- rejected DSC cannot fallback to L7, Stream or scalar.

### L7-SDC / lane7

Required behavior:

- `ACCEL_SUBMIT` requires descriptor sideband;
- descriptorless submit remains fail-closed;
- token/capability observation is evidence only;
- rejected submit has no DSC/scalar fallback.

### VMX projection-only

Required behavior:

- VMX is compatibility/projection/no-emission boundary;
- VMCS is not state owner;
- VmxCaps is not authority;
- compiler cannot emit VMX backend carrier.

### SecureCompute admission-only

Required behavior:

- compiler may produce policy/admission/evidence envelope only;
- no secure backend execution claim;
- no nested commit/retire/publication claim;
- host-owned evidence never enters guest/domain architectural state.

## Additional required negative tests

Add these to `09_phase_tests_migration_and_exit.md` or a new dedicated test matrix file:

```text
1. `HybridCpuBundleLowerer.LowerBundle` returns carrier artifact only; no execution/publication/commit/retire authority.
2. `HybridCpuBundleLowerer.EmitFactsForBundle` produces typed-slot facts evidence only; no legality authority.
3. `HybridCpuCompiledProgram.EmitVliwBundleImage` writes an image but does not claim execution, publication, commit or retire.
4. `VliwBundleAnnotations.Empty` compatibility path never strengthens authority.
5. `IrAdmissibilityAgreement.TotalBundleCount` match is structural agreement only.
6. `IrCandidateBundleAnalysis.IsLegal` cannot be consumed as runtime Legality A/B.
7. `HybridCpuSlotModel.SearchAssignments(...).HasLegalAssignment` cannot map directly to bridge accepted/runtime legal.
8. MatrixTile `TryRecoverFromInstruction == true` is helper ABI recognition only.
9. VectorTransfer `TryRecoverFromInstruction == true` is helper/transport recognition only.
10. MatrixTile/vector recovery false must produce typed reject/parser/no-emission decision, not silent null semantics.
11. `ACCEL_SUBMIT` without `AcceleratorCommandDescriptor` remains fail-closed after refactor.
12. Lane6 `DmaStreamComputeDescriptor` on non-DSC opcode is rejected.
13. Lane6 and lane7 descriptors on same instruction are rejected.
14. Same-contour placement fallback from global search to local materialization is recorded as structural placement fallback, not lowering fallback.
15. Any cross-contour fallback without explicit policy fails.
16. Descriptor ABI valid status does not allow memory/register publication.
17. Bridge accepted status still requires runtime Legality A and B.
18. SecureCompute evidence envelope cannot be stored in guest/domain architectural state.
19. VMX projection cannot mutate VMCS ownership or emit backend carrier.
20. Capability observation cannot set `ProductionAllowed` unless an explicit compiler gate and runtime dependency are recorded.
```

## Migration risks not reflected enough

1. **Semantic drift of `legal` terminology.** Current Core uses `Legality` for structural scheduling checks. The refactor must quarantine this vocabulary before introducing bridge types.
2. **Aggregate artifact split risk.** `HybridCpuCompiledProgram` already carries multiple products. Splitting envelopes may break consumers unless adapter compatibility is explicit.
3. **Exception path migration.** Several invalid helper/descriptor cases throw. Replacing them with typed rejects changes error surfaces and test expectations.
4. **Placement fallback confusion.** Current bundler uses same-contour global/local search fallback. This must not be confused with forbidden cross-contour lowering fallback.
5. **Runtime contract duplication.** `CompilerContract` already owns typed-slot runtime policy vocabulary. Compiler types should reference/observe it, not redefine authority.
6. **Telemetry as policy leak.** Profile/certificate tie-break evidence may start influencing decisions; must remain advisory unless typed as explicit structural policy.
7. **Compatibility empty sideband.** Current annotations default to empty; the new envelope model must not treat missing sideband as proof of correctness.
8. **Helper encoded instruction preservation.** MatrixTile/vector lowerer paths may look like production lowering because they return encoded instructions. The decision API must label them as helper/ABI scoped.

## Revised phase structure

Recommended replacement structure:

```text
Phase 00 — README and invariant freeze
Phase 01 — Current behavior inventory and legacy ambiguity map
Phase 02 — Authority/evidence/publication taxonomy
Phase 03 — Early negative gates for authority boundaries
Phase 04 — Intent and contour classification in diagnostic mode
Phase 05 — Lowering decision API and legacy adapters
Phase 06 — Artifact envelope split: carrier/sideband/descriptor/facts/evidence
Phase 07 — Typed-slot bridge alignment with runtime CompilerContract
Phase 08 — Contour provider registry with fail-closed unknown contours
Phase 09 — Evidence/telemetry/audit snapshots
Phase 10 — Caller migration and legacy cleanup
Phase 11 — Exit criteria and ADR promotion
```

The most important reorder is early negative gates before behavior migration.

## Concrete patch proposal for existing plan files

### `00_README.md`

Add this file to the phase index and state that Phase 10 is a normative hardening addendum.

### `01_phase_inventory_and_freeze.md`

Add mandatory source inventory table with current files and symbols listed in this addendum.

### `02_phase_authority_taxonomy.md`

Add `CompilerAuthoritySourceKind`, `CompilerRuntimeAuthorityDependency`, and the legacy `Legal*` quarantine rule.

### `03_phase_ir_intent_and_contours.md`

Split `SemanticIntentClassification` into `CompilerSemanticIntent` and `CompilerExecutionContourSelection`. Add scalar/load-store/branch split.

### `04_phase_lowering_decision_api.md`

Add `CompilerLoweringDecisionKind`, `CompilerEmissionClass`, `CompilerProductionLoweringStatus`, `LegacyApiTranslation`, and required base fields.

### `05_phase_carrier_sideband_descriptor_abi.md`

Add `CompilerEmissionPackage`, `CompilerArtifactKind`, `SidebandRequirement`, and explicit compatibility adapter from `HybridCpuCompiledProgram`.

### `06_phase_typed_slot_and_legality_bridge.md`

Bind bridge semantics to observed runtime `CompilerTypedSlotPolicyMode`; explicitly state missing facts in compatibility mode are weaker, not stronger.

### `07_phase_contour_providers.md`

Split analyzer/provider interfaces and add same-contour placement fallback distinction.

### `08_phase_evidence_and_telemetry.md`

Add evidence ownership and authority-semantics enums. Require telemetry keys listed above.

### `09_phase_tests_migration_and_exit.md`

Split Phase 09 into early negative gates and migration/cleanup gates. Add all additional negative tests listed above.

## Final machine-checkable checklist

Refactor is not complete unless all conditions are true:

```text
[ ] Every public compiler lowering entrypoint returns CompilerLoweringDecision or explicit NoEmission decision.
[ ] No public compiler API exposes raw Success/Valid/Accepted/IsLegal/CanExecute without typed authority class.
[ ] All legacy bool/Try/HasLegalAssignment APIs have LegacyApiTranslation adapters or are obsolete/internal.
[ ] CompilerStructuralAdmissionResult cannot be assigned to runtime LegalityDecision.
[ ] Carrier, sideband, descriptor, typed-slot facts, agreement, bridge and evidence are separate envelope fields.
[ ] Descriptor ABI valid status never implies execution, publication, commit, retire or runtime legality.
[ ] Helper success never implies production lowering.
[ ] Parser success never implies production lowering.
[ ] L7 ACCEL_SUBMIT without descriptor sideband rejects fail-closed.
[ ] MatrixTile unsupported op/dtype/shape/layout/accumulator rejects with no fallback.
[ ] DSC rejected path has no L7/Stream/scalar fallback.
[ ] Unknown contour rejects or future-gates with no scalar fallback.
[ ] Same-contour placement fallback is recorded separately from lowering fallback.
[ ] Runtime bridge accepted status still requires runtime Legality A/B.
[ ] VMX remains projection/no-emission and cannot own VMCS state.
[ ] SecureCompute remains policy/admission/evidence-only and cannot emit secure backend execution.
[ ] Capability observation cannot become authority without explicit source and runtime dependency.
[ ] Host-owned evidence is blocked from guest/domain architectural state.
[ ] Telemetry contains decision.kind, contour.kind, authority.class, evidence.class, emission.class, production_lowering.status and fallback proof id.
[ ] Early negative gates pass before behavior migration starts.
[ ] Final ADRs document what each compiler product is and what authority it does not have.
```
