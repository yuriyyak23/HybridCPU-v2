# DmaStreamCompute Contract Pack

Status: current bounded DSC1 contour

`DmaStreamCompute` is the lane6 descriptor/token execution plane. It is not a
custom accelerator, StreamEngine vector execution, MatrixTile, or assist.

## Placement

- runtime class: DSC/DMA stream compute;
- slot class: `SlotClass.DmaStreamClass`;
- physical lane: 6;
- physical capacity conflicts with `MatrixTileStreamClass`.

## Reading Order

1. `01_Current_Contract.md`
2. `02_Phase_Evidence_Ledger.md`
3. `03_Validation_And_Rollback.md`
4. `../05_Lane6_Lane7_Separation.md`

The former continuation prompt and superseded Phase 3 ADR were removed from the
current WhiteBook. Git history remains the archive for those planning artifacts.

## Boundary

The current DSC1 materialized path is executable. Unsupported descriptor shapes,
DSC2 execution, queue/async lifecycle, coherent DMA/cache, successful partial
completion, and fallback to StreamEngine, DMAController, scalar, vector, or
custom accelerator execution fail closed.
