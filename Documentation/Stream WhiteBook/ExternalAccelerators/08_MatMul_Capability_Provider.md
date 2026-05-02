# MatMul Capability Provider

## Legacy fixture quarantine

The retained `MatMulAccelerator` implements `ICustomAccelerator` for legacy descriptor
fixture behavior and test metadata. It is not the L7-SDC production execution path, and
production L7-SDC backends do not call `ICustomAccelerator.Execute()`.

Code anchors:

- `HybridCPU_ISE/Core/Accelerators/MatMulAccelerator.cs`
- `HybridCPU_ISE/Core/Diagnostics/InstructionRegistry.Types.cs`
- `HybridCPU_ISE/Core/Diagnostics/InstructionRegistry.Accelerators.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcLegacyQuarantineTests.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcMatMulNoLegacyExecuteTests.cs`

## Metadata-only provider

`MatMulCapabilityProvider` registers `matmul.fixture.v1` metadata: operation name,
supported datatypes, rank-2 shape envelope, and conservative resource model. Query
results expose metadata only; guard-backed acceptance is still required before token
admission.

Capability metadata is not authority and not execution evidence. It does not
make `ACCEL_QUERY_CAPS` executable, does not write architectural `rd`, and does
not authorize compiler/backend production lowering.

Code anchors:

- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Capabilities/MatMulCapabilityProvider.cs`
- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Capabilities/AcceleratorCapabilityRegistry.cs`
- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Capabilities/MatMulResourceModel.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcMatMulCapabilityTests.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcCapabilityIsNotAuthorityTests.cs`

## Descriptor and backend contour

`MatMulDescriptor` validates dimensions, datatype triples, strides, layout flags,
footprints, alias policy, and binding to the guard-accepted command descriptor. The fake
MatMul backend is test-only and stages descriptor-bound result bytes. `DeviceComplete`
from that backend is still not commit.

This contour is not a universal external accelerator command protocol and is
not reachable through `SystemDeviceCommandMicroOp.Execute(...)`, which remains
fail-closed.

Code anchors:

- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Descriptors/MatMulDescriptor.cs`
- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Backends/FakeMatMulExternalAcceleratorBackend.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcMatMulDescriptorTests.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcBackendTests.cs`

WhiteBook / Ex1 anchors:

- `Documentation/Stream WhiteBook/ExternalAccelerators/03_Descriptor_ABI_And_Carrier_Cleanliness.md`
- `Documentation/Stream WhiteBook/ExternalAccelerators/06_Backend_Staging_Commit_And_Rollback.md`
- `Documentation/Refactoring/Phases Ex1/10_External_Accelerator_L7_SDC_Gate.md`
- `Documentation/Refactoring/Phases Ex1/11_Compiler_Backend_Lowering_Contract.md`
