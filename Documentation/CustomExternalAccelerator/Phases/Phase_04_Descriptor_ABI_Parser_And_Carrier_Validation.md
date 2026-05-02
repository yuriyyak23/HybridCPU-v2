# Phase 04 - Descriptor ABI parser and carrier validation

Status: closed.

Goal:

- Add the typed `AcceleratorCommandDescriptor` sideband ABI.
- Add raw native carrier cleanliness validation equivalent in rigor to the
  existing DmaStreamCompute carrier discipline.

ISE files likely touched:

- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Descriptors/*`
- `HybridCPU_ISE/Core/Pipeline/MicroOps/InstructionIR.cs`
- `HybridCPU_ISE/Core/Contracts/CompilerTransport/InstructionSlotMetadata.cs`
- `HybridCPU_ISE/Core/Decoder/VliwDecoderV4.cs`
- `HybridCPU_ISE/Core/Decoder/DecodedBundleTransportProjector.cs`
- `HybridCPU_ISE/Arch/IsaV4Surface.cs`
- `HybridCPU_ISE/Core/Pipeline/MicroOps/SystemDeviceCommandMicroOp.cs`

Compiler files likely touched:

- `HybridCPU_Compiler/Core/IR/Model/IrSlotMetadata.cs`
- `HybridCPU_Compiler/Core/IR/Model/IrInstruction.cs`
- `HybridCPU_Compiler/Core/IR/Construction/HybridCpuIrBuilder.cs`
- `HybridCPU_Compiler/API/Threading/HybridCpuThreadCompilerContext.cs`

New ISE types:

- `AcceleratorCommandDescriptor`
- `AcceleratorDescriptorHeader`
- `AcceleratorDescriptorParser`
- `AcceleratorDescriptorFault`
- `AcceleratorDescriptorReference`
- `AcceleratorDescriptorIdentity`
- `AcceleratorNormalizedFootprint`
- `AcceleratorMemoryRange`
- `AcceleratorScratchRequirement`
- `AcceleratorAlignmentRequirement`
- `AcceleratorPartialCompletionPolicy`
- `AcceleratorCarrierValidationResult`

Methods to design:

- `AcceleratorDescriptorParser.Parse(...)`
- `AcceleratorDescriptorParser.ValidateNativeCarrier(...)`
- `AcceleratorDescriptorParser.TryReadHeader(...)`
- `AcceleratorDescriptorParser.ValidateReservedZero(...)`
- `AcceleratorDescriptorParser.ValidateDescriptorSize(...)`
- `AcceleratorDescriptorParser.ValidateCapabilityShape(...)`
- `AcceleratorDescriptorParser.NormalizeFootprints(...)`
- `AcceleratorDescriptorParser.ValidateDescriptorIdentityHash(...)`
- `AcceleratorDescriptorParser.ValidateNormalizedFootprintHash(...)`
- `InstructionIR.SetAcceleratorCommandDescriptor(...)`
- `InstructionSlotMetadata.WithAcceleratorDescriptor(...)`

Raw carrier rules:

- opcode must be canonical native `ACCEL_*`
- slot index must be 7
- reserved bits must be zero
- raw VT hint must be zero in v1; nonzero rejects
- no raw pointer field is accepted as authority
- no raw custom opcode registry identity is accepted
- `ACCEL_SUBMIT` requires descriptor sideband
- sideband reference must match descriptor identity
- clean raw carrier with missing sideband rejects
- dirty raw carrier with sideband rejects

Descriptor required fields:

- `magic`
- `abi_version`
- `descriptor_size`
- `accelerator_class`
- `accelerator_id`
- `operation_kind`
- `datatype`
- `shape`
- `source_ranges`
- `destination_ranges`
- `scratch_requirements`
- `alignment_requirements`
- `partial_completion_policy`
- `owner_binding`
- `domain_tag`
- `capability_version`
- `descriptor_identity_hash`
- `normalized_footprint_hash`
- reserved fields zero

Tests to add:

- `L7SdcDescriptorParserTests`
- `L7SdcNativeCarrierValidationTests`
- `L7SdcInstructionTransportSidebandTests`

Test cases:

- missing sideband rejects
- dirty raw reserved bits reject
- raw VT hint rejects
- wrong lane rejects
- wrong class rejects
- descriptor reference mismatch rejects
- unknown ABI rejects
- unknown accelerator id rejects
- unsupported operation/shape/datatype rejects
- dirty descriptor reserved fields reject
- footprint hash mismatch rejects
- descriptor identity mismatch rejects

Must not break:

- `VLIW_Instruction` size
- 8-slot bundle shape
- DmaStreamCompute sideband transport
- compiler transport for existing slot metadata

Phase closure validation:

- Run focused tests:

```powershell
dotnet test --filter "L7SdcDescriptorParser|L7SdcNativeCarrierValidation|L7SdcInstructionTransportSideband"
```

- Run affected baseline filters:

```powershell
dotnet test --filter "DmaStreamComputeDescriptor|DmaStreamComputeIsaEncoding|CompilerV5ContractAlignment"
```

- Run diagnostics console:

```powershell
dotnet run --project TestAssemblerConsoleApps/TestAssemblerConsoleApps.csproj -- matrix-smoke --iterations 200 --telemetry-logs minimal
```

- Compare with `Documentation/AsmAppTestResults.md`; descriptor transport must
  not alter non-accelerator diagnostic programs.

Definition of done:

- descriptor parser accepts only guarded, clean, sideband-backed native carriers
- tests fail for every raw-carrier shortcut

Rollback rule:

- keep all L7-SDC opcodes fail-closed if descriptor parser coverage is
  incomplete
