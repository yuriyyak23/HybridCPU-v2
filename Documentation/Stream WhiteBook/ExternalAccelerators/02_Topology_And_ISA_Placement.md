# Topology And ISA Placement

## Fixed W=8 topology

The live topology is fixed:

| Lane set | Slot class |
| --- | --- |
| 0-3 | `AluClass` |
| 4-5 | `LsuClass` |
| 6 | `DmaStreamClass` |
| 7 | `BranchControl` / `SystemSingleton` aliases |

Code anchors:

- `HybridCPU_ISE/Core/Pipeline/Scheduling/SlotClassDefinitions.cs`
- `HybridCPU_Compiler/Core/IR/Model/IrHazardEnums.cs`
- `HybridCPU_Compiler/Core/IR/Hazards/HybridCpuSlotModel.Analysis.cs`

## SystemSingleton vs BranchControl

`BranchControl` and `SystemSingleton` share the physical lane7 mask, but L7-SDC uses the
system-device command class. The carrier constructor calls
`SetHardPinnedPlacement(SlotClass.SystemSingleton, 7)`, and decoder/projector validation
rejects mismatched sideband placement.

Code anchors:

- `HybridCPU_ISE/Core/Pipeline/MicroOps/SystemDeviceCommandMicroOp.cs`
- `HybridCPU_ISE/Core/Decoder/VliwDecoderV4.cs`
- `HybridCPU_ISE/Core/Decoder/DecodedBundleTransportProjector.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcHardPinnedPlacementTests.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcNoBranchControlAuthorityTests.cs`

## ACCEL_* carrier rules

The native command surface is:

- `ACCEL_QUERY_CAPS`: system command, CSR ordered.
- `ACCEL_SUBMIT`: system command, memory ordered, requires typed descriptor sideband.
- `ACCEL_POLL`: system command, CSR ordered.
- `ACCEL_WAIT`: system command, full serial.
- `ACCEL_CANCEL`: system command, full serial.
- `ACCEL_FENCE`: system command, full serial.

Current carriers are placement/decode carriers only:

- `SystemDeviceCommandMicroOp.WritesRegister = false`;
- `WriteRegisters` is empty for every `ACCEL_*` subclass;
- direct `Execute(...)` throws fail-closed;
- model APIs are explicit runtime-side helpers and do not imply backend
  execution, staged write publication, architectural commit, `rd` writeback, or
  fallback routing.
- Ex1 Phase10 keeps all executable `ACCEL_*` behavior future-gated. Query,
  poll, wait, cancel, fence, submit, and backend-dispatch semantics are model
  surfaces only until a later ADR, implementation, tests, and Phase12 migration
  close the relevant gate.

Raw carrier validation requires lane7 placement and clean reserved/retired policy fields,
zero raw VT hint, valid packed register tuple, and zero raw `Src2` pointer fields. The
descriptor ABI travels as sideband, not raw VLIW payload fields.

Code anchors:

- `HybridCPU_ISE/Arch/OpcodeInfo.Registry.Data.System.cs`
- `HybridCPU_ISE/Arch/InstructionClassifier.cs`
- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Descriptors/AcceleratorDescriptorParser.cs`
- `HybridCPU_ISE/Core/Contracts/CompilerTransport/InstructionSlotMetadata.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcNativeCarrierValidationTests.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcInstructionTransportSidebandTests.cs`

## Bundle legality boundary

Compiler annotations and ISE slot metadata carry the sideband. Bundle legality still
comes from typed slot class, hard-pinned placement, and decoder/projector checks. Class
membership alone is insufficient for L7-SDC because the command carrier must be exactly
lane7 `SystemSingleton`.

Typed placement and clean carrier evidence do not prove executable L7 ISA.
Compiler sideband transport and carrier projection are downstream evidence under
Ex1 Phase13 and must not satisfy upstream execution gates.

Code anchors:

- `HybridCPU_Compiler/Core/IR/Bundling/HybridCpuBundleLowerer.cs`
- `HybridCPU_Compiler/API/Threading/HybridCpuThreadCompilerContext.cs`
- `HybridCPU_ISE/Core/Decoder/VliwDecoderV4.cs`
- `HybridCPU_ISE/Core/Decoder/DecodedBundleTransportProjector.cs`
