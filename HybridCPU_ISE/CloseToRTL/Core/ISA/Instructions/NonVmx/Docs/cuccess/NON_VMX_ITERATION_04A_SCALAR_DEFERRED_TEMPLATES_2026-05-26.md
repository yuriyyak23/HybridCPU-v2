# Non-VMX Iteration 04A Scalar Deferred Templates Closure

Date: 2026-05-26

Scope: CloseToRTL template/no-emission boundary only. This snapshot does not claim executable instruction support.

## Closed as template boundaries

- `CPOP`, `POPCNT`: population-count alias/canonical decision remains open.
- `ANDN`, `ORN`, `XNOR`: boolean-invert scalar ALU forms remain reserved.
- `MIN`, `MAX`, `MINU`, `MAXU`: scalar min/max forms remain reserved and distinct from vector/AMO forms.
- `REV8`, `BREV8`: scalar byte/bit reverse forms remain reserved and distinct from vector `VBREV8`.
- `BSET`, `BCLR`, `BINV`, `BEXT`, `BSETI`, `BCLRI`, `BINVI`, `BEXTI`: bitfield forms retain explicit reserved catalog rows from Iteration 02A.
- `ROLI`, `RORI`: user-requested rotate-immediate template anchors added as explicit reserved/no-emission rows; register-register `ROL`/`ROR` remain the only closed rotate execution forms.

## Evidence state

- Status/catalog: explicit `Reserved` / `RuntimeEvidence.None` rows for all template mnemonics.
- Opcode/decoder/encoder ABI: not allocated.
- InstructionIR/projection: not allocated.
- Registry/materializer: not allocated.
- Typed MicroOp/runtime execution: not allocated.
- CloseToRTL object: template metadata only; no `Opcode`, no `Execute`, no side-effect or writeback authority.
- Retire/replay/conformance: not executable; tests prove no execution claim.
- Compiler boundary: helper surface remains no-emission.

## Deferred contours

Vector predicate/select, vector widen/narrow/convert, vector segment/structure memory, vector fixed-point/saturating, dot/matrix, Lane6 descriptor/queue/DSC2, and Lane7 system/control-plane contours remain deferred or fail-closed for later iterations.
