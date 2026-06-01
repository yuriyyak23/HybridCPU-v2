# Phase 01D - Byte/Bit Reverse Decision Gate

Date: 2026-05-26

## Slice

Fourth Phase 01 mini-group only: `REV8` and `BREV8`.

This package does not advance bitfield, conditional select, vector byte/bit
reverse, compiler helper, Lane6, Lane7, or VMX-specific work.

## Production Path Overlay

`REV8` and `BREV8` are closed through the scalar full production path in
`README.md`: opcode/status, canonical unary payload, byte/bit-order ABI, IR,
registry/materializer, typed MicroOp/InternalOp, ALU/dispatcher semantics,
retire/x0, replay, golden vectors, deferred guardrail removal, and compiler
no-emission. Do not reuse vector `VBREV8` evidence as scalar evidence. VMX is
generic only.

## Current Status

`REV8` and `BREV8` are now production scalar ALU rows. Their leaf metadata is
promoted from `DeferredNoEmission` to `ExecutableScalarAlu`, with opcode,
decoder/materializer, typed MicroOp/InternalOp, execute/capture, retire,
replay, and golden evidence closed by the Phase 01D package.

The compiler boundary remains no-emission: no helper/facade authority is opened
by this slice. `InstructionSupportStatusCatalog` keeps both mnemonics as
`OptionalEnabled` with `RuntimeInstructionEvidence.ConformanceTested`.

## Decision

Phase 01D closes the byte/bit reverse production mini-group after the closed
Phase 01B `ANDN`/`ORN`/`XNOR` and Phase 01C `MIN`/`MAX`/`MINU`/`MAXU` slices.
The scalar opcode/runtime decision is:

- `REV8 = 331`, `InternalOpKind.Rev8`;
- `BREV8 = 332`, `InternalOpKind.Brev8`.

The canonical scalar payload is unary `rd, rs1`, encoded with `rs2=x0` and
`Immediate=0`. Non-zero `rs2` or non-zero immediate is rejected as a
non-canonical alias. Compiler helper emission remains closed: no facade/helper
authority is opened by this slice. VMX remains generic only.

The exact ordering ABI is:

- `REV8` must define byte-order reversal over XLEN=64 without relying on
  mnemonic text alone: byte 0 moves to byte 7, byte 1 to byte 6, and so on;
- `BREV8` must define bit reversal within each byte while preserving byte
  positions; bit 0 of each byte moves to bit 7 of that same byte, and so on;
- scalar `REV8`/`BREV8` evidence is separate from vector `VBREV8` evidence;
- no compiler helper may infer scalar support from vector, metadata-only, or
  parser-adjacent rows.

## Evidence Gate State

| Gate | State |
|---|---|
| status/catalog | Closed as `OptionalEnabled`, `ConformanceTested`, `ScalarBitmanipCore` |
| opcode value | Closed: `REV8=331`, `BREV8=332` |
| decoder/encoder ABI | Closed: unary `rd, rs1`, `rs2=x0`, `Immediate=0` |
| InstructionIR/projection | Closed through canonical scalar unary IR |
| registry/materializer | Closed through scalar unary register materializer |
| typed MicroOp / object | Closed as `ScalarALUMicroOp`, `InternalOpKind.Rev8/Brev8`, executable CloseToRTL objects |
| execute/capture semantics | Closed through `ScalarAluOps`, dispatcher capture, and CloseToRTL `Execute` |
| retire/writeback | Closed: scalar register writeback, x0 discard, no side effects |
| replay/rollback/conformance | Closed by Phase 01D executable tests |
| golden/no-emission | Closed by scalar golden vectors and compiler no-emission guardrails |

## Production Follow-On Rules

- Future edits must preserve the full scalar production path: no change to
  opcode values, ordering ABI, materializer shape, or helper authority without
  updating decoder, IR, execution, retire, replay, status, and golden evidence.
- Do not infer any other executable support from `Operation`, `MicroOpShape`, or
  byte/bit-order metadata.
- Do not reuse vector `VBREV8` evidence as scalar ISA evidence.
- Add compiler helper emission only by explicit helper authority.
- Add VMX-compatible projection only if a virtualization boundary is crossed.
