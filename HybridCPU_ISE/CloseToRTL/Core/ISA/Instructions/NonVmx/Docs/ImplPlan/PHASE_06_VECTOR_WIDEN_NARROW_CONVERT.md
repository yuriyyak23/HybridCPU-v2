# Phase 06 - Vector Widen, Narrow, And Convert

## Goal

Promote VLM-gated width-changing and conversion contours after
source/destination width, signedness, rounding, saturation, trap, and conversion
ABI are explicit.

## Production Path Overlay

Use the vector full production path in `README.md`. Width-changing rows require
VLM contour opening, typed vector MicroOps, lane binding, conversion policy in
IR/projection, retire/replay, and golden vectors. VMX remains a client of shared
projection unless conversion policy exposes guest-visible privileged state.

## Instructions / Contours

- Widening: `VWADD`, `VWADDU`, `VWSUB`, `VWSUBU`, `VWMUL`, `VWMULU`, `VWMACC`.
- Narrowing: `VNSRL`, `VNSRA`.
- Conversion/extension: `VSEXT`, `VCVT.I`, `VCVT.U`, `VCVT.F`.

## Existing Partial Files

- `Lanes00_03Vector\Widening\*.cs`
- `Lanes00_03Vector\Narrowing\VnsrlInstruction.cs`
- `Lanes00_03Vector\Narrowing\VnsraInstruction.cs`
- `Lanes00_03Vector\Conversion\VsextInstruction.cs`
- `Lanes00_03Vector\Conversion\VcvtIInstruction.cs`
- `Lanes00_03Vector\Conversion\VcvtUInstruction.cs`
- `Lanes00_03Vector\Conversion\VcvtFInstruction.cs`

## New Partial Files Allowed

- `*.Legality.cs` for width/signedness/VLM shape metadata.
- `*.Semantics.cs` for non-authoritative lane formulas.
- `*.ConversionContract.cs` for rounding/saturation/trap notes.

## Local CloseToRTL Logic

Production/local partials may encode width sideband requirements, signedness markers, accumulator-shape notes for `VWMACC`, and conversion policy placeholders. They claim executable vector behavior only when the same package closes the vector production path.

## Production Evidence Gates

VLM closure, opcode/contour allocation, decoder/encoder ABI, `InstructionIR` projection, typed vector MicroOps, materializer, scheduler/bundler/lane binding, execute/capture, staged retire publication, replay/rollback, conformance vectors, and golden artifacts.

## Metadata Constants

Preserve `VectorWidenNarrowConvertFailClosed`, `RequiresSourceDestinationWidthSideband`, `RequiresSignednessAbi`, `RequiresAccumulatorAbi`, `RequiresNarrowingPolicyAbi`, `RequiresRoundingSaturationTrapPolicy`, `RequiresConversionPolicyAbi`, `RequiresVectorLegalityMatrixClosure`, `NoDescriptorFallback=true`, `NoHiddenScalarLowering=true`, `NoMultiOpEmission=true`, `NoVmxSpecificPath=true`, `IsExecutable=false`, and `CompilerHelperAllowed=false`.

## Phase 06A Decision Gate - Widening Arithmetic

Status: explicit negative production decision. `VWADD`, `VWADDU`, `VWSUB`,
`VWSUBU`, `VWMUL`, `VWMULU`, and `VWMACC` remain reserved/no-allocation rows in
the `VectorWidenNarrowConvert` extension bucket. This slice does not allocate
opcodes, does not add decoder/encoder acceptance, does not add widening
`InstructionIR` projection, does not register a materializer, does not publish
typed vector MicroOps, does not bind vector execution lanes, and does not add
compiler helper authority.

Decision details:

- Source/destination width ABI: unresolved; element width, widening ratio,
  LMUL, and VL contour must be explicit before materialization.
- Signedness: unresolved; signed and unsigned widening rows require canonical
  signedness policy rather than inference from mnemonic adjacency.
- Overflow/result footprint: unresolved; widening result precision and staged
  publication footprint must be explicit.
- `VWMACC`: accumulator precision, source-width relationship, and destination
  footprint remain separate ABI blockers.
- Mask/tail: unresolved; mask and tail behavior require a matching VLM contour.
- Lowering: no descriptor fallback, hidden base-vector arithmetic expansion,
  hidden scalar lowering, or multi-op emission.

Rationale: closed base vector arithmetic proves only same-width vector compute.
It does not define widening width transforms, result footprint, or accumulator
publication.

## Phase 06B Decision Gate - Narrowing Shifts

Status: explicit negative production decision. `VNSRL` and `VNSRA` remain
reserved/no-allocation rows in the `VectorWidenNarrowConvert` extension bucket.
This slice does not allocate opcodes, decoder/encoder ABI, IR projection,
materializer rows, typed vector MicroOps, execution, retire publication, or
compiler helper authority.

Decision details:

- Source/destination width ABI: unresolved; narrowing width, LMUL, and VL
  contour must be explicit.
- Shift operand ABI: unresolved; register/immediate shift source transport is
  not defined.
- Result policy: unresolved; truncation, rounding, saturation, and trap behavior
  are mandatory ABI blockers.
- Mask/tail: unresolved; staged publication and rollback must include mask/tail
  policy.
- Lowering: no descriptor fallback, hidden scalar lowering, or multi-op
  emission.

Rationale: closed same-width vector shifts and fixed-width integer shifts do not
define narrowing result policy or shift-source sidebands.

## Phase 06C Decision Gate - Sign Extension And Conversions

Status: explicit negative production decision. `VSEXT`, `VCVT.I`, `VCVT.U`,
and `VCVT.F` remain reserved/no-allocation rows in the
`VectorWidenNarrowConvert` extension bucket. This slice does not allocate
opcodes, decoder/encoder ABI, IR projection, materializer rows, typed vector
MicroOps, execution, retire publication, or compiler helper authority.

Decision details:

- `VSEXT`: source width, signed source interpretation, destination width, LMUL,
  VL, and staged publication remain open; closed `VZEXT` is not evidence for
  sign extension.
- `VCVT.I`/`VCVT.U`: signed/unsigned integer result footprint, invalid
  conversion, NaN, rounding, saturation, and trap policy remain open.
- `VCVT.F`: floating-point result footprint, NaN/invalid conversion behavior,
  FP exception/trap policy, and rounding remain open.
- Mask/tail: unresolved; conversion publication and rollback must include
  mask/tail policy.
- VMX/compiler: generic boundary only; no VMX-specific path, helper, descriptor
  fallback, hidden scalar lowering, or multi-op emission is allowed.

Rationale: closed `VZEXT` proves only packed unsigned zero-extension memory
publication. It does not define signed extension, integer/float conversion,
NaN, rounding, saturation, trap, or conversion result footprint semantics.

## Evidence Chain Stories

- Decoder/encoder ABI: production gate; width and conversion policy must be encoded or sidebanded.
- InstructionIR/projection: production gate; IR must carry width, signedness, accumulator, and conversion policy.
- Typed MicroOp/materializer: production gate; distinct widen/narrow/convert typed ops.
- Execute/capture semantics: local formulas only; no vector execution opening.
- Retire/writeback: staged vector publication with rollback.
- Replay/rollback/conformance: require overflow, signed/unsigned, narrowing rounding/saturation/trap, NaN/float conversion if approved, tail/mask behavior.

## Boundaries

- Vector VLM: mandatory fail-closed gate.
- Lane6: no descriptor fallback.
- Lane7/VMX: no VMX-specific integration.
- No-emission: compiler helpers remain closed.

## Risks

- Treating closed `VZEXT` or base vector arithmetic as evidence for `VSEXT`/widening.
- Under-specifying conversion rounding/trap behavior.
- Accumulator footprint ambiguity for `VWMACC`.

## Closure Criteria

- A production package may promote each row only after width/conversion policy, VLM, vector runtime, retire, replay, and golden evidence close.
- Local partials without that package document contracts without executable claims.

## Prohibited Actions

- Open VLM contours, allocate opcodes, or add vector materializers only in the matching vector production package.
- Publish compiler helpers only by explicit helper authority.
