# Token Lifecycle And Model Register ABI

## Token states

The token lifecycle is:

`Created -> Validated -> Queued -> Running -> DeviceComplete -> CommitPending -> Committed`

Terminal and fault paths are `Faulted`, `Canceled`, `TimedOut`, and `Abandoned`.
`CommitPending` and `Committed` are coordinator-owned transitions; the public token
methods reject self-promotion through those states.

Code anchors:

- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Tokens/AcceleratorToken.cs`
- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Tokens/AcceleratorTokenStatusWord.cs`
- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Commit/AcceleratorCommitModel.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcTokenLifecycleTests.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcCommitTests.cs`

## Submit, poll, wait, cancel, fence

These are explicit model API behaviors, not current pipeline instruction
semantics.

- Submit creates a nonzero opaque handle only after descriptor, capability, guard, feature
  switch, and optional conflict checks pass.
- Poll is guarded observation.
- Wait observes completion, terminal state, or timeout policy without publishing staged
  writes.
- Cancel can resolve created/validated/queued/running tokens under policy, but cannot
  discard `DeviceComplete` or `CommitPending` obligations.
- Fence can observe, reject active tokens, cancel/fault active tokens by policy, or commit
  completed tokens only through `AcceleratorCommitCoordinator`.

Current `ACCEL_QUERY_CAPS`, `ACCEL_POLL`, `ACCEL_WAIT`, `ACCEL_CANCEL`, and
`ACCEL_FENCE` carriers do not write architectural registers or publish memory.
Future read-only query/poll or full submit/wait/fence/cancel execution requires
the Ex1 Phase10 L7 gate, ordering/conflict/cache/backend/fault semantics,
compiler/backend conformance, and Phase12 documentation migration.

Code anchors:

- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Tokens/AcceleratorTokenStore.cs`
- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Tokens/AcceleratorObservationControlPolicies.cs`
- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Fences/AcceleratorFenceModel.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcPollWaitCancelFenceTests.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcFaultPublicationTests.cs`

## Model Register ABI

`AcceleratorRegisterAbi` is a model result helper. It is not wired into
`SystemDeviceCommandMicroOp.Execute(...)`.

In the model API, accepted submit can produce a nonzero opaque handle result,
non-trapping rejection can produce zero, precise fault produces no write, status
helpers can produce a packed `AcceleratorTokenStatusWord`, and capability query
can produce a bounded metadata summary after guard-backed capability acceptance.

In current pipeline execution, `ACCEL_SUBMIT`, `ACCEL_POLL`, `ACCEL_WAIT`,
`ACCEL_CANCEL`, `ACCEL_FENCE`, and `ACCEL_QUERY_CAPS` do not write
architectural `rd`: every carrier has `WritesRegister = false`, empty
`WriteRegisters`, and direct `Execute(...)` throws fail-closed. Therefore
`AcceleratorRegisterAbiResult.WritesRegister` is a model result property only
and must not be used as a compiler/backend promise of pipeline writeback.

L7-SDC model faults are guarded observations/results. They are not current
retire exceptions; `AcceleratorCommitResult.RequiresRetireExceptionPublication`
is `false`.

Code anchors:

- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Tokens/AcceleratorRegisterAbi.cs`
- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Tokens/AcceleratorTokenStatusWord.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcRegisterAbiTests.cs`

WhiteBook / Ex1 anchors:

- `Documentation/Stream WhiteBook/ExternalAccelerators/06_Backend_Staging_Commit_And_Rollback.md`
- `Documentation/Stream WhiteBook/ExternalAccelerators/07_Memory_Conflict_Model.md`
- `Documentation/Refactoring/Phases Ex1/10_External_Accelerator_L7_SDC_Gate.md`
- `Documentation/Refactoring/Phases Ex1/12_Testing_Conformance_And_Documentation_Migration.md`
