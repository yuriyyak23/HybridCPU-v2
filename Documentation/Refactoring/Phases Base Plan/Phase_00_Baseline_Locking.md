# Phase 0 - Baseline Locking

Status date: 2026-04-29.

Status: closed for the approved refactoring scope.

Goal: freeze current implemented behavior before documentation rewrite or code
cleanup changes semantics.

## Locked Current Contract

- `DmaStreamComputeMicroOp` is a lane6 typed-slot descriptor/decode carrier.
- `DmaStreamComputeMicroOp.Execute(ref Processor.CPU_Core core)` throws
  fail-closed.
- `DmaStreamComputeDescriptorParser.ExecutionEnabled` is `false`.
- `DmaStreamComputeRuntime` is an explicit runtime/model helper, not canonical
  micro-op execution.
- `DmaStreamAcceleratorBackend` reads physical main memory through
  `Processor.MainMemoryArea.TryReadPhysicalRange`.
- `DmaStreamComputeToken.Commit(...)` writes physical main memory only from
  `CommitPending`, after fresh guard validation, exact staged coverage, and
  all-or-none write/rollback checks.
- `SystemDeviceCommandMicroOp` is a lane7 system-device carrier family.
- Every `ACCEL_*` carrier direct `Execute(...)` throws fail-closed.
- Every `ACCEL_*` carrier has `WritesRegister = false` and empty
  `WriteRegisters`.
- `AcceleratorRegisterAbi` is model-only.
- `AcceleratorFenceModel` is model-only.
- `AcceleratorCommitResult.RequiresRetireExceptionPublication` is `false`.
- `ExternalAcceleratorConflictManager` is explicit/optional and not wired into
  the global CPU load/store pipeline.
- `StreamEngine.BurstIO` can drive `DMAController.ExecuteCycle()` but does so
  synchronously in helper code.

## Unsupported Current Behavior

These are not implemented behavior:

- executable `DmaStreamComputeMicroOp.Execute`;
- executable `ACCEL_SUBMIT`, `ACCEL_POLL`, `ACCEL_WAIT`, `ACCEL_CANCEL`,
  `ACCEL_FENCE`, or `ACCEL_QUERY_CAPS`;
- architectural `rd` writeback from any `ACCEL_*` carrier;
- backend execution through `SystemDeviceCommandMicroOp.Execute`;
- global conflict-manager checks for every CPU load/store;
- architectural async CPU/DMA overlap;
- DSC ISA pause/resume/cancel/reset/fence controls;
- DSC1 stride/tile/2D/scatter-gather or partial-success modes;
- cache-coherent or virtual/IOMMU-backed DSC runtime memory.

## Audit Status

| Audit ID | Status | Baseline assertion |
|---|---|---|
| AUDIT-001 | Closed/current | L7 carriers do not write architectural registers. |
| AUDIT-002 | Closed/current | Lane6 direct execution throws fail-closed. |
| AUDIT-003 | Closed/current | L7 model APIs are not instruction execution. |
| AUDIT-004 | Closed/current | Conflict manager is optional/explicit. |
| AUDIT-005 | Closed/current | DSC runtime/helper does not call `StreamEngine` or `DMAController`. |
| AUDIT-006 | Closed/current | StreamEngine DMA helper loops synchronously. |
| AUDIT-007 | Closed/current | DSC runtime/helper uses physical main memory. |
| AUDIT-008 | Closed/current | L7 faults are model results, not retire exceptions. |
| AUDIT-009 | Future decision | DMA channel controls are not DSC ISA controls. |
| AUDIT-010 | Closed/current | Fence model is not executable `ACCEL_FENCE`. |
| AUDIT-011 | Closed/current | Components have separate ownership/lifecycle. |
| AUDIT-012 | Closed/current | Current documentation anchors live under `Documentation/Stream WhiteBook/...`. |

## Test Coverage Evidence

- Fail-closed carrier execution:
  `DmaStreamComputeMicroOp`, all `SystemDeviceCommandMicroOp` subclasses.
- Descriptor ABI:
  DSC1 positive/negative parser cases and SDC1 parser/conformance cases.
- Model/executable separation:
  `AcceleratorRegisterAbi`, `AcceleratorFenceModel`, queue/backend/commit
  model APIs versus fail-closed carriers.
- Memory and ordering:
  DSC physical memory bounds, staged-before-commit, rollback, DMAController
  control surface separation, and explicit conflict-manager optionality.
- Documentation safety:
  current WhiteBook anchors, stale-anchor guard, and mandatory claim checks.

## Exit Result

Phase 0 is complete. The baseline can be changed only by intentionally changing
tests and documentation together with an approved architecture decision.
