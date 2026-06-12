# MatrixTile Tile-Stream Transport

## Placement

`MTILE_LOAD` and `MTILE_STORE` classify as `MatrixTileMemory` and require
`MatrixTileStreamClass` on physical lane6. Capacity is one and conflicts with
`DmaStreamClass`.

## Load

```text
bound memory
  -> typed MatrixTile ingress session
  -> SRF row windows
  -> MatrixTileExecutionCaptureRecord
  -> retire validation
  -> MatrixTileArchitecturalTileRegisterFile
```

## Store

```text
architectural tile snapshot
  -> typed MatrixTile egress session
  -> SRF row windows and staged writes
  -> MatrixTileExecutionCaptureRecord
  -> retire all-or-none memory commit
  -> overlapping SRF invalidation
```

## Transfer Identity

`MatrixTileStreamTransferRecord` binds owner, core, opcode, operation kind,
resource class, slot class, lane6, StreamEngine channel, transfer direction,
row windows, total bytes, and fingerprint.

## Boundaries

- StreamEngine/SRF transport does not publish tile state.
- Store transport does not mutate memory before retire.
- Generic StreamEngine write modes are not MTILE authority.
- SRF valid/dirty/host state is not guest architectural evidence.
- DSC descriptors, tokens, queues, and commit are not used.
- precise row/column fault identity and all-or-none rollback are preserved.
