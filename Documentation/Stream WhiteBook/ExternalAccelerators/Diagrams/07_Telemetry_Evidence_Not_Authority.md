# Telemetry Evidence Not Authority

This diagram is the Ex1 non-inversion rule in L7 form: evidence can explain or
bind model decisions, but it cannot satisfy upstream executable gates.

```mermaid
flowchart LR
    subgraph AuthorityPlane["Authority plane"]
        A["GuardPlane evidence"]
        B["Owner/domain match"]
        C["Mapping/IOMMU epoch validation"]
        D["Descriptor/capability/submit guards"]
        E["Commit coordinator"]
        F["Exception publication guard"]
    end

    subgraph EvidencePlane["Evidence plane"]
        G["Telemetry snapshot"]
        H["Token handle"]
        I["Registry metadata"]
        J["Replay/certificate identity"]
        K["Descriptor identity hash"]
        L["Conflict evidence records"]
    end

    A --> B --> C --> D --> E
    D --> F
    G -. "observation only" .-> D
    H -. "lookup key only" .-> D
    I -. "metadata only" .-> D
    J -. "correlation only" .-> D
    K -. "binding/evidence" .-> E
    L -. "validation input" .-> E
```

## Code anchors

- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Auth/AcceleratorOwnerDomainGuard.cs`
- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Telemetry/AcceleratorTelemetry.cs`
- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Tokens/AcceleratorTokenHandle.cs`
- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Capabilities/AcceleratorCapabilityRegistry.cs`
- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Commit/AcceleratorCommitModel.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcEvidenceIsNotAuthorityTests.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcTelemetryTests.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcCapabilityIsNotAuthorityTests.cs`
