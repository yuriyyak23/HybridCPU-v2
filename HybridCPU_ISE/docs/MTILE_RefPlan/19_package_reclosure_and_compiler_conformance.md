# Phase 19 - Package Reclosure And Compiler Conformance

Status: closed/compiler-sideband-conformance

Review date: 2026-06-17

## Objective

Re-attest runtime package readiness after the explicit numeric/layout policy
work and prove that the compiler-owned MTILE path is a conforming downstream
consumer of runtime-owned policy identities.

## Closure Result

Phase 15-18 runtime evidence is closed:

- `MatrixTileNumericPolicyAbi` publishes ABI version 1 and ten supported
  integer/binary profiles;
- `MatrixTileLayoutPolicyAbi` publishes MACC and transpose layout identities;
- MACC arithmetic uses deterministic software integer/binary profiles and
  canonical little-endian codecs;
- capture, retire, rollback, and replay identity bind numeric/layout policy,
  epoch, dependency fingerprint, resource contour, and publication surface;
- the v1 JSON corpus executes through the production runtime path.

Phase 19 closes compiler-sideband conformance. The compiler-owned positive
MTILE helpers now carry runtime-owned policy sidebands through both source
annotations and lowered bundle annotations:

- `MTILE_MACC` carries explicit `MatrixTileNumericPolicy` and
  `MatrixTileLayoutPolicy`;
- `MTRANSPOSE` carries explicit `MatrixTileLayoutPolicy` and no MACC numeric
  sideband;
- `MTILE_LOAD` and `MTILE_STORE` remain free of compute numeric/layout
  authority;
- strict runtime projection/materialization still rejects missing, tampered,
  unsupported, or operation-mismatched sidebands before arithmetic side
  effects.

The compiler bridge is a transport bridge only. Numeric and layout profiles
come from the runtime ABI types and are revalidated by runtime projection and
materialization; compiler metadata does not become arithmetic authority.

## Package Constants

- `Phase19RuntimeNumericEvidenceReady = true`
- `Phase19CompilerCarrierConformanceReady = true`
- `Phase19CompilerNoFallbackConformanceReady = true`
- `Phase19CompilerRuntimeRejectionConformanceReady = true`
- `Phase19CompilerSidebandConformanceReady = true`
- `Phase19PackageReclosureReady = true`
- `NumericSensitivePackageReadiness = true`
- `NumericSensitiveClosesGoldenArtifacts = true`
- `PositiveNumericHandoffReady = true`

Closure decision:

`ClosedCompilerMatrixTileLoweredAnnotationsCarryNumericLayoutPolicySidebands`

## Compiler Lowering Conformance

The bridge is implemented in the compiler request/plan/lowering path and in
the annotation transport path:

- `CompilerMatrixTileAccumulatorPolicyAbi` carries nullable runtime-owned
  `MatrixTileNumericPolicy` and `MatrixTileLayoutPolicy` sidebands.
- `CompilerMatrixTileTransposePolicyAbi` carries nullable runtime-owned
  `MatrixTileLayoutPolicy`.
- `CompilerMatrixTileEmissionLowerer` builds strict runtime projections with
  `requireExplicitNumericPolicy: true`.
- `HybridCpuThreadCompilerContext.AppendMatrixTileInstruction` publishes the
  plan sidebands into source `InstructionSlotMetadata`.
- `HybridCpuIrBuilder` recovers MTILE helper plans only through sideband-aware
  metadata.
- `HybridCpuBundleLowerer.BuildInstructionSlotMetadata` preserves the
  sidebands into lowered `InstructionSlotMetadata`.

No fallback path was added. The no-fallback tests continue to reject scalar,
VectorALU, dot, DSC, Lane7, VMX, assist, external backend, and raw MTILE
ingress substitution.

## Verification Evidence

Focused Phase09-19/compiler filter:

```text
dotnet test HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj --no-restore --filter "FullyQualifiedName~Phase09MatrixTileRuntimeIsaPackageContractTests|FullyQualifiedName~Phase10MatrixTileRetirePublicationTests|FullyQualifiedName~Phase11MatrixTileReplayRollbackConformanceTests|FullyQualifiedName~Phase12MatrixTilePositiveGoldenArtifactTests|FullyQualifiedName~Phase13MatrixTileCompilerHandoffTests|FullyQualifiedName~Phase14MatrixTileResourceContourCorrectionTests|FullyQualifiedName~Phase15MatrixTileNumericPolicyAbiTests|FullyQualifiedName~Phase16MatrixTileFormalArithmeticAndLayoutTests|FullyQualifiedName~Phase17MatrixTilePolicyBoundIdentityTests|FullyQualifiedName~Phase18MatrixTileNumericLayoutGoldenCorpusTests|FullyQualifiedName~Phase19MatrixTilePackageReclosureAndCompilerConformanceTests|FullyQualifiedName~CompilerMatrixTilePositiveEmissionTests|FullyQualifiedName~CompilerEmissionInventoryTests|FullyQualifiedName~CompilerNoEmissionBoundaryTests|FullyQualifiedName~Phase09DotMatrixDeferralBoundaryTests" -v:minimal
Passed: 278/278
```

Additional sideband-specific evidence:

- positive compiler helper emission proves source and lowered annotations carry
  the expected runtime sidebands;
- runtime projection/materialization accepts valid sidebands and rejects
  missing/tampered/unsupported sidebands;
- compiler no-fallback and emission inventory tests remain green;
- the Phase 18 golden corpus loader remains in the same focused filter.

`git diff --name-only -- HybridCPU_Compiler` is intentionally non-empty for
Phase 19 because the closure specifically requires compiler-side sideband
transport edits.

## Scope Boundary

Closed Phase 19 does not change the MatrixTile architectural authority model:

- `MatrixTileArchitecturalTileRegisterFile` remains the architectural tile
  state owner;
- execute capture remains architecturally invisible;
- retire remains the only tile, accumulator, transpose, and memory publication
  authority;
- replay/rollback continues to use core-owned checkpoints and policy-bound
  identity validation;
- compiler emission remains downstream transport evidence, not numeric/layout
  authority.
