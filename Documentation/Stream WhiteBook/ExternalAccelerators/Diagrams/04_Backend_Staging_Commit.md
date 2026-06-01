# Backend Staging Commit

This is the staged backend and commit contour behind the current scoped L7
runtime. `ACCEL_SUBMIT` admits and stages work; it does not directly publish
memory. Rejected commits do not publish current retire exceptions. Fake/test
backend completion, staged data, commit coordination, rollback, and SRF/cache
invalidation prove only the tested contour; they are not a universal production
protocol, pipeline retire publication model, or global coherency model.

```mermaid
flowchart TD
    A["Accepted token"] --> B["Queue admission"]
    B --> C["Fake backend Tick"]
    C --> D["Mark Running"]
    D --> E["Read source ranges through guarded portal"]
    E --> F["Stage destination bytes in staging buffer"]
    F --> G["Mark DeviceComplete"]
    G --> H{"Commit requested through coordinator?"}
    H -- no --> I["Staged data remains non-architectural"]
    H -- yes --> J["Validate guard, token, descriptor identity, footprint, conflicts"]
    J --> K{"Preconditions pass?"}
    K -- no --> L["Fault/reject and release if terminal"]
    K -- yes --> M["Promote CommitPending"]
    M --> N["Snapshot destinations"]
    N --> O["Write all staged bytes"]
    O --> P{"Write failure?"}
    P -- yes --> Q["Rollback snapshots and fault/reject"]
    P -- no --> S["Validate and invalidate SRF/cache windows"]
    S --> T{"Invalidation failure?"}
    T -- yes --> Q
    T -- no --> R["Promote Committed"]
    R --> U["Conflict manager commit notification and release"]
```

## Code anchors

- `HybridCPU_ISE/NonRTL/Core/Execution/ExternalAccelerators/Backends/ExternalAcceleratorBackends.cs`
- `HybridCPU_ISE/NonRTL/Core/Execution/ExternalAccelerators/Memory/AcceleratorMemoryModel.cs`
- `HybridCPU_ISE/NonRTL/Core/Execution/ExternalAccelerators/Commit/AcceleratorCommitModel.cs`
- `HybridCPU_ISE/NonRTL/Core/Execution/ExternalAccelerators/Conflicts/ExternalAcceleratorConflictManager.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcBackendTests.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcCommitTests.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcRollbackTests.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcSrfCacheInvalidationTests.cs`
