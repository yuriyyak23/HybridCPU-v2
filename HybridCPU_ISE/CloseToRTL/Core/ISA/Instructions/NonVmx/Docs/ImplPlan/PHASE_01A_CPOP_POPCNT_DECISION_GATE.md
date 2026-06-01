# Phase 01A - CPOP / POPCNT Decision Gate

Date: 2026-05-26

## Slice

First Phase 01 mini-group only: population-count scalar candidate, before
`ANDN`/`ORN`/`XNOR`, scalar min/max, byte/bit reverse, or bitfield forms.

## Production Path Overlay

This gate is now closed. `CPOP` is the canonical hardware mnemonic and
`POPCNT` remains a reserved/no-emission alias boundary. `CPOP` closes the scalar
full production path in `README.md`; `POPCNT` does not receive a parser,
decoder, runtime, or compiler-helper alias in this package. No VMX-specific path
is added.

## Current Status

`CPOP` is closed as an executable scalar ALU/bitmanip row:

- opcode `334`;
- canonical payload `rd, rs1`, encoded as `rd, rs1, rs2=x0` with
  `Immediate=0`;
- decoder/encoder ABI uses the existing packed scalar unary register form;
- `InstructionIR`/projection uses the existing unary scalar register shape;
- registry/materializer publishes `ScalarALUMicroOp`;
- `InternalOpKind.Cpop` is materialized;
- execute semantics are XLEN=64 population count;
- retire-owned scalar writeback and x0 discard are covered by the scalar ALU
  runtime path;
- replay/rollback/conformance and golden vectors are covered by
  `NonVmxPhase01BitCountExecutableTests`;
- compiler helper emission remains closed.

`POPCNT` remains a no-emission alias boundary: no opcode allocation, decoder
row, `InstructionIR` projection, materializer, typed MicroOp, runtime execution,
or compiler helper authority is published.

Existing metadata that mentioned `POPCNT` is intentionally not treated as
runtime authority:

- `IsaV4Surface.OptionalExtensions` mentions `POPCNT`, but optional extension
  naming is metadata only and is not opcode/runtime authority.
- Existing no-emission tests continue to include the `Popcnt` helper facade
  boundary.

## Decision

This iteration selects `CPOP` as the only canonical runtime mnemonic. `POPCNT`
is not accepted as a second opcode and is not opened as parser-only alias or
compiler lowering. A future alias policy may be introduced only by an explicit
compiler/parser decision and new no-emission/alias tests.

## Evidence Gate State

| Gate | State |
|---|---|
| status/catalog | `CPOP` is `OptionalEnabled` / `ConformanceTested`; `POPCNT` remains reserved/no-emission |
| opcode value | `CPOP=334`; no `POPCNT` opcode |
| decoder/encoder ABI | `CPOP` uses canonical unary `rd, rs1, rs2=x0`, `Immediate=0`; `POPCNT` has no decoder row |
| InstructionIR/projection | `CPOP` uses existing unary scalar projection; `POPCNT` has no IR projection |
| registry/materializer | `CPOP` registered through scalar ALU materializer; `POPCNT` has no registry row |
| typed MicroOp / object | `CPOP` publishes `ScalarALUMicroOp` and `InternalOpKind.Cpop`; `POPCNT` remains metadata-only |
| execute/capture semantics | `CPOP` closed as XLEN=64 popcount; `POPCNT` has no execution authority |
| retire/writeback | `CPOP` uses scalar register retire with x0 discard; `POPCNT` publishes no retire path |
| replay/rollback/conformance | `CPOP` covered by Phase 01 bitcount tests; `POPCNT` stays no-emission alias |
| golden/no-emission | `CPOP` has scalar golden vectors and no helper authority; `POPCNT` no-emission remains authoritative |

## Production Follow-On Rules

- Do not add `Opcode` or `Execute` to `PopcntInstruction`.
- Do not infer `POPCNT` executable status from `IsaV4Surface.OptionalExtensions`.
- Add compiler helper emission only by explicit helper authority; otherwise keep
  both mnemonics covered by no-emission tests.
- Add VMX-compatible projection only if a virtualization boundary is crossed;
  ordinary scalar execution stays on the shared runtime path.
