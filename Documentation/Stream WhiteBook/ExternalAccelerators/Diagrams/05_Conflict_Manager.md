# Conflict Manager

This diagram describes an explicit conflict-manager model instance. It is not a
global CPU load/store hook and does not publish memory by itself.
It is absent/passive current evidence under Ex1 Phase05, not an installed global
conflict authority for executable overlap.

```mermaid
flowchart TD
    A["Accepted token and normalized footprint"] --> B["TryReserveOnSubmit"]
    B --> C{"Complete footprint and no active write/write overlap?"}
    C -- no --> X["Reject submit reservation"]
    C -- yes --> D["ActiveFootprintTable"]
    D --> E["NotifyCpuLoad / NotifyCpuStore"]
    D --> F["NotifyDmaStreamComputeAdmission"]
    D --> G["NotifySrfWarmWindow"]
    D --> H["NotifyAssistIngressWindow"]
    D --> I["NotifySerializingBoundary"]
    D --> J["NotifyVmDomainOrMappingTransition"]
    E --> K["Serialize / reject / fault decision"]
    F --> K
    G --> K
    H --> K
    I --> K
    J --> K
    D --> L["ValidateBeforeCommit"]
    L --> M{"Reservation still token-bound and no blocking overlap?"}
    M -- no --> N["Commit conflict reject/fault"]
    M -- yes --> O["Commit coordinator may continue validation"]
    O --> P["ReleaseTokenFootprint after guarded resolution"]
```

## Code anchors

- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Conflicts/ExternalAcceleratorConflictManager.cs`
- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Commit/AcceleratorCommitModel.cs`
- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Fences/AcceleratorFenceModel.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcConflictManagerTests.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcDmaStreamComputeConflictTests.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcSrfAssistConflictTests.cs`
