# CURRENT_BEHAVIOR — Compiler Core Semantic Inventory

Статус: inventory template required by Phase 01.

This document is intentionally a planning artifact. It records the minimum current Core surfaces that must be classified before behavior migration starts. The inventory must be completed against the current `master` code before Phase 02 implementation PRs begin.

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

Every row below must state what the current symbol produces and what authority it does **not** have.

## Classification vocabulary

Use only these current-behavior classes unless Phase 02 adds a stricter one:

```text
StructuralObservation
StructuralAdmissionEvidence
StructuralPlacementEvidence
TransportConstruction
CompilerEvidence
RuntimeBridgeInput
RuntimeAuthorityRequired
ParserOnly
HelperOnly
NoEmission
FutureGated
```

## Required columns

```text
Symbol
File path
Namespace
Current return shape
Current success/failure shape
Creates carrier bytes/objects?
Creates sideband?
Creates descriptor?
Creates typed-slot facts?
Creates structural agreement?
Creates evidence/telemetry?
Mentions legality/legal/valid/success/accepted?
Could caller misread it as runtime legality?
Could caller misread it as execution/publication/commit/retire?
Current fallback behavior
Same-contour structural fallback?
Cross-contour fallback?
Required replacement/wrapper type
Required negative test id
Notes
```

## Current aggregate anchors

### `HybridCpuCompiledProgram`

Minimum facts to verify:

```text
- carries IrProgramSchedule;
- carries IrProgramBundlingResult;
- carries IReadOnlyList<VLIW_Bundle> lowered bundles;
- carries byte[] ProgramImage;
- carries ContractVersion;
- carries optional EmissionBaseAddress;
- carries IrAdmissibilityAgreement;
- carries IReadOnlyList<VliwBundleAnnotations>.
```

Required classification:

```text
Artifact aggregate / transport construction / structural agreement evidence.
Not execution.
Not runtime legality.
Not architectural publication.
Not commit.
Not retire.
Not production lowering by itself.
```

Required migration note:

```text
Project through ICompiledProgramEnvelopeAdapter into CompilerEmissionPackage.
Adapter preserves behavior only and must not strengthen authority.
```

## Mandatory symbol inventory table

| Symbol | File path | Current behavior class | Current success/failure shape | Ambiguity risk | Required wrapper/replacement | Required negative test |
|---|---|---|---|---|---|---|
| `HybridCpuCompiledProgram.EmitVliwBundleImage` | `Core/IR/Model/HybridCpuCompiledProgram.cs` | TransportConstruction | returns compiled program with emission base | `Emit` may sound like publication/execution | `EmitCarrierDecision` / package projection metadata | image emission != execution/publication/commit/retire |
| `HybridCpuCompiledProgram.ValidateRuntimeContractCompatibility` | `Core/IR/Model/HybridCpuCompiledProgram.cs` | RuntimeBridgeInput / contract compatibility | throws on mismatch | compatibility may sound execution-ready | `BridgeIngressStatus.VersionRejected` / contract observation | stale contract rejected; accepted still requires runtime legality |
| `HybridCpuBundleLowerer.LowerProgram` | `Core/IR/Bundling/HybridCpuBundleLowerer.cs` | TransportConstruction | returns `IReadOnlyList<VLIW_Bundle>` | `Lower` may sound production lowering | `EmitCarrierDecision` with runtime dependency | carrier != execution |
| `HybridCpuBundleLowerer.LowerBlock` | `Core/IR/Bundling/HybridCpuBundleLowerer.cs` | TransportConstruction | returns `IReadOnlyList<VLIW_Bundle>` | `Lower` may sound production lowering | `EmitCarrierDecision` | carrier != runtime legal |
| `HybridCpuBundleLowerer.LowerBundle` | `Core/IR/Bundling/HybridCpuBundleLowerer.cs` | TransportConstruction | returns `VLIW_Bundle` | carrier object may sound executable | `VliwCarrierEnvelope` | LowerBundle produces carrier only |
| `HybridCpuBundleLowerer.EmitAnnotationsForProgram` | `Core/IR/Bundling/HybridCpuBundleLowerer.cs` | Sideband / CompilerEvidence | returns annotations | `Emit` may sound authority | `CompilerSidebandEnvelope` | sideband != authority |
| `HybridCpuBundleLowerer.EmitAnnotationsForBundle` | `Core/IR/Bundling/HybridCpuBundleLowerer.cs` | Sideband / CompilerEvidence | returns `VliwBundleAnnotations` | annotations may be treated as legality | `CompilerSidebandEnvelope` | empty sideband does not strengthen authority |
| `HybridCpuBundleLowerer.EmitFactsForBundle` | `Core/IR/Bundling/HybridCpuBundleLowerer.cs` | TypedSlotEvidence | returns typed-slot facts | facts may be treated as legality | `TypedSlotFactsEnvelope` | typed-slot facts != runtime legality |
| `HybridCpuIrBuilder.BuildProgram` | `Core/IR/Construction/HybridCpuIrBuilder.cs` | StructuralObservation / IR construction | returns `IrProgram` | recovered helper plan may be read as lowering | `CompilerSemanticIntent` diagnostics | build/recover != production lowering |
| `HybridCpuIrBuilder.BuildInstruction` | `Core/IR/Construction/HybridCpuIrBuilder.cs` | StructuralObservation | constructs `IrInstruction` | exception path may hide typed reject | `CompilerSemanticIntent` / typed reject adapter | recovery false -> typed decision |
| `HybridCpuIrBuilder.ValidateExplicitAcceleratorIntent` | `Core/IR/Construction/HybridCpuIrBuilder.cs` | Structural/descriptor guard | throws on descriptor misuse | exception-only fail closed | typed `RejectAtCompileTimeDecision` | L7 descriptorless submit fail-closed |
| `CompilerMatrixTileEmissionLowerer.TryRecoverFromInstruction` | relevant helper lowerer | HelperOnly / ParserOnly | `bool` + out plan | `true` may look production lowering | `HelperOnlyDecision` / `LegacyApiTranslation` | helper success != production lowering |
| `CompilerVectorTransferEmissionLowerer.TryRecoverFromInstruction` | relevant helper lowerer | HelperOnly / ParserOnly | `bool` + out plan | `true` may look vector backend success | `HelperOnlyDecision` / `ParserOnlyDecision` | vector recovery success != production lowering |
| `HybridCpuInstructionLegalityChecker.AnalyzeCandidateBundle` | `Core/IR/Hazards/HybridCpuInstructionLegalityChecker.cs` | StructuralAdmissionEvidence | returns analysis with legality field | compiler `Legality` may be read as runtime legality | `CompilerStructuralAdmissionReport` | structural legal != runtime legal |
| `HybridCpuInstructionLegalityChecker.EvaluateCandidateBundle` | `Core/IR/Hazards/HybridCpuInstructionLegalityChecker.cs` | StructuralAdmissionEvidence | returns `IrBundleLegalityResult` | `Evaluate...Legality` ambiguous | `AnalyzeStructuralCandidateBundle` | no runtime legality conversion |
| `HybridCpuInstructionLegalityChecker.EvaluateClusterPreparedLegality` | `Core/IR/Hazards/HybridCpuInstructionLegalityChecker.cs` | StructuralAdmissionEvidence | returns `IrBundleLegalityResult` | cluster legality ambiguous | `AnalyzeClusterPreparedStructuralAdmission` | structural only |
| `HybridCpuBundleFormer.BundleProgram` | `Core/IR/Bundling/HybridCpuBundleFormer.cs` | StructuralPlacementEvidence / layout construction | returns bundling result | materialized bundles may sound executable | `CompilerStructuralPlacementReport` + carrier later | placement != execution |
| `HybridCpuBundleFormer.BundleBlock` | `Core/IR/Bundling/HybridCpuBundleFormer.cs` | StructuralPlacementEvidence | returns block bundling result | same | `CompilerStructuralPlacementReport` | placement != runtime legal |
| `HybridCpuBundleFormer.TryBundleProgramGlobally` | `Core/IR/Bundling/HybridCpuBundleFormer.cs` | StructuralPlacementEvidence | bool + out result | `Try` success may hide fallback | `CompilerPlacementFallbackProof` | global fallback recorded as structural only |
| `HybridCpuBundleFormer.TryMaterializeBlockGlobalLookahead` | `Core/IR/Bundling/HybridCpuBundleFormer.cs` | StructuralPlacementEvidence | bool + out bundle | same-contour fallback ambiguity | `CompilerPlacementFallbackProof` | not lowering fallback |
| `HybridCpuBundleFormer.TryMaterializeAdjacentBundleTripletLookahead` | `Core/IR/Bundling/HybridCpuBundleFormer.cs` | StructuralPlacementEvidence | bool + out bundle | same-contour fallback ambiguity | `CompilerPlacementFallbackProof` | not cross-contour fallback |
| `HybridCpuBundleFormer.TryMaterializeAdjacentBundlePair` | `Core/IR/Bundling/HybridCpuBundleFormer.cs` | StructuralPlacementEvidence | bool + out bundles | same-contour fallback ambiguity | `CompilerPlacementFallbackProof` | not cross-contour fallback |
| `HybridCpuSlotModel.SearchAssignments` | Core slot model | StructuralPlacementEvidence | result with `HasLegalAssignment` | legal assignment may sound runtime legal | `CompilerStructuralPlacementReport` | HasLegalAssignment != runtime legality |
| `HybridCpuSlotModel.SearchProgramAssignments` | Core slot model | StructuralPlacementEvidence | result with placement candidate | same | `CompilerStructuralPlacementReport` | placement != bridge accepted |
| `HybridCpuSlotModel.SearchGlobalBasicBlockAssignments` | Core slot model | StructuralPlacementEvidence | result with placement candidate | same | `CompilerStructuralPlacementReport` | placement != execution |
| `HybridCpuHazardModel.GetExecutionProfile` | `Core/IR/Hazards/HybridCpuHazardModel.cs` | StructuralObservation / resource expectation | returns execution profile | `ExecutionProfile` may sound execution authority | `CompilerResourceExpectationEvidence` | execution profile != execution |
| `TelemetryProfileReader` | `Core/IR/Telemetry` | CompilerEvidence / advisory telemetry | telemetry profile | telemetry may become policy | `CompilerEvidenceEnvelope` | telemetry != authority |
| `CompilerContract.ThrowIfVersionMismatch` | runtime/ISE contract | RuntimeBridgeInput / contract observation | throws on mismatch | pass may sound execution-ready | `BridgeIngressStatus` | version pass != runtime legal |
| `CompilerTypedSlotPolicyMode` | runtime/ISE contract | Runtime-owned policy vocabulary | enum | compiler may duplicate policy | observed policy only | compiler does not own policy |
| `CompilerTypedSlotIngressAction` | runtime/ISE contract | Runtime bridge diagnostic | enum | action may sound authority | bridge ingress status only | ingress action != runtime legality |

## Fallback inventory

### Same-contour structural fallback

Allowed only when explicitly recorded:

```text
Global placement search -> block/global lookahead -> local materialization
```

This remains structural placement fallback only. It must not change semantic intent, execution contour, sideband requirement, descriptor requirement, emission class, authority class or runtime dependency.

### Forbidden cross-contour fallback

Default forbidden cases:

```text
MatrixTile -> scalar/vector/Stream
Stream/vector -> scalar
DSC/lane6 -> L7/Stream/scalar
L7-SDC/lane7 -> DSC/Stream/scalar
VMX projection -> native backend emission
SecureCompute admission -> secure backend execution
Unknown contour -> scalar
```

## Completion checklist

```text
[ ] Every mandatory symbol has a completed row.
[ ] Every `Legal*`/`IsLegal`/`HasLegalAssignment` surface is marked structural-only.
[ ] Every helper/parser success is marked not production lowering.
[ ] Every descriptor success is marked ABI/evidence only.
[ ] Every current aggregate product has a target envelope.
[ ] Same-contour fallback and cross-contour fallback are separated.
[ ] Every row points to at least one negative test.
```
