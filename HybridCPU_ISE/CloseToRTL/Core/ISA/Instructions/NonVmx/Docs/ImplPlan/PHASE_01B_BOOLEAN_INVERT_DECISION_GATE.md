# Phase 01B - Boolean-Invert Decision Gate

Date: 2026-05-26

## Slice

Second Phase 01 mini-group only: `ANDN`, `ORN`, and `XNOR`.

This package does not advance scalar min/max, byte/bit reverse, bitfield,
conditional select, vector, Lane6, Lane7, compiler helper, or VMX-specific
work.

## Production Path Overlay

Closed rows in this phase are the reference scalar full-path pattern. New
boolean/bitmanip rows should use the same scalar profile in `README.md`: opcode,
status/catalog, decoder/encoder canonical payload, IR/projector, registry,
typed MicroOp/InternalOp, ALU/dispatcher, retire/x0, replay, golden vectors,
compiler no-emission, and generic VMX boundary only.

## Current Status

`ANDN`, `ORN`, and `XNOR` are now selected as production hardware-form rows for
this mini-group. The closed opcode allocation is:

- `ANDN = 65`;
- `ORN = 66`;
- `XNOR = 67`.

All three rows use canonical scalar register-register payload
`rd, rs1, rs2` with `Immediate=0`. Non-zero immediate aliases are rejected by
the decoder/materializer ABI.

`InstructionSupportStatusCatalog` keeps all three rows as `OptionalEnabled`
with `RuntimeInstructionEvidence.ConformanceTested` only after the full
opcode/decoder/IR/materializer/execute/retire/replay/test chain is closed.

## Decision

Open `ANDN`, `ORN`, and `XNOR` as single scalar register-register hardware
opcodes. Keep compiler helper emission closed; no hidden multi-op lowering is
allowed for `rs1 & ~rs2`, `rs1 | ~rs2`, or `~(rs1 ^ rs2)`.

## Evidence Gate State

| Gate | State |
|---|---|
| status/catalog | `ANDN`/`ORN`/`XNOR` closed as optional enabled |
| opcode value or facade decision | Hardware opcodes allocated: 65/66/67 |
| decoder/encoder ABI | Canonical `rd, rs1, rs2`, `Immediate=0`; alias rejection covered |
| InstructionIR/projection | Scalar IR `Rd, Rs1, Rs2, Imm=0` |
| registry/materializer | Each row publishes `ScalarALUMicroOp` |
| typed MicroOp / object | `InternalOpKind.AndN`, `OrN`, and `Xnor` closed |
| execute/capture semantics | ALU and dispatcher capture closed for all three |
| retire/writeback | Scalar register writeback with x0 discard covered |
| replay/rollback/conformance | Rollback and pipeline conformance covered |
| golden/no-emission | Golden vectors covered; compiler helper no-emission remains authoritative |

## Preserved Boundaries

- Do not infer executable support from the `Operation` metadata strings.
- Do not lower compiler helpers through `AND`/`OR`/`XOR` plus `NOT` without an
  explicit facade policy and no-emission update.
- Add VMX-compatible projection only if a virtualization boundary is crossed;
  ordinary boolean invert execution stays on the shared runtime path.
