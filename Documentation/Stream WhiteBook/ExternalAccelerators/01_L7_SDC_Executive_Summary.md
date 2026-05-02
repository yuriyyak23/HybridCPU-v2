# L7-SDC Executive Summary

L7-SDC is the lane7 System Device Command carrier/model surface for external
accelerators. Its job is not to turn legacy custom accelerators into a pipeline
execution path. Current code provides `ACCEL_*` lane7 carriers, typed descriptor
sideband, owner/domain guarded model admission, token lifecycle helpers, staged
backend model results, and commit through an explicit coordinator.

The current pipeline-visible instruction behavior is fail-closed:
`SystemDeviceCommandMicroOp.Execute(...)` throws for every `ACCEL_*` carrier,
`WritesRegister` is `false`, and no architectural `rd` writeback, direct backend
execution, staged write publication, architectural commit, or fallback routing is
implemented through `Execute(...)`.

## Why lane7 SystemSingleton

The fixed topology gives lane7 two class aliases: `BranchControl` and
`SystemSingleton`. L7-SDC uses the system-device command side of that lane. The carrier
classes set `SlotClass.SystemSingleton` and hard-pin physical lane7; they do not use the
branch-control authority surface.

Code anchors:

- `HybridCPU_ISE/Core/Pipeline/Scheduling/SlotClassDefinitions.cs`
- `HybridCPU_ISE/Core/Pipeline/MicroOps/SystemDeviceCommandMicroOp.cs`
- `HybridCPU_ISE/Core/Decoder/VliwDecoderV4.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcHardPinnedPlacementTests.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcNoBranchControlAuthorityTests.cs`

## Difference from legacy custom accelerators

The retained custom accelerator registry and `MatMulAccelerator` are quarantined fixture
and metadata surfaces. `CustomAcceleratorMicroOp` and retained DMA seams fail closed.
The L7-SDC path is separate native ISA transport plus explicit guard and commit models.

Code anchors:

- `HybridCPU_ISE/Core/Diagnostics/InstructionRegistry.Accelerators.cs`
- `HybridCPU_ISE/Core/Diagnostics/InstructionRegistry.Types.cs`
- `HybridCPU_ISE/Core/Execution/BurstIO/AcceleratorRuntimeFailClosed.cs`
- `HybridCPU_ISE/Core/Accelerators/MatMulAccelerator.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcLegacyQuarantineTests.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcMatMulNoLegacyExecuteTests.cs`

## Difference from DmaStreamCompute

`DmaStreamCompute` is lane6 CPU-native stream compute with its own descriptor, guard,
token, and commit contour. L7-SDC is lane7 system-device command issue for external
accelerator control. A rejected L7-SDC command does not move into lane6 or stream/vector
helpers after native submit emission.

Code anchors:

- `HybridCPU_ISE/Core/Pipeline/MicroOps/DmaStreamComputeMicroOp.cs`
- `HybridCPU_ISE/Core/Execution/DmaStreamCompute/*`
- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/*`
- `HybridCPU_ISE.Tests/tests/L7SdcDmaStreamComputeConflictTests.cs`
- `HybridCPU_ISE.Tests/CompilerTests/L7SdcCompilerPhase12Tests.cs`

## Architectural position

The current contour is a fail-closed carrier plus descriptor-backed,
guard-rooted, staging-and-commit model. Ex1 Phase10 keeps executable L7 under a
future ADR gate, Phase12 controls any documentation migration, and Phase13 is
planning/dependency order only. The model deliberately separates authority from
evidence: descriptor identity, registry metadata, token handle, telemetry,
replay identity, and certificate identity can explain or correlate decisions,
but they do not replace guard checks or the commit coordinator.

`AcceleratorRegisterAbi` and `AcceleratorFenceModel` describe model API results.
They are not executable `ACCEL_*` pipeline semantics until a future architecture
decision explicitly wires them into `SystemDeviceCommandMicroOp.Execute(...)`.

Current WhiteBook and Ex1 anchors:

- `Documentation/Stream WhiteBook/ExternalAccelerators/00_README.md`
- `Documentation/Stream WhiteBook/ExternalAccelerators/05_Token_Lifecycle_And_Register_ABI.md`
- `Documentation/Stream WhiteBook/ExternalAccelerators/09_Compiler_Emission_Path.md`
- `Documentation/Refactoring/Phases Ex1/10_External_Accelerator_L7_SDC_Gate.md`
- `Documentation/Refactoring/Phases Ex1/12_Testing_Conformance_And_Documentation_Migration.md`
- `Documentation/Refactoring/Phases Ex1/13_Dependency_Graph_And_Execution_Order.md`
