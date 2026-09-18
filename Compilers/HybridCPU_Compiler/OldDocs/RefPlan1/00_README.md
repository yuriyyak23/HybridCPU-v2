# HybridCPU Compiler Core Refactor Plan

Status: Phase 09 compiler cleanup closed for the current backlog.

This directory contains the normative plan and implementation notes for the
`HybridCPU_Compiler/Core` refactor. The original phase documents remain
architecture and audit context. The final Phase 09 cleanup state is now
summarized in ADR-style documents under `CompilerRefDocs`.

## Read This First

For a new continuation session, read these files in order:

1. `00_README.md`
2. `PHASE09_REMAINING_COMPILER_WORK.md`
3. `PHASE09_CLEANUP_MIGRATION_READINESS.md`
4. `CompilerRefDocs/README.md`
5. `CONTINUATION_PROMPT_PHASE09.md`

Use the original phase files as background and constraints:

- `01_phase_inventory_and_freeze.md`
- `02_phase_authority_taxonomy.md`
- `03_phase_ir_intent_and_contours.md`
- `04_phase_lowering_decision_api.md`
- `05_phase_carrier_sideband_descriptor_abi.md`
- `06_phase_typed_slot_and_legality_bridge.md`
- `07_phase_contour_providers.md`
- `08_phase_evidence_and_telemetry.md`
- `09_phase_tests_migration_and_exit.md`
- `10_architectural_audit_addendum.md`
- `CURRENT_BEHAVIOR.md`

`CURRENT_BEHAVIOR.md` is a Phase 01 inventory snapshot. Treat its wrapper
target lists as historical unless the final Phase 09 docs say a surface is
still open.

## Current State

Implemented or substantially introduced:

- Phase 01 inventory/current behavior artifact.
- Phase 02 compiler authority taxonomy.
- Phase 03 semantic intent and execution contour split.
- Phase 04 `CompilerLoweringDecision` and `LegacyApiTranslation`.
- Phase 05 carrier/sideband/descriptor/facts/evidence envelope split.
- Phase 06 typed-slot/runtime bridge envelopes.
- Phase 07 contour analyzer/provider shells and no hidden fallback policy.
- Phase 08 evidence/telemetry snapshots and isolation validation.
- Phase 09 negative matrices for:
  - contract and bridge;
  - typed-slot/structural admission;
  - carrier/sideband/descriptor;
  - MatrixTile;
  - Stream/vector;
  - DSC lane6;
  - L7-SDC lane7;
  - VMX;
  - SecureCompute;
  - fallback/evidence;
  - validation/result cleanup.
- Phase 09 positive MatrixTile/VectorTransfer emission wrappers:
  `CompilerPositiveEmissionResult<TPlan>`, `LowerWithDecision(...)`, and
  `Compile*WithDecision(...)`.
- Phase 09 `HybridCpuThreadCompilerContext` facade audit and runtime guard
  observation wrappers.
- Phase 09 directive parser cleanup:
  `DirectiveParseResult.IsDirectiveParsed`,
  `CompilerDirectiveParseObservation`, and obsolete compatibility `Success`.
- Phase 09 structural slot cleanup for annotation/profile/admission
  descriptor `LegalSlots` surfaces, SlotModel placement/search entrypoints,
  materialized bundle placement wording, and placement-quality slot facts.
- Phase 09 helper recovery result placement cleanup:
  `CompilerHelperRecoveryResult<TPlan>` lives in its own lowering result file.
- Phase 09 final public API scan/obsolete sweep for bare authority-like names
  and plan-only public lowering facades.
- Phase 09 validation-only runtime-local `IsValid` audit for memory/IOMMU/
  domain validation, event/completion routing, assist transport and pipeline
  slot descriptors.
- Phase 09 final ADR promotion in `CompilerRefDocs`.

Recent green gates:

- targeted cleanup/readiness after runtime-local validation-only `IsValid`
  audit: 30/30
- MatrixTile/VectorTransfer positive-emission contracts: 16/16
- wide Phase 09 authority/negative matrix slice set: 175/175

## Current Open Work

The broad Phase 02-08 architecture construction, Phase 09 compiler-code public
API cleanup, and requested validation-only/runtime-local classification are
complete in this backlog. The final documentation promotion is also complete in
`CompilerRefDocs`.

See `PHASE09_REMAINING_COMPILER_WORK.md` for details.

## Core Invariant

```text
carrier != execution
execution != publication
publication != authority
authority != commit
commit != retire
retire != evidence
evidence != production lowering
```

## Non-Negotiable Rules

- Compiler is not runtime authority.
- Compiler never owns final runtime `LegalityDecision`.
- Runtime Legality A/B remains runtime-owned.
- Typed-slot facts are structural evidence only.
- Sideband/descriptor/token/certificate/evidence are not execution rights.
- Descriptor parser success is not production lowering.
- Helper success is not production lowering.
- Carrier emission is not execution, publication, commit, or retire.
- VMX is projection/no-emission only in the compiler layer.
- VMCS is not compiler-owned state.
- `VmxCaps` is not capability authority.
- SecureCompute is policy/admission/evidence-only in compiler layer.
- SecureCompute must not become secure backend execution.
- MatrixTile is helper ABI only unless explicit production gates exist.
- DSC and L7-SDC are distinct contours with no hidden fallback.
- L7 descriptorless submit must fail closed.
- Host-owned evidence must not enter guest/domain architectural state.

## Historical Slices

Files named `PHASE0*_..._IMPLEMENTATION.md` and
`PHASE09_NEGATIVE_MATRIX_SLICE*.md` are retained as historical implementation
slices. Their old "next task" sections have been converted to historical notes.
For the final Phase 09 state, use `PHASE09_REMAINING_COMPILER_WORK.md` and
`CompilerRefDocs`.

## Validation Policy

After each meaningful implementation slice:

1. Run the smallest relevant targeted test filter.
2. Run the Phase 09 authority/negative matrix.
3. Do not treat runtime-local validation names as compiler issues unless a
   compiler-facing caller consumes them.

The current wide Phase 09 filter is documented in the latest session notes and
should include the cleanup/readiness tests plus MatrixTile, Stream/vector, DSC,
L7-SDC, VMX, SecureCompute, fallback/evidence, no-emission, contract handshake
and positive-emission contract tests.
