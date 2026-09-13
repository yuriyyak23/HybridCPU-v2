# Lane6 And Lane7 Execution Separation

## Lane6

Three distinct contours may touch lane6-related resources:

| Contour | Slot class | Meaning |
| --- | --- | --- |
| `DmaStreamCompute` | `DmaStreamClass` | DSC descriptor/token compute |
| lane6 assist | `DmaStreamClass` | non-retiring SRF warming |
| MTILE load/store | `MatrixTileStreamClass` | typed tile-stream memory transport |

They share physical capacity but not descriptor, resource, execution, retire,
replay, or commit ABI.

The deep DSC contract remains under `DmaStreamCompute/`.

## Lane7

L7 external accelerator commands use `SystemSingleton` on lane7. Branches use
the aliased `BranchControl` class. L7 descriptor, token, backend staging,
register ABI, and commit coordinator are independent from lane6.

The deep L7 contract remains under `ExternalAccelerators/`.

## Forbidden Substitutions

- MTILE -> DSC
- MTILE -> ordinary LSU or VectorALU
- DSC -> StreamEngine or DMAController fallback
- assist warm success -> DSC/MTILE/L7 execution
- L7 backend success -> direct architectural commit
- physical lane alias -> semantic or ABI alias
