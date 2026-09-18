# Phase 02 - Architectural Tile State And Descriptor ABI

## Status: closed/runtime-owned-owner-and-canonical-descriptor-abi

Decision: `GuestArchitecturalTileRegisterFileOwnerSelected`.

`MatrixTileArchitecturalTileRegisterFile` is the guest architectural tile-state
owner. Tiles are keyed by owner thread and tile id and contain a canonical
descriptor plus packed bytes. StreamEngine, SRF, capture records, compiler
objects, and host/backend state are not architectural tile authority.

Decision: `CanonicalMatrixTileDescriptorCarrier`.

The canonical descriptor defines rows, columns, element size, layout, strides,
and packed footprint. zero and reserved descriptors fail closed.

Publication rules:

- execute may snapshot or stage tile data but cannot publish it;
- load, MACC, and transpose publish only at retire;
- rollback restores the core-owned architectural checkpoint.

Dependent contracts are closed:

- Phase 03 tile memory shape/fault;
- Phase 04 accumulator/transpose semantic ABI;
- Phase 05 runtime-owned VLM rows are closed.
