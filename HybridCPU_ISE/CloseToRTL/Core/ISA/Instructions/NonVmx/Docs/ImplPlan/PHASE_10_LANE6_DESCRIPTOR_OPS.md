# Phase 10 - Lane6 Descriptor Ops

## Goal

Promote Lane6 descriptor-owned compute op-types and shape/range contours as
descriptor sideband transport, not scalar ISA opcodes.

## Current Phase 10 Decision

Phase 10 closes this pool as an explicit negative production decision gate, not
as executable closure. `DmaStreamCompute.SUB/MIN/MAX/ABSDIFF/CLAMP`,
`DmaStreamCompute.CONVERT`, `DmaStreamCompute.COMPARE/SELECT`, explicit
`DmaStreamCompute.REDUCE_*`, and `DSC_SHAPE_*` rows remain descriptor-only,
declared-only, and fail-closed. The scoped executable `DmaStreamCompute`
DSC1 contour does not authorize these op-type or shape expansions.

Existing Lane6 evidence for `Copy/Add/Mul/Fma/Reduce` with
`Contiguous1D`/`FixedReduce`, `InlineContiguous`, and `AllOrNone` is evidence
only for that scoped contour. It is not evidence for arithmetic subtract,
min/max, absolute difference, clamp, conversion, compare/select, named
reductions, strided/tiled/scatter-gather/2D/multi-range shapes, or any
descriptor parser/materializer/runtime fallback.

## Production Path Overlay

Use the `Lane6 Descriptor / Queue / DSC Carrier Rows` path in `README.md`.
Descriptor ops require descriptor op enum/payload ABI, owner/domain guard,
typed descriptor projection, backend execution/admission, retire publication,
replay/rollback, golden descriptor artifacts, and no-emission tests. VMX
projection is required only when the descriptor exposes DMA, external backend,
host-owned evidence, migration/checkpoint, or nested virtualization policy.

## Instructions / Contours

- Arithmetic descriptor ops: `DmaStreamCompute.SUB`, `MIN`, `MAX`, `ABSDIFF`, `CLAMP`.
- Type conversion: `DmaStreamCompute.CONVERT`.
- Predicate ops: `DmaStreamCompute.COMPARE`, `SELECT`.
- Reductions: `REDUCE_SUM`, `REDUCE_MIN`, `REDUCE_MAX`, `REDUCE_AND`, `REDUCE_OR`, `REDUCE_XOR`.
- Shape/range contours: `DSC_SHAPE_STRIDED`, `DSC_SHAPE_TILED`, `DSC_SHAPE_SCATTER_GATHER`, `DSC_SHAPE_2D`, `DSC_SHAPE_MULTI_RANGE`.

## Existing Partial Files

- `Lane06DmaStream\DescriptorOps\Arithmetic\*.cs`
- `Lane06DmaStream\DescriptorOps\TypeConversion\DscConvertDescriptorOp.cs`
- `Lane06DmaStream\DescriptorOps\Predicate\*.cs`
- `Lane06DmaStream\DescriptorOps\Reduction\*.cs`
- `Lane06DmaStream\DescriptorOps\ShapeRange\*.cs`

## New Partial Files Allowed

- `*.DescriptorContract.cs` for op-type/shape ABI notes.
- `*.AdmissionContract.cs` for owner/domain/token/staged-commit requirements.
- `*.ReplayContract.cs` for deterministic replay and partial-completion notes.

## Local CloseToRTL Logic

Production/local partials may encode descriptor ownership, op-type/shape names, normalized footprint metadata, owner/domain guard requirements, and fail-closed admission contracts. Descriptor execution opens only through the Lane6 production path; scalar opcode allocation remains invalid.

## Production Evidence Gates

Descriptor op-type or shape enum allocation, descriptor parser validation, token admission, owner/domain guard, range normalization, backend runtime execution, staged writes, retire commit authority, rollback/replay, capability policy, virtualization-boundary policy, conformance, and golden artifacts.

## Metadata Constants

Preserve `Lane6DescriptorOwnedNoExecution`, `Lane6ShapeContourNoExecution`, `IsDescriptorOwned=true`, `HasScalarOpcodeAllocation=false`, `RequiresDescriptorOpTypeAllocation`, `RequiresShapeEnumAllocation`, `RequiresDescriptorParserValidation`, `RequiresOwnerDomainGuard`, `RequiresTokenAdmission`, `RequiresStagedCommit`, `RequiresRetireCommitAuthority`, `RequiresReplayDeterminism`, `NoGuestVisibleHostEvidence`, `IsExecutable=false`, and `CompilerHelperAllowed=false`.

Phase 10 adds const marker ABI for the negative gate: `ExecutionLaneBinding`,
`GenericRuntimeOnly` VMX boundary, descriptor payload/projection/materializer
requirements, backend admission, future retire/replay/golden artifact
requirements, no host-owned evidence publication, no scalar opcode publication,
no decoder/encoder ABI publication, no hidden scalar/vector lowering, no
multi-op emission, no generic `DmaStreamCompute` fallback, no
StreamEngine/DMAController fallback, no Lane7/external backend fallback, no
VMX-specific path, and future virtualization-boundary policy. Shape rows
additionally require shape parser manifest, shape fault model, alias/overlap
policy, and no `DSC2` fallback.

## Evidence Chain Stories

- Decoder/encoder ABI: descriptor ABI production gate, not scalar decoder.
- InstructionIR/projection: production gate only if descriptor carrier projects to typed IR.
- Typed MicroOp/materializer: production descriptor materialization gate, not scalar ALU.
- Execute/capture semantics: descriptor backend execution production gate.
- Retire/writeback/side effects: staged commit authority required before any write.
- Replay/rollback/conformance: owner/domain, token admission, partial completion, aliasing, reduction result footprint, conversion policy tests.

## Boundaries

- Vector VLM: only relevant if descriptor touches vector surfaces; still not a vector opcode shortcut.
- Lane6 sideband: mandatory descriptor-owned boundary.
- Lane7/VMX: DMA/external backend authority may require future virtualization policy; no host evidence leak.
- No-emission: no compiler helpers.

## Risks

- Accidentally turning descriptor op-types into scalar instructions.
- Missing owner/domain guard or token admission.
- Publishing backend capabilities as guest architectural state.

## Closure Criteria

- A production package may promote each descriptor row only after descriptor ABI, token/owner guard, backend execution, retire, replay, golden, and virtualization-boundary policy close where needed.
- Scalar opcode allocation remains false.
- Until that production package exists, the rows are `DescriptorOnly` /
  `DeclaredOnly`; they publish no numeric opcode, runtime opcode metadata,
  decoder/encoder ABI, InstructionIR projection, registry/materializer row,
  typed descriptor MicroOp, scheduler/lane binding, execution/capture,
  retire/writeback, replay/rollback, compiler helper, or VMX-specific path.

## Prohibited Actions

- Descriptor ops remain descriptor-owned; scalar opcodes are not a production path for them.
- Execute descriptor ops only with token, retire, replay, and virtualization-boundary gates closed.
- Do not use `DmaStreamCompute`, `DSC2`, Lane7, VMX, StreamEngine,
  DMAController, external backend, scalar lowering, vector lowering, or
  multi-op compiler emission as a hidden integration path for this pool.
