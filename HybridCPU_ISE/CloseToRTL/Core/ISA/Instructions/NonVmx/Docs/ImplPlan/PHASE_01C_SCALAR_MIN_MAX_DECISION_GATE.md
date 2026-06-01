# Phase 01C - Scalar Min/Max Production Closure

Date: 2026-05-27

## Slice

Third Phase 01 mini-group only: `MIN`, `MAX`, `MINU`, and `MAXU`.

This package does not advance byte/bit reverse, bitfield, conditional select,
vector prefix/reduction min/max, Lane6 descriptor min/max, compiler helper, or
VMX-specific work.

## Production Path Overlay

This phase is closed and is the reference scalar signedness pattern. Future
scalar compare/min/max-like rows use the scalar full production path in
`README.md`; vector min/max, AMO min/max, and Lane6 descriptor min/max remain
separate evidence domains. VMX remains generic unless a row crosses a
virtualization boundary.

## Decision

Open scalar hardware rows for XLEN=64 register-register min/max:

- `MIN = 327`: signed minimum, `rd = ((int64)rs1 <= (int64)rs2) ? rs1 : rs2`;
- `MAX = 328`: signed maximum, `rd = ((int64)rs1 >= (int64)rs2) ? rs1 : rs2`;
- `MINU = 329`: unsigned minimum, `rd = (rs1 <= rs2) ? rs1 : rs2`;
- `MAXU = 330`: unsigned maximum, `rd = (rs1 >= rs2) ? rs1 : rs2`.

The opcodes live in the scalar extension continuation block after the already
allocated vector extension rows. Internal publication is typed as
`InternalOpKind.Min`, `Max`, `MinU`, and `MaxU`; no signedness sideband is
required because signedness is encoded by the opcode/internal kind pair.

## Evidence Gate State

| Gate | State |
|---|---|
| status/catalog | `OptionalEnabled`, `ConformanceTested`, `ScalarBitmanipCore` |
| opcode value | `327`, `328`, `329`, `330` allocated in `CPU_Core.Enums.cs` |
| decoder/encoder ABI | canonical scalar `rd, rs1, rs2`, `Immediate=0`; non-zero immediate aliases rejected |
| InstructionIR/projection | `Rd`, `Rs1`, `Rs2`, `Imm=0`, no vector payload |
| registry/materializer | `ScalarALUMicroOp` published with register-register operands |
| typed MicroOp / InternalOp | `Min`, `Max`, `MinU`, `MaxU`, `DWord`, `Computation` |
| execute/capture semantics | `ScalarAluOps` and `ExecutionDispatcherV4` share signed/unsigned XLEN=64 semantics |
| retire/writeback | scalar retire writes `rd`; `x0` destination is discarded |
| replay/rollback/conformance | rollback restores destination register after writeback; executable test covers all four rows |
| golden/no-emission | local CloseToRTL golden vectors covered; compiler helper emission remains closed |
| VMX boundary | no VMX-specific handler or projection; only generic scalar legality applies |

## Separation Boundaries

- Scalar `MIN*`/`MAX*` evidence is separate from vector `VMIN*`/`VMAX*`
  evidence.
- Scalar `MIN*`/`MAX*` evidence is separate from AMO min/max evidence.
- Lane6 descriptor-owned `DmaStreamCompute.MIN/MAX` remains descriptor-sideband
  transport, not scalar opcode evidence.
- Compiler helper emission is not opened by this slice.
