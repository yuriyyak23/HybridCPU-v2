# Phase 09 - Dot/Matrix Deferral Boundary

## Goal

Promote advanced dot/matrix rows only when a production package defines
tile/dot ABI, VLM legality, execution, retire, replay, and optional-disabled
transition end to end.

## Production Path Overlay

Use the vector full production path plus any tile/matrix-specific runtime files
named by the implementation. Dot/matrix rows must define accumulator footprint,
tile memory/fault model, scheduling/lane binding, retire publication,
replay/migration, compiler no-emission or helper authority, and golden
artifacts. VMX-compatible projection is required only if tile state, migration,
or host/backend evidence becomes visible across virtualization boundaries.

## Instructions / Contours

- Dot/mixed precision: `VDOT.BLOCKSCALE`, `VDOT.ACCUM`, `VDOT.WIDE.I16`, `VDOT.WIDE.I32`.
- Matrix/tile: `MTILE_LOAD`, `MTILE_STORE`, `MTILE_MACC`, `MTRANSPOSE`.

## Existing Partial Files

- `Lanes00_03Vector\DotMixedPrecision\VdotBlockscaleInstruction.cs`
- `Lanes00_03Vector\DotMixedPrecision\VdotAccumInstruction.cs`
- `Lanes00_03Vector\DotMixedPrecision\VdotWideI16Instruction.cs`
- `Lanes00_03Vector\DotMixedPrecision\VdotWideI32Instruction.cs`
- `Lanes00_03Vector\MatrixTile\MtileLoadInstruction.cs`
- `Lanes00_03Vector\MatrixTile\MtileStoreInstruction.cs`
- `Lanes00_03Vector\MatrixTile\MtileMaccInstruction.cs`
- `Lanes00_03Vector\MatrixTile\MtransposeInstruction.cs`

## New Partial Files Allowed

- `*.DeferralContract.cs` for explicit optional-disabled/no-execution boundaries.
- `*.Legality.cs` only for fail-closed VLM metadata, not executable legality.
- `*.AbiNotes.cs` for tile descriptor or accumulator footprint notes.

## Local CloseToRTL Logic

Production/local partials may strengthen deferral metadata, name tile/dot ABI
gates, and prevent name-only extension of existing scoped dot operations.
Executable semantics open only when the same package closes tile/dot ABI, VLM,
runtime, retire, replay, and virtualization-boundary policy where needed.

## Production Evidence Gates

Tile execution model, dot/matrix opcode or contour authority, tile descriptor
ABI, accumulator/result footprint ABI, memory-shape/fault model, VLM closure,
decoder/encoder ABI, `InstructionIR` projection, typed MicroOps,
scheduler/lane binding, retire/replay model, conformance, and golden artifacts.

## Metadata Constants

Preserve `VectorDotMatrixDeferredNoExecution`, `RequiresDotAbiDecision`,
`RequiresAccumulatorPrecisionAbi`, `RequiresScaleMetadataAbi`,
`RequiresWiderIntegerContourAbi`, `SeparateFromScopedVdotWide`,
`NoNameOnlyVdotWideExtension`, `OptionalDisabledInIsaV4`,
`RequiresTileExecutionModel`, `RequiresTileDescriptorAbi`,
`RequiresAccumulatorTileAbi`, `RequiresTileMemoryShapeFaultModel`,
`RequiresTransposeTilePolicyAbi`, `RequiresVectorLegalityMatrixClosure`,
`RequiresFutureRetireReplayEvidence`, `Phase09NegativeDecisionGate`,
`NoDecoderEncoderAbiPublication`, `NoInstructionIrProjectionPublication`,
`NoRegistryMaterializerPublication`, `NoTypedMicroOpPublication`,
`NoSchedulerLaneBindingPublication`, `NoExecutionCapturePublication`,
`NoRetireWritebackPublication`, `NoReplayRollbackPublication`,
`NoCompilerHelperEmission`, `NoHostOwnedEvidencePublication`,
`NoDescriptorFallbackWithoutGenericRuntimeOwnership`, `NoHiddenScalarLowering`,
`NoHiddenVectorLowering`, `NoMultiOpEmission`, `NoVmxSpecificPath`,
`IsExecutable=false`, and `CompilerHelperAllowed=false`.

## Phase 09A Decision Gate - Advanced Dot/Mixed Precision

Status: explicit negative production decision. `VDOT.BLOCKSCALE`,
`VDOT.ACCUM`, `VDOT.WIDE.I16`, and `VDOT.WIDE.I32` remain reserved/no-allocation
rows in the `VectorDotMatrixDeferred` status bucket. This slice does not
allocate numeric opcodes, does not add decoder/encoder ABI, does not add
`InstructionIR` projection, does not register materializers, does not publish
typed vector/tile MicroOps, does not bind scheduler lanes, does not execute or
capture results, does not publish retire/writeback effects, does not add
replay/rollback evidence, and does not add compiler helper or emission
authority.

Decision details:

- Dot ABI decision: unresolved; advanced dot forms require canonical operand,
  sideband, scalar/vector result, accumulator, mask/tail, and VLM contour
  policy.
- Scale metadata: `VDOT.BLOCKSCALE` requires a scale metadata ABI and cannot
  borrow ordinary dot or `VDOT.WIDE` payloads.
- Accumulator/result footprint: `VDOT.ACCUM` requires accumulator precision and
  separate result footprint evidence.
- Wider integer contours: `VDOT.WIDE.I16` and `VDOT.WIDE.I32` are not opened by
  the existing scoped executable `VDOT.WIDE` contour. Name similarity is not
  production evidence.
- Evidence separation: existing `VDOT`, `VDOTU`, `VDOTF`, `VDOT_FP8`, generic
  vector arithmetic, and scoped `VDOT.WIDE` prove only their closed scalar
  footprint contours.
- Lowering: no descriptor fallback unless explicitly owned by the generic
  runtime model, no hidden scalar/vector lowering, and no multi-op emission.
- VMX: generic runtime boundary only; no VMX-specific path is introduced.

Rationale: blockscale, accumulator, and wider-integer dot variants change
sideband ABI, result footprint, precision, and replay requirements. Reusing
current dot/vector evidence would skip ABI and retire/replay decisions.

## Phase 09B Decision Gate - Matrix/Tile Optional-Disabled Contours

Status: explicit negative production decision. `MTILE_LOAD`, `MTILE_STORE`,
`MTILE_MACC`, and `MTRANSPOSE` remain optional-disabled declared matrix rows.
Existing numeric enum/opcode constants are retained only as prior declared
optional-disabled surface; they are not decoder, registry, materializer,
execution, retire, replay, helper, VMX, Lane6, Lane7, or external-backend
authority.

Decision details:

- Tile descriptor ABI: unresolved; tile identity, shape, element type, lifetime,
  ownership, and sideband encoding must be canonical.
- Tile memory/fault model: `MTILE_LOAD` and `MTILE_STORE` require memory-shape,
  alignment, partial-fault, staged publication/commit, and replay rules.
- Tile execution model: `MTILE_MACC` requires accumulator tile footprint,
  scheduling/lane binding, deterministic execution, and rollback policy.
- Transpose policy: `MTRANSPOSE` requires tile transpose shape/order policy and
  staged publication semantics.
- Evidence separation: vector transfer, vector arithmetic, FMA, dot, Lane6
  descriptor, Lane7 accelerator, and VMX evidence do not authorize tile memory,
  tile MACC, or matrix transpose.
- Backend boundary: no host-owned evidence publication, no hidden VMX/Lane6/
  Lane7/external backend integration, and no descriptor fallback unless the
  generic runtime model explicitly owns it.
- Lowering: no hidden scalar/vector lowering and no multi-op compiler emission.

Rationale: matrix/tile rows introduce architectural state and fault/replay
contours that are not proven by existing vector carriers or external backend
descriptors.

## Evidence Chain Stories

- Decoder/encoder ABI: production gate; parser/decoder opening must be paired
  with tile/dot ABI.
- InstructionIR/projection: production gate; tile/dot shapes require typed IR.
- Typed MicroOp/materializer: production gate; typed tile/dot MicroOps require
  full runtime evidence.
- Execute/capture semantics: remain absent.
- Retire/writeback/side effects: future staged tile/vector publication and
  memory commit.
- Replay/rollback/conformance: future tile fault, accumulator rollback,
  deterministic dot ordering, and no-emission tests.

## Boundaries

- Vector VLM: fail-closed and optional-disabled; no new executable row.
- Lane6: no descriptor backend fallback.
- Lane7: no accelerator or external backend fallback.
- VMX: no VMX-specific path unless a future package has an explicit
  virtualization-boundary reason.
- No-emission: mandatory.

## Risks

- Name-only widening of existing `VDOT.WIDE` scope.
- Treating existing dot/vector arithmetic evidence as coverage for blockscale,
  accumulator variants, tile memory, tile MACC, or matrix transpose.
- Tile state becoming implicit architectural state without retire/replay
  evidence.
- Host accelerator evidence leaking through matrix rows.

## Closure Criteria

- A production package may promote each row only after tile/dot ABI, VLM,
  runtime, retire, replay, golden, and VMX-boundary evidence close where needed.
- Local partials without that package keep the deferral boundary explicit.
- Phase 09 closure is a negative decision gate, not executable closure.

## Prohibited Actions

- Implement dot/matrix execution, allocate advanced dot opcodes, open VLM, add
  compiler helpers, bind scheduler lanes, materialize typed tile/dot MicroOps,
  publish retire/writeback effects, or bind to external accelerators except in
  a matching production package with helper/backend authority.
