# Phase 08 - Vector Fixed-Point And Saturating

## Goal

Promote VLM-gated fixed-point, saturating, average, clip, and prefix min/max
rows once saturation/rounding/scan policy is explicit and tested.

## Production Path Overlay

Use the vector full production path in `README.md`. Saturating/fixed-point rows
must publish clamp, signedness, element width, rounding/truncation, exception,
retire, replay, and golden behavior. Prefix min/max remains separate from
closed `VSCAN.SUM` and scalar `MIN/MAX` evidence. VMX is generic unless policy
state becomes guest-visible or VMCS-projected.

## Instructions / Contours

- Saturating arithmetic/shift: `VSUB.SAT`, `VMUL.SAT`, `VSLL.SAT`, `VSRL.SAT`, `VSRA.SAT`.
- Average/clip: `VAVG`, `VAVG.R`, `VCLIP`.
- Prefix scans: `VSCAN.MIN`, `VSCAN.MAX`.

## Existing Partial Files

- `Lanes00_03Vector\SaturatingFixedPoint\*.cs`
- `Lanes00_03Vector\PrefixScan\VscanMinInstruction.cs`
- `Lanes00_03Vector\PrefixScan\VscanMaxInstruction.cs`

## New Partial Files Allowed

- `*.Legality.cs` for VLM element-width/signedness/tail metadata.
- `*.Semantics.cs` for non-authoritative lane formulas.
- `*.SaturationContract.cs` and `*.ScanContract.cs` for policy notes.

## Local CloseToRTL Logic

Production/local partials may capture policy placeholders for signedness, element width, clamp range, rounding, truncation, clip bounds, prefix scan ordering, and fail-closed checks. Execution opens only when the same package closes the vector production path.

## Production Evidence Gates

VLM executable contour, opcode/contour allocation, decoder/encoder ABI, `InstructionIR` projection, typed vector MicroOps, materializer, scheduler/lane binding, execute/capture, staged retire, replay/rollback, conformance, and golden artifacts.

## Metadata Constants

Preserve `VectorFixedPointSaturatingFailClosed`, `VectorScanContourFailClosed`, `RequiresSaturatingPolicyAbi`, `RequiresAveragePolicyAbi`, `RequiresClipBoundsAbi`, `RequiresPrefixScanPolicyAbi`, `RequiresRoundingTruncationPolicyAbi`, `RequiresSaturatingShiftMeaningDecision`, `MayRemainReservedIfNonMeaningful`, `SeparateFromClosedVaddSat`, `SeparateFromClosedVscanSum`, `RequiresVectorLegalityMatrixClosure`, `NoDescriptorFallback=true`, `NoHiddenScalarLowering=true`, `NoMultiOpEmission=true`, `NoVmxSpecificPath=true`, `IsExecutable=false`, and `CompilerHelperAllowed=false`.

## Phase 08A Decision Gate - Saturating Arithmetic And Shifts

Status: explicit negative production decision. `VSUB.SAT`, `VMUL.SAT`,
`VSLL.SAT`, `VSRL.SAT`, and `VSRA.SAT` remain reserved/no-allocation rows in
the `VectorSaturatingFixedPoint` status bucket. This slice does not allocate
opcodes, does not add decoder/encoder sidebands, does not add saturating
`InstructionIR` projection, does not register materializers, does not publish
typed vector MicroOps, does not bind runtime execution, and does not add
compiler helper authority.

Decision details:

- Saturating policy ABI: unresolved; signedness, element width, clamp range,
  overflow, mask/tail behavior, and staged publication must be canonical.
- Shift operand ABI: unresolved for `VSLL.SAT`, `VSRL.SAT`, and `VSRA.SAT`.
- Shift meaningfulness: unresolved; right-shift saturating forms may remain
  reserved if the approved policy says they are not meaningful.
- Evidence separation: closed `VADD.SAT` proves only saturating add and does not
  authorize subtract, multiply, or saturating shift forms.
- Lowering: no descriptor fallback, hidden scalar lowering, or multi-op
  emission.
- VMX: generic runtime boundary only; no VMX-specific path is introduced.

Rationale: saturating arithmetic is policy-sensitive. Reusing base vector
arithmetic or closed `VADD.SAT` would skip result clamp, overflow, and replay
evidence.

## Phase 08B Decision Gate - Average And Clip

Status: explicit negative production decision. `VAVG`, `VAVG.R`, and `VCLIP`
remain reserved/no-allocation rows in the `VectorSaturatingFixedPoint` status
bucket. This slice does not allocate opcodes, decoder/encoder sidebands, IR
policy projection, materializer rows, typed vector MicroOps, execution, retire
publication, or compiler helper authority.

Decision details:

- Average policy ABI: unresolved; signedness, element width, overflow,
  truncation, rounding, and tie behavior must be explicit.
- `VAVG.R`: rounding mode and tie policy remain separate ABI blockers.
- `VCLIP`: clip bounds encoding, narrowing/result-width policy, signedness,
  truncation/rounding behavior, mask/tail, and staged publication remain open.
- Lowering: no descriptor fallback, hidden scalar lowering, or multi-op
  emission.
- VMX: no VMX-specific path.

Rationale: average and clip are not derivable from same-width arithmetic without
rounding/truncation, bounds, and result-footprint evidence.

## Phase 08C Decision Gate - Prefix Min/Max Scans

Status: explicit negative production decision. `VSCAN.MIN` and `VSCAN.MAX`
remain reserved/no-allocation rows in the `VectorScanSegmentMovement` status
bucket. This slice does not allocate opcodes, decoder/encoder sidebands,
prefix-scan IR projection, materializer rows, typed vector MicroOps, execution,
retire publication, or compiler helper authority.

Decision details:

- Prefix policy ABI: unresolved; inclusive/exclusive behavior, element order,
  signedness/element type, tail/mask behavior, and replay determinism must be
  canonical.
- Evidence separation: closed `VSCAN.SUM` proves only the packed 1D inclusive
  prefix-sum contour and does not authorize min/max scan forms.
- Lowering: no descriptor fallback, hidden scalar lowering, scalar `MIN/MAX`
  lowering, or multi-op emission.
- VMX: generic runtime boundary only.

Rationale: min/max prefix scan changes ordering, comparison, and replay
semantics. `VSCAN.SUM` and scalar/vector `MIN/MAX` are not sufficient runtime
evidence.

## Evidence Chain Stories

- Decoder/encoder ABI: production gate; element width, signedness, rounding, and scan policy must be explicit.
- InstructionIR/projection: production gate; IR must carry policy sidebands.
- Typed MicroOp/materializer: production gate; saturating and scan typed ops.
- Execute/capture semantics: local policy contracts only.
- Retire/writeback: staged vector publication with rollback.
- Replay/rollback/conformance: require clamp boundaries, overflow, rounding ties, shift meaningfulness, prefix order, tail/mask behavior.

## Boundaries

- Vector VLM: mandatory fail-closed gate.
- Lane6: no descriptor fallback.
- Lane7/VMX: no VMX-specific path.
- No-emission: compiler helpers closed.

## Risks

- Treating `VADD.SAT` or `VSCAN.SUM` evidence as coverage for these rows.
- Opening `VSRL.SAT`/`VSRA.SAT` before deciding whether saturating right shift is meaningful.
- Ambiguous average rounding.

## Closure Criteria

- A production package may promote each row only after semantics, VLM, vector runtime, retire, replay, and golden evidence close.
- Local partials without that package record policy contracts only.

## Prohibited Actions

- Open VLM, opcodes, materializers, runtime execution, or compiler emission only in the matching vector production package with helper authority where needed.
