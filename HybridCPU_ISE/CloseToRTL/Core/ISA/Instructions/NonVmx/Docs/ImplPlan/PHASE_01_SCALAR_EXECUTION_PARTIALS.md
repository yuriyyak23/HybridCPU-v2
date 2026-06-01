# Phase 01 - Scalar Execution Partials

## Goal

Implement small scalar production slices for pure register ALU candidates while
preserving ABI/no-emission boundaries. Metadata-only rows may be promoted when
the scalar full production path closes end to end.

## Production Path Overlay

Use the `Scalar ALU / Bitmanip / Facade-Hardware Rows` path in `README.md`.
Each promoted instruction file must list the concrete paths it touched. VMX is
generic only for these rows unless the instruction creates a guest-visible
privileged effect, VMCS-projected state, new VM-exit, capability publication,
migration/checkpoint effect, or host-owned evidence.

## Instructions / Contours

- Remaining local Phase 01 runtime candidates: none.
- External carrier-gated candidate: `CSEL` is closed as a negative Phase 01E
  carrier decision; no four-register scalar carrier is approved in Phase 01.
- Closed Phase 01 scalar rows: `ANDN`, `ORN`, `XNOR`, `MIN`, `MAX`, `MINU`,
  `MAXU`, `REV8`, `BREV8`, `CZERO.NEZ`, and canonical `CPOP`.
- Facade/no-emission decisions: `SEQZ`, `SNEZ`, and `POPCNT`.
- Already executable baselines, not to modify except for reference: `CTZ`, `SEXT.B`, `SEXT.H`, `ZEXT.H`, `ROL`, `ROR`.
- Closed Phase 02 rows: rotate-immediate `ROLI`, `RORI`; register-indexed bitfield `BSET`, `BCLR`, `BINV`, `BEXT`; immediate-indexed bitfield `BSETI`, `BCLRI`, `BINVI`, `BEXTI`.

## Current Iteration Gate

- `PHASE_01A_CPOP_POPCNT_DECISION_GATE.md` records the first mini-group:
  `CPOP` is selected as the canonical hardware row with opcode `334`, unary
  `rd, rs1, rs2=x0`, `Immediate=0`, typed scalar ALU materialization,
  retire/replay/conformance evidence, and no compiler helper authority.
  `POPCNT` remains a reserved/no-emission alias boundary.
- `PHASE_01B_BOOLEAN_INVERT_DECISION_GATE.md` records the second mini-group:
  `ANDN`, `ORN`, and `XNOR` are production-opened hardware rows with
  canonical `rd, rs1, rs2` and `Immediate=0`. Compiler helper emission and
  hidden multi-op lowering remain closed.
- `PHASE_01C_SCALAR_MIN_MAX_DECISION_GATE.md` records the third mini-group:
  `MIN`/`MAX`/`MINU`/`MAXU` are production-opened hardware rows with scalar
  opcodes `327..330`, canonical `rd, rs1, rs2`, and `Immediate=0`. Compiler
  helper emission remains closed, and vector, AMO, and Lane6 descriptor
  min/max evidence remains separate.
- `PHASE_01D_BYTE_BIT_REVERSE_DECISION_GATE.md` records the fourth mini-group:
  `REV8`/`BREV8` are production-opened hardware rows with scalar opcodes
  `331..332`, canonical unary `rd, rs1`, `rs2=x0`, and `Immediate=0`. Scalar
  byte/bit-order golden evidence is separate from vector `VBREV8`, and compiler
  helper emission remains closed.
- `PHASE_01E_SCALAR_SELECT_DECISION_GATE.md` records the fifth mini-group:
  `CZERO.NEZ` is production-closed as opcode `333` with canonical `rd, rs1,
  rs2`, `Immediate=0`, `InternalOpKind.CzeroNez`, scalar ALU materialization,
  retire/x0/replay/conformance evidence, and no compiler helper authority.
  `CSEL` is closed as `Phase01ECarrierGateClosedNoApprovedCarrier`: no opcode,
  decoder, IR, materializer, MicroOp, runtime execute, retire, replay, or
  compiler helper authority is opened in Phase 01.
- `PHASE_01F_ZERO_COMPARE_FACADE_DECISION_GATE.md` records the final Phase 01
  facade pair: `SEQZ`/`SNEZ` are closed as facade-only/no-emission rows with no
  opcode, decoder, runtime, compiler helper, or hidden lowering authority.
- `PHASE_02_SCALAR_BITFIELD_AND_ROTATE_IMMEDIATE.md` records the local Phase 02
  follow-up: `ROLI`/`RORI` are production-closed with opcodes `335..336`,
  canonical `rd, rs1, imm6`, `Word1=(rd, rs1, x0)`, imm6 range rejection,
  `InternalOpKind.RolI/RorI`, scalar ALU materialization, retire/replay/
  conformance evidence, and no compiler helper authority. Register-indexed
  `BSET`/`BCLR`/`BINV`/`BEXT` are production-closed with opcodes `337..340`,
  canonical `rd, rs1, rs2`, `Immediate=0`, `rs2 & 0x3F` index masking,
  `InternalOpKind.Bset/Bclr/Binv/Bext`, scalar ALU materialization,
  retire/replay/conformance evidence, and no compiler helper authority.
  Immediate-indexed `BSETI`/`BCLRI`/`BINVI`/`BEXTI` are production-closed
  with opcodes `341..344`, canonical `rd, rs1, imm6`, `Word1=(rd, rs1, x0)`,
  imm6 range rejection, `InternalOpKind.BsetI/BclrI/BinvI/BextI`, scalar ALU
  materialization, retire/replay/conformance evidence, and no compiler helper
  authority.

## Existing Partial Files

- `Lanes00_03Scalar\BitManipulation\BitCount\CpopInstruction.cs`
- `Lanes00_03Scalar\BitManipulation\BitCount\PopcntInstruction.cs`
- `Lanes00_03Scalar\BitManipulation\BooleanInvert\AndnInstruction.cs`
- `Lanes00_03Scalar\BitManipulation\BooleanInvert\OrnInstruction.cs`
- `Lanes00_03Scalar\BitManipulation\BooleanInvert\XnorInstruction.cs`
- `Lanes00_03Scalar\BitManipulation\ByteBitReverse\Rev8Instruction.cs`
- `Lanes00_03Scalar\BitManipulation\ByteBitReverse\Brev8Instruction.cs`
- `Lanes00_03Scalar\ScalarMinMax\*.cs`
- `Lanes00_03Scalar\ConditionalSelect\CselInstruction.cs`
- `Lanes00_03Scalar\ZeroingSelect\CzeroNezInstruction.cs`
- `Lanes00_03Scalar\FacadeCandidates\ZeroCompare\*.cs`

## New Partial Files Allowed

- `CpopInstruction.Semantics.cs` or `PopcntInstruction.Semantics.cs` after canonical mnemonic decision.
- `AndnInstruction.Semantics.cs`, `OrnInstruction.Semantics.cs`, `XnorInstruction.Semantics.cs`.
- `Rev8Instruction.Semantics.cs`, `Brev8Instruction.Semantics.cs`.
- `CselInstruction.LocalSemantics.cs` remains a no-emission local semantics
  seed only; `CzeroNezInstruction.LocalSemantics.cs` is closed by Phase 01E.
- Optional `*.NoEmissionContract.cs` partials for facade rows if the project wants local guard metadata.

## Local CloseToRTL Logic

Local-only future work may add pure XLEN=64 semantics helpers, operand-shape assertions, side-effect-free markers, and capture contract metadata. These partials must not claim runtime closure unless a future production package closes the required gates.

## Production Evidence Gates

Opcode/facade decision, decoder/encoder ABI, `InstructionIR` projection, registry/materializer registration, typed `ScalarALUMicroOp` or equivalent publication, execution dispatcher capture, retire-owned scalar writeback, replay/rollback tests, conformance/golden artifacts, and compiler no-emission regression for the remaining Phase 01 rows. `REV8`/`BREV8` are closed by Phase 01D, `CZERO.NEZ` is closed by Phase 01E, `CPOP` is closed by Phase 01A closure, `SEQZ`/`SNEZ`/`POPCNT` are closed as no-emission decisions, and `CSEL` is closed as a negative Phase 01E carrier decision; no local Phase 01 runtime candidate remains.

## Metadata Constants

For remaining deferred rows, preserve `Mnemonic`, `EvidenceBoundary`,
`HasOpcodeAllocation=false`, `IsExecutable=false`, `CompilerHelperAllowed=false`,
alias/no-emission flags, VMX-neutral markers, and retire/replay requirements.
Do not replace `POPCNT`, `SEQZ`, `SNEZ`, or `CSEL` metadata with an executable
claim without a new explicit production package.

## Evidence Chain Stories

- Decoder/encoder ABI: production gate; decide one opcode per runtime instruction or explicit facade rejection/lowering policy.
- InstructionIR/projection: production gate; scalar rows need canonical `rd, rs1` or `rd, rs1, rs2` projection.
- Typed MicroOp/materializer: production gate; each opened row needs one typed scalar op or explicit facade-only treatment.
- Execute/capture semantics: local pure methods may exist as no-emission seeds; capture remains future package work until dispatcher/materializer closes.
- Retire/writeback: scalar register writeback with x0 discard; no side effects.
- Replay/rollback/conformance: require signedness, zero, all-ones, boundary values, x0 discard, and alias no-emission coverage.

## Boundaries

- Vector VLM: not applicable.
- Lane6: not applicable.
- Lane7/VMX: no VMX frontend integration; generic projection only if later needed.
- No-emission: facade candidates and unopened rows must remain no-emission.

## Risks

- Accidentally implementing both `CPOP` and `POPCNT` as separate runtime opcodes without ABI evidence.
- Treating boolean-invert rows as hidden compiler lowers instead of explicit facade policy.
- Missing signed/unsigned distinction for min/max.

## Closure Criteria

- Production packages may promote selected rows when their scalar full path closes.
- Metadata-only PRs may add local semantics/capture-contract partials without executable claims.
- Existing executable scalar rows are not duplicated.

## Prohibited Actions

- Allocate opcodes, add decoder paths, add compiler helpers, or mark `IsExecutable=true` only in the matching production package.
- Introduce VMX-compatible projection only for an explicit virtualization-boundary decision.
- Multi-op lowering is not runtime execution evidence.
