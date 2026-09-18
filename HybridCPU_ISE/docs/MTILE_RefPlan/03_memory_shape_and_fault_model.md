# Phase 03 - Tile Memory Shape And Fault Model

## Status: closed/runtime-owned-memory-shape-and-fault-abi

Decision: `ClosedTileMemoryShapeAndFaultAbi`.

The memory ABI uses `ExplicitRuntimeTileMemoryOperandEaSelected`. Effective
address comes from the runtime tile-memory operand and canonical descriptor.
EA fallback remains non-authority.

Validation is `DescriptorRowsColumnsElementStrideAndAddressOverflowValidated`.
Each row address and byte range is checked before architectural publication.

Fault identity is
`PreciseRowColumnFaultPointAndReplayIdentitySelected`: operation, row, column,
address, owner, descriptor, and replay identity remain deterministic.

Publication is `RetireOwnedLoadPublicationAndStoreCommitSelected`:

- load stages bytes and publishes the tile only at retire;
- store stages all rows and commits all-or-none at retire;
- partial commit failure restores memory from checkpoints.

Phase 05 runtime-owned VLM rows are closed. Stream transport changes in Phase
14 preserve this memory and fault ABI.
