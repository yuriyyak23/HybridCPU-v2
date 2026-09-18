# Phase 08 - Typed Tile MicroOp And Scheduler Metadata

Status: closed/runtime-isa

Typed tile MicroOp ABI is opened for all four MTILE operations.
Scheduler lane binding is opened through operation-specific metadata, not a
shared constructor default.

Phase 14 supersedes the former all-ALU placement:

- load/store -> `MatrixTileMemory`, `MatrixTileStreamClass`, lane6;
- MACC/transpose -> `MatrixTileCompute`, deterministic tile-compute placement.

Tile memory dependency metadata is published for load/store.
Tile register and accumulator dependency metadata is published for compute and
tile-state operations.

Resource masks distinguish memory ordering facts from placement authority and
include typed StreamEngine, SRF/window, tile ingress/egress, tile-state,
accumulator, and transpose channels as applicable.

Scheduler/materializer/VLM bypass remains impossible.
Compiler scope remains closed to this runtime phase.
