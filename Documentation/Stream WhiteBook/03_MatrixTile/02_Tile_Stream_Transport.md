# MatrixTile Tile-Stream Transport

## Control-plane annotations versus data-plane transfer

MatrixTile uses two related but separate paths:

- the VLIW annotation control-plane, which carries compiler-produced
  `LoweredBundleAnnotations` to runtime projection and materialization; and
- the MatrixTile memory data-plane, which stages `MTILE_LOAD` and
  `MTILE_STORE` bytes through typed lane6/SRF transport.

The annotation ingress path is:

```text
Compiler LoweredBundleAnnotations
-> EmitProgram / EmitVliwBundleImage
-> MainMemory VLIW annotation store
-> L2 VLIW cache annotation carrier
-> L1 VLIW cache annotation carrier
-> pipeIF.BundleAnnotations
-> production decode
-> MatrixTile IR projection/materializer
-> typed MatrixTileMicroOp
-> scheduler and issue placement
-> execute capture
-> writeback-owned retire publication
```

This path carries policy and placement metadata. It does not move tile payload
bytes and does not publish architectural tile state. Runtime projection,
materialization, execute capture, and retire validation remain the authority.
Compiler annotations may preserve runtime-owned numeric and layout identities,
but they cannot make those identities legal, cannot grant MatrixTile memory
authority, and cannot replace fail-closed runtime rejection.

## Memory contour and placement

`MTILE_LOAD` and `MTILE_STORE` are `MatrixTileMemory`, not compute operations.
They retain memory-semantic ordering and conflict facts, but their hard
placement is `MatrixTileStreamClass` on physical lane6 (mask `0b_0100_0000`),
with capacity one. `DmaStreamClass` shares that physical lane, so the two slot
classes cannot be admitted together. Memory ordering metadata does not turn an
MTILE operation into ordinary LSU authority.

The typed transfer contour is:

```text
MTILE_LOAD
MainMemory rows
-> typed MatrixTile ingress
-> _matrixTileStreamRegisterFile / SRF row windows on lane6
-> staged tile execute capture
-> retire validation
-> _matrixTileRegisterFile / MatrixTileArchitecturalTileRegisterFile

MTILE_STORE
_matrixTileRegisterFile / architectural tile snapshot
-> typed MatrixTile egress
-> _matrixTileStreamRegisterFile / SRF row windows on lane6
-> staged store execute capture
-> retire all-or-none memory commit
-> overlapping SRF-window invalidation
```

StreamEngine and SRF are bounded transport and staging resources. A typed
MatrixTile session records the row windows and produces a
`MatrixTileStreamTransferRecord`; it neither publishes tile state nor mutates
memory before retire.

The implementation has two distinct register-file roles:

- `_matrixTileStreamRegisterFile` is the bounded SRF staging substrate used by
  typed MatrixTile stream sessions.
- `_matrixTileRegisterFile` / `MatrixTileArchitecturalTileRegisterFile` is the
  guest architectural tile-state owner.

Therefore, it is inaccurate to describe `MTILE_LOAD` as a generic DMA path that
loads directly into the architectural MatrixTile register file. Load rows are
staged through the typed lane6/SRF data-plane, captured invisibly at execute,
and published to the architectural tile register file only at retire.

## Transfer and fault identity

Each typed transfer binds core, owner thread, opcode, operation kind,
`MatrixTileMemory` resource class, `MatrixTileStreamClass`, lane6, channel,
direction, expected row count, row-window identities, byte count, completion,
and transfer fingerprint. A row-window identity includes its row, byte offset,
byte count, and data fingerprint. Load direction is memory ingress; store
direction is tile egress.

The runtime validates the canonical descriptor, explicit runtime effective
address, element alignment, row ranges, stride, and address overflow. A memory
fault has a precise row/column point and remains bound to the operation,
owner, descriptor, capture, and replay identity. Page crossing is allowed only
under that precise-fault model.

At retire, load can publish its staged tile image to
`MatrixTileArchitecturalTileRegisterFile`. Store commits its complete staged
write set all-or-none; a failed commit restores its memory checkpoint. A fault,
cancellation, invalid transfer, or failed validation creates no partial
architectural tile change and no partial store. Completed or rolled-back stores
invalidate overlapping SRF windows deterministically.

## Fail-closed boundaries

Retire rejects a missing or mismatched typed transfer, wrong owner/core/opcode/
operation, wrong resource or slot class, wrong lane/channel/direction,
incomplete non-faulted transport, and any transfer claiming tile publication,
pre-retire memory mutation, generic StreamEngine execution authority,
`DmaStreamCompute` authority, or host-owned architectural evidence.

`DmaStreamClass` and `MatrixTileStreamClass` alias the same physical lane6
capacity, so they conflict in placement. That physical alias is not semantic
ownership. MatrixTile memory uses `MatrixTileStreamClass` and explicitly keeps
`UsesDmaStreamComputeAuthority`, generic StreamEngine execution authority, and
host-owned architectural evidence false.

MTILE memory transport has no compute numeric or layout authority:
`MTILE_LOAD` and `MTILE_STORE` neither require nor create MACC numeric/layout
sidebands. It is prohibited to substitute ordinary LSU, generic ALU/vector,
DSC, Lane7, VMX, assist, or external-backend paths for this contour. Runtime
projection/materialization and retire validation remain the authority; compiler
metadata cannot reinterpret the transfer or replace a runtime rejection.
