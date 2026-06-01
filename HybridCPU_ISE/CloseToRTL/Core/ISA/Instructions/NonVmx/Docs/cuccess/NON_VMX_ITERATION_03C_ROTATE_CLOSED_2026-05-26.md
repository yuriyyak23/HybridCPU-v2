# Non-VMX Iteration 03C Rotate Closure Snapshot

Date: 2026-05-26

Scope: close `ROL` and `ROR` as scalar XLEN=64 binary ALU instructions. This
does not open rotate-immediate forms, `CPOP`/`POPCNT`, boolean-invert, scalar
min/max, scalar byte/bit reverse, bitfield, vector, Lane6, Lane7, or compiler
helper contours.

## Status Rows

| Mnemonic | Status | Runtime evidence | Opcode | Internal op | Side effects |
|---|---|---|---:|---|---|
| `ROL` | `OptionalEnabled` | `ConformanceTested` | 63 | `Rol` | none |
| `ROR` | `OptionalEnabled` | `ConformanceTested` | 64 | `Ror` | none |

## ABI And Semantics

- Canonical register form: `rd, rs1, rs2`.
- Rotate amount is `rs2 & 0x3F`.
- `ROL`: `(rs1 << amount) | (rs1 >> (64 - amount))`, with amount zero returning `rs1`.
- `ROR`: `(rs1 >> amount) | (rs1 << (64 - amount))`, with amount zero returning `rs1`.
- Non-zero immediate payloads are rejected as immediate-form aliases.
- Retire publication is a register write to `rd`; writes to x0 are discarded.
- Compiler facade/helper emission remains closed for `Rol`, `Ror`, `RotateLeft`, and `RotateRight`.

## Evidence Chain

| Gate | Evidence |
|---|---|
| status/catalog | `InstructionSupportStatusCatalog` explicit `ROL`/`ROR` rows |
| opcode value | `InstructionsEnum.ROL = 63`, `InstructionsEnum.ROR = 64`, `IsaOpcodeValues.ROL/ROR` |
| decoder/encoder ABI | canonical binary register decode; immediate alias rejection |
| InstructionIR/projection | `InstructionIR` carries `Rd`, `Rs1`, `Rs2`, `Imm=0`; carrier projector materializes canonical scalar op |
| registry/materializer | `RegisterScalarRotateRegisterOp` with descriptor-backed scalar ALU materialization |
| typed MicroOp / object | `ScalarALUMicroOp`, `InternalOpKind.Rol/Ror`, CloseToRTL typed instruction objects |
| execute/capture | `ScalarAluOps.RotateLeft64/RotateRight64`, `ExecutionDispatcherV4` capture path |
| retire/writeback | retire-owned scalar register publication; x0 discard covered |
| replay/rollback | rollback token restores destination architectural register |
| conformance tests | `NonVmxIteration03CRotateExecutableTests` |
| golden/no-emission | plan and shortlist updated; compiler boundary remains no-emission |

## Deferred / Still Closed

`CPOP`, `POPCNT`, `ANDN`, `ORN`, `XNOR`, `MIN`, `MAX`, `MINU`, `MAXU`,
`REV8`, `BREV8`, `BSET`, `BCLR`, `BINV`, `BEXT`, `BSETI`, `BCLRI`,
`BINVI`, `BEXTI`, rotate-immediate forms, vector contours, Lane6
descriptor/control commands, and Lane7 system/control-plane commands remain
reserved/deferred/no-emission until their own evidence chain closes.
