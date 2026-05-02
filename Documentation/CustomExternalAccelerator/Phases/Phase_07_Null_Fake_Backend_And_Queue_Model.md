# Phase 07 - Null/fake backend and queue model

Status: closed.

Goal:

- Add external accelerator backend and queue interfaces.
- Provide null and fake test backends that cannot directly publish
  architectural memory.

ISE files likely touched:

- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Backends/*`
- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Queues/*`
- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Tokens/*`
- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Memory/*`
- `HybridCPU_ISE/Core/Pipeline/MicroOps/SystemDeviceCommandMicroOp.cs`

Compiler files likely touched:

- none required until compiler emission phase

New ISE interfaces:

- `IExternalAcceleratorBackend`
- `IAcceleratorCommandQueue`
- `IAcceleratorDevice`
- `IAcceleratorMemoryPortal`
- `IAcceleratorStagingBuffer`
- `IAcceleratorBackendClock`

New ISE implementations:

- `NullExternalAcceleratorBackend`
- `FakeExternalAcceleratorBackend`
- `DirectWriteViolationBackend`
- `AcceleratorCommandQueue`
- `AcceleratorQueuedCommand`
- `AcceleratorQueueAdmissionResult`

Methods to design:

- `IExternalAcceleratorBackend.TrySubmit(...)`
- `IExternalAcceleratorBackend.Tick(...)`
- `IExternalAcceleratorBackend.TryCancel(...)`
- `IAcceleratorCommandQueue.TryEnqueue(...)`
- `IAcceleratorCommandQueue.TryDequeueReady(...)`
- `IAcceleratorMemoryPortal.ReadSourceRanges(...)`
- `IAcceleratorStagingBuffer.StageWrite(...)`
- `IAcceleratorStagingBuffer.GetStagedWriteSet(...)`
- `DirectWriteViolationBackend.AttemptDirectWriteForTest(...)`

Backend rules:

- backend reads source ranges through a guarded memory portal
- backend writes only staged buffers
- queue admission requires descriptor, capability, owner/domain, mapping, and
  conflict acceptance
- backend `DeviceComplete` is not commit
- null backend rejects or faults deterministically
- fake backend is deterministic and test-only
- StreamEngine/SRF/VectorALU may be used only behind explicit fake backend test
  wrappers and never as authority

Tests to add:

- `L7SdcBackendTests`
- `L7SdcQueueAdmissionTests`
- `L7SdcDirectWriteViolationTests`

Test cases:

- null backend rejects submit without memory write
- queue full rejects and increments evidence counter
- device busy rejects and increments evidence counter
- fake backend stages writes only
- direct write violation is detected and faults/rejects
- backend completion leaves token `DeviceComplete`, not `Committed`

Must not break:

- memory subsystem invariants
- DmaStreamCompute backend helper tests
- StreamEngine/SRF helper behavior

Phase closure validation:

- Run focused tests:

```powershell
dotnet test --filter "L7SdcBackend|L7SdcQueueAdmission|L7SdcDirectWriteViolation"
```

- Run affected baseline filters:

```powershell
dotnet test --filter "DmaStreamComputeExecution|StreamRegisterFile|AssistRuntime"
```

- Run diagnostics console:

```powershell
dotnet run --project TestAssemblerConsoleApps/TestAssemblerConsoleApps.csproj -- matrix-smoke --iterations 200 --telemetry-logs minimal
```

- Compare with `Documentation/AsmAppTestResults.md`; direct-write violation
  tests must not alter normal memory diagnostics.

Definition of done:

- fake backend can produce staged data
- no backend can publish architectural memory directly

Rollback rule:

- switch default backend to null reject mode if fake backend violates staging
