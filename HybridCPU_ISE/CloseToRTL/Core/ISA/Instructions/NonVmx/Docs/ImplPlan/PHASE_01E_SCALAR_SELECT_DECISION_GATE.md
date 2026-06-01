# Phase 01E - Scalar Select Decision Gate

Date: 2026-05-26

## Slice

Fifth Phase 01 mini-group only: `CSEL` and `CZERO.NEZ`.

This package does not advance zero-compare facade rows, bitfield, compiler
helper, VMX-specific, vector, Lane6, or Lane7 work.

## Production Path Overlay

`CZERO.NEZ` is promoted through the scalar full production path in this slice as
a canonical binary scalar ALU row. `CSEL` is not promoted because the intended
`rd, rs_true, rs_false, rs_cond` shape needs an approved 4-register carrier or
sideband that is not present in the current packed scalar decoder/IR ABI. The
Phase 01E carrier-gate decision is closed negatively: no Phase 01 CSEL carrier
is approved.

VMX remains generic unless a select row gains guest-visible privileged or
projected state.

## Current Status

`CZERO.NEZ` is closed as an executable scalar ALU row:

- opcode `333`;
- canonical payload `rd, rs1, rs2` with `Immediate=0`;
- decoder/encoder ABI uses the existing packed scalar register fields;
- `InstructionIR`/projection uses the existing binary scalar register shape;
- registry/materializer publishes a typed `ScalarALUMicroOp`;
- `InternalOpKind.CzeroNez` is materialized;
- execute polarity is `rs2 != 0 ? 0 : rs1`;
- retire-owned scalar writeback and x0 discard are covered by the scalar ALU
  runtime path;
- replay/rollback/conformance and golden vectors are covered by
  `NonVmxPhase01ScalarSelectExecutableTests`;
- compiler helper emission remains closed.

`CSEL` remains a CloseToRTL metadata catalog row only. Its leaf metadata records
the intended branchless select shape, but that metadata is not runtime evidence.
The current packed scalar path carries only `rd, rs1, rs2`; Phase 01 gate
closure does not approve any carrier for the fourth source `rs_cond`.
`CselInstruction` records this as
`CarrierGateDecision="Phase01ECarrierGateClosedNoApprovedCarrier"`,
`ExternalCarrierGateClosed=true`, and
`ExternalCarrierApprovedInPhase01=false`.

## Decision

This iteration closes `CZERO.NEZ` as runtime and closes the `CSEL` external
carrier gate as a negative Phase 01 decision. The current plan explicitly
rejects opening `CSEL` on the current ABI:

- `CSEL` requires an approved four-register carrier or sideband ABI for
  `rd, rs_true, rs_false, rs_cond`;
- no such carrier is approved in Phase 01E;
- `CSEL` may not introduce hidden predicate register state;
- `CSEL` may not use compiler facade lowering as runtime evidence.

`CZERO.NEZ` is intentionally separate from closed `CZERO.EQZ`: `CZERO.EQZ`
zeros on `rs2 == 0`, while `CZERO.NEZ` zeros on `rs2 != 0`.

## Evidence Gate State

| Gate | State |
|---|---|
| status/catalog | `CZERO.NEZ` is `OptionalEnabled` / `ConformanceTested`; `CSEL` remains reserved/no-emission |
| opcode value | `CZERO.NEZ=333`; `CSEL` has no opcode and the Phase 01 carrier decision is closed negative |
| decoder/encoder ABI | `CZERO.NEZ` uses canonical `rd, rs1, rs2`, `Immediate=0`; no 4-source `CSEL` carrier is approved in Phase 01 |
| InstructionIR/projection | `CZERO.NEZ` uses existing binary scalar projection; current packed scalar IR cannot carry `CSEL` |
| registry/materializer | `CZERO.NEZ` registered through scalar ALU materializer; `CSEL` publishes no registry/materializer row |
| typed MicroOp / object | `CZERO.NEZ` publishes `ScalarALUMicroOp` and `InternalOpKind.CzeroNez`; `CSEL` still metadata-only |
| execute/capture semantics | `CZERO.NEZ` closed with `rs2 != 0 ? 0 : rs1`; `CSEL` has local `EvaluateXLen64`/golden seeds only, not runtime `Execute` |
| retire/writeback | `CZERO.NEZ` uses scalar register retire with x0 discard; `CSEL` has no Phase 01 retire path |
| replay/rollback/conformance | `CZERO.NEZ` covered by Phase 01E executable tests; `CSEL` carrier-gate/no-emission assertions remain authoritative |
| golden/no-emission | `CZERO.NEZ` has scalar golden vectors and no helper authority; `CSEL` no-emission boundary remains authoritative |

## Production Follow-On Rules

- Add `Opcode` and `Execute` to `CselInstruction` only in a scalar select
  production package that closes source-count ABI, decoder, materializer,
  execution, retire, replay, golden, and no-emission evidence.
- Do not treat `CSEL` as an open Phase 01 task after this carrier-gate closure;
  future hardware CSEL requires a new explicit production package.
- Do not infer executable support from `ParameterDescriptor` or `MicroOpShape`
  metadata.
- Do not reopen `CZERO.NEZ` as facade/compiler lowering without explicit helper
  authority; its runtime closure is a direct scalar ALU row.
- Introduce predicate state or VMX-compatible projection only by explicit
  architecture/virtualization-boundary decision.
- Add compiler helper emission only by explicit helper authority.
