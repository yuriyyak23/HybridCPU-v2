# Phase 09 Cleanup / Migration Readiness

Status: implemented 2026-07-08.

This slice audits public compiler lowering and validation surfaces for raw
`Success`/`Valid`/`Accepted`/`IsLegal`-style authority leaks before migrating
callers. It does not change scheduling, bundling, lowering, descriptor, bridge,
or runtime behavior.

## Compatibility Strategy

The cleanup follows the compatible Phase 09 order:

```text
observe -> wrap -> early negative gates -> type decisions -> migrate callers -> remove legacy ambiguity
```

The legacy APIs remain callable for existing code, but public ambiguous names
are now marked as migration-only and are paired with typed replacements or
`LegacyApiTranslation` adapters.

## Quarantined Legacy Surfaces

The following compiler-side names are now explicitly obsolete as authority
surfaces:

| Legacy surface | Replacement / wrapper | Authority semantics |
|---|---|---|
| `IrCandidateBundleAnalysis.IsLegal` | `CompilerStructuralAuthorityQuarantine.FromCandidateBundleAnalysis` | structural admission evidence only |
| `IrBundleLegalityResult.IsLegal` | `CompilerStructuralAuthorityQuarantine.FromBundleLegalityResult` | structural admission evidence only |
| `IrBundleLegalityResult.Legal` | `CompilerStructuralAuthorityQuarantine.FromBundleLegalityResult` | no compiler hazards only |
| `IrSlotAssignmentAnalysis.HasLegalAssignment` | `CompilerStructuralPlacementReport.HasStructuralPlacement` | structural placement evidence only |
| `IrSlotAssignmentAnalysis.CombinedLegalSlots` | `CompilerStructuralPlacementReport.StructurallyAllowedSlots` | structural slot facts only |
| `IrSlotAssignmentAnalysis.InstructionLegalSlots` | `CompilerStructuralPlacementReport.InstructionStructurallyAllowedSlots` | structural slot facts only |
| `IrInstructionAnnotation.LegalSlots` | `IrInstructionAnnotation.StructurallyAllowedSlots` | structural slot facts only |
| `IrOpcodeExecutionProfile.LegalSlots` | `IrOpcodeExecutionProfile.StructurallyAllowedSlots` | structural slot facts only |
| `IrTypedSlotAdmissionDescriptor.LegalSlots` | `IrTypedSlotAdmissionDescriptor.StructurallyAllowedSlots` | structural slot facts only |
| `HybridCpuSlotModel.GetLegalSlots(...)` | `GetStructurallyAllowedSlots(...)` | resource-class structural slot mask only |
| `HybridCpuSlotModel.HasLegalAssignment(...)` | `HasStructuralPlacement(...)` | structural placement evidence only |
| `HybridCpuSlotModel.AnalyzeAssignment(...)` | `AnalyzeStructuralAssignment(...)` | structural placement feasibility only |
| `HybridCpuSlotModel.MaterializeAssignment(...)` | `MaterializeStructuralAssignment(...)` | structural slot placement artifact only |
| `HybridCpuSlotModel.Search*Assignments(...)` | `Search*StructuralAssignments(...)` | structural placement search artifact only |
| `IrBundlePlacementQuality.Create(... legalSlots ...)` | `CreateForStructuralSlotFacts(...)` | placement quality over structural slot facts only |
| `IrMaterializedBundleSlot.InstructionLegalSlots` | `InstructionStructurallyAllowedSlots` | materialized structural slot fact only |
| `IrMaterializedBundleSlot.IsLegalPlacement` | `IsStructuralPlacement` | materialized structural placement check only |
| `HybridCpuInstructionLegalityChecker.EvaluateCandidateBundle` | `AnalyzeStructuralCandidateBundle` | typed structural admission result |
| `HybridCpuInstructionLegalityChecker.EvaluateClusterPreparedLegality` | `AnalyzeClusterPreparedStructuralAdmission` | typed cluster structural admission result |
| `CompilerBackendLoweringDecision.IsAllowed` | `CompilerLoweringDecision.FromLegacyBackendLoweringDecision` | capability observation/evidence only |
| `CompilerBackendLoweringContract.CanSelect*` / `Allows*` | typed lowering decision or capability observation | observation only, not capability authority |
| `CompilerArtifactValidationResult.IsValid` | inspect `AuthorityClass`, `EvidenceClass`, `ExecutionClaim` | envelope validation only |
| `EvidenceIsolationValidationResult.IsValid` | inspect isolation diagnostics and evidence semantics | isolation validation only |
| `HybridCpuCompilerDirectives.DirectiveParseResult.Success` | `IsDirectiveParsed` / `CompilerDirectiveParseObservation` | frontend parser evidence only; no lowering, runtime legality, execution, publication, commit or retire authority |
| `CompilerVmxPreflightResult.Success` | `ProjectionPreflightPassed` | VMX projection/preflight evidence only; no VMX execution, VMCS ownership, commit, retire or publication authority |
| MatrixTile memory/numeric/layout/semantic `IsValid` | `IsMemoryShapeAbiAccepted`, `IsRuntimeOwnedNumericPolicyAccepted`, `IsRuntimeOwnedLayoutPolicyAccepted`, `IsSemanticAbiAccepted` | domain-local ABI/semantic acceptance only |
| `CompilerMatrixTileEmissionLowerer.Lower(...)` | `CompilerPositiveEmissionResult<CompilerMatrixTileEmissionPlan>` via `LowerWithDecision(...)` | positive helper carrier plan is an artifact only; runtime legality, execution, commit, retire and publication remain required |
| MatrixTile `HybridCpuThreadCompilerContext.Compile*` plan-returning methods | `Compile*WithDecision(...)` | obsolete compatibility shims over decision-bearing positive helper emission |
| `DmaStreamComputeValidationResult.IsValid` in compiler facade | `IsDescriptorAbiAccepted` | descriptor/parser admission only, execution remains disabled |
| `HybridCpuThreadCompilerContext` public methods | `HybridCpuThreadCompilerFacadeAudit` | every public facade is classified as typed boundary, compatibility facade, artifact observation, metadata boundary, or state mutation |
| DSC/L7 compiler facade guard `IsAllowed` reads | `CompilerRuntimeGuardObservation` | runtime-owned guard/admission observation only; no compiler runtime legality, execution, publication, commit, retire, or production authority |
| `CompilerVectorTransferEmissionLowerer.Lower(...)` | `CompilerPositiveEmissionResult<CompilerVectorTransferEmissionPlan>` via `LowerWithDecision(...)` | positive helper/transport carrier plan is an artifact only; runtime legality, execution, commit, retire and publication remain required |
| VectorTransfer `HybridCpuThreadCompilerContext.Compile*` plan-returning methods | `Compile*WithDecision(...)` | obsolete compatibility shims over decision-bearing positive helper emission |
| `DmaStreamComputeStructuralReadResult.IsValid` in DSC descriptor tests/runtime parser | `IsStructuralDescriptorReadAccepted` | structural owner-binding read only; descriptor ABI and execution remain unaccepted |
| `DmaStreamComputeDsc2ValidationResult.IsParserAccepted` in DSC2 tests | keep typed parser-only predicate | DSC2 parser/footprint acceptance only; execution, token issue, memory publication, and production lowering remain disabled |
| `AcceleratorDescriptorValidationResult.IsValid` in L7-SDC parser tests | `IsDescriptorAbiAccepted` | descriptor/parser ABI acceptance only, no token/execution authority |
| `DataTypeUtils.IsValid` in compiler positive-emission ABI contracts | `IsKnownMatrixTileElementType`, `IsKnownVectorElementType` | primitive enum-shape validation only |
| `ValidationResult.IsValid` in metadata compatibility tests | `IsMetadataSchemaCompatible` | compiler-emitted metadata schema compatibility only; not legality or execution authority |

Runtime-local `IsValid` surfaces retained after classification:

| Runtime-local surface | Classification | Why no typed compiler predicate was added |
|---|---|---|
| `VmxDmaDescriptorValidationResult.IsValid` | VMX runtime DMA descriptor materialization result | host-owned Lane6/IOMMU evidence path; compiler source must not reference it |
| `AcceleratorTokenHandle.IsValid` / L7 token handle `IsValid` | runtime token handle identity | opaque token identity only; descriptor/parser acceptance uses `IsDescriptorAbiAccepted` |
| MatrixTile policy/replay/capture identity `IsValid` | runtime correlation identity | not a compiler lowering validation result |
| nested VMX projection/domain `IsValid` | runtime projection/admission identity | VMX compiler layer remains projection/no-emission and does not own VMCS/runtime admission |
| memory/IOMMU/domain `IsValid` (`DmaWindowDescriptor`, `IommuDomainBinding`, `DomainValidationResult`) | runtime descriptor/domain validation | runtime-owned domain/IO validation only; compiler source must not reference these surfaces |
| event/completion routing `IsValid` (`LaneCompletionDescriptor`, `EventInjectionDescriptor`, `MemoryTrapRange`) | runtime routing/posted-event/trap descriptor validation | runtime event/completion routing only; not compiler publication or execution authority |
| assist transport `IsValid` (`AssistInterCoreTransport`) | runtime assist transport eligibility | assist runtime transport only; compiler source must not consume it |
| pipeline slot descriptor `IsValid` (`DecodedBundleDescriptor` slot/FSP slot descriptors) | runtime decode/FSP occupancy and slot descriptor validity | runtime pipeline-local stage/slot state only; not compiler structural placement or runtime legality |

## Typed Adapter

`CompilerLoweringDecision.FromLegacyBackendLoweringDecision` maps legacy backend
bool decisions into typed compiler lowering decisions with:

- `LegacyApiTranslation.StrengthensAuthority == false`
- `CompilerEmissionClass.EvidenceOnly`
- `CompilerPublicationClass.EvidenceOnly`
- `CompilerExecutionClaim.NoExecutionClaim`
- runtime Legality A/B, execution, commit, retire, and publication dependencies
  explicit
- cross-contour fallback forbidden

Even a legacy `IsAllowed == true` is represented as a future-gated capability
observation, not production execution authority.

## Negative Gates

Added:

```text
HybridCPU_ISE.Tests/CompilerTests/CompilerPhase09CleanupMigrationReadinessTests.cs
```

The tests verify:

- legacy structural legality/placement names are obsolete and have typed wrappers
- legacy `LegalSlots` model/mask surfaces are obsolete and expose structural
  aliases
- legacy SlotModel assignment/search/materialization and placement-quality
  methods are obsolete and expose structural slot-fact entrypoints
- materialized bundle slots expose structural placement aliases, and bundle
  validation reads structural placement rather than `IsLegalPlacement`
- old `Evaluate*Legality` entrypoints are obsolete
- new structural entrypoints return `CompilerStructuralBundleAdmissionResult`
- backend bool helper surfaces are obsolete
- backend bool decisions adapt through `LegacyApiTranslation`
- envelope/evidence `IsValid` predicates are obsolete and must be interpreted
  with authority/evidence fields
- public `CompilerLoweringDecision` types do not expose bare
  `Success`/`Valid`/`Accepted`/`IsLegal`/`CanExecute` bool names
- migrated schedulers and bundler do not read `.IsLegal` or
  `.HasLegalAssignment` directly
- migrated IR construction, hazard analysis, bundling, late lane binding and
  local-list scheduling read `StructurallyAllowedSlots` or
  `GetStructurallyAllowedSlots(...)` instead of direct `Annotation.LegalSlots`
  or `GetLegalSlots(...)`
- migrated bundling and hazard analysis call structural SlotModel entrypoints
  instead of legacy `AnalyzeAssignment`, `MaterializeAssignment`, or
  `Search*Assignments` methods
- backend lowering evaluator no longer self-calls legacy
  `CanSelectForProductionLowering(request.State)`
- `CompilerLoweringDecision.FromLegacyBackendLoweringDecision` reads the
  internal observation value, not public legacy `IsAllowed`
- MatrixTile and VectorTransfer helper recovery return typed
  `CompilerHelperRecoveryResult<TPlan>` records at the compiler boundary
- `CompilerHelperRecoveryResult<TPlan>` lives in its own lowering result file,
  not inside `CompilerLoweringDecision.cs`
- helper/parser recovery decisions use `LegacyApiTranslation` and cannot
  become production lowering, execution, publication, commit, or retire
- MatrixTile and VectorTransfer positive helper emission return typed
  `CompilerPositiveEmissionResult<TPlan>` records with
  `CompilerLoweringDecision`
- positive helper carrier plans remain artifacts and cannot become runtime
  legality, execution-ready, publication, commit, retire, or final production
  authority
- compatibility facades call `Compile*WithDecision(...)` before appending the
  carrier, while raw plan-returning `Compile*` methods remain obsolete shims
- every public declared `HybridCpuThreadCompilerContext` method has a
  `HybridCpuThreadCompilerFacadeAudit` classification with no-authority
  semantics
- runtime-owned DSC/L7 guard reads in `HybridCpuThreadCompilerContext` are
  wrapped as `CompilerRuntimeGuardObservation`, not consumed as compiler
  authority
- directive parse success is exposed as `IsDirectiveParsed` and
  `CompilerDirectiveParseObservation`, while legacy `Success` is obsolete and
  no compiler parser caller reads `.Success`
- VMX preflight uses `ProjectionPreflightPassed`, while legacy `Success` is
  obsolete and remains projection/preflight evidence only
- final public API scan allowlists only deliberate obsolete compatibility
  shims for bare `Success`, `Valid`, `Accepted`, `IsLegal`, `CanExecute`, raw
  bool lowering and plan-only public lowering surfaces
- artifact and evidence validation consumers use typed predicates instead of
  bare `IsValid`
- MatrixTile compiler lowerer uses domain-local acceptance predicates instead
  of bare `IsValid` for memory shape, numeric policy, layout policy, and
  semantic ABI checks
- DSC compiler facade uses `IsDescriptorAbiAccepted` instead of bare `IsValid`
  before requiring the descriptor for compiler admission
- DSC descriptor, domain-guard, telemetry, footprint, token, and typed-slot tests
  use `IsDescriptorAbiAccepted` for v1 descriptor ABI acceptance,
  `IsStructuralDescriptorReadAccepted` for owner-binding structural reads, and
  `IsParserAccepted` for DSC2 parser-only/footprint acceptance
- MatrixTile and VectorTransfer positive-emission ABI contracts use named
  primitive datatype predicates instead of `DataTypeUtils.IsValid`
- MatrixTile runtime projection/capture/retire helpers that are exercised by
  compiler-facing helper recovery paths consume typed memory/numeric/layout/
  semantic acceptance predicates instead of validation-result `IsValid`
- L7-SDC descriptor parser result exposes `IsDescriptorAbiAccepted`; Phase 09
  descriptor/evidence tests use it instead of parser-result `IsValid`
- metadata compatibility validation exposes `IsMetadataSchemaCompatible`; tests
  use it instead of `ValidationResult.IsValid`
- VMX DMA descriptor validation, token handles, and nested/projection identity
  predicates remain runtime-local; compiler source is guarded against consuming
  those `IsValid` members as authority
- memory/IOMMU/domain validation, event/completion routing validation, assist
  transport validation, and pipeline slot descriptor validation remain
  runtime-local; compiler source is guarded against consuming those types or
  their `.IsValid` predicates as authority

## Caller Migration Completed

This follow-up slice completed the first caller migration batch:

- `HybridCpuProgramOrderLocalScheduler` uses
  `IrCandidateBundleAnalysis.IsStructurallyAdmissible`
- `HybridCpuLocalListScheduler` uses
  `IrCandidateBundleAnalysis.IsStructurallyAdmissible`
- `HybridCpuBundleFormer` uses structural admission and structural placement
  predicates
- `HybridCpuInstructionLegalityChecker` uses
  `IrBundleLegalityResult.StructurallyAdmissible` and structural slot predicates
- `CompilerStructuralAuthorityQuarantine` projects from structural members
- `HybridCpuSlotModel` internal search paths use structural placement members
- `CompilerBackendLoweringContract` uses private
  `IsProductionExecutableState` for self-evaluation
- `CompilerLoweringDecision.FromLegacyBackendLoweringDecision` uses internal
  `IsAllowedObservation`
- `HybridCpuIrBuilder` uses `RecoverFromInstruction` typed helper recovery
  results instead of legacy `TryRecoverFromInstruction` bools
- `CompilerHelperRecoveryResult<TPlan>` is split into
  `Core/IR/Lowering/CompilerHelperRecoveryResult.cs` while preserving namespace
  and public behavior
- `CompilerMatrixTileEmissionLowerer.TryRecoverFromInstruction` and
  `CompilerVectorTransferEmissionLowerer.TryRecoverFromInstruction` are
  obsolete compatibility shims over typed helper/parser recovery decisions
- `CompilerLoweringDecision.FromLegacyHelperRecoveryBool` records
  helper/parser recovery through `LegacyApiTranslation` with
  `StrengthensAuthority == false`
- `CompilerLoweringDecision.FromPositiveHelperEmission` records positive
  MatrixTile/VectorTransfer helper carrier emission as `HelperAbiOnly` /
  `CarrierCandidate` with runtime legality A/B, execution, commit, retire and
  publication dependencies still required
- `CompilerPositiveEmissionResult<TPlan>` carries the plan artifact beside the
  decision; the plan itself is not authority
- `CompilerMatrixTileEmissionLowerer.LowerWithDecision` and
  `CompilerVectorTransferEmissionLowerer.LowerWithDecision` preserve the
  emitted carrier plans and wrap them with positive helper decisions
- `HybridCpuThreadCompilerContext.CompileMtile*WithDecision`,
  `CompileMtransposeWithDecision`, `CompileVloadWithDecision`, and
  `CompileVstoreWithDecision` are the typed public positive-emission facades;
  the older plan-returning methods are obsolete compatibility shims
- `HybridCpuThreadCompilerFacadeAudit` classifies the public thread context
  surface and readiness tests require every public method to have an explicit
  boundary classification
- `CompilerRuntimeGuardObservation` wraps DSC owner guard, L7 descriptor owner
  guard, and L7 submit guard observations with `RuntimeOwnedPolicyReference`,
  `RuntimeContractObservationEvidence`, `NoExecutionClaim`, `EvidenceOnly`,
  and runtime legality/execution/commit/retire/publication dependencies
- `HybridCpuThreadCompilerContext` no longer reads
  `.OwnerGuardDecision.IsAllowed` or `submitGuard.IsAllowed` directly; fail-
  closed behavior is preserved through the typed observation
- `IrInstructionAnnotation`, `IrOpcodeExecutionProfile`, and
  `IrTypedSlotAdmissionDescriptor` now expose `StructurallyAllowedSlots` as
  structural fact aliases; the old `LegalSlots` members remain obsolete
  compatibility properties
- `HybridCpuSlotModel.GetStructurallyAllowedSlots(...)` is the typed
  resource-class mask entrypoint; `GetLegalSlots(...)` remains an obsolete
  compatibility shim
- `HybridCpuSlotModel.HasStructuralPlacement(...)`,
  `AnalyzeStructuralAssignment(...)`, `MaterializeStructuralAssignment(...)`,
  and `Search*StructuralAssignments(...)` are the typed structural SlotModel
  entrypoints; legacy legal/assignment/search names remain obsolete
  compatibility shims.
- Unused internal legacy SlotModel overloads were removed after migrated Core
  callers moved to structural entrypoints.
- `IrBundlePlacementQuality.CreateForStructuralSlotFacts(...)` replaces the
  three-argument `Create(... legalSlots ...)` placement-quality surface.
- `IrMaterializedBundleSlot.InstructionStructurallyAllowedSlots` and
  `IsStructuralPlacement` replace the legacy materialized bundle slot wording;
  `IrMaterializedBundle` validates structural placement through the new alias.
- `HybridCpuCompilerDirectives.DirectiveParseResult.Success` remains an
  obsolete compatibility alias; `IsDirectiveParsed` and
  `CompilerDirectiveParseObservation` classify directive parsing as
  parser/negative-gate evidence only with `NoExecutionClaim`, `EvidenceOnly`,
  and `NoRuntimeActionBecauseNoEmission`
- `CompilerVmxPreflightResult.ProjectionPreflightPassed` replaces the legacy
  bare `Success` singleton; the old `Success` property remains an obsolete
  compatibility alias and is not consumed by VMX preflight evaluation.
- The final public API scan is now encoded as readiness coverage: remaining
  bare authority-like names are deliberate obsolete shims with typed
  replacements, and raw MatrixTile/VectorTransfer plan facades are obsolete
  shims over decision-bearing APIs.
- `CompilerArtifactValidationResult.IsAuthorityScopedValidation` replaces
  public caller reads of `IsValid` for carrier/sideband/descriptor/facts/package
  validation
- `EvidenceIsolationValidationResult.IsEvidenceIsolated` and
  `HasIsolationViolations` replace public caller reads of `IsValid` for
  evidence isolation checks
- MatrixTile validation result structs remain runtime/domain validation
  surfaces, but compiler-side callers now consume authority-neutral predicates:
  `IsMemoryShapeAbiAccepted`, `IsRuntimeOwnedNumericPolicyAccepted`,
  `IsRuntimeOwnedLayoutPolicyAccepted`, and `IsSemanticAbiAccepted`
- `DmaStreamComputeValidationResult.IsDescriptorAbiAccepted` replaces the
  compiler facade read of descriptor parser `IsValid`; the result remains
  descriptor ABI/admission evidence and does not grant execution, publication,
  commit, or retire authority
- DSC runtime-local tests now classify v1 parser results as descriptor ABI
  acceptance, structural owner reads as structural read acceptance, and DSC2
  normalized footprint parsing as parser-only acceptance. These predicates do
  not publish memory/register state and do not imply runtime legality,
  execution, commit, retire, or compiler production lowering.
- `CompilerMatrixTileDescriptorAbi.IsKnownMatrixTileElementType` and
  `CompilerVectorTransferShapeAbi.IsKnownVectorElementType` classify only
  architecture-known `DataTypeEnum` values. They are enum-shape checks, not
  helper success, production lowering, runtime legality, or capability
  authority.
- MatrixTile runtime identity and replay handles keep their local `IsValid`
  naming because they are runtime correlation identities, not compiler lowering
  validation results. MatrixTile memory/numeric/layout/semantic validation
  result consumers in the projection/capture/retire path use the typed
  acceptance predicates.
- L7-SDC token handles keep `Handle.IsValid` as runtime handle identity
  validity. L7-SDC descriptor parser results use `IsDescriptorAbiAccepted`
  because descriptor acceptance is parser/ABI evidence only and does not grant
  token, execution, publication, commit, retire, or runtime legality authority.
- `ValidationResult.IsMetadataSchemaCompatible` replaces metadata compatibility
  test reads of `IsValid`. This accepts current/compatible schema versions only
  as metadata shape compatibility and does not imply runtime legality,
  execution, publication, commit, retire, or production lowering authority.
- `VmxDmaDescriptorValidationResult.IsValid` remains a runtime-owned VMX DMA
  descriptor materialization predicate over host-owned Lane6/IOMMU evidence.
  It is not compiler-facing and the readiness gate asserts compiler source does
  not reference `VmxDmaDescriptorValidator` or
  `VmxDmaDescriptorValidationResult`.
- L7 token handle identity, nested projection identity, and VMX retire
  projection internals retain local `IsValid` naming because they are runtime
  identity/admission checks. The compiler readiness gate asserts these surfaces
  are not consumed by compiler Core.
- Memory/IOMMU/domain validation, event/completion routing, assist transport
  and pipeline slot descriptor `IsValid` predicates retain local runtime naming
  because they are descriptor, routing, transport, occupancy or pipeline-local
  validation checks. No compiler typed predicate was added because the compiler
  does not consume these surfaces.

The public legacy members remain for compatibility and remain marked obsolete.
They are not used by the migrated compiler Core path.

## Latest Validation

Post-runtime-local-validation-only-audit gates run on 2026-07-09:

- `CompilerPhase09CleanupMigrationReadinessTests`: 30/30
- `CompilerMatrixTilePositiveEmissionTests` plus
  `CompilerVectorTransferPositiveEmissionTests`: 16/16
- wide Phase 09 authority/negative matrix slice set: 175/175

## Remaining Migration Work

No compiler-code cleanup or validation-only target remains in this readiness
backlog. ADR-style documentation/promotion is complete in `CompilerRefDocs`.
