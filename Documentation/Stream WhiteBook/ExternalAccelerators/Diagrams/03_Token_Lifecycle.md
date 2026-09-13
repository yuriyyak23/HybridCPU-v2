# Token Lifecycle

This is the L7-SDC token lifecycle used by the current scoped runtime contour.
`SystemDeviceCommandMicroOp.Execute(...)` can reach it through guarded submit,
poll, wait, cancel, fence, and status commands. `DeviceComplete`,
`CommitPending`, progress/status observations, and wait/poll results do not
publish memory by themselves; register writeback is conditional on the runtime
ABI result and carrier destination register.

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

- `HybridCPU_ISE/NonRTL/Core/Execution/ExternalAccelerators/Tokens/AcceleratorToken.cs`
- `HybridCPU_ISE/NonRTL/Core/Execution/ExternalAccelerators/Tokens/AcceleratorTokenStore.cs`
- `HybridCPU_ISE/NonRTL/Core/Execution/ExternalAccelerators/Tokens/AcceleratorTokenStatusWord.cs`
- `HybridCPU_ISE/NonRTL/Core/Execution/ExternalAccelerators/Commit/AcceleratorCommitModel.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcTokenLifecycleTests.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcPollWaitCancelFenceTests.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcCommitTests.cs`
