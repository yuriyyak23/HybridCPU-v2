# Phase 15 - Compatibility And Conformance Sweep

## Phase 15 Closure

Phase 15 is closed as a compatibility and conformance sweep, not as a new executable production slice. The package adds a CloseToRTL audit contract and
focused regression tests that cross-check executable catalog rows, deferred
rows, VLM boundaries, no-emission boundaries, and VMX-neutral integration rules.

It does not allocate opcodes, open decoder/encoder ABI, add `InstructionIR`
projection, materializers, typed MicroOps, scheduler binding, execution,
retire side effects, replay engines, golden execution artifacts, compiler
helpers, external backends, or VMX-specific handlers.

## Goal

Define and run the compatibility sweep required after individual production
slices close.

## Production Path Overlay

Phase 15 is not a blocker; it is the cross-phase audit package. It verifies that
closed rows have catalog/opcode/decoder/IR/materializer/MicroOp/execution/
retire/replay/golden/no-emission evidence and that deferred rows still fail
closed. It also checks that VMX observes Non-VMX rows only through the generic
runtime model unless an explicit virtualization-boundary projection was opened.

## Instructions / Contours

All Non-VMX rows covered by phases 01-14, plus already executable baseline rows used for regression comparison: `CTZ`, `SEXT.B`, `SEXT.H`, `ZEXT.H`, `ROL`, `ROR`.

## Existing Partial Files

All existing partials under:

- `Lanes00_03Scalar`
- `Lanes00_03Vector`
- `Lanes04_05Memory`
- `Lane06DmaStream`
- `Lane07SystemControl`
- `CompatibilityConformance/Phase15CompatibilityConformanceSweepContract.cs`

## New Partial Files Allowed

- No instruction semantics should be added by the sweep itself.
- Optional local audit partials may be added only if they record compatibility metadata inside `CloseToRTL\Core` and do not change execution claims.

## Local CloseToRTL Logic

Check that future local partials remain consistent with metadata constants, no-emission boundaries, VLM gates, descriptor ownership, Lane7 privilege/host-evidence boundaries, and existing executable scalar objects.

## Production Evidence Gates

Full compatibility requires cross-subsystem work outside this planning folder: status/catalog, opcode or descriptor registries, decoder/encoder ABI, `InstructionIR`, materializers, typed MicroOps, runtime dispatch/capture, retire, vector legality, Lane6/Lane7 runtimes, replay/rollback tests, conformance suites, golden artifacts, and compiler no-emission tests.

## Metadata Constants

Do not change constants during the sweep unless a preceding phase closed the relevant evidence chain. Especially preserve reserved/no-emission, parser-only, descriptor-only, VLM fail-closed, no-base-opcode-duplication, no-host-evidence-leak, and VMX-neutral markers.

## Evidence Chain Stories

- Decoder/encoder ABI: verify only after phase-specific ABI gates close.
- InstructionIR/projection: verify projections match operand shapes and sidebands.
- Typed MicroOp/materializer: verify every executable row has exactly one authority path.
- Execute/capture semantics: verify no metadata-only row has an execution path.
- Retire/writeback/side effects: verify register writeback, staged publication, staged commit, and owned side effects are retire-controlled.
- Replay/rollback/conformance: run phase-specific rollback and conformance suites plus no-emission regression.

## Special Boundaries

- Vector VLM: all vector rows must fail closed unless explicitly opened by their phase.
- Lane6 sideband: descriptor and queue rows must not appear as scalar opcodes.
- Lane7/VMX: privileged/control-plane/host evidence rows require virtualization policy before guest-visible effects.
- No-emission: compiler helpers remain closed unless a dedicated evidence chain opens them.

## Risks

- One phase accidentally changing ABI assumptions for another phase.
- Aggregate partial metadata masking leaf status.
- Golden artifacts drifting from catalog/status or no-emission boundaries.

## Closure Criteria

- Every executable row has complete evidence from status/catalog through golden artifacts.
- Every non-executable row has regression coverage proving no decoder/materializer/compiler/runtime path opened accidentally.
- Existing executable baseline rows remain compatible.
- The Phase 15 audit contract and focused sweep tests prove the sweep is
  compatibility/conformance-only and does not open a production surface.

## Prohibited Actions

- Do not batch multiple domain openings into the sweep.
- Do not use the sweep to sneak in opcodes, VMX handlers, compiler helpers, or runtime execution.
