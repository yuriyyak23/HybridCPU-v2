# Phase 10 - Retire Publication And Commit

Status: closed/runtime-isa

Decision: `ClosedMatrixTileRetirePublicationAndCommit`.

Publication rows:

- `RetireOwnedTileLoadPublication`
- `RetireOwnedAllOrNoneTileStoreCommit`
- `RetireOwnedAccumulatorPublication`
- `RetireOwnedTransposeDestinationPublication`
- `DeterministicFaultRetirementWithoutPartialPublication`

`MatrixTileMicroOpWriteBackOwnsCaptureConsumption`. Retire validates capture
identity, owner, opcode, descriptor, resource contour, stream transfer, and
fault/cancellation state before publication.

`CaptureInvisibleUntilRetirePublication` remains the governing boundary.
Store snapshots destination memory before commit and rolls back partial write
failure. Successful or rolled-back store completion invalidates overlapping
SRF windows deterministically.

Compiler scope remains closed to this runtime phase.
