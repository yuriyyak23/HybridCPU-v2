# L7-SDC Executive Summary

L7-SDC is the lane7 System Device Command carrier/model surface for external
accelerators. Its job is not to turn legacy custom accelerators into a pipeline
execution path. Current code provides `ACCEL_*` lane7 carriers, typed descriptor
sideband, owner/domain guarded admission, token lifecycle helpers, staged backend
results, register ABI writeback, and guarded commit through an explicit
coordinator.

The current pipeline-visible instruction behavior is scoped, not universal:
`SystemDeviceCommandMicroOp.Execute(...)` dispatches Phase 08 / Phase 08A
runtime commands for `ACCEL_QUERY_CAPS`, `ACCEL_SUBMIT`, `ACCEL_POLL`,
`ACCEL_WAIT`, `ACCEL_CANCEL`, `ACCEL_FENCE`, and `ACCEL_STATUS`.
`WritesRegister` follows `DestinationRegister != 0`; retire emits a writeback
only when `AcceleratorRegisterAbiResult.WritesRegister` is true and the carrier
has a destination register. Descriptorless submit, VMX guest compatibility
execution, legacy custom accelerator fallback, and expansion beyond the current
SDC contour remain fail-closed.

## Why lane7 SystemSingleton

The fixed topology gives lane7 two class aliases: `BranchControl` and
`SystemSingleton`. L7-SDC uses the system-device command side of that lane. The carrier
classes set `SlotClass.SystemSingleton` and hard-pin physical lane7; they do not use the
branch-control authority surface.

Code anchors:

- `HybridCPU_ISE/CloseToRTL/Core/Pipeline/Scheduling/SlotLegality/SlotClassDefinitions.cs`
- `HybridCPU_ISE/CloseToRTL/Core/Pipeline/MicroOps/Lane7Accelerator/SystemDeviceCommandMicroOp.cs`
- `HybridCPU_ISE/NonRTL/Core/Execution/ExternalAccelerators/ExternalAcceleratorRuntime.cs`
- `HybridCPU_ISE/CloseToRTL/Core/Frontend/Decode/VliwDecoderV4Bridge/VliwDecoderV4.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcHardPinnedPlacementTests.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcNoBranchControlAuthorityTests.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcPhase08ExecutableTests.cs`

## Difference from legacy custom accelerators

The retained custom accelerator registry and `MatMulAccelerator` are quarantined fixture
and metadata surfaces. `CustomAcceleratorMicroOp` and retained DMA seams fail closed.
The L7-SDC path is separate native ISA transport plus explicit guard and commit models.

Code anchors:

- `HybridCPU_ISE/NonRTL/Core/Diagnostics/InstructionRegistry.Accelerators.cs`
- `HybridCPU_ISE/NonRTL/Core/Diagnostics/InstructionRegistry.Types.cs`
- `HybridCPU_ISE/NonRTL/Core/Execution/BurstIO/AcceleratorRuntimeFailClosed.cs`
- `HybridCPU_ISE/CloseToRTL/Core/Execution/ExternalAccelerators/Backends/MatMulAccelerator.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcLegacyQuarantineTests.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcMatMulNoLegacyExecuteTests.cs`

## Difference from DmaStreamCompute

`DmaStreamCompute` is lane6 CPU-native stream compute with its own descriptor, guard,
token, and commit contour. L7-SDC is lane7 system-device command issue for external
accelerator control. A rejected L7-SDC command does not move into lane6 or stream/vector
helpers after native submit emission.

Code anchors:

- `HybridCPU_ISE/CloseToRTL/Core/Pipeline/MicroOps/Lane6DmaStream/DmaStreamComputeMicroOp.cs`
- `HybridCPU_ISE/NonRTL/Core/Execution/DmaStreamCompute/*`
- `HybridCPU_ISE/NonRTL/Core/Execution/ExternalAccelerators/*`
- `HybridCPU_ISE.Tests/tests/L7SdcDmaStreamComputeConflictTests.cs`
- `HybridCPU_ISE.Tests/CompilerTests/L7SdcCompilerPhase12Tests.cs`

## Architectural position

The current contour is descriptor-backed, guard-rooted, staged, and
retire/commit-owned. Ex1 Phase10 keeps expansion beyond the current scoped L7
commands under an ADR/conformance gate, Phase12 controls documentation
migration, and Phase13 is planning/dependency order only. The implementation
deliberately separates authority from evidence: descriptor identity, registry
metadata, token handle, telemetry, replay identity, and certificate identity can
explain or correlate decisions, but they do not replace guard checks or the
commit coordinator.

`AcceleratorRegisterAbi` and `AcceleratorFenceModel` are current runtime
surfaces used by the scoped executable commands. They are not evidence for a
universal external accelerator ABI, arbitrary backend execution, or compiler
lowering beyond the tested contour.

Current WhiteBook and Ex1 anchors:

- `Documentation/InstructionsRefactor/WhiteBook/00_README.md`
- `Documentation/InstructionsRefactor/WhiteBook/01_Authority_And_Phase_Map.md`
- `Documentation/InstructionsRefactor/WhiteBook/02_Runtime_Surface_Closure.md`
- `Documentation/InstructionsRefactor/WhiteBook/03_ABI_Decode_MicroOp_Retire_Contract.md`
- `Documentation/InstructionsRefactor/WhiteBook/04_Memory_Atomic_Fence_Model.md`
- `Documentation/InstructionsRefactor/WhiteBook/05_NonExecutable_And_Future_Gates.md`
- `Documentation/InstructionsRefactor/WhiteBook/06_Verification_And_Risk_Closure.md`
- `Documentation/Stream WhiteBook/ExternalAccelerators/00_README.md`
- `Documentation/Stream WhiteBook/ExternalAccelerators/05_Token_Lifecycle_And_Register_ABI.md`
- `Documentation/Stream WhiteBook/ExternalAccelerators/09_Compiler_Emission_Path.md`
- `Documentation/Refactoring/Phases Ex1/10_External_Accelerator_L7_SDC_Gate.md`
- `Documentation/Refactoring/Phases Ex1/12_Testing_Conformance_And_Documentation_Migration.md`
- `Documentation/Refactoring/Phases Ex1/13_Dependency_Graph_And_Execution_Order.md`
