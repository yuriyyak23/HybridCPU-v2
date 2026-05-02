# Compiler To ISE Transport

This diagram shows sideband preservation and carrier projection. It is not
production executable lowering; Phase11/Phase12/Phase13 keep production lowering
last-mile and gated by executable semantics plus conformance.

```mermaid
flowchart TD
    A["IrAcceleratorIntent"] --> B["CompilerAcceleratorCapabilityModel.Decide"]
    B --> C{"Lowering mode"}
    C -- "CPU/non-accelerator before submit" --> D["No ACCEL_SUBMIT emitted"]
    C -- "DmaStreamCompute before submit" --> E["Lane6 DmaStreamCompute contour"]
    C -- "Reject before submit" --> F["Compile-time reject"]
    C -- "EmitAcceleratorSubmit" --> G["IrAcceleratorCommand"]
    G --> H["CompileAcceleratorSubmit"]
    H --> I["Raw ACCEL_SUBMIT slot"]
    H --> J["InstructionSlotMetadata.AcceleratorCommandDescriptor"]
    I --> K["Bundle annotations"]
    J --> K
    K --> L["VliwDecoderV4 validates carrier and sideband"]
    L --> M["DecodedBundleTransportProjector"]
    M --> N["AcceleratorSubmitMicroOp"]
    N --> O["WritesRegister=false; Execute throws fail-closed"]
```

Emitted sideband does not make `ACCEL_SUBMIT` executable and does not create
architectural `rd` writeback in the current implementation.

## Code anchors

- `HybridCPU_Compiler/Core/IR/Model/IrAcceleratorModels.cs`
- `HybridCPU_Compiler/API/Threading/HybridCpuThreadCompilerContext.cs`
- `HybridCPU_Compiler/Core/IR/Bundling/HybridCpuBundleLowerer.cs`
- `HybridCPU_ISE/Core/Contracts/CompilerTransport/InstructionSlotMetadata.cs`
- `HybridCPU_ISE/Core/Decoder/VliwDecoderV4.cs`
- `HybridCPU_ISE/Core/Decoder/DecodedBundleTransportProjector.cs`
- `HybridCPU_ISE.Tests/CompilerTests/L7SdcCompilerPhase12Tests.cs`
