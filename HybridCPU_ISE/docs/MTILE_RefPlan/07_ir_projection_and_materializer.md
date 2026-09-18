# Phase 07 - IR Projection And Materializer

## Status: closed/ir-projection-and-materializer

Decision: `ClosedMatrixTileIrProjectionAndMaterializer`.

InstructionIR tile descriptor projection is opened for the canonical runtime
carrier. registry/materializer entries are opened for all four mnemonics, and
typed runtime objects are materialized.

Invalid IR projections remain blocked before execution. Materialization
preserves opcode, operation kind, owner, descriptor, tile operands, and the
operation-specific resource contour selected by Phase 14.

Compiler IR is not runtime projection evidence.
