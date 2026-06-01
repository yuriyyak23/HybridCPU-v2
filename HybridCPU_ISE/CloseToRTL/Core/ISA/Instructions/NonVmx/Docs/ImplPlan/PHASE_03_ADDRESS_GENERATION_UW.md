# Phase 03 - Address Generation UW

## Goal

Promote scalar address-generation and `.UW` rows through explicit hardware
evidence without turning them into hidden multi-op helpers.

## Production Path Overlay

Use the scalar full production path in `README.md`. Each `.UW` row must encode
source-width and shift/add semantics in decoder/encoder ABI, IR projection,
InternalOp, ALU/dispatcher execution, retire/replay, and golden vectors. Do not
reuse closed `SH1ADD` evidence for distinct `.UW` or higher-shift rows. VMX is
generic only.

## Instructions / Contours

- Closed `.UW` rows: `ADD.UW`, `SH1ADD.UW`, `SH2ADD.UW`, `SH3ADD.UW`, `SLLI.UW`.
- Closed shifted-add rows in this subtree: `SH2ADD`, `SH3ADD`.
- Already closed elsewhere and not to duplicate: current closed `SH1ADD` if present in shared runtime evidence.

## Existing Partial Files

- `Lanes00_03Scalar\AddressGeneration\AddUwInstruction.cs`
- `Lanes00_03Scalar\AddressGeneration\Sh1addUwInstruction.cs`
- `Lanes00_03Scalar\AddressGeneration\Sh2addInstruction.cs`
- `Lanes00_03Scalar\AddressGeneration\Sh2addUwInstruction.cs`
- `Lanes00_03Scalar\AddressGeneration\Sh3addInstruction.cs`
- `Lanes00_03Scalar\AddressGeneration\Sh3addUwInstruction.cs`
- `Lanes00_03Scalar\AddressGeneration\SlliUwInstruction.cs`

## Local CloseToRTL Logic

The closed leaf objects now carry production path comments, low-32 zero-extension policy, shift/add semantics, XLEN=64 wrapping, operand-shape metadata, no-side-effect capture contracts, golden vectors, and retire x0 discard predicates.

## Production Evidence Gates

Closed in Phase 03 for this pool: opcode allocation, decoder/encoder ABI for `.UW` and shifted-add forms, `InstructionIR` projection, scalar materializer registration, typed MicroOp kind, dispatcher capture, retire writeback, replay/rollback, conformance, and golden/no-emission tests.

## Metadata Constants

The old `ScalarAddressGenerationUwDeferredNoEmission` / `ScalarAddressGenerationDeferredNoEmission` markers are retired for `SH2ADD`, `SH3ADD`, `ADD.UW`, `SH1ADD.UW`, `SH2ADD.UW`, `SH3ADD.UW`, and `SLLI.UW`. These rows now expose `EvidenceBoundary=ExecutableScalarAlu`, `HasOpcodeAllocation=true`, `IsExecutable=true`, `CompilerHelperAllowed=false`, `NoHiddenMultiOpEmission=true`, no LSU bypass authority, and VMX-neutral markers.

## Evidence Chain Stories

- Opcode allocation: `SH2ADD=345`, `SH3ADD=346`, `ADD.UW=347`, `SH1ADD.UW=348`, `SH2ADD.UW=349`, `SH3ADD.UW=350`, `SLLI.UW=351`.
- Decoder/encoder ABI: register forms use `rd, rs1, rs2` and canonical `Immediate=0`; `SLLI.UW` uses `rd, rs1, imm6`, canonical `Word1=(rd, rs1, x0)`, and imm6 range rejection.
- InstructionIR/projection: `.UW` rows distinguish source zero-extension from full-width shifted-add; `SLLI.UW` projects unsigned imm6 through the immediate field.
- Typed MicroOp/materializer: one typed scalar ALU op per executable row; `SLLI.UW` materializes with `UsesImmediate=true` and no source-2 register.
- Execute/capture semantics: `SH2ADD/SH3ADD` compute `(rs1 << 2|3) + rs2`; `.UW` rows zero-extend low 32 bits of `rs1` before shift/add; dispatcher capture publishes retire records without eager state mutation.
- Retire/writeback: scalar writeback with x0 discard and no memory/LSU side effect.
- Replay/rollback/conformance: covered by low-32 truncation, carry wrapping, boundary shifts, sign-bit low word, x0 discard, replay/rollback, and golden vector tests.

## Boundaries

- Vector VLM: not applicable.
- Lane6: not applicable.
- Lane7/VMX: no VMX-specific projection.
- No-emission: no compiler helper or hidden lowering in this phase.

## Risks

- Treating `.UW` rows as simple aliases for existing full-width shifts.
- Hiding `ZEXT.W + ADD/SH*ADD` as compiler emission without a facade decision.
- Duplicating closed `SH1ADD` runtime evidence.

## Closure Criteria

- Closed for this pool. Future address-generation changes must be new explicit rows or ABI decisions and must not reuse this evidence chain without matching decoder/materializer/runtime tests.

## Prohibited Actions

- Add opcodes, decoder paths, helpers, materializer registrations, or runtime execution only in the matching scalar production package.
- Do not reuse closed shifted-add evidence for `.UW` rows without explicit ABI closure.
