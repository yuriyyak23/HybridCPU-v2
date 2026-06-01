# Non-VMX Iteration 11A Dot/Matrix Leaf Templates Snapshot

Date: 2026-05-26

Scope: `HybridCPU_ISE/CloseToRTL/Core/ISA/Instructions/NonVmx/Lanes00_03Vector/{DotMixedPrecision,MatrixTile}/`.

## Closed Boundary

Iteration 11A materializes the dot/matrix no-emission template metadata directly in the per-instruction leaf partial files. This is not an executable instruction closure.

Leaf templates materialized:

- `VDOT.BLOCKSCALE`, `VDOT.ACCUM`
- `VDOT.WIDE.I16`, `VDOT.WIDE.I32`
- `MTILE_LOAD`, `MTILE_STORE`, `MTILE_MACC`, `MTRANSPOSE`

## Evidence Statement

Each leaf template exposes mnemonic/operand/evidence metadata and sets:

- `RequiresVectorLegalityMatrixClosure = true`
- `RequiresFutureRetireReplayEvidence = true`
- `IsExecutable = false`
- `CompilerHelperAllowed = false`

Advanced dot rows additionally set:

- `RequiresDotAbiDecision = true`
- `RequiresAccumulatorPrecisionAbi = true`
- `NoHostOwnedEvidencePublication = true`
- `RequiresScaleMetadataAbi = true` for `VDOT.BLOCKSCALE`
- `RequiresAccumulatorResultFootprintAbi = true` for `VDOT.ACCUM`
- `SeparateFromScopedVdotWide = true` and `NoNameOnlyVdotWideExtension = true` for `VDOT.WIDE.I16` and `VDOT.WIDE.I32`

Matrix/tile rows additionally set:

- `OptionalDisabledInIsaV4 = true`
- `RequiresTileExecutionModel = true`
- `RequiresTileDescriptorAbi = true`
- `RequiresTileMemoryShapeFaultModel = true` for `MTILE_LOAD` and `MTILE_STORE`
- `RequiresAccumulatorTileAbi = true` for `MTILE_MACC`
- `RequiresTransposeTilePolicyAbi = true` for `MTRANSPOSE`

No Iteration 11A row allocates:

- numeric opcode or descriptor op-type
- decoder/encoder path
- `InstructionIR` projection
- registry/materializer entry
- typed MicroOp
- executable `VectorLegalityMatrix` contour
- execute/capture semantics
- retire/writeback or vector publication semantics
- compiler helper emission authority

## ABI Blockers

- `VDOT.BLOCKSCALE`: scale metadata, accumulator precision, separate result surface, and no host-owned evidence publication remain open.
- `VDOT.ACCUM`: accumulator/result footprint, separate result surface, and retire/replay behavior remain open.
- `VDOT.WIDE.I16`/`VDOT.WIDE.I32`: wider-integer contour ABI remains open and cannot be inferred from current scoped `VDOT.WIDE`.
- `MTILE_LOAD`/`MTILE_STORE`: tile execution model, tile descriptor ABI, memory-shape/fault model, and retire/replay behavior remain open.
- `MTILE_MACC`/`MTRANSPOSE`: tile accumulator or transpose policy ABI and tile retire/replay behavior remain open.

## Verification

- `dotnet build HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-restore`
- targeted `dotnet test` for `NonVmxIteration04BDeferredTemplateSurfaceTests` plus related Non-VMX catalog/no-emission parity tests.
