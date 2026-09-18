# CURRENT_BEHAVIOR - Compiler Core Semantic Inventory

Status: Phase 01 inventory snapshot, updated against the local source tree on
2026-07-08. This artifact freezes observed behavior before behavior migration.
It is historical context, not the current Phase 09 open-task list.

This document is not a design wish-list. It records what the current
`HybridCPU_Compiler/Core` produces today and what authority those products do not
have.

For the final Phase 09 compiler cleanup state, use
`CompilerRefDocs/README.md` and `PHASE09_REMAINING_COMPILER_WORK.md`.

## Invariant

```text
carrier != execution
execution != publication
publication != authority
authority != commit
commit != retire
retire != evidence
evidence != production lowering
```

## Current Core Symbol Map

| Symbol | File path | Namespace | Return / success shape | Produces today | Carrier | Sideband | Descriptor | Typed-slot facts | Structural agreement | Evidence / telemetry | Ambiguous terms | Runtime legality risk | Execution/publication/commit/retire risk | Current fallback | Same-contour structural fallback | Cross-contour fallback | Required wrapper / replacement | Required negative test id | Notes |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `HybridCpuCompiledProgram` | `HybridCPU_Compiler/Core/IR/Model/HybridCpuCompiledProgram.cs` | `HybridCPU.Compiler.Core.IR` | aggregate object; constructor throws on shape mismatch | schedule, bundle layout, lowered bundles, serialized image, contract version, optional emission address, agreement, annotations | yes, via `LoweredBundles` and `ProgramImage` | yes, via `LoweredBundleAnnotations` | only if annotations carry descriptor sideband | indirectly, through agreement and lowered facts elsewhere | yes, `IrAdmissibilityAgreement` | yes, agreement comments/summary | `Compiled`, `EmissionBaseAddress` | medium | high if aggregate is treated as executable package | empty annotations are filled for compatibility | none inside aggregate | none recorded | `CompilerEmissionPackage` projected by `ICompiledProgramEnvelopeAdapter` | `NEG-CARRIER-AGGREGATE-NO-AUTHORITY` | Existing comments correctly state descriptor sideband is transport evidence and agreement is not runtime legality. |
| `HybridCpuCompiledProgram.EmitVliwBundleImage` | same | same | returns `HybridCpuCompiledProgram`; throws through canonical emit on stale contract/runtime errors | writes/emits already materialized carrier image and records base address | yes | preserves existing sideband | preserves existing descriptor sideband | no new facts | preserves agreement | no new structured evidence | `Emit` | low | high: `Emit` can be misread as runtime publication | none | none | none | `EmitCarrierDecision` plus package metadata | `NEG-EMIT-IMAGE-NOT-PUBLICATION` | `EmissionBaseAddress` is compiler product metadata, not architectural publication/commit/retire. |
| `HybridCpuCompiledProgram.ValidateRuntimeContractCompatibility` | same | same | `void`; throws on contract mismatch | compatibility check against runtime-owned `CompilerContract.Version` | no | no | no | no | no | no | `Validate`, `Compatibility` | medium | medium: success can sound execution-ready | fail-closed stale version | none | none | `BridgeIngressStatus.VersionRejected` / contract observation | `NEG-STALE-CONTRACT-REJECTED` | Passing this check still requires runtime Legality A and B for executable candidates. |
| `HybridCpuBundleLowerer.LowerProgram` | `HybridCPU_Compiler/Core/IR/Bundling/HybridCpuBundleLowerer.cs` | `HybridCPU.Compiler.Core.IR` | `IReadOnlyList<VLIW_Bundle>` | backend-facing carrier objects in program order | yes | no | no direct descriptor envelope | no | no | no | `Lower` | medium | high: carrier can look executable | none | none | none | `EmitCarrierDecision` / `VliwCarrierEnvelope` | `NEG-LOWER-PROGRAM-CARRIER-ONLY` | Current behavior preserves MatrixTile/vector encoded helper instructions as carriers only. |
| `HybridCpuBundleLowerer.LowerBlock` | same | same | `IReadOnlyList<VLIW_Bundle>` | backend-facing carrier objects for one block | yes | no | no | no | no | no | `Lower` | medium | high | none | none | none | `VliwCarrierEnvelope` | `NEG-LOWER-BLOCK-CARRIER-ONLY` | No production lowering authority is created by the return type. |
| `HybridCpuBundleLowerer.LowerBundle` | same | same | `VLIW_Bundle` | one physical carrier bundle | yes | no | no direct descriptor envelope | no | no | no | `Lower` | medium | high | NOP fill for empty physical slots | same-contour slot padding only | none | `VliwCarrierEnvelope` | `NEG-LOWER-BUNDLE-CARRIER-ONLY` | Lane6/Lane7 descriptors are not emitted here; they are copied by annotation emission. |
| `HybridCpuBundleLowerer.EmitAnnotationsForProgram` | same | same | `IReadOnlyList<VliwBundleAnnotations>` | sideband annotations for each lowered bundle | no | yes | yes, descriptor references can be carried in metadata | no | no | sideband evidence only | `Emit` | medium | medium | empty annotations for compatibility | none | none | `CompilerSidebandEnvelope` | `NEG-SIDEBAND-NO-AUTHORITY` | Sideband absence may be compatibility-only, not proof of correctness. |
| `HybridCpuBundleLowerer.EmitAnnotationsForBundle` | same | same | `VliwBundleAnnotations` | per-slot metadata sideband | no | yes | yes: `DmaStreamComputeDescriptor`, `AcceleratorCommandDescriptor`, MatrixTile policies | no | no | sideband evidence only | `Emit` | medium | medium | default slot metadata for empty slots | none | none | `CompilerSidebandEnvelope` with `SidebandRequirement` | `NEG-EMPTY-SIDEBAND-NOT-AUTHORITY` | Descriptor sideband remains transport evidence and must be revalidated by runtime/projector. |
| `HybridCpuBundleLowerer.EmitFactsForBundle` | same | same | `TypedSlotBundleFacts` | typed-slot facts side channel | no | no | no | yes | no | structural evidence | `Emit`, `Facts` | high | medium | none | none | none | `TypedSlotFactsEnvelope` | `NEG-TYPED-FACTS-NOT-RUNTIME-LEGAL` | Facts are compiler/runtime structural agreement input only. |
| `HybridCpuIrBuilder.BuildProgram` | `HybridCPU_Compiler/Core/IR/Construction/HybridCpuIrBuilder.cs` | `HybridCPU.Compiler.Core.IR` | `IrProgram`; throws on invalid metadata/contour misuse | normalized IR from encoded instruction stream and optional sideband | no | consumes optional sideband | consumes descriptor sideband | no | no | structural observation | `Build` | medium | medium | sideband missing falls back to default slot metadata | compatibility metadata fallback only | none | `CompilerSemanticIntent` diagnostics | `NEG-BUILD-IR-NOT-LOWERING` | Helper recovery in `BuildInstruction` becomes nullable plans, not production lowering. |
| `HybridCpuIrBuilder.BuildInstruction` | same | same | private `IrInstruction`; throws on invalid inputs | single IR instruction and annotation | no | consumes optional slot metadata | consumes descriptor sideband | no | no | structural observation | `Build`, helper `TryRecover` | medium | medium | `TryRecoverFromInstruction == false` becomes `null` plan | parser/helper recognition fallback to normal IR only | none | intent/contour typed adapter | `NEG-RECOVERY-FALSE-TYPED-DECISION` | Future migration must replace silent null semantics with typed parser/helper/no-emission decisions at boundary. |
| `HybridCpuIrBuilder.ValidateExplicitAcceleratorIntent` | same | same | private `void`; throws fail-closed | descriptor misuse guard for lane6/lane7 | no | consumes sideband | validates descriptor presence/contour | no | no | negative guard evidence by exception only | `Validate` | low | medium | fail-closed exceptions | none | forbids lane6/lane7 mixing and descriptorless L7 | `RejectAtCompileTimeDecision` | `NEG-L7-DESCRIPTORLESS-SUBMIT` | `ACCEL_SUBMIT` without descriptor remains fail-closed. |
| `CompilerMatrixTileEmissionLowerer.Lower` | `HybridCPU_Compiler/Core/IR/Construction/CompilerMatrixTileEmissionLowerer.cs` | `HybridCPU.Compiler.Core.IR` | `CompilerMatrixTileEmissionPlan`; throws on malformed ABI or runtime-owned projection failure | scoped MatrixTile helper carrier plan | yes, encoded helper instruction | requires policy sideband for some ops | descriptor ABI inside request | no | no | helper/projection evidence | `Lower`, `Emission` | medium | high if treated as broad matrix compiler | explicit validation, no scalar/vector fallback | none | forbidden MatrixTile -> scalar/vector/Stream | `HelperOnlyDecision` or carrier decision with `HelperAbiOnly` | `NEG-MTILE-HELPER-NOT-PRODUCTION` | Existing plan flags `UsesScalarVectorDotOrBackendFallback=false`. |
| `CompilerMatrixTileEmissionLowerer.TryRecoverFromInstruction` | same | same | `bool` + out `CompilerMatrixTileEmissionPlan?` | helper ABI recognition/recovery | no new carrier | consumes policy sideband | reconstructs helper descriptor/request | no | no | parser/helper evidence | `Try`, `true` | high | high | false on non-MTile or projection fault | none | forbidden MatrixTile -> scalar/vector/Stream | `HelperOnlyDecision` + `LegacyApiTranslation` | `NEG-MTILE-TRYRECOVER-HELPER-ONLY` | `true` is recognition only; it is not production lowering. |
| `CompilerVectorTransferEmissionLowerer.Lower` | `HybridCPU_Compiler/Core/IR/Construction/CompilerVectorTransferEmissionLowerer.cs` | `HybridCPU.Compiler.Core.IR` | `CompilerVectorTransferEmissionPlan`; throws on unsupported shape/addressing | scoped direct vector-transfer helper plan | yes | no | shape/address ABI in request | no | no | helper evidence | `Lower`, `Emission` | medium | high if treated as broad vector backend | fail-closed for zero length/stride/indexed/2D | none | forbidden Stream/vector -> scalar | `HelperOnlyDecision` or scoped carrier decision | `NEG-VECTOR-HELPER-NOT-PRODUCTION` | Existing plan records no base/scalar/vector-dot fallback flags. |
| `CompilerVectorTransferEmissionLowerer.TryRecoverFromInstruction` | same | same | `bool` + out `CompilerVectorTransferEmissionPlan?`; throws for malformed positive opcode payload | helper/transport recognition | no new carrier | no | request shape ABI | no | no | helper evidence | `Try`, `true` | high | high | false for non-positive vector transfer opcode | none | forbidden Stream/vector -> scalar | `HelperOnlyDecision` / `ParserOnlyDecision` + `LegacyApiTranslation` | `NEG-VECTOR-TRYRECOVER-HELPER-ONLY` | `true` is helper/transport recovery, not production vector backend success. |
| `HybridCpuInstructionLegalityChecker.AnalyzeCandidateBundle` | `HybridCPU_Compiler/Core/IR/Hazards/HybridCpuInstructionLegalityChecker.cs` | `HybridCPU.Compiler.Core.IR` | `IrCandidateBundleAnalysis` | structural hazard, slot, capacity and resource analysis | no | no | no | no | no | structural admission evidence | `Legality`, `IsLegal` via result | high | medium | no lowering fallback | none | none | `CompilerStructuralAdmissionReport` | `NEG-ISCANDIDATE-LEGAL-NOT-RUNTIME-LEGAL` | This is compiler structural admission only. |
| `HybridCpuInstructionLegalityChecker.EvaluateCandidateBundle` | same | same | `IrBundleLegalityResult` | structural hazards for same-cycle candidate | no | no | no | no | no | structural admission evidence | `Legality`, `Legal` | high | medium | none | none | none | `AnalyzeStructuralCandidateBundle` | `NEG-IRBUNDLE-LEGAL-NOT-LEGALITYDECISION` | `IrBundleLegalityResult.Legal` means no compiler hazards, not runtime Legality A/B. |
| `HybridCpuInstructionLegalityChecker.EvaluateClusterPreparedLegality` | same | same | `IrBundleLegalityResult` | cluster-prepared structural hazards | no | no | no | no | no | structural admission evidence | `Legality`, `Legal` | high | medium | standard analysis reused first | same-contour structural tightening | none | `AnalyzeClusterPreparedStructuralAdmission` | `NEG-CLUSTER-LEGALITY-STRUCTURAL-ONLY` | Tightens compiler structural grouping; it does not execute or publish. |
| `HybridCpuBundleFormer.BundleProgram` | `HybridCPU_Compiler/Core/IR/Bundling/HybridCpuBundleFormer.cs` | `HybridCPU.Compiler.Core.IR` | `IrProgramBundlingResult`; throws when local materialization impossible | materialized bundle layout from schedule | no carrier bytes | no | no | no | no | placement/search evidence | `Bundle`, `Try`, `Legal` internally | high | medium | whole-program global placement then block/local materialization | yes | none | `CompilerStructuralPlacementReport` | `NEG-BUNDLE-PROGRAM-PLACEMENT-ONLY` | Fallback stays within structural placement. |
| `HybridCpuBundleFormer.BundleBlock` | same | same | `IrBasicBlockBundlingResult`; throws on illegal/no placement | materialized block bundle layout | no | no | no | no | no | placement evidence | `Legal` internally | high | medium | block lookahead, triplet, pair, local materialization | yes | none | `CompilerStructuralPlacementReport` | `NEG-BUNDLE-BLOCK-PLACEMENT-ONLY` | Does not lower carrier. |
| `HybridCpuBundleFormer.TryBundleProgramGlobally` | same | same | private `bool` + out result | optional program-wide placement | no | no | no | no | no | search summary | `Try`, `HasLegalAssignment` | high | medium | false falls back to block/local formation | yes | none | `CompilerPlacementFallbackProof` | `NEG-GLOBAL-PLACEMENT-FALLBACK-STRUCTURAL` | Same-contour structural fallback only. |
| `HybridCpuBundleFormer.TryMaterializeBlockGlobalLookahead` | same | same | private `bool` + out bundle | lookahead placement for first remaining block bundle | no | no | no | no | no | search summary | `Try`, `HasLegalAssignment` | high | medium | false falls through to other lookahead/local | yes | none | `CompilerPlacementFallbackProof` | `NEG-BLOCK-LOOKAHEAD-FALLBACK-STRUCTURAL` | Same semantic intent/contour. |
| `HybridCpuBundleFormer.TryMaterializeAdjacentBundleTripletLookahead` | same | same | private `bool` + out bundle | adjacent triplet placement search | no | no | no | no | no | search summary | `Try`, `HasLegalAssignment` | high | medium | false falls through to pair/local | yes | none | `CompilerPlacementFallbackProof` | `NEG-TRIPLET-FALLBACK-STRUCTURAL` | Not a contour lowering fallback. |
| `HybridCpuBundleFormer.TryMaterializeAdjacentBundlePair` | same | same | private `bool` + two out bundles | adjacent pair placement search | no | no | no | no | no | search summary | `Try`, `HasLegalAssignment` | high | medium | false falls through to local materialization | yes | none | `CompilerPlacementFallbackProof` | `NEG-PAIR-FALLBACK-STRUCTURAL` | Not a contour lowering fallback. |
| `HybridCpuSlotModel.GetLegalSlots` | `HybridCPU_Compiler/Core/IR/Hazards/HybridCpuSlotModel.Analysis.cs` | `HybridCPU.Compiler.Core.IR` | `IrIssueSlotMask` | structural allowed slot mask by resource class | no | no | no | no | no | structural observation | `LegalSlots` | high | low | default scalar for unhandled resource class | same-contour default structural model | potential Unknown -> scalar risk at contour layer | `StructurallyAllowedSlots` | `NEG-LEGAL-SLOTS-STRUCTURAL-ONLY` | Must not be used as contour selector authority. |
| `HybridCpuSlotModel.HasLegalAssignment` | same | same | `bool` | structural feasibility predicate | no | no | no | no | no | structural placement evidence | `HasLegalAssignment` | high | medium | none | none | none | `HasStructuralPlacement` / `CompilerStructuralPlacementReport` | `NEG-HASLEGALASSIGNMENT-NOT-RUNTIME-LEGAL` | Bare bool must be quarantined before new public API exposure. |
| `HybridCpuSlotModel.SearchAssignments` | `HybridCPU_Compiler/Core/IR/Hazards/HybridCpuSlotModel.BundleSearch.cs` | same | `IrBundlePlacementSearchResult` with `HasLegalAssignment` | placement candidates and best physical slots | no | no | no | no | no | placement/search summary | `Legal`, `HasLegalAssignment` | high | medium | empty result when no placement | yes | none | `CompilerStructuralPlacementReport` | `NEG-SEARCHASSIGNMENTS-STRUCTURAL-ONLY` | Best placement is not bridge accepted or executable. |
| `HybridCpuSlotModel.SearchProgramAssignments` | `HybridCPU_Compiler/Core/IR/Hazards/HybridCpuSlotModel.ProgramSearch.cs` | same | `IrProgramPlacementSearchResult` with `HasLegalAssignment` | scalable global program placement | no | no | no | no | no | placement/search summary | `Legal`, `HasLegalAssignment` | high | medium | empty result on failure | yes | none | `CompilerStructuralPlacementReport` | `NEG-SEARCHPROGRAM-STRUCTURAL-ONLY` | This is a bundler search surface only. |
| `HybridCpuSlotModel.SearchGlobalBasicBlockAssignments` | same | same | `IrGlobalBasicBlockPlacementSearchResult` with `HasLegalAssignment` | global block placement state | no | no | no | no | no | placement/search summary | `Legal`, `HasLegalAssignment` | high | medium | empty result on failure | yes | none | `CompilerStructuralPlacementReport` | `NEG-SEARCHGLOBALBLOCK-STRUCTURAL-ONLY` | Records retained placement states; no lowering authority. |
| `HybridCpuHazardModel.GetExecutionProfile` | `HybridCPU_Compiler/Core/IR/Hazards/HybridCpuHazardModel.cs` | `HybridCPU.Compiler.Core.IR` | `IrOpcodeExecutionProfile` | compiler-visible resource/latency/slot profile | no | no | no | no | no | resource expectation evidence | `ExecutionProfile`, `LegalSlots` | medium | high due `Execution` wording | fallback latency/resource defaults | structural resource fallback only | potential Unknown -> scalar risk at contour layer | `CompilerResourceExpectationEvidence` | `NEG-EXECUTIONPROFILE-NOT-EXECUTION` | Name is legacy; result is not execution authority. |
| `TelemetryProfileReader` | `HybridCPU_Compiler/Core/IR/Telemetry/TelemetryProfileReader.cs` | `HybridCPU.Compiler.Core.IR.Telemetry` | profile reader values, `TryResolveLoopProfile`, phase eligibility snapshots | advisory telemetry/profile data | no | no | no | no | no | yes | `Try`, profile/certificate terms | medium | medium | empty profile returns default/advisory values | scheduler tie-break only | none | `CompilerEvidenceEnvelope` | `NEG-TELEMETRY-NOT-POLICY-AUTHORITY` | Certificate/profile signals are advisory placement inputs only. |
| `CompilerBackendLoweringContract` | `HybridCPU_Compiler/Core/IR/Model/CompilerBackendLoweringContract.cs` | `HybridCPU.Compiler.Core.IR` | contract rows and capability helpers | backend capability vocabulary | no | no | no | no | no | capability observation | `Capability`, `ExecutableExperimental`, `ProductionExecutable`, `CanSelectForProductionLowering` | high | high | capability state helpers | none | possible if consumed as authority | `CompilerCapabilityObservation` | `NEG-CAPABILITY-OBSERVATION-NOT-AUTHORITY` | Must be quarantined as observation unless explicit authority source/runtime dependency is added. |
| `CompilerContract.ThrowIfVersionMismatch` | `HybridCPU_ISE/CloseToHSL/Core/Contracts/CompilerContract.cs` | `YAKSys_Hybrid_CPU.Core.Contracts` | `void`; throws on mismatch | runtime-owned contract compatibility check | no | no | no | no | no | runtime contract observation | `Version`, `Mismatch` | medium | medium | fail-closed mismatch | none | none | `BridgeIngressStatus.VersionRejected` | `NEG-STAGEAB-STILL-REQUIRED-AFTER-VERSION-PASS` | Runtime-owned; compiler may observe/reference only. |
| `CompilerTypedSlotPolicyMode` | same | same | enum | runtime-owned typed-slot policy vocabulary | no | no | no | no | no | runtime policy observation | `Policy`, `RequiredForAdmission` | medium | medium | compatibility mode accepts missing facts | none | none | `BridgeRuntimePolicyObservation` | `NEG-COMPILER-DOES-NOT-OWN-POLICY` | `RequiredForAdmission` is a future seam and not runtime selectable today. |
| `CompilerTypedSlotIngressAction` | same | same | enum | bridge ingress diagnostic action | no | no | no | no | no | bridge diagnostic evidence | `AcceptedMissingFacts`, `RecordedValidatedFacts` | high | medium | compatibility missing facts accepted | none | none | `BridgeAcceptanceReport` / `BridgeIngressStatus` | `NEG-BRIDGE-ACCEPTED-STILL-REQUIRES-AB` | Diagnostic ingress action is not runtime legality, execution ready, commit, retire or publication. |

## Legacy Ambiguous API Surfaces

Current compiler-side names that must be quarantined as structural-only before any
new public API exposes them:

```text
IsLegal
Legality
LegalSlots
HasLegalAssignment
Try*
Success
Valid
Accepted
Emit
Lower
Capability
CanExecute
ExecutionProfile
ProductionExecutable
ExecutableExperimental
```

Observed classes:

- `IsLegal`, `Legality`, `IrBundleLegalityResult.Legal`: structural admission.
- `LegalSlots`, `HasLegalAssignment`: structural placement.
- `TryRecoverFromInstruction`: parser/helper ABI recognition.
- `Emit*`: compiler product construction, never runtime publication.
- `Lower*`: carrier/helper construction, never production lowering by itself.
- `Capability*`, `CanSelect*`, `ProductionExecutable`: observation/gating vocabulary that must not become authority without explicit source and runtime dependency.

## Original Wrapper Targets (Historical Snapshot)

```text
HybridCpuCompiledProgram.EmitVliwBundleImage -> EmitCarrierDecision / CompilerEmissionPackage metadata
HybridCpuCompiledProgram.ValidateRuntimeContractCompatibility -> BridgeAcceptanceReport / VersionRejected
HybridCpuBundleLowerer.LowerProgram -> VliwCarrierEnvelope
HybridCpuBundleLowerer.LowerBlock -> VliwCarrierEnvelope
HybridCpuBundleLowerer.LowerBundle -> VliwCarrierEnvelope
HybridCpuBundleLowerer.EmitAnnotationsForProgram -> CompilerSidebandEnvelope
HybridCpuBundleLowerer.EmitAnnotationsForBundle -> CompilerSidebandEnvelope
HybridCpuBundleLowerer.EmitFactsForBundle -> TypedSlotFactsEnvelope
HybridCpuIrBuilder.BuildProgram -> CompilerSemanticIntent diagnostic adapter
HybridCpuIrBuilder.BuildInstruction -> CompilerSemanticIntent + typed parser/helper adapter
HybridCpuIrBuilder.ValidateExplicitAcceleratorIntent -> RejectAtCompileTimeDecision
CompilerMatrixTileEmissionLowerer.TryRecoverFromInstruction -> HelperOnlyDecision + LegacyApiTranslation
CompilerVectorTransferEmissionLowerer.TryRecoverFromInstruction -> HelperOnlyDecision/ParserOnlyDecision + LegacyApiTranslation
HybridCpuInstructionLegalityChecker.AnalyzeCandidateBundle -> CompilerStructuralAdmissionReport
HybridCpuInstructionLegalityChecker.EvaluateCandidateBundle -> AnalyzeStructuralCandidateBundle
HybridCpuInstructionLegalityChecker.EvaluateClusterPreparedLegality -> AnalyzeClusterPreparedStructuralAdmission
HybridCpuBundleFormer.BundleProgram -> CompilerStructuralPlacementReport
HybridCpuBundleFormer.BundleBlock -> CompilerStructuralPlacementReport
HybridCpuBundleFormer.TryBundleProgramGlobally -> CompilerPlacementFallbackProof
HybridCpuBundleFormer.TryMaterializeBlockGlobalLookahead -> CompilerPlacementFallbackProof
HybridCpuBundleFormer.TryMaterializeAdjacentBundleTripletLookahead -> CompilerPlacementFallbackProof
HybridCpuBundleFormer.TryMaterializeAdjacentBundlePair -> CompilerPlacementFallbackProof
HybridCpuSlotModel.SearchAssignments -> CompilerStructuralPlacementReport
HybridCpuSlotModel.SearchProgramAssignments -> CompilerStructuralPlacementReport
HybridCpuSlotModel.SearchGlobalBasicBlockAssignments -> CompilerStructuralPlacementReport
HybridCpuHazardModel.GetExecutionProfile -> CompilerResourceExpectationEvidence
TelemetryProfileReader -> CompilerEvidenceEnvelope
CompilerBackendLoweringContract -> CompilerCapabilityObservation
CompilerContract.ThrowIfVersionMismatch -> BridgeIngressStatus.VersionRejected
CompilerTypedSlotPolicyMode -> BridgeRuntimePolicyObservation
CompilerTypedSlotIngressAction -> BridgeIngressStatus / BridgeAcceptanceReport
```

## Methods Requiring Rename Or Deprecation

Recommended compatibility names for migration:

```text
IrBundleLegalityResult -> CompilerStructuralBundleAdmissionResult
IrCandidateBundleAnalysis.IsLegal -> IsStructurallyAdmissible
IrIssueSlotMask LegalSlots -> StructurallyAllowedSlots
HasLegalAssignment -> HasStructuralPlacement
EvaluateCandidateBundle -> AnalyzeStructuralCandidateBundle
EvaluateClusterPreparedLegality -> AnalyzeClusterPreparedStructuralAdmission
HybridCpuHazardModel.GetExecutionProfile -> GetCompilerResourceExpectation
CompilerBackendCapabilityState -> CompilerCapabilityObservationState
CanSelectForProductionLowering -> ObservesProductionGateOnly / typed decision adapter
```

Do not remove these in Phase 01. Add wrappers/adapters first, then migrate callers.

## Same-Contour Structural Fallbacks Observed Today

These are currently allowed structural placement retries. They must be recorded
separately from lowering fallback:

```text
BundleProgram:
  whole-program global placement
  -> per-block BundleBlock materialization

BundleBlock:
  block-global lookahead
  -> adjacent triplet lookahead
  -> adjacent pair lookahead
  -> local MaterializeBundle

MaterializeBundle:
  class-first deterministic lane binding
  -> exhaustive slot search
```

Constraints for preserving these in later phases:

```text
same semantic intent
same execution contour
same sideband requirement
same descriptor requirement
same emission class
same authority class
same runtime dependency
```

## Forbidden Cross-Contour Fallbacks

The current inventory records no approved cross-contour lowering fallback.
The following must stay forbidden until an explicit reviewed `FallbackPolicy`
exists:

```text
MatrixTile -> scalar/vector/Stream
Stream/vector -> scalar
DSC/lane6 -> L7/Stream/scalar
L7-SDC/lane7 -> DSC/Stream/scalar
VMX projection -> native/backend emission
SecureCompute admission -> secure backend execution
Unknown contour -> scalar
Descriptor parser success -> execution/publication/commit/retire
Helper success -> production lowering
Typed-slot facts -> runtime legality
Bridge ingress accepted -> execution ready
Capability observation -> authority
```

## Required Early Negative Gates

Minimum test IDs attached to this inventory:

```text
NEG-STALE-CONTRACT-REJECTED
NEG-HASLEGALASSIGNMENT-NOT-RUNTIME-LEGAL
NEG-ISCANDIDATE-LEGAL-NOT-RUNTIME-LEGAL
NEG-IRBUNDLE-LEGAL-NOT-LEGALITYDECISION
NEG-MTILE-TRYRECOVER-HELPER-ONLY
NEG-VECTOR-TRYRECOVER-HELPER-ONLY
NEG-HELPER-SUCCESS-NOT-PRODUCTION-LOWERING
NEG-DESCRIPTOR-ABI-NOT-EXECUTION-AUTHORITY
NEG-L7-DESCRIPTORLESS-SUBMIT
NEG-VMX-BACKEND-EMISSION-FORBIDDEN
NEG-SECURECOMPUTE-EMISSION-FORBIDDEN
NEG-BRIDGE-ACCEPTED-STILL-REQUIRES-AB
NEG-CARRIER-AGGREGATE-NO-AUTHORITY
NEG-TYPED-FACTS-NOT-RUNTIME-LEGAL
NEG-TELEMETRY-NOT-POLICY-AUTHORITY
NEG-CAPABILITY-OBSERVATION-NOT-AUTHORITY
```

## Phase 01 Exit Status

```text
[x] Mandatory symbols inventoried.
[x] `Legal*` / `IsLegal` / `HasLegalAssignment` surfaces marked structural-only.
[x] Helper/parser success marked not production lowering.
[x] Descriptor success marked ABI/evidence only.
[x] Current aggregate products mapped to target envelopes.
[x] Same-contour structural fallback separated from forbidden cross-contour fallback.
[x] Every mandatory row points to a negative gate id.
```
