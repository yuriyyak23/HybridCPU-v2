# Phase Evidence Ledger

## Closed Phases

| Phase | Status | Closure Boundary |
| --- | --- | --- |
| 01 Audit and semantic closure | Closed | Truth boundary established; VDSA/custom accelerator/MatMul active-compute claims quarantined. |
| 02 Descriptor ABI and validator | Closed | DSC1 descriptor ABI, typed sideband, raw carrier rejection, `ExecutionEnabled=true` for the current DSC1 contour. |
| 03 Micro-op surface and typed-slot legality | Closed | `DmaStreamComputeMicroOp` lane6 surface, materialized Phase 06 execution, no custom/scalar/ALU/vector fallback. |
| 04 Domain and owner guard integration | Closed | Owner/domain guard before descriptor acceptance, replay/certificate reuse, helper admission, commit. |
| 05 Footprint and alias invalidation | Closed | Mandatory normalized read/write footprints, alias policy, replay invalidation. |
| 06 Retire/commit token and exception contour | Closed | Staged writes publish only through token commit; faults route through retire exception. |
| 07 explicit runtime/backend substrate | Closed | Copy/add/mul/fma/reduce runtime stages writes through token contour without StreamEngine or DMAController fallback. |
| 08 Replay evidence and determinism envelope | Closed | Replay reuse bounded by descriptor/carrier/footprint/owner-domain/certificate/token/lane evidence. |
| 09 Telemetry, backpressure, quota | Closed | Compute-specific telemetry, quota/backpressure, append-only reject taxonomy; telemetry is observation-only. |
| 10 Compiler/ISA adoption | Closed | Compiler emits canonical native opcode plus descriptor sideband; decoder/projector preserve ABI; no fallback. |
| 11 Legacy cleanup and documentation closure | Closed | Legacy accelerator/MatMul/HRoT wording quarantined; fail-closed seams preserved. |
| 12 Full validation baseline | Closed | Required validation passed; validation drift fixed without expanding lane6/native/custom-accelerator/owner-domain/commit semantics. |

## Latest Validation Evidence

All evidence below was captured on 2026-04-28 in the working tree.

- Phase 09 closure checkpoint:
  - Phase 09 filter: `95/95`.
  - All `DmaStreamCompute*`: `92/92`.
  - Validation baseline: `52/52`.
  - `TestAssemblerConsoleApps --iterations 200`: aggregate `Succeeded`, 10 child runs.
- Phase 10 closure checkpoint:
  - Required compiler/ISA adoption filter: `205/205`.
  - Focused `DmaStreamCompute` contour: `106/106`.
  - Validation baseline: `52/52`.
  - `TestAssemblerConsoleApps --iterations 200`: aggregate `Succeeded`, 10 child runs.
- Phase 11 closure checkpoint:
  - Phase 11 documentation/legacy filter: `40/40`.
  - Phase 10 compiler/ISA filter re-run after cleanup: `205/205`.
  - Validation baseline: `52/52`.
  - `TestAssemblerConsoleApps --iterations 200`: aggregate `Succeeded`, 10 child runs.
- Phase 12 closure checkpoint:
  - Full test suite: `5650/5650` passed, `2` skipped, `0` failed.
  - Validation baseline: `52/52`.
  - `TestAssemblerConsoleApps --iterations 200`: aggregate `Succeeded`, 10 child runs.
  - Focused `DmaStreamCompute` contour: `107/107`.
  - Compiler/ISA/native VLIW focused contour: `205/205`.
  - Legacy/quarantine documentation focused contour: `40/40`.
  - Validation drift fixes only: ISA freeze tests now recognize the canonical `97` opcode count
    and `DMA_STREAM` pipeline class; registry raw factory coverage preserves the
    `DmaStreamCompute` fail-closed seam; retired policy gap quarantine allows only the descriptor
    carrier ingress validator.

## Harness Delta Classification

Against `Documentation/AsmAppTestResults.md`, the observed accepted deltas are:

- elapsed time;
- artifact paths and timestamps;
- additive replay-reuse fallback/warmup detail lines;
- additive assistant visibility/non-retirement detail lines;
- minimal/non-interactive logging text;
- absolute artifact roots and final `Done.` line from the current non-interactive harness.

Observed real regressions: none.

## Ex1 Closure Addendum

Captured for WhiteBook actualization on 2026-04-30:

- Ex1 Phase00-Phase13 are closed in documentation/conformance scope.
- Phase12 adds the traceability matrix and documentation claim-safety migration
  guard. Future Design moves to Current Implemented Contract only after
  architecture approval, code evidence, positive/negative tests,
  compiler/backend conformance, and documentation claim-safety.
- Phase13 records dependency order as a planning/documentation gate only. It
  does not approve expansion beyond current lane6 Phase 06 DSC1, expansion
  beyond current L7 Phase 08 / Phase 08A commands, DSC2 execution,
  IOMMU-backed execution, coherent DMA/cache, async DMA overlap, successful
  partial completion, or production compiler/backend lowering.
- Current Ex1 evidence preserves:
  lane6 DSC1 Phase 06 enabled and `ExecutionEnabled=true`; token store
  issue-admission separation; retire/fault publication; absent or
  passive conflict-service authority; backend/addressing/IOMMU no-fallback and
  non-wiring; DSC2 parser-only behavior; all-or-none/progress non-publication;
  explicit non-coherent cache invalidation surfaces without coherent-DMA claim;
  L7 Phase08/08A scoped executable command contour plus remaining adjacent
  fail-closed boundaries; and Phase11 compiler/backend production lowering
  prohibitions.
- Downstream evidence cannot close upstream execution gates:
  parser-only DSC2 descriptors and footprints; model token stores and retire
  observations; progress diagnostics; L7 fake backend, queue, fence, token,
  register ABI, and commit APIs; IOMMU backend infrastructure and resolver
  decisions; conflict/cache observers; compiler sideband emission, descriptor
  preservation, and carrier projection.

## Current Closure

Phase 12 is closed. Phase 3 of the 2026-04-29 refactoring plan is recorded as a
historical baseline for lane6 `DmaStreamComputeMicroOp`; current code supersedes
that baseline for the scoped DSC1 Phase 06 contour.

The current refactoring baseline opens the Phase 06 DSC1 materialized lane6
execution contour: `DmaStreamComputeMicroOp.Execute(...)` enters
`DmaStreamComputeRuntime.ExecuteMaterializedMicroOpToCommitPending(...)`, while
`DmaStreamComputeRuntime.ExecuteToCommitPending(...)` remains a direct helper.
DSC2 execution, queue lifecycle, production DSC compiler/backend lowering,
async scheduling, global fences, and cache/coherency remain future architecture
decisions.

## Current Risk Closure

The current live `TestAssemblerConsoleApps` profile is `250` iterations,
`11` child runs, and `stream-vector` passed. Compare that run against
`Documentation/AsmAppTestResults.md`; the older successful `200` / `10`
profile remains the stored comparison log baseline only.

## Historical Residual-Risk Snapshot

Known residual risk from the older full-suite snapshot remains documented for
archive purposes only: `5928` passed, `2` skipped, and `3` failures in
independent `Phase12VliwCompatFreezeTests` coverage for missing
`build\run-compat-freeze-gate.ps1` and allowlist hits in
`TestAssemblerConsoleApps\StreamVectorSpecSuite.cs`. That snapshot is not
evidence of a current Stream/DMA/Accelerator refactor regression without a
matching failure in the focused contour.
