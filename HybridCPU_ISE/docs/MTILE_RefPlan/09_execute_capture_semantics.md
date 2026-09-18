# Phase 09 - Execute/Capture Semantics

Status: closed/runtime-isa

The executable execute/capture ABI is closed.
`NoExecutionCapturePublication = false` means execution semantics exist; it
does not mean execute may publish architectural state.

Opened capture surfaces:

- Tile load capture buffer is opened.
- Tile store pending write buffer is opened.
- Matrix/tile capture result is opened.
- Deterministic exception capture is opened.
- Memory fault capture ABI is opened.
- Tile-state read snapshot is opened.
- Accumulator read snapshot is opened.
- MTILE memory capture carries a typed `MatrixTileStreamTransferRecord`.

Phase 09 itself opens no retire-owned publication and no replay/rollback authority.
Capture is immutable input to retire and remains architecturally invisible.
Compiler scope remains closed to this runtime phase.
