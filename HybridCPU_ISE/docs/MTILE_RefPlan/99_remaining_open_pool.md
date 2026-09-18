# MTILE Remaining Open Pool

Status: closed/no-active-mtile-runtime-isa-blockers

Review date: 2026-06-17

## Runtime/ISA Pool

Phases 01-19 are closed for the established MatrixTile runtime/ISA contour:

| Phase | Closure |
| --- | --- |
| 01 | opcode identity and status promotion rule |
| 02 | architectural tile state and canonical descriptor |
| 03 | memory shape and precise fault identity |
| 04 | accumulator and transpose semantics |
| 05 | runtime-owned descriptor legality rows |
| 06 | decoder/encoder |
| 07 | IR projection and materialization |
| 08 | typed MicroOp and scheduler metadata |
| 09 | execute/capture |
| 10 | retire publication and all-or-none commit |
| 11 | replay/rollback |
| 12 | executable golden and no-fallback evidence |
| 13 | status/package/compiler handoff |
| 14 | tile-stream resource contour correction |
| 15 | explicit numeric-policy ABI and supported profile matrix |
| 16 | formal MACC arithmetic and layout-policy separation |
| 17 | numeric capture/retire/replay/rollback identity |
| 18 | machine-readable numeric and layout golden corpus |
| 19 | package reclosure and compiler-sideband conformance |

## Active Blockers

No active MatrixTile runtime/ISA blockers remain in this pool.

Current closure decision:

`ClosedCompilerMatrixTileLoweredAnnotationsCarryNumericLayoutPolicySidebands`

## Final Runtime Contour

- `MTILE_LOAD/STORE`: `MatrixTileMemory`, lane6
  `MatrixTileStreamClass`, typed StreamEngine/SRF transport.
- `MTILE_MACC/MTRANSPOSE`: `MatrixTileCompute`, deterministic
  tile-compute placement.
- execute capture is architecturally invisible.
- retire is the only architectural publication and memory commit authority.
- replay/rollback preserves owner, resource, transfer, checkpoint,
  numeric/layout policy, epoch, dependency, and publication-surface identity.
- numeric/layout golden corpus lives at
  `Documentation/Stream WhiteBook/03_MatrixTile/Golden/matrix_tile_numeric_layout_golden_v1.json`.
- ordinary LSU, common ALU fallback for all MTILE, `DmaStreamCompute`, Lane7,
  VMX, scalar/vector/dot, assist, and external backend fallback are prohibited.

## Compiler Boundary

The compiler-owned positive MTILE implementation is a downstream transport
consumer. It now carries the runtime-owned numeric/layout policy sidebands into
source and lowered `InstructionSlotMetadata`, but it does not own arithmetic,
layout legality, tile state, execute capture, retire publication, replay, or
rollback authority.

For compute rows:

- `MTILE_MACC` requires explicit runtime `MatrixTileNumericPolicy` and
  `MatrixTileLayoutPolicy` sidebands;
- `MTRANSPOSE` requires explicit runtime `MatrixTileLayoutPolicy` and carries
  no MACC numeric sideband;
- `MTILE_LOAD/STORE` do not introduce compute numeric authority.

Runtime strict projection/materialization remains the fail-closed authority for
missing, tampered, unsupported, or operation-mismatched sidebands.

## Reopening Rule

Reopen this pool only for a new runtime/ISA requirement or a regression in one
of the closed gates above. Documentation, compiler metadata, telemetry, or
host/backend evidence alone cannot reopen or close a runtime gate.
