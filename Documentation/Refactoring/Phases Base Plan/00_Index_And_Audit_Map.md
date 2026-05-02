# HybridCPU-v2 Stream/DMA/Accelerator Refactoring Plan

Status date: 2026-04-29.

This folder is the active phase index for the Stream/DMA/ExternalAccelerator
refactoring. It reflects the implemented CPU ISE contract after the Phase 0-2
cleanup pass and the Phase 3 lane6 execution-model decision. Code remains the
source of truth when this plan and code diverge.

## Current Outcome

Phase 0-3 are complete for the approved cleanup/decision scope:

- current implemented behavior is locked as fail-closed carrier plus
  model/runtime helper APIs;
- WhiteBook sections under `Documentation/Stream WhiteBook/...` were aligned to
  code truth;
- diagnostics, comments, and tests now protect model-only versus executable
  boundaries;
- Phase 3 records Option A for lane6 `DmaStreamComputeMicroOp`: keep the
  fail-closed descriptor/decode evidence carrier;
- stale documentation roots are not current anchors;
- no executable lane6 DMA, executable `ACCEL_*`, async DMA overlap, global
  conflict hook, global fence, cache/coherency model, or ABI-breaking DSC1/SDC1
  change was implemented.

No executable DMA implementation is approved by Phase 3. Any future executable
lane6 DMA work remains a separate Phase 6 architecture decision and planning
item.

## Phase Files

| Phase | File | Status | Purpose |
|---|---|---|---|
| Phase 0 | `Phase_00_Baseline_Locking.md` | Closed | Baseline inventory and audit status. |
| Phase 1 | `Phase_01_Documentation_Truth_Alignment.md` | Closed | WhiteBook truth alignment. |
| Phase 2 | `Phase_02_Code_Contract_Cleanup.md` | Closed | Safe diagnostics/comments/tests cleanup. |
| Phase 3 | `Phase_03_DMA_Execution_Model_Decision.md` | Closed decision | Option A selected: keep fail-closed carrier; executable lane6 DMA remains future-only. |
| Phase 4 | `Phase_04_StreamEngine_DMA_Separation.md` | Closed for current contract | Separation is documented; future integration remains gated. |
| Phase 5 | `Phase_05_ExternalAccelerator_Contract.md` | Closed for current contract | L7-SDC current model-only contract is documented. |
| Phase 6 | `Phase_06_Future_Feature_Backlog.md` | Open backlog | Future architecture queue requiring approval. |
| Phase 6 gate | `Phase_06_FEATURE_000_Executable_Lane6_DSC_Gate.md` | Open / not approved | Intake checklist for future executable lane6 DSC; no implementation authorization. |

## Audit Transfer Map

| Audit ID | Current status | Closed evidence / gate |
|---|---|---|
| AUDIT-001 | Closed for current contract | L7 carriers remain no-write; `AcceleratorRegisterAbi` is model-only. |
| AUDIT-002 | Closed for current contract | `DmaStreamComputeMicroOp.Execute` remains fail-closed. |
| AUDIT-003 | Closed for current contract | L7 model APIs are not instruction execution. |
| AUDIT-004 | Closed for current contract | Conflict manager is explicit/optional, not a global CPU memory hook. |
| AUDIT-005 | Closed for current contract | DSC runtime/helper is separate from `StreamEngine` and `DMAController`. |
| AUDIT-006 | Closed for current contract | `StreamEngine.BurstIO` DMA route is synchronous helper behavior. |
| AUDIT-007 | Closed for current contract | DSC runtime/helper uses physical main memory ranges. |
| AUDIT-008 | Closed for current contract | L7 faults are model observations/results, not retire exceptions. |
| AUDIT-009 | Future design decision | No DSC pause/resume/cancel/reset/fence ISA surface. |
| AUDIT-010 | Closed for current contract | `AcceleratorFenceModel` is model-only; executable `ACCEL_FENCE` fails closed. |
| AUDIT-011 | Closed for current contract | Component ownership and lifecycle are documented as separate. |
| AUDIT-012 | Closed | Current docs use `Documentation/Stream WhiteBook/...` roots; legacy roots are not current anchors. |

## Current Source Evidence Inventory

Lane6 DmaStreamCompute:

- `HybridCPU_ISE/Core/Pipeline/MicroOps/DmaStreamComputeMicroOp.cs`
- `HybridCPU_ISE/Core/Execution/DmaStreamCompute/DmaStreamComputeDescriptorParser.cs`
- `HybridCPU_ISE/Core/Execution/DmaStreamCompute/DmaStreamComputeRuntime.cs`
- `HybridCPU_ISE/Core/Execution/DmaStreamCompute/DmaStreamAcceleratorBackend.cs`
- `HybridCPU_ISE/Core/Execution/DmaStreamCompute/DmaStreamComputeToken.cs`

Lane7 L7-SDC ExternalAccelerators:

- `HybridCPU_ISE/Core/Pipeline/MicroOps/SystemDeviceCommandMicroOp.cs`
- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Descriptors/AcceleratorDescriptorParser.cs`
- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Tokens/AcceleratorRegisterAbi.cs`
- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Fences/AcceleratorFenceModel.cs`
- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Commit/AcceleratorCommitModel.cs`
- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Queues/AcceleratorCommandQueue.cs`
- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Conflicts/ExternalAcceleratorConflictManager.cs`

StreamEngine, DMA, and memory:

- `HybridCPU_ISE/Core/Execution/StreamEngine/StreamEngine.cs`
- `HybridCPU_ISE/Core/Execution/StreamEngine/StreamEngine.BurstIO.cs`
- `HybridCPU_ISE/Memory/DMA/DMAController.cs`
- `HybridCPU_ISE/Processor/Memory/Processor.Memory.cs`

## Validation Snapshot

Last recorded validation for this refactoring pass:

- focused Stream/DMA/L7 suite: 103 passed, 0 failed;
- L7 carrier regression recheck: 59 passed, 0 failed;
- full `dotnet test ".\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj"`:
  5928 passed, 2 skipped, 3 failed;
- the three full-suite failures are unrelated residual Phase12 compat-freeze
  issues: missing `build\run-compat-freeze-gate.ps1` and existing allowlist
  hits in `TestAssemblerConsoleApps\StreamVectorSpecSuite.cs`;
- `TestAssemblerConsoleApps -- --iterations 200`: aggregate succeeded,
  current matrix has 11 child runs due `stream-vector`;
- `matrix-smoke --iterations 200 --telemetry-logs minimal`: aggregate
  succeeded, 3 child runs;
- `matrix-memory --iterations 200 --telemetry-logs minimal`: aggregate
  succeeded, 2 child runs and stable baseline counters.

Do not treat absent `build` scripts as current required commands until those
scripts are restored or the Phase12 compat-freeze gate is repaired.

## Open Work

No approved Phase 0-3 code cleanup or implementation task remains open.

Current gate result:

1. Lane6 `DmaStreamComputeMicroOp` remains a fail-closed descriptor/decode
   evidence carrier.
2. `DmaStreamComputeRuntime` remains an explicit runtime/model helper.
3. No pipeline token allocation, executable DSC compiler/backend lowering, or
   executable lane6 DMA path is approved.
4. Future executable DMA is tracked as Phase 6 `FEATURE-000` and requires a
   separate implementation plan covering
   pipeline stage, token allocation, commit/retire, fault priority, replay,
   memory ordering, compiler lowering, and conformance tests before coding.
