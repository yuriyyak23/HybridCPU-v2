# Compiler Emission Path

## Explicit accelerator intent

The compiler emits L7-SDC only from explicit accelerator intent. Direct raw emission of
L7-SDC system-device opcodes through general instruction APIs is rejected in favor of
`CompileAcceleratorSubmit(...)` with typed descriptor sideband.

Code anchors:

- `HybridCPU_Compiler/Core/IR/Model/IrAcceleratorModels.cs`
- `HybridCPU_Compiler/API/Threading/HybridCpuThreadCompilerContext.cs`
- `HybridCPU_ISE.Tests/CompilerTests/L7SdcCompilerPhase12Tests.cs`

## IR and sideband transport

`IrAcceleratorIntent` captures high-level operation intent and descriptor sideband.
`IrAcceleratorCommand` is the lowered command when the compiler strategy selects native
submit emission. `InstructionSlotMetadata.AcceleratorCommandDescriptor` carries the
descriptor through the VLIW bundle annotation path into ISE decode/projector.

Code anchors:

- `HybridCPU_Compiler/Core/IR/Model/IrAcceleratorModels.cs`
- `HybridCPU_Compiler/Core/IR/Model/IrInstruction.cs`
- `HybridCPU_Compiler/Core/IR/Model/IrSlotMetadata.cs`
- `HybridCPU_Compiler/Core/IR/Bundling/HybridCpuBundleLowerer.cs`
- `HybridCPU_ISE/NonRTL/Core/Contracts/CompilerTransport/InstructionSlotMetadata.cs`
- `HybridCPU_ISE/CloseToRTL/Core/Frontend/Decode/VliwDecoderV4Bridge/VliwDecoderV4.cs`

## Compile-time strategy boundary

Compiler capability strategy may select CPU/non-accelerator lowering or lane6
`DmaStreamCompute` before native `ACCEL_SUBMIT` exists. Once native `ACCEL_SUBMIT` is
emitted, runtime rejection remains an L7-SDC rejection; the compiler model forbids a
post-submit alternate execution promise.

Current compiler/backend conformance must preserve the scoped executable
boundary:

- emitting an `ACCEL_SUBMIT` carrier with descriptor sideband admits only the
  current guarded SDC1 runtime contour;
- the current projected carrier advertises `WritesRegister` only when a
  destination register is present;
- backend code must treat architectural `rd` writeback as conditional on the
  runtime ABI result plus carrier destination register;
- broad async completion, global fences, universal external accelerator command
  dispatch, and new result publication forms require future architecture
  approval and new conformance tests;
- IOMMU-backed L7 memory execution, coherent DMA/cache, successful partial
  completion, and fake/backend promotion beyond the current contour are also
  future-gated and cannot be inferred from sideband emission;
- production compiler/backend lowering remains last-mile work under Ex1
  Phase11/Phase13 for any behavior beyond the tested Phase 08 / Phase 08A
  commands.

Code anchors:

- `HybridCPU_Compiler/Core/IR/Model/IrAcceleratorModels.cs`
- `HybridCPU_Compiler/API/Threading/HybridCpuThreadCompilerContext.cs`
- `HybridCPU_ISE.Tests/CompilerTests/L7SdcCompilerPhase12Tests.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcInstructionTransportSidebandTests.cs`

WhiteBook / Ex1 anchors:

- `Documentation/InstructionsRefactor/WhiteBook/03_ABI_Decode_MicroOp_Retire_Contract.md`
- `Documentation/InstructionsRefactor/WhiteBook/05_NonExecutable_And_Future_Gates.md`
- `Documentation/InstructionsRefactor/WhiteBook/06_Verification_And_Risk_Closure.md`
- `Documentation/Stream WhiteBook/ExternalAccelerators/02_Topology_And_ISA_Placement.md`
- `Documentation/Stream WhiteBook/ExternalAccelerators/05_Token_Lifecycle_And_Register_ABI.md`
- `Documentation/Refactoring/Phases Ex1/11_Compiler_Backend_Lowering_Contract.md`
- `Documentation/Refactoring/Phases Ex1/13_Dependency_Graph_And_Execution_Order.md`
