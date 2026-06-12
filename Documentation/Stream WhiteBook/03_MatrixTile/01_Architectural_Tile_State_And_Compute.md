# MatrixTile Architectural State And Compute

## Tile Register File

The MatrixRegisterFile implementation,
`MatrixTileArchitecturalTileRegisterFile`, stores canonical packed
`MatrixTileTileImage` values keyed by owner thread and tile id. It validates
owner range, descriptor equality, packed footprint, and snapshot cloning.

This register file is the architectural authority. The CPU core owns its
lifetime and exposes controlled seed, snapshot, publish, remove, and rollback
operations.

## Compute Contour

`MTILE_MACC` and `MTRANSPOSE` classify as `MatrixTileCompute`.

- MACC captures source tiles and accumulator state, validates shape and dtype,
  computes a staged result, and publishes the accumulator only at retire.
- Transpose captures the source tile, validates alias/layout policy, computes a
  staged destination, and publishes only at retire.

Their physical compute placement may use `AluClass`, but runtime resource
ownership remains MatrixTile compute. They do not become generic vector or
scalar ALU operations.

## Fault And Replay

Faulted or cancelled captures do not publish. Replay uses core-owned tile and
accumulator checkpoints and rejects stale, duplicate, wrong-owner, or
wrong-resource identities.
