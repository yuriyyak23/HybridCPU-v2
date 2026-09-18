# Phase 06 - Decoder/Encoder ABI

## Status: closed/decoder-encoder-abi

Decision: `ClosedMatrixTileDecoderEncoderAbi`.

The runtime decoder and encoder preserve the Canonical vector-carrier binary field layout.
They also preserve descriptor projection, mnemonic/opcode identity, datatype,
tile operands, and reserved-bit rules.

Encoder round-trip is executable evidence.
Illegal rows remain rejected before typed MicroOp/scheduler materialization.
Compiler acceptance is not used as evidence.

The historical blocking reason moved forward through execute and retire and is
now cleared: all downstream phases, including Phase 14 resource correction,
are closed.
