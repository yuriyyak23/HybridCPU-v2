# Phase 01B - ORN/XNOR Hardware Decision Closed

Date: 2026-05-27

## Trigger

Follow-on request after the production `ANDN` slice explicitly authorized
closing the remaining ORN/XNOR evidence gates where needed.

## Current Status

`ANDN`, `ORN`, and `XNOR` are production-opened boolean-invert scalar hardware
rows. They use the same scalar binary ABI contour:

- canonical payload: `rd, rs1, rs2`;
- canonical immediate: `Immediate=0`;
- materialization: `ScalarALUMicroOp`;
- retire: scalar register writeback with x0 discard;
- compiler: no helper and no hidden multi-op lowering;
- VMX: generic scalar projection only, no VMX-specific frontend path.

## Production Path Overlay

This file is a closed decision note. Treat it as a production template for
future scalar binary rows: all code moves through the scalar full path in
`README.md`, and VMX remains a client of shared runtime legality rather than a
manual integration point.

## Decision

Close `ORN` and `XNOR` as hardware rows:

- `ORN = 66`, `InternalOpKind.OrN`;
- `XNOR = 67`, `InternalOpKind.Xnor`.

No facade-only policy is adopted. Compiler helpers remain unauthorized.

## Evidence Closed

- status/catalog: `OptionalEnabled`, `RuntimeInstructionEvidence.ConformanceTested`;
- opcode: numeric `InstructionsEnum` and `IsaOpcodeValues` allocation;
- decoder/encoder ABI: canonical payload accepted, non-zero immediate alias rejected;
- `InstructionIR`: `CanonicalOpcode`, `Rd`, `Rs1`, `Rs2`, `Imm=0`;
- registry/materializer: scalar binary ALU publication;
- ALU/dispatcher: `rs1 | ~rs2` and `~(rs1 ^ rs2)`;
- retire/replay: x0 discard, writeback, rollback;
- golden/no-emission: local vectors and compiler/VMX no-emission tests.
