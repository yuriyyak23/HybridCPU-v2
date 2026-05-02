# Phase 2 - Code Contract Cleanup

Status date: 2026-04-29.

Status: closed for the approved cleanup scope.

Goal: make code contracts architecturally unambiguous without implementing new
execution behavior.

## Cleanup Rules Preserved

- Fail-closed carrier execution remains fail-closed.
- Model APIs are clarified as model/runtime-side APIs, not pipeline execution.
- Unsupported operations fail closed.
- No executable lane6 DMA or executable L7-SDC command path was introduced.
- No DSC1/SDC1 ABI-breaking change was introduced.

## Closed Work Items

| Refactor ID | Status | Evidence |
|---|---|---|
| REFACTOR-020 | Closed | `SystemDeviceCommandMicroOp.Execute` diagnostic preserves fail-closed carrier semantics, no backend execution, no staged write publication, no architectural `rd` writeback, no fallback routing. |
| REFACTOR-021 | Closed | `DmaStreamComputeMicroOp.Execute` diagnostic states explicit runtime helper, not `MicroOp.Execute`, and no `StreamEngine`/`DMAController` fallback. |
| REFACTOR-022 | Closed | DSC parser tests cover missing guard, unsupported range encoding, non-`AllOrNone` policy, reserved fields, and positive ABI cases. |
| REFACTOR-023 | Closed | `DmaStreamComputeRuntime` comments mark explicit helper boundary; token/runtime tests cover staged commit, physical bounds, and no DSC pause/resume/reset/fence surface. |
| REFACTOR-024 | Closed | `AcceleratorRegisterAbi` comment/tests compare model `WritesRegister` with carrier `WritesRegister = false`. |
| REFACTOR-025 | Closed | `AcceleratorFenceModel` comment/test pairs model fence success with fail-closed `ACCEL_FENCE`. |
| REFACTOR-026 | Closed | L7 backend/commit/fault tests assert `CanPublishException = false` and `RequiresRetireExceptionPublication = false` for current L7 model paths. |
| REFACTOR-027 | Closed | Conflict manager optionality test proves absent manager does not imply global load/store ordering; queue placeholder is documented. |
| REFACTOR-028 | Closed | `StreamEngine.BurstIO` and `DMAController` comments describe current synchronous helper behavior and separate channel API. |

## Touched Code Areas

- `HybridCPU_ISE/Core/Pipeline/MicroOps/DmaStreamComputeMicroOp.cs`
- `HybridCPU_ISE/Core/Pipeline/MicroOps/SystemDeviceCommandMicroOp.cs`
- `HybridCPU_ISE/Core/Execution/DmaStreamCompute/DmaStreamComputeDescriptorParser.cs`
- `HybridCPU_ISE/Core/Execution/DmaStreamCompute/DmaStreamComputeRuntime.cs`
- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Tokens/AcceleratorRegisterAbi.cs`
- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Fences/AcceleratorFenceModel.cs`
- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Queues/AcceleratorCommandQueue.cs`
- `HybridCPU_ISE/Core/Execution/StreamEngine/StreamEngine.BurstIO.cs`
- `HybridCPU_ISE/Memory/DMA/DMAController.cs`

## Test Evidence

- Focused Stream/DMA/L7 suite: 103 passed, 0 failed.
- L7 carrier diagnostic regression recheck: 59 passed, 0 failed.
- Full project test result: 5928 passed, 2 skipped, 3 unrelated Phase12
  compat-freeze failures.
- `TestAssemblerConsoleApps` default and Phase15-style matrix profiles
  succeeded.

## Remaining Code Work

No approved Phase 2 code cleanup task remains open.

Do not start implementation of any future feature from Phase 3-6 without a
separate architecture approval.
