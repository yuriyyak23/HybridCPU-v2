# Phase 14 - Tile-Stream Resource Contour Correction

Status: closed/runtime-isa

Review date: 2026-06-12

## Audit Finding

The pre-correction implementation had three coupled defects:

- all four MTILE MicroOps could inherit common `AluClass` placement;
- `MTILE_LOAD/STORE` could be described mainly by ordinary
  `ForLoad()`/`ForStore()` resource facts;
- memory transfer could bypass a typed MatrixTile StreamEngine/SRF identity.

That model confused ISA semantic class, runtime resource ownership, physical
placement, and transport substrate.

## Closed Decision

`MatrixTileResourceContour` classifies before generic memory/ALU mapping:

- `MTILE_LOAD/STORE` -> `MatrixTileMemory`;
- `MTILE_MACC/MTRANSPOSE` -> `MatrixTileCompute`.

`MTILE_LOAD/STORE` retain `InstructionClass.Memory` as semantic metadata but use
`SlotClass.MatrixTileStreamClass`, physical mask `0b_0100_0000`, lane6,
capacity one. `DmaStreamClass` and `MatrixTileStreamClass` alias the same
physical lane and therefore cannot be admitted together.

Compute operations retain deterministic tile-compute placement. They do not
own LSU, StreamEngine, or SRF resources.

## Resource Model

MTILE memory metadata includes:

- memory domain and ordering facts;
- StreamEngine channel;
- SRF/window ownership;
- MatrixTile ingress or egress;
- architectural tile-state read or write.

`ForLoad()` and `ForStore()` remain ordering/conflict facts only. They are not
placement or execution authority.

## Typed Transfer

`MatrixTileStreamTransferAbi` provides the bounded transfer envelope:

```text
load:  memory -> typed ingress -> SRF windows -> staged tile capture
store: architectural tile snapshot -> typed SRF windows -> staged writes
```

The transfer record binds core, owner, opcode, operation kind, runtime resource
class, slot class, lane, channel, direction, row windows, byte count, and
fingerprint.

It explicitly declares:

- no architectural tile publication;
- no memory mutation before retire;
- no `DmaStreamCompute` authority;
- no generic StreamEngine execution authority;
- no host-owned architectural evidence.

## Execute, Retire, Replay

- Execute produces only `MatrixTileExecutionCaptureRecord`.
- Load publishes the tile only at retire.
- Store writes memory only through retire-owned all-or-none commit.
- MACC publishes the accumulator only at retire.
- Transpose publishes the destination tile only at retire.
- Store commit and rollback invalidate overlapping SRF windows deterministically.
- Replay identity includes the MatrixTile resource class, slot class, resource
  contour fingerprint, and stream transfer fingerprint.
- stale, duplicate, wrong-owner, wrong-resource, cancelled, or mismatched
  records fail closed without architectural side effects.

## Production Evidence

The Phase 14 suite covers all four opcodes, lane6 alias/capacity, resource
masks, typed load/store transport, retire-only publication, all-or-none store,
SRF invalidation, capture tamper rejection, and replay resource identity.

Dependent Phase 08-13 regressions and regenerated Phase 12 no-fallback evidence
are closed. `ClosesGoldenArtifacts = true` and positive handoff readiness are
restored.

No compiler source was changed and no positive compiler implementation was
performed by Phase 14. The checkout already contains a separate compiler-owned
positive MTILE implementation; runtime legality remains final authority.

## Exit Criteria

All exit criteria are met. Phase 14 is closed.
