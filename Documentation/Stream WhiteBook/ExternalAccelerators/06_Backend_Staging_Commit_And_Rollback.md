# Backend Staging, Commit, And Rollback

## Backend model

`NullExternalAcceleratorBackend` deterministically rejects or faults by explicit policy.
`FakeExternalAcceleratorBackend` and `FakeMatMulExternalAcceleratorBackend` are test-only
contours that queue guarded commands, read source ranges through a read-only portal, and
stage result bytes into backend-private buffers. Backend results cannot publish
architectural memory.
Backend result surfaces are model results; current backend results do not
publish exceptions through instruction retire (`CanPublishException = false`).
Fake/test backends are not production protocol and do not prove executable L7
ISA.

Code anchors:

- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Backends/ExternalAcceleratorBackends.cs`
- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Backends/FakeMatMulExternalAcceleratorBackend.cs`
- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Queues/AcceleratorCommandQueue.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcBackendTests.cs`

## Guarded source reads and staging

Source reads require token-bound descriptor identity, `Running` state, current guard
evidence, and device-execution guard acceptance. Staged writes require `Running` state,
current token guard, and exact destination footprint coverage. Staged data is copied and
held outside architectural memory until commit.

Code anchors:

- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Memory/AcceleratorMemoryModel.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcBackendTests.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcDirectWriteViolationTests.cs`

## Commit coordinator

`AcceleratorCommitCoordinator` owns explicit model-side architectural memory
publication. It validates token state, descriptor identity, normalized footprint
binding, exact staged coverage, guard/epoch authority, direct-write violation
flags, and optional conflict-manager state. It promotes tokens through
coordinator-only `CommitPending` and `Committed` transitions.

This coordinator is not `SystemDeviceCommandMicroOp.Execute(...)` and does not
turn `ACCEL_SUBMIT` or `ACCEL_FENCE` into executable pipeline operations.
Commit rejection is a model result, not a current retire exception:
`AcceleratorCommitResult.RequiresRetireExceptionPublication` is `false`.

Code anchors:

- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Commit/AcceleratorCommitModel.cs`
- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Tokens/AcceleratorToken.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcCommitTests.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcTokenHandleIsNotAuthorityTests.cs`

## Rollback and invalidation

Commit snapshots destination bytes before all-or-none publication. If a later write fails,
rollback restores snapshots when guard, token binding, descriptor identity, footprint, and
backup evidence are valid. Successful commit invalidates overlapping SRF/cache windows and
records telemetry.

The invalidation contour is explicit non-coherent model fan-out. It is not a
coherent DMA/cache hierarchy, global snooping, or current pipeline retire
publication. Coherent DMA/cache remains future-gated behind Phase09 and a
separate coherent-DMA ADR.

Code anchors:

- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Commit/AcceleratorCommitModel.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcRollbackTests.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcSrfCacheInvalidationTests.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcTelemetryTests.cs`

WhiteBook / Ex1 anchors:

- `Documentation/Stream WhiteBook/ExternalAccelerators/05_Token_Lifecycle_And_Register_ABI.md`
- `Documentation/Stream WhiteBook/ExternalAccelerators/07_Memory_Conflict_Model.md`
- `Documentation/Refactoring/Phases Ex1/09_Cache_Prefetch_And_NonCoherent_Protocol.md`
- `Documentation/Refactoring/Phases Ex1/10_External_Accelerator_L7_SDC_Gate.md`
