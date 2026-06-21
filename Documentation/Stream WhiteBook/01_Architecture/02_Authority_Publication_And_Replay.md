# Authority, Publication, And Replay

## Authority Stack

1. ISA semantic class describes architectural meaning.
2. Runtime resource class identifies the owning execution contour.
3. Typed-slot metadata controls physical placement.
4. Transport moves or stages data.
5. Execute produces operation-owned results or capture.
6. Retire or the contour-specific guarded commit publishes architecture.
7. Replay/rollback restores the owning architectural checkpoint.

Skipping a layer is invalid. In particular, semantic `Memory` does not imply
ordinary LSU ownership.

## Architectural Owners

- scalar/vector register and predicate state: core architectural state;
- tile state: `MatrixTileArchitecturalTileRegisterFile`;
- memory: bound runtime memory, changed only by the owning commit path;
- DSC staged destination: token-owned until guarded commit;
- L7 staged destination: accelerator commit coordinator until commit.

SRF, StreamEngine buffers, capture records, telemetry, and backend-local state
are not architectural owners.

## Publication Rules

- vector helper output is visible only through its canonical instruction path;
- MTILE load/MACC/transpose publish at retire;
- MTILE store commits at retire, all-or-none;
- DSC publishes through guarded token commit;
- L7 publishes through its guarded coordinator;
- assists never retire architecturally.

## Replay Rules

Replay identity is contour-specific. Reuse requires exact owner, operation,
descriptor/shape, placement, resource, and checkpoint identity. Transport
state may be fingerprinted, but current SRF contents or telemetry cannot prove
guest state.

## Fail-Closed Rule

Wrong-owner, stale, duplicate, cancelled, mismatched-resource, incomplete, or
partially committed evidence cannot be normalized into success or redirected
to another execution plane.
