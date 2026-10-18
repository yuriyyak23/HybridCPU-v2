# HybridCPU Stream, Vector, Matrix, And Accelerator WhiteBook

Status: current architecture reference

Review date: 2026-06-12

## Purpose

This WhiteBook separates the execution and transport planes that share memory,
stream buffers, or physical lanes but do not share semantic authority.

## Canonical Taxonomy

| Plane | Role | Architectural authority |
| --- | --- | --- |
| Vector ISA/runtime | vector instruction semantics and retire-visible vector state | runtime legality plus canonical retire path |
| `StreamEngine` | strip-mining, address-mode dispatch, buffer orchestration | no independent ISA or retire authority |
| `VectorALU` | typed element compute over supplied spans | compute helper only |
| `BurstIO` | bounded memory movement through planned bursts/backends | transport only |
| SRF (`StreamRegisterFile`) | transient ingress warming, staging, and exact-window bypass | never guest architectural state |
| MatrixTile | tile memory and tile compute execution plane | runtime legality plus retire-owned publication |
| MatrixRegisterFile (`MatrixTileArchitecturalTileRegisterFile`) | architectural tile images by owner/tile id | authoritative tile state |
| `DmaStreamCompute` | lane6 DSC descriptor/token/commit execution | separate DSC authority, not StreamEngine or MTILE |
| assists | non-retiring cache/SRF warming | no architectural publication |
| L7 external accelerators | lane7 system-device command contour | separate guarded command/commit model |

## Physical Topology

```text
lanes 0..3 : AluClass
lanes 4..5 : LsuClass
lane 6     : DmaStreamClass or MatrixTileStreamClass
lane 7     : BranchControl or SystemSingleton
```

Physical aliasing does not merge ABIs:

- MTILE lane6 is not `DmaStreamCompute`.
- lane6 assist warming is not MTILE or DSC execution.
- StreamEngine use does not imply lane6 ownership.
- VectorALU has no standalone slot class.
- lane7 L7 commands are not branch execution.

## Reading Order

1. `01_Architecture/01_Execution_Planes_And_Lanes.md`
2. `01_Architecture/02_Authority_Publication_And_Replay.md`
3. `02_VectorStream/00_README.md`
4. `02_VectorStream/01_StreamEngine.md`
5. `02_VectorStream/02_SRF_StreamRegisterFile.md`
6. `02_VectorStream/03_BurstIO_And_Memory_Backends.md`
7. `02_VectorStream/04_VectorALU.md`
8. `03_MatrixTile/00_README.md`
9. `03_MatrixTile/01_Architectural_Tile_State_And_Compute.md`
10. `03_MatrixTile/02_Tile_Stream_Transport.md`
11. `04_Assists/00_README.md`
12. `05_Lane6_Lane7_Separation.md`

The existing `DmaStreamCompute/` and `ExternalAccelerators/` packs remain stable
deep references. The obsolete combined compatibility folder was removed
because its name implied a false shared contour.

## Naming

`SRF` is the canonical abbreviation used here for `StreamRegisterFile`.
Historical documents sometimes used `SFR`; that spelling does not denote a
different architectural structure.

## Non-Inversion Rule

Transport completion, SRF state, telemetry, replay fingerprints, descriptors,
tokens, compiler metadata, backend success, and parser acceptance are evidence
only unless the owning runtime contour explicitly validates and publishes them.
