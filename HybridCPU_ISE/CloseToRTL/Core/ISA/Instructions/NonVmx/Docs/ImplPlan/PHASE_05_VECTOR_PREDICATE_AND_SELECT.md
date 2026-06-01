# Phase 05 - Vector Predicate And Select

## Goal

Promote VLM-gated vector predicate/select rows through exact contour legality,
typed vector materialization, and retire/publication evidence.

## Production Path Overlay

Use the `Vector / VLM / Vector Memory Rows` path in `README.md`. Predicate
sidebands, tail/mask policy, scalar-result publication, and predicate-only
state must be explicit in VLM, decoder/encoder ABI, IR, MicroOp, execution,
retire, replay, and tests. VMX is generic unless predicate state becomes
VMCS-visible or a guest-visible capability.

## Instructions / Contours

- Vector select/merge: `VMERGE`, `VSELECT`.
- Predicate scalar-result: `VFIRST`, `VANY`, `VALL`.
- Predicate-only publication: `VMSIF`, `VMSOF`.

## Existing Partial Files

- `Lanes00_03Vector\PredicateMask\VmergeInstruction.cs`
- `Lanes00_03Vector\PredicateMask\VselectInstruction.cs`
- `Lanes00_03Vector\PredicateMask\VfirstInstruction.cs`
- `Lanes00_03Vector\PredicateMask\VanyInstruction.cs`
- `Lanes00_03Vector\PredicateMask\VallInstruction.cs`
- `Lanes00_03Vector\PredicateMask\VmsifInstruction.cs`
- `Lanes00_03Vector\PredicateMask\VmsofInstruction.cs`

## New Partial Files Allowed

- `*.Legality.cs` for local VLM contour metadata.
- `*.Semantics.cs` for non-authoritative element/predicate behavior.
- `*.PublicationContract.cs` for scalar-result or predicate-result retire notes.

## Local CloseToRTL Logic

Production/local partials may express predicate mask sideband requirements, tail/mask policy placeholders, scalar-result shape metadata, and fail-closed legality checks. Execution opens only when the same package closes the vector production path.

## Production Evidence Gates

Vector status/catalog rows, opcode or contour authority, decoder/encoder ABI, sideband predicate representation, `InstructionIR` projection, vector materializer, typed vector MicroOps, execution lane binding, staged publication, retire, replay/rollback, VLM executable row, and conformance/golden artifacts.

## Metadata Constants

Preserve `VectorContourFailClosed`, `VectorScalarResultContourFailClosed`, `VectorPredicateOnlyContourFailClosed`, `RequiresPredicateMaskSideband=true`, `RequiresVectorLegalityMatrixClosure=true`, `RequiresRetireStagedPublication=true`, `NoDescriptorFallback=true`, `NoHiddenStreamEngineFallback=true`, `NoHiddenDmaFallback=true`, `IsExecutable=false`, and `CompilerHelperAllowed=false`.

## Phase 05A Decision Gate - VMERGE / VSELECT

Status: explicit negative production decision. `VMERGE` and `VSELECT` remain
reserved/no-allocation rows in the `VectorMaskSelect` extension bucket. This
slice does not allocate opcodes, does not add decoder/encoder acceptance, does
not add `InstructionIR` projection, does not register a materializer, does not
publish a typed vector MicroOp, does not bind a vector lane for execution, and
does not add compiler helper authority.

Decision details:

- Alias policy: treat `VMERGE` and `VSELECT` as distinct mnemonics until an
  explicit ABI decision proves an alias relationship.
- Result/source polarity: unresolved; no canonical source/result polarity is
  inferred from existing vector mask publication or predicative movement rows.
- Masked-off/tail behavior: unresolved; requires explicit mask/tail policy ABI
  before execution can open.
- Predicate sideband: must be explicit carrier state; it is not inferred from
  VLM membership or from `StreamLength`.
- Element width / LMUL / VL: unresolved; requires a dedicated VLM contour and
  vector type sideband before materialization.
- Lowering: no descriptor fallback, no hidden StreamEngine/DMA fallback, no
  hidden scalar lowering, and no multi-op compiler/runtime emission.
- VMX: generic boundary only; no VMX-specific path exists for this slice.

Rationale: existing production evidence closes `VMSBF`,
predicate-mask logical publication, scalar-result `VPOPC`, predicative movement,
and permutation contours, but none of those rows specifies select/merge
polarity, masked-off/tail behavior, or a destination/source carrier ABI for
`VMERGE`/`VSELECT`. Name adjacency is not evidence.

## Phase 05B Decision Gate - VFIRST / VANY / VALL

Status: explicit negative production decision. `VFIRST`, `VANY`, and `VALL`
remain reserved/no-allocation rows in the `VectorMaskSelect` extension bucket.
This slice does not allocate opcodes, does not add decoder/encoder acceptance,
does not add scalar-result `InstructionIR` projection, does not register a
materializer, does not publish a typed vector/scalar-result MicroOp, and does
not add compiler helper authority.

Decision details:

- Scalar result destination ABI: unresolved; scalar `rd` publication must be
  retire-owned and replay-described before execution can open.
- Empty-mask policy: unresolved; no-active-bit sentinel for `VFIRST` and boolean
  empty-mask policy for `VANY`/`VALL` remain explicit ABI blockers.
- `VFIRST` first-index policy: unresolved; index width/sign and x0 discard
  behavior must be canonical before decoder/materializer acceptance.
- `VANY`/`VALL` boolean result encoding: unresolved; no implicit 0/1 or mask
  width inference is accepted.
- Active VL/tail semantics: unresolved; active VL and tail policy must be part
  of the VLM contour and scalar-result carrier.
- VMX/compiler: generic boundary only; no helper, descriptor fallback, hidden
  StreamEngine/DMA fallback, hidden lowering, or multi-op emission is allowed.

Rationale: closed scalar-result `VPOPC` proves only its own popcount footprint.
It does not define first-index sentinel, boolean encoding, empty-mask policy, or
retire/replay publication for these scalar-result predicate rows.

## Phase 05C Decision Gate - VMSIF / VMSOF

Status: explicit negative production decision. `VMSIF` and `VMSOF` remain
reserved/no-allocation rows in the `VectorMaskSelect` extension bucket. This
slice does not allocate opcodes, does not add decoder/encoder acceptance, does
not add predicate-only `InstructionIR` projection, does not register a
materializer, does not publish a typed vector MicroOp, and does not add compiler
helper authority.

Decision details:

- Predicate-only destination representation: unresolved; publication target and
  source sideband must be canonical before execution can open.
- Prefix semantics: unresolved; including-first (`VMSIF`) and only-first
  (`VMSOF`) behavior must be stated as distinct predicate publication policies.
- Tail/mask policy: unresolved; staged predicate publication must include
  tail/mask behavior and rollback evidence.
- Vector RF exposure: forbidden for this slice; predicate-only publication must
  not create a vector register-file result surface.
- VMX/compiler: generic boundary only; no helper, descriptor fallback, hidden
  StreamEngine/DMA fallback, hidden lowering, or multi-op emission is allowed.

Rationale: closed `VMSBF` proves only set-before-first prefix publication.
It does not define including-first or only-first predicate publication, staged
predicate-only destination representation, or rollback semantics for
`VMSIF`/`VMSOF`.

## Evidence Chain Stories

- Decoder/encoder ABI: production gate; predicate sideband and scalar-result encodings must be explicit.
- InstructionIR/projection: production gate; vector source/destination/predicate fields must project into typed IR.
- Typed MicroOp/materializer: production gate; each row needs typed vector MicroOp publication.
- Execute/capture semantics: local non-authoritative element behavior only; no vector lane capture.
- Retire/writeback: vector staged publication or scalar-result publication with rollback.
- Replay/rollback/conformance: require mask/tail policy, first-index behavior, empty mask, all mask, and predicate-only publication tests.

## Boundaries

- Vector VLM: mandatory; rows fail closed until VLM contour is executable.
- Lane6: no descriptor, StreamEngine, or DMA fallback.
- Lane7/VMX: no VMX-specific projection.
- No-emission: compiler helpers remain closed.

## Risks

- Treating `VMERGE` and `VSELECT` as aliases before ABI decides result/source polarity.
- Publishing scalar results without a stable scalar-result ABI.
- Bypassing VLM because metadata exists.

## Closure Criteria

- A production package may promote each row only after VLM, vector ABI, materializer, runtime, retire, replay, and golden evidence close.
- Local partials without that package preserve fail-closed VLM state.

## Prohibited Actions

- Open vector contour only when VLM closes in the same production package.
- Add compiler emission only by explicit helper authority; hidden lowering is not evidence.
