# 00 — Current state and authority-boundary assessment

## Purpose

This phase is a docs/research baseline. It records what the current compiler already does, what it deliberately does not do, and which public/semi-public surfaces are compatibility, helper ABI, parser/evidence, future-gated, or real production-lowering candidates.

No code changes belong in this phase.

## Current compiler shape

The current `master` compiler has three important layers:

1. **Intent and contour selection**
   - `CompilerSemanticIntent` describes operation meaning.
   - `CompilerDefaultExecutionContourSelector` maps intent to an `ExecutionContourKind`.
   - Selection is explicitly not lowering and does not create carrier, sideband, descriptor, typed-slot facts, or runtime authority.

2. **Authority/evidence taxonomy**
   - `CompilerAuthorityClass` classifies compiler products only.
   - `CompilerPublicationClass` distinguishes carrier bytes, sideband, descriptor, facts, evidence, and runtime-bridge envelopes.
   - `CompilerExecutionClaim` has no value that means completed runtime execution.
   - Runtime Legality A/B, execution, commit, retire, and publication remain dependencies, not compiler rights.

3. **Provider registry and shells**
   - `DefaultContourLoweringProviderRegistry` registers contour-specific providers.
   - Current providers are shells.
   - `ContourLoweringProviderShell.Lower(...)` rejects production lowering with `RuntimeAuthorityOwned`.
   - Unknown contours fail closed through rejected analyzer/provider and do not scalar-fallback.

## Current entrypoint classification

| Surface | Current return | Product | Current status | Future role |
| --- | --- | --- | --- | --- |
| `IContourAnalyzer.Analyze` | `ContourAnalysisReport` | evidence report | evidence-only | keep analysis/gate/missing-requirement surface |
| `IContourLoweringProvider.Lower` | `CompilerLoweringDecision` | shell reject | not production | keep as compatibility/fail-closed shell |
| `CompilerVectorTransferEmissionLowerer.LowerWithDecision` | `CompilerPositiveEmissionResult<CompilerVectorTransferEmissionPlan>` | VLOAD/VSTORE carrier candidate + decision | helper/transport ABI only | can shadow a scoped production candidate later |
| `CompilerMatrixTileEmissionLowerer.LowerWithDecision` | `CompilerPositiveEmissionResult<CompilerMatrixTileEmissionPlan>` | MTILE carrier candidate + optional sideband + decision | helper ABI only | keep helper-only unless RFC creates production contour |
| vector/matrix `RecoverFromInstruction` | `CompilerHelperRecoveryResult<TPlan>` | parser/helper evidence | parser/helper only | never production authority |
| `CompileDmaStreamComputeDescriptor` | `DmaStreamComputeDescriptor` | parser/admission + descriptor | descriptor ABI compatibility | later wrapped by descriptor-backed production package |
| `CompileDmaStreamCompute` | `void` | lane6 carrier + sideband metadata | compatibility transport | later migrated behind DSC production provider |
| `CompileAcceleratorSubmit` | `CompilerAcceleratorLoweringDecision` | L7 carrier + descriptor sideband | typed/compat bridge | later migrated behind L7 production provider |
| raw `CompileInstruction` | `void` | raw VLIW carrier append | obsolete compatibility | never production boundary directly |
| `GetCompiledInstructions` | `ReadOnlySpan<VLIW_Instruction>` | carrier observation | artifact observation | never execution/publication |
| `GetBundleAnnotations` | `VliwBundleAnnotations` | sideband observation | artifact observation | never authority |

## Current gap summary

The implementation already has good authority vocabulary, but it does not yet have production-lowering vocabulary. In particular:

- `CompilerLoweringDecisionKind` lacks a production-package kind.
- `CompilerProductionLoweringStatus` lacks a production value that still keeps runtime authority pending.
- `IContourLoweringProvider` mixes the future production-shaped method name `Lower` with shell behavior.
- Existing positive carrier emission is helper ABI, not production lowering.
- DSC and L7 have rich descriptor/guard validation in facades, but not a production-provider lifecycle.

## Must remain true after refactor

- Existing shell providers continue to reject production lowering unless an explicit production-provider path is used.
- Existing helper/parser/recovery bool surfaces remain adapters only.
- Descriptor parser success remains descriptor/evidence success only.
- Runtime guard observations remain evidence of runtime-owned checks, not compiler authority.
- Carrier append remains transport construction, not execution, commit, retire, or publication.
- VMX and SecureCompute remain no-emission/non-production.

## Merge gate for this phase

- Documentation-only diff.
- No source or test behavior changes.
- Review confirms every current public/semi-public lowering surface is classified.
