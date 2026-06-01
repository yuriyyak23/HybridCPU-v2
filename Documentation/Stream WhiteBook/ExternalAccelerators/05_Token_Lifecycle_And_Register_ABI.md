# Token Lifecycle And Model Register ABI

## Token states

The token lifecycle is:

`Created -> Validated -> Queued -> Running -> DeviceComplete -> CommitPending -> Committed`

Terminal and fault paths are `Faulted`, `Canceled`, `TimedOut`, and `Abandoned`.
`CommitPending` and `Committed` are coordinator-owned transitions; the public token
methods reject self-promotion through those states.

Code anchors:

- `HybridCPU_ISE/NonRTL/Core/Execution/ExternalAccelerators/Tokens/AcceleratorToken.cs`
- `HybridCPU_ISE/NonRTL/Core/Execution/ExternalAccelerators/Tokens/AcceleratorTokenStatusWord.cs`
- `HybridCPU_ISE/NonRTL/Core/Execution/ExternalAccelerators/Commit/AcceleratorCommitModel.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcTokenLifecycleTests.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcCommitTests.cs`

## Submit, poll, wait, cancel, fence

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
`ACCEL_FENCE` carriers execute through `ExternalAcceleratorRuntime` inside the
current scoped contour. They can produce register ABI results when the command
semantics and carrier destination register permit it. Staged memory publishes
only through the guarded commit coordinator. Expansion beyond these command
semantics requires the Ex1 Phase10 L7 gate, ordering/conflict/cache/backend/fault
semantics, compiler/backend conformance, and Phase12 documentation migration.

Code anchors:

- `HybridCPU_ISE/NonRTL/Core/Execution/ExternalAccelerators/Tokens/AcceleratorTokenStore.cs`
- `HybridCPU_ISE/NonRTL/Core/Execution/ExternalAccelerators/Tokens/AcceleratorObservationControlPolicies.cs`
- `HybridCPU_ISE/NonRTL/Core/Execution/ExternalAccelerators/Fences/AcceleratorFenceModel.cs`
- `HybridCPU_ISE/NonRTL/Core/Execution/ExternalAccelerators/ExternalAcceleratorRuntime.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcPollWaitCancelFenceTests.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcPhase08ExecutableTests.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcFaultPublicationTests.cs`

## Model Register ABI

`AcceleratorRegisterAbi` is the runtime result helper used by the current scoped
`SystemDeviceCommandMicroOp.Execute(...)` path.

In the model API, accepted submit can produce a nonzero opaque handle result,
non-trapping rejection can produce zero, precise fault produces no write, status
helpers can produce a packed `AcceleratorTokenStatusWord`, and capability query
can produce a bounded metadata summary after guard-backed capability acceptance.

In current pipeline execution, carriers advertise `WritesRegister` only when a
destination register is present. Retire writes architectural `rd` only when
`AcceleratorRegisterAbiResult.WritesRegister` is true and the carrier has that
destination. A non-writing runtime result, missing destination register, faulted
command, descriptorless submit, or compatibility-denied path must not publish an
architectural register write.

L7-SDC model faults are guarded observations/results. They are not current
retire exceptions; `AcceleratorCommitResult.RequiresRetireExceptionPublication`
is `false`.

Code anchors:

- `HybridCPU_ISE/NonRTL/Core/Execution/ExternalAccelerators/Tokens/AcceleratorRegisterAbi.cs`
- `HybridCPU_ISE/NonRTL/Core/Execution/ExternalAccelerators/Tokens/AcceleratorTokenStatusWord.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcRegisterAbiTests.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcPhase08ExecutableTests.cs`

WhiteBook / Ex1 anchors:

- `Documentation/InstructionsRefactor/WhiteBook/02_Runtime_Surface_Closure.md`
- `Documentation/InstructionsRefactor/WhiteBook/03_ABI_Decode_MicroOp_Retire_Contract.md`
- `Documentation/InstructionsRefactor/WhiteBook/06_Verification_And_Risk_Closure.md`
- `Documentation/Stream WhiteBook/ExternalAccelerators/06_Backend_Staging_Commit_And_Rollback.md`
- `Documentation/Stream WhiteBook/ExternalAccelerators/07_Memory_Conflict_Model.md`
- `Documentation/Refactoring/Phases Ex1/10_External_Accelerator_L7_SDC_Gate.md`
- `Documentation/Refactoring/Phases Ex1/12_Testing_Conformance_And_Documentation_Migration.md`
