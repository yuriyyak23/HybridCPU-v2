# Lane Placement And Carrier Flow

This is a carrier/projection flow for the current scoped L7 runtime contour.
Dirty carriers and missing descriptors fail closed; accepted carriers dispatch
only the implemented command surface, with register writeback and commit still
owned by runtime/retire rules.

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
    K --> L["Execute dispatches scoped ExternalAcceleratorRuntime command"]
    L --> M{"Runtime accepts command?"}
    M -- no --> R
    M -- yes --> N["Retire-owned ABI writeback / guarded commit as applicable"]
```

## Code anchors

- `HybridCPU_Compiler/Core/IR/Model/IrAcceleratorModels.cs`
- `HybridCPU_Compiler/API/Threading/HybridCpuThreadCompilerContext.cs`
- `HybridCPU_Compiler/Core/IR/Bundling/HybridCpuBundleLowerer.cs`
- `HybridCPU_ISE/NonRTL/Core/Contracts/CompilerTransport/InstructionSlotMetadata.cs`
- `HybridCPU_ISE/CloseToRTL/Core/Frontend/Decode/VliwDecoderV4Bridge/VliwDecoderV4.cs`
- `HybridCPU_ISE/NonRTL/Core/Decoder/DecodedBundleTransportProjector.cs`
- `HybridCPU_ISE/CloseToRTL/Core/Pipeline/MicroOps/Lane7Accelerator/SystemDeviceCommandMicroOp.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcHardPinnedPlacementTests.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcInstructionTransportSidebandTests.cs`
