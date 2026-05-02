# Lane Placement And Carrier Flow

This is a carrier/projection flow. The terminal state is fail-closed direct
execution, not backend dispatch or architectural writeback.

```mermaid
flowchart TD
    A["Compiler explicit accelerator intent"] --> B["IrAcceleratorCommand"]
    B --> C["ACCEL_SUBMIT raw opcode"]
    B --> D["InstructionSlotMetadata.AcceleratorCommandDescriptor"]
    C --> E["VliwDecoderV4"]
    D --> E
    E --> F{"Carrier clean and slot 7?"}
    F -- no --> R["InvalidOpcodeException / fail closed"]
    F -- yes --> G{"Descriptor guard-backed?"}
    G -- no --> R
    G -- yes --> H["Decoded instruction with sideband"]
    H --> I["DecodedBundleTransportProjector"]
    I --> J["AcceleratorSubmitMicroOp"]
    J --> K["SetHardPinnedPlacement(SystemSingleton, 7)"]
    K --> L["Direct Execute throws fail-closed"]
```

## Code anchors

- `HybridCPU_Compiler/Core/IR/Model/IrAcceleratorModels.cs`
- `HybridCPU_Compiler/API/Threading/HybridCpuThreadCompilerContext.cs`
- `HybridCPU_Compiler/Core/IR/Bundling/HybridCpuBundleLowerer.cs`
- `HybridCPU_ISE/Core/Contracts/CompilerTransport/InstructionSlotMetadata.cs`
- `HybridCPU_ISE/Core/Decoder/VliwDecoderV4.cs`
- `HybridCPU_ISE/Core/Decoder/DecodedBundleTransportProjector.cs`
- `HybridCPU_ISE/Core/Pipeline/MicroOps/SystemDeviceCommandMicroOp.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcHardPinnedPlacementTests.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcInstructionTransportSidebandTests.cs`
