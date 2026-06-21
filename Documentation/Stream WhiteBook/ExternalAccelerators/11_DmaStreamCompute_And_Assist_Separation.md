# Execution Contour Separation

This page records the boundary between adjacent stream, matrix, assist, DSC,
and external-accelerator surfaces. Shared physical resources do not merge their
ISA, descriptor, publication, or replay authority.

## VectorStream

`StreamEngine` orchestrates vector-stream work. SRF is transient stream state,
`BurstIO` performs bounded memory transport, and `VectorALU` supplies typed
vector compute. None of these helpers chooses an ISA slot or publishes guest
architectural MatrixTile state.

Canonical documentation:
`Documentation/Stream WhiteBook/02_VectorStream/`.

## MatrixTile

`MTILE_LOAD` and `MTILE_STORE` are memory-semantic ISA instructions with
runtime class `MatrixTileMemory`. They use `MatrixTileStreamClass` on physical
lane6 and a typed StreamEngine/SRF transfer envelope.

`MTILE_MACC` and `MTRANSPOSE` use the separate `MatrixTileCompute` contour.
Execute creates an invisible capture; retire is the only tile, accumulator, or
store publication authority. Stream completion and SRF valid/dirty state are
not architectural evidence.

Canonical documentation:
`Documentation/Stream WhiteBook/03_MatrixTile/`.

## DmaStreamCompute

`DmaStreamCompute` is the lane6 descriptor carrier plus explicit runtime/helper
token model for descriptor-backed stream compute. Its micro-op uses
`SlotClass.DmaStreamClass`. The current DSC1 path enters
`DmaStreamComputeRuntime.ExecuteMaterializedMicroOpToCommitPending(...)`.

DSC2 execution, queue/async lifecycle, and fallback to StreamEngine,
DMAController, scalar, vector, MatrixTile, assist, or L7-SDC fail closed.
Physical lane6 conflict with MatrixTile does not grant DSC descriptor, token,
queue, replay, or commit authority to MTILE.

Canonical documentation:
`Documentation/Stream WhiteBook/DmaStreamCompute/01_Current_Contract.md`.

## Assists

Assist is architecturally invisible, non-retiring, replay-discardable warming.
An assist may consume LSU or lane6 DMA-class transport resources, but it does
not become VectorALU execution, DSC descriptor acceptance, MatrixTile
transport, or L7-SDC authority.

Canonical documentation:
`Documentation/Stream WhiteBook/04_Assists/`.

## L7-SDC

The current external-accelerator command contour uses lane7
`SystemSingleton`. Its typed descriptor, guard, token, backend staging,
commit coordinator, and register ABI are scoped L7 runtime surfaces. Production
L7-SDC paths do not call `ICustomAccelerator.Execute()`.

L7-SDC is not a lane6 fallback for VectorStream, MatrixTile, DSC, or assists.

## Ownership Glossary

- `DMA`: separate memory-transfer controller/channel API.
- `StreamEngine`: in-core vector-stream orchestration and typed MTILE transport
  substrate.
- `SRF`: transient stream register/window state, never architectural tile
  authority.
- `VectorALU`: typed vector compute helper.
- `MatrixTileRegFile`: architectural tile-state authority.
- `DmaStreamCompute`: DSC descriptor/token/commit execution contour.
- `Assist`: non-retiring warming contour.
- `ExternalAccelerator`: lane7 L7-SDC command contour.
- `backend`: scoped executor behind a guard, never automatic fallback
  authority.
- `commit`: contour-specific publication operation; transport completion is
  not commit.

## Fail-Closed Rule

Wrong owner, wrong domain, wrong lane, stale token/capture, mismatched resource
identity, missing staged payload, or attempted cross-contour reuse rejects
execution or publication. Telemetry, certificates, host state, SRF state, and
compiler metadata cannot substitute for runtime-owned legality.

Historical refactoring records may mention superseded paths. They are archive
evidence only and are not current WhiteBook roots.
