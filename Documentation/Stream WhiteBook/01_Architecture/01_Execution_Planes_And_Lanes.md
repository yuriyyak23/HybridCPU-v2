# Execution Planes And Physical Lanes

## Semantic Plane Is Not Physical Placement

HybridCPU uses fixed `W=8` physical lanes, but a lane is a carrier, not a
semantic class. Runtime metadata must identify both the operation contour and
the eligible lane class.

| Runtime contour | Slot class | Physical lanes |
| --- | --- | --- |
| scalar/vector ALU carriers | `AluClass` | 0-3 |
| ordinary load/store carriers | `LsuClass` | 4-5 |
| DSC and lane6 assist carriers | `DmaStreamClass` | 6 |
| MTILE memory transport | `MatrixTileStreamClass` | 6 |
| branch | `BranchControl` | 7 |
| system/L7 command | `SystemSingleton` | 7 |

`DmaStreamClass` and `MatrixTileStreamClass` alias lane6 with combined physical
capacity one. `BranchControl` and `SystemSingleton` similarly alias lane7.

## Vector Stream Plane

Vector instructions may use `StreamEngine`, `BurstIO`, SRF, and `VectorALU`.
Those helpers do not create a `StreamVectorClass`; placement remains owned by
the materialized MicroOp and typed-slot scheduler.

## MatrixTile Plane

MatrixTile is a distinct execution plane:

- memory semantics: `MatrixTileMemory`, lane6 tile-stream;
- compute semantics: `MatrixTileCompute`, deterministic tile-compute placement;
- state authority: `MatrixTileArchitecturalTileRegisterFile`;
- publication authority: retire.

## DSC Plane

`DmaStreamCompute` owns DSC descriptor, guard, token, backend, replay, and commit
semantics. It must not consume the MTILE transfer ABI or use StreamEngine as a
fallback.

## Assist Plane

Assists are non-retiring MicroOps. They may be LSU-hosted cache prefetch or
lane6 DMA-class SRF prefetch. Their carrier choice does not grant ISA execution
or architectural publication.

## L7 Plane

External accelerator commands use lane7 `SystemSingleton`. Their descriptor,
token, register ABI, backend staging, and commit coordinator remain separate
from lane6 DSC, MTILE, and assists.
