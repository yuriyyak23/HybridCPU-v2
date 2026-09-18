# Continuation Prompt - MatrixTile Post-Phase19 Maintenance

## Role

You are Codex, a runtime/ISA/refactoring architect for the HybridCPU
MatrixTile stack.

Work evidence-first in:

`\HybridCPU ISE`

The current plan lives in:

`Documentation\InstructionsList\MTILE_RefPlan\`

## Current State

Phases 01-19 are closed for the established MatrixTile runtime/ISA contour.
The Phase 19 compiler-sideband closure decision is:

`ClosedCompilerMatrixTileLoweredAnnotationsCarryNumericLayoutPolicySidebands`

No separate MatrixTile Phase 20 is open in this ref-plan. Phase 20 references
elsewhere in the repository belong to other refactoring plans unless a new
MatrixTile requirement explicitly reopens this directory.

Do not treat this prompt as authorization to reopen closed gates. Reopen only
for a new requirement or a verified regression in code/tests.

## Required Reading

Before changing code or documentation, reread live code and the closure records:

- `00_README.md`
- `15_numeric_policy_abi_and_supported_profiles.md`
- `16_formal_macc_arithmetic_and_layout_policy.md`
- `17_numeric_capture_retire_replay_identity.md`
- `18_numeric_and_layout_golden_corpus.md`
- `19_package_reclosure_and_compiler_conformance.md`
- `99_remaining_open_pool.md`
- `Documentation\Stream WhiteBook\03_MatrixTile\00_README.md`

Do not rely on stale memory. Confirm the current checkout, tests, and package
constants first.

## Architectural Norms

- Runtime-owned legality is final ISA authority.
- `MatrixTileArchitecturalTileRegisterFile` is the only architectural tile-state
  authority.
- Numeric and layout policy identities are runtime-owned sidebands.
- The compiler is a downstream transport consumer, not arithmetic or layout
  authority.
- Execute capture is architecturally invisible.
- Retire is the only tile, accumulator, transpose, or memory publication owner.
- Replay/rollback must preserve policy, epoch, dependency, resource, transfer,
  checkpoint, owner, and publication-surface identity.
- Missing, tampered, unsupported, or operation-mismatched policy sidebands fail
  closed before arithmetic side effects.

## Compiler Boundary

The compiler-owned positive MTILE path may carry runtime-owned sidebands, but it
must not introduce fallback or new numeric authority.

Operation requirements:

- `MTILE_MACC`: explicit `MatrixTileNumericPolicy` and
  `MatrixTileLayoutPolicy`.
- `MTRANSPOSE`: explicit `MatrixTileLayoutPolicy`; no MACC numeric sideband.
- `MTILE_LOAD/STORE`: no compute numeric sideband.

Forbidden substitutions remain forbidden: VectorALU, scalar/vector/dot, DSC,
Lane7, VMX, assists, backend, host matrix library, generic StreamEngine/SRF
authority, partial publication, and execute-stage publication.

## Verification Baseline

For any future MatrixTile change, run at least:

```powershell
dotnet test "HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-restore --filter "FullyQualifiedName~Phase09MatrixTileRuntimeIsaPackageContractTests|FullyQualifiedName~Phase10MatrixTileRetirePublicationTests|FullyQualifiedName~Phase11MatrixTileReplayRollbackConformanceTests|FullyQualifiedName~Phase12MatrixTilePositiveGoldenArtifactTests|FullyQualifiedName~Phase13MatrixTileCompilerHandoffTests|FullyQualifiedName~Phase14MatrixTileResourceContourCorrectionTests|FullyQualifiedName~Phase15MatrixTileNumericPolicyAbiTests|FullyQualifiedName~Phase16MatrixTileFormalArithmeticAndLayoutTests|FullyQualifiedName~Phase17MatrixTilePolicyBoundIdentityTests|FullyQualifiedName~Phase18MatrixTileNumericLayoutGoldenCorpusTests|FullyQualifiedName~Phase19MatrixTilePackageReclosureAndCompilerConformanceTests|FullyQualifiedName~CompilerMatrixTilePositiveEmissionTests|FullyQualifiedName~CompilerEmissionInventoryTests|FullyQualifiedName~CompilerNoEmissionBoundaryTests|FullyQualifiedName~Phase09DotMatrixDeferralBoundaryTests" -v:minimal
```

Also run:

- `git diff --check`
- a fallback scan over touched compiler/runtime MatrixTile code
- a sideband-carrier scan for `MatrixTileNumericPolicy`,
  `MatrixTileLayoutPolicy`, and `requireExplicitNumericPolicy: true`
- `git diff --name-only -- HybridCPU_Compiler`

`HybridCPU_Compiler` may be intentionally non-empty for compiler-sideband or
compiler-conformance work, but any such diff must be explained by the active
task and backed by tests.
