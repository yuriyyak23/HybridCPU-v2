# Descriptor Authority Admission

This diagram is the explicit model admission contour. Token creation here is not
`ACCEL_SUBMIT` instruction execution and does not authorize production backend
dispatch, architectural `rd` writeback, or memory publication.

```mermaid
flowchart TD
    A["Descriptor bytes / sideband"] --> B["Read structural owner binding"]
    B --> C{"Guard evidence source is GuardPlane?"}
    C -- no --> X["Reject: evidence-plane input"]
    C -- yes --> D["EnsureBeforeDescriptorAcceptance"]
    D --> E{"Owner/domain matches?"}
    E -- no --> X
    E -- yes --> F["Parse guarded descriptor"]
    F --> G{"Reserved fields, shape, ranges, hashes valid?"}
    G -- no --> X
    G -- yes --> H["AcceleratorCommandDescriptor"]
    H --> I["Capability registry query"]
    I --> J{"Capability accepted with guard?"}
    J -- no --> X
    J -- yes --> K["TokenStore.Create"]
    K --> L{"Submit guard and optional conflict pass?"}
    L -- no --> X
    L -- yes --> M["Accepted token handle"]
```

## Code anchors

- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Descriptors/AcceleratorDescriptorParser.cs`
- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Auth/AcceleratorOwnerDomainGuard.cs`
- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Capabilities/AcceleratorCapabilityRegistry.cs`
- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Tokens/AcceleratorTokenStore.cs`
- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Conflicts/ExternalAcceleratorConflictManager.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcDescriptorParserTests.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcCapabilityIsNotAuthorityTests.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcOwnerDomainGuardTests.cs`
