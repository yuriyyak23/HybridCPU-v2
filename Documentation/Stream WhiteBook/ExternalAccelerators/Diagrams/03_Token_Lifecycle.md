# Token Lifecycle

This is the explicit L7-SDC model token lifecycle. It is not reached by
`SystemDeviceCommandMicroOp.Execute(...)`, which remains fail-closed.
`DeviceComplete`, `CommitPending`, progress/status observations, and wait/poll
model results do not publish memory or architectural registers.

```mermaid
stateDiagram-v2
    [*] --> Created: guarded submit accepted
    Created --> Validated: queue admission validation
    Validated --> Queued: queue enqueue
    Queued --> Running: backend dequeue/tick
    Running --> DeviceComplete: backend staged result complete
    DeviceComplete --> CommitPending: commit coordinator only
    CommitPending --> Committed: commit coordinator only
    Created --> Canceled: cancel
    Validated --> Canceled: cancel
    Queued --> Canceled: cancel
    Running --> Canceled: cooperative cancel
    Running --> Faulted: fault policy / backend reject
    DeviceComplete --> Faulted: guarded fault path
    CommitPending --> Faulted: guarded commit failure
    Created --> TimedOut: wait timeout policy
    Validated --> TimedOut: wait timeout policy
    Queued --> TimedOut: wait timeout policy
    Running --> TimedOut: wait timeout policy
    DeviceComplete --> Abandoned: guarded abandon path
    Committed --> [*]
    Faulted --> [*]
    Canceled --> [*]
    TimedOut --> [*]
    Abandoned --> [*]
```

## Code anchors

- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Tokens/AcceleratorToken.cs`
- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Tokens/AcceleratorTokenStore.cs`
- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Tokens/AcceleratorTokenStatusWord.cs`
- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Commit/AcceleratorCommitModel.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcTokenLifecycleTests.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcPollWaitCancelFenceTests.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcCommitTests.cs`
