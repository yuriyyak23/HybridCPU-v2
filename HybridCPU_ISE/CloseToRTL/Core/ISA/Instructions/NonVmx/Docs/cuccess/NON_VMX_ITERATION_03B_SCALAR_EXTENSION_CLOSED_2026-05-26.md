# Non-VMX Iteration 03B Scalar Extension Closure Snapshot

Date: 2026-05-26

Scope: close `SEXT.B`, `SEXT.H`, and `ZEXT.H` as scalar XLEN=64 unary
ALU instructions. This snapshot is the golden/status artifact for the
selected Iteration 03B package only; it does not close adjacent scalar
bitcount aliases, boolean-invert, min/max, byte/bit reverse,
vector, Lane6, or Lane7 contours.

## Closed rows

| Mnemonic | Status | Runtime evidence | Opcode | Internal op | Data width | Compiler helper |
|---|---|---|---:|---|---|---|
| `SEXT.B` | `OptionalEnabled` | `ConformanceTested` | 60 | `SextB` | byte | none |
| `SEXT.H` | `OptionalEnabled` | `ConformanceTested` | 61 | `SextH` | half | none |
| `ZEXT.H` | `OptionalEnabled` | `ConformanceTested` | 62 | `ZextH` | half | none |

## ABI and semantics

- Canonical scalar payload: `rd, rs1`.
- Register aliases with `rs2 != 0` are rejected by the decoder.
- Immediate aliases with `Immediate != 0` are rejected by the decoder.
- XLEN is 64 bits.
- `SEXT.B` sign-extends the low 8 bits of `rs1`.
- `SEXT.H` sign-extends the low 16 bits of `rs1`.
- `ZEXT.H` zero-extends the low 16 bits of `rs1`.
- The instructions write only `rd`.
- Writes to `x0` are discarded at retire.
- No memory, branch, system, descriptor, counter, or hidden state side
  effects are published.

## Evidence chain

| Evidence step | Closed by |
|---|---|
| status/catalog | `InstructionSupportStatusCatalog` explicit rows |
| opcode value | `InstructionsEnum`/`IsaOpcodeValues` opcodes 60, 61, 62 |
| decoder/encoder ABI | canonical unary scalar decoder acceptance and alias rejection |
| InstructionIR/projection | `InstructionIR` canonical opcode, `DecodedBundleTransportProjector` scalar carrier |
| registry/materializer | `OpcodeRegistry`, `InstructionRegistry`, `InternalOpBuilder` |
| typed MicroOp / CloseToRTL object | `ScalarALUMicroOp`, `InternalOpKind.SextB/SextH/ZextH`, typed instruction objects |
| execute/capture semantics | `ScalarAluOps` extension helpers, `ExecutionDispatcherV4` capture |
| retire/writeback publication | retire records and scalar writeback only |
| replay/rollback/conformance | focused Iteration 03B tests |
| no-emission boundary | compiler facade remains closed for byte/half extension helpers |

## Deferred contours kept closed

`CPOP`, `POPCNT`, `ANDN`, `ORN`, `XNOR`, `MIN`, `MAX`,
`MINU`, `MAXU`, `REV8`, `BREV8`, `BSET`, `BCLR`, `BINV`, and `BEXT`
remain non-executable in this package.
