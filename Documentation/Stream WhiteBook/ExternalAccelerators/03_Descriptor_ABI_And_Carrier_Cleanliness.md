# Descriptor ABI And Carrier Cleanliness

## Typed sideband descriptor

L7-SDC command descriptors are typed sideband data represented by
`AcceleratorCommandDescriptor`. The descriptor includes ABI/header fields, operation and
shape metadata, owner binding, guard decision, descriptor identity, normalized footprint,
source/destination/scratch ranges, and partial completion policy.

Code anchors:

- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Descriptors/AcceleratorCommandDescriptor.cs`
- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Descriptors/AcceleratorDescriptorParser.cs`
- `HybridCPU_ISE/Core/Contracts/CompilerTransport/InstructionSlotMetadata.cs`
- `HybridCPU_Compiler/Core/IR/Model/IrSlotMetadata.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcDescriptorParserTests.cs`

## SDC1 Descriptor ABI

The implemented SDC1 descriptor ABI is exactly:

- magic: `0x31434453`, `"SDC1"` as a little-endian scalar;
- ABI version: `1`;
- header size: `128`;
- range entry size: `16`;
- maximum range count per table: `16`;
- supported accelerator class: `Matrix`;
- supported accelerator id: `ReferenceMatMul`;
- supported operation: `MatMul`;
- supported datatypes: `Float32`, `Float64`, `Int32`;
- supported shape: `Matrix2D` with rank `2`;
- partial completion policy: `AllOrNone`;
- alignment: non-zero power-of-two `AlignmentBytes`;
- owner binding fields: `OwnerVirtualThreadId`, `OwnerContextId`,
  `OwnerCoreId`, `OwnerPodId`, `DomainTag`;
- source and destination ranges are required;
- scratch ranges are required only when `ScratchRequiredBytes` is non-zero;
- descriptor identity and normalized footprint hashes must match the parsed
  payload when supplied through the sideband reference.

SDC1 does not define a universal external accelerator command protocol. It is a
guarded descriptor ABI for the current L7-SDC model surfaces.

Descriptor acceptance is model-only under the current contract. It does not make
`ACCEL_*` executable, does not publish architectural `rd`, does not dispatch a
production backend, and does not authorize compiler/backend production lowering.

## Parse, accept, reject contour

The parser can structurally read owner fields, but full acceptance requires a guard-plane
owner/domain decision. Calling `Parse(...)` without guard evidence returns an
owner/domain fault after structural read; guarded overloads validate owner binding,
guard source, epochs where applicable, reserved fields, shape, range layout, identity
hash, and normalized footprint hash.

Code anchors:

- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Auth/AcceleratorOwnerDomainGuard.cs`
- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Descriptors/AcceleratorDescriptorParser.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcOwnerDomainGuardTests.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcMappingEpochGuardTests.cs`

## Raw carrier cleanliness

The raw native VLIW carrier is only a clean command envelope. Reserved bits, retired
policy gap bits, raw VT hints, raw `Src2`, descriptor reference identities, registry
identities, and telemetry correlation data are not descriptor ABI and are not authority.
Dirty carriers fail before projector materialization.

Carrier cleanliness does not make the command executable: direct
`SystemDeviceCommandMicroOp.Execute(...)` remains fail-closed, and `ACCEL_*`
carriers do not write architectural `rd`.

Code anchors:

- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Descriptors/AcceleratorDescriptorParser.cs`
- `HybridCPU_ISE/Core/Decoder/VliwDecoderV4.cs`
- `HybridCPU_ISE/Core/Decoder/DecodedBundleTransportProjector.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcNativeCarrierValidationTests.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcInstructionTransportSidebandTests.cs`

WhiteBook / Ex1 anchors:

- `Documentation/Stream WhiteBook/ExternalAccelerators/02_Topology_And_ISA_Placement.md`
- `Documentation/Stream WhiteBook/ExternalAccelerators/05_Token_Lifecycle_And_Register_ABI.md`
- `Documentation/Refactoring/Phases Ex1/10_External_Accelerator_L7_SDC_Gate.md`
- `Documentation/Refactoring/Phases Ex1/12_Testing_Conformance_And_Documentation_Migration.md`
