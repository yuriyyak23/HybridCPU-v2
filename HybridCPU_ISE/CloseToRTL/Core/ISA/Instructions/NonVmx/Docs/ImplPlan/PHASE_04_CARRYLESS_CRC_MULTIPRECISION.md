# Phase 04 - Carryless, CRC, And Multi-Precision

## Goal

Promote carry-less variants, CRC rows, and multi-precision carry/borrow rows
only after polynomial, bit-order, carry, and retire/replay ABI are explicit.

Current slice decision: `CLMULH` and `CLMULR` are promoted to production.
Their bit-order ABI is frozen as XLEN=64 LSB-first GF(2) multiplication using
the same product convention as `CLMUL`: `CLMULH` publishes product bits
`[127:64]`, and `CLMULR` publishes product bits `[126:63]`.

Current ABI gate decisions:

- `CRC32`/`CRC64`: negative gate closed. No opcode allocation or decoder
  acceptance is authorized because the current plan does not choose a unique
  polynomial, reflection mode, seed/final-xor convention, or byte/word endian
  ingestion policy.
- `ADC`/`SBC`/`ADDC`/`SUBC`: negative gate closed. No opcode allocation or
  decoder acceptance is authorized because the current scalar carrier has no
  explicit retire-owned carry/borrow publication ABI and no implicit
  architectural flags are allowed.

## Production Path Overlay

Use the scalar full production path in `README.md`. `CLMULH/CLMULR` now carry
their own opcode/decode/materializer/runtime/retire/replay/golden evidence and
share only the explicit LSB-first GF(2) product convention with closed `CLMUL`.
CRC rows must publish polynomial/width policy;
multi-precision rows must define carry-in/carry-out publication without hidden
architectural flags. VMX is generic unless carry/CRC state becomes
guest-visible privileged or projected state.

## Instructions / Contours

- Carry-less variants: `CLMULH`, `CLMULR` - production closed, opcodes 352/353.
- CRC: `CRC32`, `CRC64` - ABI gate closed negative; reserved/no-allocation.
- Multi-precision: `ADC`, `SBC`, `ADDC`, `SUBC` - ABI gate closed negative; reserved/no-allocation.

## Existing Partial Files

- `Lanes00_03Scalar\CarrylessMultiply\ClmulhInstruction.cs`
- `Lanes00_03Scalar\CarrylessMultiply\ClmulrInstruction.cs`
- `Lanes00_03Scalar\CRC\Crc32Instruction.cs`
- `Lanes00_03Scalar\CRC\Crc64Instruction.cs`
- `Lanes00_03Scalar\MultiPrecision\AdcInstruction.cs`
- `Lanes00_03Scalar\MultiPrecision\SbcInstruction.cs`
- `Lanes00_03Scalar\MultiPrecision\AddcInstruction.cs`
- `Lanes00_03Scalar\MultiPrecision\SubcInstruction.cs`

## New Partial Files Allowed

- `ClmulhInstruction.Semantics.cs`, `ClmulrInstruction.Semantics.cs`.
- `Crc32Instruction.Semantics.cs`, `Crc64Instruction.Semantics.cs`.
- `AdcInstruction.Semantics.cs`, `SbcInstruction.Semantics.cs`, `AddcInstruction.Semantics.cs`, `SubcInstruction.Semantics.cs`.
- Optional `*.AbiNotes.cs` local metadata partials for polynomial/carry contract reminders.

## Local CloseToRTL Logic

Production/local partials may encode bit-order contract helpers, polynomial identifiers, carry-in/carry-out contract metadata, and pure result helpers. `CLMULH/CLMULR` now publish pure scalar register results only: `rd, rs1, rs2`, `Immediate=0`, no side effects, no flags, retire-owned x0 discard. CRC and multi-precision rows publish architectural carry/CRC state, side effects, or host/runtime evidence only when the same package closes retire/replay evidence.

## Production Evidence Gates

Opcode allocation, polynomial ABI, carry flag/register ABI, decoder/encoder ABI, `InstructionIR` projection, materializer, typed MicroOp, dispatcher capture, retire writeback and carry publication rules, replay/rollback, conformance vectors, and golden/no-emission artifacts. `CLMULH/CLMULR` close these gates for register-register scalar ALU publication; CRC and multi-precision gates remain open.

## Metadata Constants

Preserve `CrcPolynomialAbiDeferredNoEmission`, `MultiPrecisionCarryAbiDeferredNoEmission`, `HasOpcodeAllocation=false`, `IsExecutable=false`, `CompilerHelperAllowed=false`, retire/replay requirements, and VMX-neutral markers for the remaining CRC and multi-precision rows. The CRC leaf files must explicitly carry `RequiresPolynomialAbi`, `RequiresReflectionAbi`, `RequiresSeedFinalXorAbi`, and `RequiresEndianPolicyAbi`. Multi-precision leaf files must explicitly carry no-hidden-flags and retire-owned publication markers. `CarryLessBitOrderDeferredNoEmission` no longer applies to `CLMULH/CLMULR`; their leaf files now use `ExecutableScalarAlu`.

## Evidence Chain Stories

- Decoder/encoder ABI: production gate; CRC polynomial/reflection/seed/final-xor/endian policy and multi-precision carry operands must be explicit.
- InstructionIR/projection: production gate; carry-in/out and CRC width cannot be implicit.
- Typed MicroOp/materializer: production gate; carry-less, CRC, and multi-precision rows need distinct typed operations or explicit facade policy.
- Execute/capture semantics: `CLMULH/CLMULR` use typed scalar ALU operations and dispatcher retire-window capture. CRC/multi-precision local pure helpers can be planned, but capture remains external.
- Retire/writeback/side effects: `CLMULH/CLMULR` publish only scalar rd writeback with x0 discard. Possible carry/borrow publication must be retire-owned and replayable before multi-precision rows open.
- Replay/rollback/conformance: `CLMULH/CLMULR` carry bit-order vectors, x0 discard, dispatcher capture, and rollback tests. CRC/multi-precision still require polynomial vectors, carry overflow/borrow boundary tests, and rollback of any published carry state.

## Boundaries

- Vector VLM: not applicable.
- Lane6: not applicable.
- Lane7/VMX: no VMX-specific path; no host evidence.
- No-emission: compiler helper authority remains closed for `CLMULH/CLMULR`; CRC and multi-precision rows stay no-emission until ABI and runtime evidence close. No hidden compiler lowering or helper facade may synthesize these rows.

## Risks

- CRC32/CRC64 naming without polynomial and reflection ABI.
- Carry/borrow side effects becoming hidden global state.
- `CLMULH`/`CLMULR` bit-order ambiguity is closed for XLEN=64 by the current production slice.

## Closure Criteria

- A production package may promote each row only after carry/CRC/polynomial ABI and scalar full path evidence close.
- `CLMULH/CLMULR` meet this criterion for the scalar carry-less variant package.
- `CRC32/CRC64` have an explicit no-allocation decision until polynomial/reflection/seed/final-xor/endian ABI is approved.
- `ADC/SBC/ADDC/SUBC` have an explicit no-allocation decision until carry/borrow input and retire-visible publication ABI is approved without implicit architectural flags.

## Prohibited Actions

- Do not publish carry flags or CRC state as architectural state without retire/replay evidence.
- Add decoder/materializer/runtime/compiler changes only in the matching production package with helper authority where needed.
