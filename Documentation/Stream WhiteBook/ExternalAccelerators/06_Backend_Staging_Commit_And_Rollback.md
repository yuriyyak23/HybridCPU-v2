# Backend Staging, Commit, And Rollback

## Backend model

`NullExternalAcceleratorBackend` deterministically rejects or faults by explicit policy.
`FakeExternalAcceleratorBackend` and `FakeMatMulExternalAcceleratorBackend` are test-only
contours that queue guarded commands, read source ranges through a read-only portal, and
stage result bytes into backend-private buffers. Backend results cannot publish
architectural memory.
Backend result surfaces are runtime results for the scoped L7 contour; current
backend results do not publish exceptions through instruction retire
(`CanPublishException = false`). Fake/test backends are not a universal
production protocol and do not prove expansion beyond the tested L7 ISA
commands.

Code anchors:

- `HybridCPU_ISE/NonRTL/Core/Execution/ExternalAccelerators/Backends/ExternalAcceleratorBackends.cs`
- `HybridCPU_ISE/NonRTL/Core/Execution/ExternalAccelerators/Backends/FakeMatMulExternalAcceleratorBackend.cs`
- `HybridCPU_ISE/NonRTL/Core/Execution/ExternalAccelerators/Queues/AcceleratorCommandQueue.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcBackendTests.cs`

## Guarded source reads and staging

Source reads require token-bound descriptor identity, `Running` state, current guard
evidence, and device-execution guard acceptance. Staged writes require `Running` state,
current token guard, and exact destination footprint coverage. Staged data is copied and
held outside architectural memory until commit.

Code anchors:

- `HybridCPU_ISE/NonRTL/Core/Execution/ExternalAccelerators/Memory/AcceleratorMemoryModel.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcBackendTests.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcDirectWriteViolationTests.cs`

## Commit coordinator

`AcceleratorCommitCoordinator` owns architectural memory publication for scoped
L7 staged writes. It validates token state, descriptor identity, normalized footprint
binding, exact staged coverage, guard/epoch authority, direct-write violation
flags, and optional conflict-manager state. It promotes tokens through
coordinator-only `CommitPending` and `Committed` transitions.

`SystemDeviceCommandMicroOp.Execute(...)` can reach this coordinator only
through the scoped runtime fence/commit path. Submit still stages work and does
not directly publish memory. Commit rejection is a runtime result, not a current
retire exception:
`AcceleratorCommitResult.RequiresRetireExceptionPublication` is `false`.

Code anchors:

- `HybridCPU_ISE/NonRTL/Core/Execution/ExternalAccelerators/Commit/AcceleratorCommitModel.cs`
- `HybridCPU_ISE/NonRTL/Core/Execution/ExternalAccelerators/Tokens/AcceleratorToken.cs`
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

- `HybridCPU_ISE/NonRTL/Core/Execution/ExternalAccelerators/Commit/AcceleratorCommitModel.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcRollbackTests.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcSrfCacheInvalidationTests.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcTelemetryTests.cs`

WhiteBook / Ex1 anchors:

- `Documentation/InstructionsRefactor/WhiteBook/02_Runtime_Surface_Closure.md`
- `Documentation/InstructionsRefactor/WhiteBook/04_Memory_Atomic_Fence_Model.md`
- `Documentation/InstructionsRefactor/WhiteBook/05_NonExecutable_And_Future_Gates.md`
- `Documentation/Stream WhiteBook/ExternalAccelerators/05_Token_Lifecycle_And_Register_ABI.md`
- `Documentation/Stream WhiteBook/ExternalAccelerators/07_Memory_Conflict_Model.md`
- `Documentation/Refactoring/Phases Ex1/09_Cache_Prefetch_And_NonCoherent_Protocol.md`
- `Documentation/Refactoring/Phases Ex1/10_External_Accelerator_L7_SDC_Gate.md`
