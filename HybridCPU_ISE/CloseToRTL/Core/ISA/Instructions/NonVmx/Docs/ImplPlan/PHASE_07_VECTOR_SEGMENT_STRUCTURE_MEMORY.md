# Phase 07 - Vector Segment, Structure, And Memory Contours

## Goal

Promote vector structure movement and memory-shape contours only with exact VLM
opening, staged memory publication, and no base-opcode duplication.

## Production Path Overlay

Use the vector/vector-memory full production path in `README.md`. Segment,
2D, indexed+2D, and structure rows require canonical sidebands, IR shape
projection, typed vector memory/compute MicroOps, fault/replay model, retire
publication, and golden tests. VMX-specific projection is needed only if the
row crosses memory-domain virtualization, DMA, migration/checkpoint, or
host-owned evidence boundaries.

## Instructions / Contours

- Structure movement: `VZIP`, `VUNZIP`, `VINTERLEAVE`, `VDEINTERLEAVE`.
- Segment loads/stores: `VLDSEG2`, `VLDSEG4`, `VLDSEG8`, `VSTSEG2`, `VSTSEG4`, `VSTSEG8`.
- 2D memory contours: `VLOAD` 2D, `VSTORE` 2D.
- Indexed+2D memory contours: `VGATHER` indexed+2D, `VSCATTER` indexed+2D.

## Existing Partial Files

- `Lanes00_03Vector\StructureMovement\*.cs`
- `Lanes04_05Memory\Segments\*.cs`
- `Lanes04_05Memory\Shapes2D\Vload2DContour.cs`
- `Lanes04_05Memory\Shapes2D\Vstore2DContour.cs`
- `Lanes04_05Memory\Indexed2D\VgatherIndexed2DContour.cs`
- `Lanes04_05Memory\Indexed2D\VscatterIndexed2DContour.cs`

## New Partial Files Allowed

- `*.Legality.cs` for VLM shape metadata.
- `*.ShapeContract.cs` for structure/segment/2D/indexed sideband contracts.
- `*.PublicationContract.cs` for staged load publication or store commit notes.

## Local CloseToRTL Logic

Production/local partials may express shape descriptors, segment count metadata, no descriptor fallback, no hidden stream-engine fallback, no base-opcode duplication, address-shape contract notes, and fail-closed VLM checks. Memory execution opens only when the same package closes the vector-memory production path.

## Production Evidence Gates

Memory-shape ABI, descriptor/sideband transport, decoder/encoder ABI, `InstructionIR` projection, vector memory materializer, typed MicroOps, scheduler/lane binding, fault model, execute/capture, retire staged publication/commit, replay/rollback, conformance, and golden artifacts.

## Metadata Constants

Preserve `VectorStructureMovementFailClosed`, `VectorSegmentMemoryFailClosed`, `VectorMemoryContourFailClosed`, `RequiresMemoryShapeAbi`, `RequiresFaultReplayPolicy`, `RequiresVectorLegalityMatrixClosure`, `NoBaseOpcodeDuplication=true`, `NoDescriptorFallback=true`, `RequiresRetireStagedPublication`, `RequiresRetireStagedCommit`, `IsExecutable=false`, and `CompilerHelperAllowed=false`.

## Phase 07A Decision Gate - Structure Movement

Status: explicit negative production decision. `VZIP`, `VUNZIP`,
`VINTERLEAVE`, and `VDEINTERLEAVE` remain reserved/no-allocation rows in the
`VectorScanSegmentMovement` status bucket. This slice does not allocate opcodes,
does not add decoder/encoder sidebands, does not add structure-shape
`InstructionIR` projection, does not register materializers, does not publish
typed vector MicroOps, does not bind runtime execution, and does not add
compiler helper authority.

Decision details:

- Shape ABI: unresolved; element order, lane grouping, source/destination
  aliasing, mask/tail behavior, and publication footprint must be canonical.
- Publication: unresolved; movement must be staged and replay-safe before any
  visible destination update.
- Lowering: no hidden StreamEngine fallback, DMA fallback, descriptor fallback,
  base-opcode duplication, scalar lowering, or multi-op emission.
- VMX: generic runtime boundary only; no VMX-specific path is introduced.

Rationale: structure movement is not ordinary vector arithmetic and cannot be
derived from base vector load/store or permutation evidence without a canonical
shape contract and staged publication model.

## Phase 07B Decision Gate - Segment Loads And Stores

Status: explicit negative production decision. `VLDSEG2`, `VLDSEG4`,
`VLDSEG8`, `VSTSEG2`, `VSTSEG4`, and `VSTSEG8` remain reserved/no-allocation
rows in the `VectorScanSegmentMovement` status bucket. This slice does not
allocate opcodes, does not add decoder/encoder sidebands, does not add segment
`InstructionIR` projection, does not register materializers, does not publish
typed vector-memory MicroOps, does not bind memory execution, and does not add
compiler helper authority.

Decision details:

- Segment shape ABI: unresolved; segment count, stride, alignment, byte order,
  mask/tail behavior, and fault granularity must be explicit.
- Load publication: unresolved; destination surfaces require staged publication
  after fault/replay ordering closes.
- Store commit: unresolved; byte commit order and rollback policy must be
  explicit before any retire side effect.
- Lowering: no hidden StreamEngine fallback, DMA fallback, descriptor fallback,
  base-opcode duplication, or multi-op emission.
- VMX: no VMX-specific path; future virtualization only matters for an explicit
  memory-domain boundary decision.

Rationale: closed 1D transfer carriers prove only their current transfer shape.
They do not define segment deinterleaving/interleaving, fault granularity, or
partial publication/commit semantics.

## Phase 07C Decision Gate - 2D And Indexed+2D Memory Contours

Status: explicit negative production decision. 2D `VLOAD`, 2D `VSTORE`,
indexed+2D `VGATHER`, and indexed+2D `VSCATTER` remain contour-only templates.
No new opcode is allocated and the existing base opcodes remain limited to the
currently closed 1D contours in `VectorLegalityMatrix`. This slice does not add
decoder/encoder sidebands, 2D/indexed+2D `InstructionIR` projection,
materializers, typed vector-memory MicroOps, runtime execution, retire
publication/commit, or compiler helper authority.

Decision details:

- 2D shape ABI: unresolved; rows, columns, element size, row stride, mask/tail
  behavior, address bounds, and fault granularity must be canonical.
- Indexed+2D ABI: unresolved; index element meaning, index surface footprint,
  bounds policy, descriptor transport, and row/column fault order must close.
- Base opcode boundary: closed 1D `VLOAD`/`VSTORE` and 1D indexed
  `VGATHER`/`VSCATTER` evidence is not evidence for 2D or indexed+2D contours.
- Lowering: no hidden StreamEngine fallback, DMA fallback, base-opcode
  duplication, descriptor shortcut, or multi-op emission.
- VMX: generic runtime boundary only; no VMX-specific path is introduced.

Rationale: a contour extension changes address generation, replay, and retire
semantics. Reusing base opcode names without a canonical sideband and VLM row
would blur executable evidence.

## Evidence Chain Stories

- Decoder/encoder ABI: production gate; structure/segment/2D sidebands must be canonical.
- InstructionIR/projection: production gate; IR must carry shape, segment count, stride/index footprints.
- Typed MicroOp/materializer: production gate; vector memory typed ops with fault/replay shape.
- Execute/capture semantics: local shape contracts only; no memory execution.
- Retire/writeback/side effects: loads staged publication; stores staged commit; no partial host leak.
- Replay/rollback/conformance: require fault ordering, partial completion, aliasing, shape bounds, segment ordering, and rollback tests.

## Boundaries

- Vector VLM: mandatory fail-closed gate.
- Lane6: do not fallback to descriptors, DMA, or StreamEngine.
- Lane7/VMX: no VMX-specific projection unless future external backend authority crosses virtualization boundary.
- No-emission: no compiler helpers.

## Risks

- Duplicating base `VLOAD`/`VSTORE`/`VGATHER`/`VSCATTER` opcodes for 2D contours.
- Opening memory rows without fault/replay model.
- Hidden descriptor/StreamEngine/DMA fallback.

## Closure Criteria

- A production package may promote each row only after shape ABI, VLM, fault/replay, staged retire, and golden evidence close.
- Local partials without that package add shape and legality contracts only.

## Prohibited Actions

- Open VLM and allocate opcodes only in the matching vector-memory production package; duplicate base opcodes and staged-retire bypass remain invalid.
- Do not publish host-owned memory/backend evidence.
