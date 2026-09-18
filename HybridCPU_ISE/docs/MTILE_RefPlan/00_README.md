# MTILE Runtime/ISA Refactoring Plan

Status: closed/phase19-compiler-sideband-conformance

Review date: 2026-06-17

## Scope

This directory is the compact runtime/ISA closure record for:

- `MTILE_LOAD`
- `MTILE_STORE`
- `MTILE_MACC`
- `MTRANSPOSE`

Phases 01-19 are closed for opcode authority, architectural state,
tile-stream resources, transport, execute/retire publication, rollback,
runtime-owned numeric/layout policy identity, golden corpus execution, and
compiler-sideband conformance.

The earlier post-Phase14 numeric audit reopened only the numeric-conformance
layer. Phase 19 reclosed package publication after the compiler-owned MTILE
path proved that runtime-owned numeric/layout sidebands survive into lowered
bundle annotations.

No separate MatrixTile Phase 20 is open in this ref-plan. Phase 20 references
elsewhere in the repository belong to other refactoring plans unless a new
MatrixTile requirement explicitly reopens this directory.

## Canonical Architecture

| ISA operation | Semantic class | Runtime resource class | Placement |
| --- | --- | --- | --- |
| `MTILE_LOAD` | memory | `MatrixTileMemory` | `MatrixTileStreamClass`, lane6 |
| `MTILE_STORE` | memory | `MatrixTileMemory` | `MatrixTileStreamClass`, lane6 |
| `MTILE_MACC` | matrix compute | `MatrixTileCompute` | tile-compute on `AluClass` |
| `MTRANSPOSE` | matrix compute | `MatrixTileCompute` | tile-compute on `AluClass` |

Lane6 is a physical carrier shared by typed slot classes. MTILE memory is not
`DmaStreamCompute`, ordinary LSU execution, or generic StreamEngine authority.

The memory path is:

```text
MTILE_LOAD:
memory -> typed MatrixTile stream ingress -> SRF windows -> execute capture
       -> retire -> MatrixTileArchitecturalTileRegisterFile

MTILE_STORE:
architectural tile snapshot -> typed SRF windows -> execute capture
       -> retire all-or-none memory commit -> overlapping SRF invalidation
```

`MTILE_MACC` and `MTRANSPOSE` use MatrixTile compute semantics and publish only
at retire.

## Authority Boundaries

- Runtime-owned legality is final ISA authority.
- `MatrixTileArchitecturalTileRegisterFile` owns architectural tile state.
- StreamEngine/SRF are bounded transport, warming, and staging substrate.
- Execute creates an architecturally invisible typed capture.
- Retire is the only tile, accumulator, transpose, or memory publication owner.
- Replay/rollback binds owner, operation, resource contour, transfer identity,
  and checkpoints; it does not depend on host-owned SRF state.
- Compiler metadata and emission cannot override runtime legality.

## Closure Chain

1. Phase 01: opcode/status authority.
2. Phase 02: architectural tile state and descriptor ABI.
3. Phase 03: memory shape and precise fault ABI.
4. Phase 04: accumulator and transpose semantic ABI.
5. Phase 05: runtime-owned VLM rows.
6. Phase 06: decoder/encoder ABI.
7. Phase 07: IR projection and materializer.
8. Phase 08: typed MicroOp and scheduler metadata.
9. Phase 09: execute/capture.
10. Phase 10: retire publication and commit.
11. Phase 11: replay/rollback.
12. Phase 12: positive golden and no-fallback evidence.
13. Phase 13: runtime package/status/compiler handoff.
14. Phase 14: tile-stream resource contour correction.
15. Phase 15: explicit numeric-policy ABI and supported profile matrix.
16. Phase 16: formal MACC arithmetic and layout-policy separation.
17. Phase 17: numeric capture, retire, replay, and rollback identity.
18. Phase 18: machine-readable numeric and layout golden corpus.
19. Phase 19: package reclosure and compiler-lowering conformance; closed by
    compiler-owned lowered-annotation sideband conformance.

## Current Compiler Fact

The checkout contains a compiler-owned positive MTILE implementation. It is a
downstream transport consumer, not numeric or layout authority. Phase 19 adds
the minimal compiler-sideband bridge: source annotations and lowered
`InstructionSlotMetadata` carry runtime-owned `MatrixTileNumericPolicy` and
`MatrixTileLayoutPolicy` identities where the operation requires them.

`MTILE_MACC` requires explicit numeric and layout sidebands. `MTRANSPOSE`
requires an explicit layout sideband and carries no MACC numeric semantics.
`MTILE_LOAD/STORE` do not introduce compute numeric authority. Runtime strict
projection/materialization remains the fail-closed authority for missing,
tampered, unsupported, or operation-mismatched sidebands. Compiler rejection or
fallback cannot replace runtime rejection.

## Reading Order

- Read `14_tile_stream_resource_contour_correction.md` for the final placement
  and transport decision.
- Read `02`, `09`, `10`, and `11` for state, capture, retire, and replay.
- Read `15` through `19` for the numeric/layout and compiler-sideband closure
  evidence.
- Use `PROMPT_CONTINUE_NUMERIC_CONFORMANCE.md` only as a maintenance prompt
  when auditing a future MatrixTile regression or new requirement.
- Read `99_remaining_open_pool.md` for the current closed-gate summary.
