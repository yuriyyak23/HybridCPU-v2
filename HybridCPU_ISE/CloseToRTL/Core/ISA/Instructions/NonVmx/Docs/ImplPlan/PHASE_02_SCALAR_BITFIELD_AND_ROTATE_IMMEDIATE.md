# Phase 02 - Scalar Bitfield And Rotate Immediate

## Goal

Promote reserved scalar bitfield and rotate-immediate rows only as complete
production slices after index/immediate ABI decisions are explicit. Local Phase
02 now closes `ROLI`/`RORI`, register-indexed `BSET`/`BCLR`/`BINV`/`BEXT`,
and immediate-indexed `BSETI`/`BCLRI`/`BINVI`/`BEXTI`.

## Production Path Overlay

Use the scalar full production path in `README.md`. Register-indexed bitfield
rows are closed with canonical `rd, rs1, rs2`, `Immediate=0`, `rs2 & 0x3F`
index masking, scalar ALU materialization, and no compiler helper authority.
Immediate rows are closed with canonical `rd, rs1, imm6`, `Word1=(rd, rs1, x0)`,
unsigned `imm6` range rejection, scalar ALU materialization, and no compiler
helper authority. Closed rotate-immediate rows use canonical
`Word1=(rd, rs1, x0)` plus unsigned `imm6` in `Immediate`, remain separate
from closed register-register `ROL/ROR` evidence, and stay VMX-generic only.

## Instructions / Contours

- Register-indexed bitfield: `BSET`, `BCLR`, `BINV`, `BEXT`.
- Immediate-indexed bitfield: `BSETI`, `BCLRI`, `BINVI`, `BEXTI`.
- Rotate immediate: `ROLI`, `RORI`.

## Existing Partial Files

- `Lanes00_03Scalar\BitManipulation\BitSetClearInvert\BsetInstruction.cs`
- `Lanes00_03Scalar\BitManipulation\BitSetClearInvert\BclrInstruction.cs`
- `Lanes00_03Scalar\BitManipulation\BitSetClearInvert\BinvInstruction.cs`
- `Lanes00_03Scalar\BitManipulation\BitSetClearInvert\BsetiInstruction.cs`
- `Lanes00_03Scalar\BitManipulation\BitSetClearInvert\BclriInstruction.cs`
- `Lanes00_03Scalar\BitManipulation\BitSetClearInvert\BinviInstruction.cs`
- `Lanes00_03Scalar\BitManipulation\BitExtract\BextInstruction.cs`
- `Lanes00_03Scalar\BitManipulation\BitExtract\BextiInstruction.cs`
- `Lanes00_03Scalar\BitManipulation\Rotates\RoliInstruction.cs`
- `Lanes00_03Scalar\BitManipulation\Rotates\RoriInstruction.cs`

## New Partial Files Allowed

- Future `*.Semantics.cs` files are still allowed only for later scalar pools that need local helpers.
- `BSET`/`BCLR`/`BINV`/`BEXT` are closed in their leaf files; no extra semantics partial is required for the current slice.
- `BSETI`/`BCLRI`/`BINVI`/`BEXTI` are closed in their leaf files; no extra semantics partial is required for the current slice.
- `ROLI`/`RORI` are closed in their leaf files; no extra semantics partial is required for the current slice.

## Local CloseToRTL Logic

`BSET`/`BCLR`/`BINV`/`BEXT` now have authoritative XLEN=64 register-indexed semantics with `index & 0x3F`, local golden vectors, and runtime evidence. `BSETI`/`BCLRI`/`BINVI`/`BEXTI` now have authoritative XLEN=64 imm6 semantics, local golden vectors, and runtime evidence. `ROLI`/`RORI` now have authoritative XLEN=64 imm6 semantics, local golden vectors, and runtime evidence.

## Production Evidence Gates

Status/catalog rows, numeric opcode allocation or explicit facade decision, decoder immediate/register forms, encoder ABI, `InstructionIR` fields for imm6/register index, scalar materializer, typed MicroOp publication, dispatcher capture, retire writeback, replay/rollback/conformance, and golden/no-emission tests.

## Metadata Constants

`BSET`/`BCLR`/`BINV`/`BEXT` and `BSETI`/`BCLRI`/`BINVI`/`BEXTI` are `EvidenceBoundary="ExecutableScalarAlu"`, `HasOpcodeAllocation=true`, `IsExecutable=true`, and `CompilerHelperAllowed=false`. `ROLI`/`RORI` are `EvidenceBoundary="ExecutableScalarAlu"`, `HasOpcodeAllocation=true`, `IsExecutable=true`, and `CompilerHelperAllowed=false`; preserve separation from closed register-register `ROL`/`ROR`.

## Evidence Chain Stories

- Decoder/encoder ABI: `ROLI`/`RORI` closed with `imm6` range rejection and no `rs2` alias; `BSET`/`BCLR`/`BINV`/`BEXT` closed with register-register `Word1=(rd, rs1, rs2)` and `Immediate=0` alias rejection; `BSETI`/`BCLRI`/`BINVI`/`BEXTI` closed with `Word1=(rd, rs1, x0)` and unsigned `imm6` range rejection.
- InstructionIR/projection: `ROLI`/`RORI` project `rd, rs1, rs2=x0, Imm=imm6`; `BSET`/`BCLR`/`BINV`/`BEXT` project `rd, rs1, rs2, Imm=0`; `BSETI`/`BCLRI`/`BINVI`/`BEXTI` project `rd, rs1, rs2=x0, Imm=imm6`.
- Typed MicroOp/materializer: `ROLI`/`RORI` and `BSETI`/`BCLRI`/`BINVI`/`BEXTI` publish `ScalarALUMicroOp` with `UsesImmediate=true`; `BSET`/`BCLR`/`BINV`/`BEXT` publish register `ScalarALUMicroOp` with `UsesImmediate=false`.
- Execute/capture semantics: `ROLI`/`RORI`, register bitfield, and immediate bitfield rows close scalar ALU and dispatcher capture paths.
- Retire/writeback: `ROLI`/`RORI`, register bitfield, and immediate bitfield rows close scalar register writeback with x0 discard.
- Replay/rollback/conformance: `ROLI`/`RORI` close imm boundaries, rotate-by-zero/max, x0, replay/rollback, and compiler no-emission coverage; register bitfield rows close index masking, canonical `BEXT` 0/1 result, x0, replay/rollback, golden vectors, and compiler no-emission coverage; immediate bitfield rows close imm6 boundaries, canonical `BEXTI` 0/1 result, x0, replay/rollback, golden vectors, and compiler no-emission coverage.

## Boundaries

- Vector VLM: not applicable.
- Lane6: not applicable.
- Lane7/VMX: generic runtime projection only; no VMX frontend.
- No-emission: `BSET`/`BCLR`/`BINV`/`BEXT`, `BSETI`/`BCLRI`/`BINVI`/`BEXTI`, and `ROLI`/`RORI` remain no-emission at the compiler helper boundary despite runtime closure.

## Risks

- Regressing `ROLI`/`RORI` into aliases of closed register-register rotates instead of their closed imm6 ABI.
- Regressing register-indexed bitfield rows into hidden shift/mask multi-op lowering instead of one typed scalar ALU row.
- Adding compiler helper emission for runtime-only rows.

## Closure Criteria

- `ROLI`/`RORI` are promoted after the scalar full path closed locally.
- `BSET`/`BCLR`/`BINV`/`BEXT` are promoted after the scalar full path closed locally with opcodes `337..340`.
- `BSETI`/`BCLRI`/`BINVI`/`BEXTI` are promoted after the scalar full path closed locally with opcodes `341..344`.
- Metadata-only or local semantics updates cannot claim runtime evidence.

## Prohibited Actions

- Add compiler helpers or hidden lowering for these runtime-only scalar rows without an explicit compiler evidence decision.
- Do not reuse base rotate evidence for `ROLI`/`RORI` without preserving the closed imm6 ABI and tests.
