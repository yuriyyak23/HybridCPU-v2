# Phase 06 - AcceleratorToken lifecycle and register ABI

Status: closed.

Goal:

- Introduce `AcceleratorToken` state, opaque token handle ABI, and packed status
  word ABI without enabling backend architectural writes.

ISE files likely touched:

- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Tokens/*`
- `HybridCPU_ISE/Core/Pipeline/MicroOps/SystemDeviceCommandMicroOp.cs`
- `HybridCPU_ISE/Core/Pipeline/Core/CPU_Core.*.cs`
- `HybridCPU_ISE/Core/Decoder/DecodedBundleTransportProjector.cs`

Compiler files likely touched:

- compiler IR result modeling for token handle/status registers
- hazard semantics for system command register writes

New ISE types:

- `AcceleratorToken`
- `AcceleratorTokenHandle`
- `AcceleratorTokenState`
- `AcceleratorTokenFaultCode`
- `AcceleratorTokenStatusWord`
- `AcceleratorTokenStore`
- `AcceleratorTokenTransition`
- `AcceleratorTokenCommitRequest`

State machine:

```text
Created
Validated
Queued
Running
DeviceComplete
CommitPending
Committed
Faulted
Canceled
TimedOut
Abandoned
```

Methods to design:

- `AcceleratorTokenStore.Create(...)`
- `AcceleratorTokenStore.TryLookup(AcceleratorTokenHandle handle, ...)`
- `AcceleratorToken.MarkValidated(...)`
- `AcceleratorToken.MarkQueued(...)`
- `AcceleratorToken.MarkRunning(...)`
- `AcceleratorToken.MarkDeviceComplete(...)`
- `AcceleratorToken.MarkCommitPending(...)`
- `AcceleratorToken.MarkCommitted(...)`
- `AcceleratorToken.MarkFaulted(...)`
- `AcceleratorToken.MarkCanceled(...)`
- `AcceleratorToken.MarkTimedOut(...)`
- `AcceleratorToken.MarkAbandoned(...)`
- `AcceleratorTokenStatusWord.Pack(...)`
- `AcceleratorTokenStatusWord.Unpack(...)`

Register ABI:

- `ACCEL_SUBMIT rd`: nonzero opaque token handle on accepted submit
- `ACCEL_SUBMIT rd`: zero on non-trapping rejection
- `ACCEL_SUBMIT` precise fault: `rd` is not architecturally written
- `ACCEL_POLL rd`: packed token state/status/fault code
- `ACCEL_WAIT rd`: final packed status
- `ACCEL_CANCEL rd`: final packed cancel/status
- `ACCEL_QUERY_CAPS rd`: packed bounded query summary or status
- `ACCEL_FENCE rd`: optional packed fence status

Token handle rules:

- zero is invalid
- nonzero is an opaque lookup key
- handle identity is not authority
- handle arithmetic is undefined
- every token operation revalidates owner/domain and mapping epoch

Tests to add:

- `L7SdcTokenLifecycleTests`
- `L7SdcRegisterAbiTests`
- `L7SdcTokenHandleIsNotAuthorityTests`

Test cases:

- legal transitions only
- token handle zero is invalid
- submit accepted writes nonzero handle
- submit rejected writes zero or faults before write
- poll/wait/cancel status word encodes state and fault
- token alone cannot commit
- owner/domain drift blocks token lookup side effects
- abandoned token never commits

Must not break:

- existing register writeback/retire semantics
- system instruction serialization behavior
- DmaStreamCompute token tests

Phase closure validation:

- Run focused tests:

```powershell
dotnet test --filter "L7SdcTokenLifecycle|L7SdcRegisterAbi|L7SdcTokenHandleIsNotAuthority"
```

- Run affected baseline filters:

```powershell
dotnet test --filter "DmaStreamComputeCommitToken|Phase09WriteRegisterPublicationContract|Phase12"
```

- Run diagnostics console:

```powershell
dotnet run --project TestAssemblerConsoleApps/TestAssemblerConsoleApps.csproj -- matrix-smoke --iterations 200 --telemetry-logs minimal
```

- Compare with `Documentation/AsmAppTestResults.md`; register writeback changes
  must not alter non-L7-SDC diagnostic workloads.

Definition of done:

- token lifecycle and GPR ABI are testable without backend execution
- no token handle can authorize commit by itself

Rollback rule:

- disable token creation and return guarded submit rejection if transition or
  register ABI tests fail
