# MatMul Capability Provider

## Legacy fixture quarantine

The retained `MatMulAccelerator` implements `ICustomAccelerator` for legacy descriptor
fixture behavior and test metadata. It is not the L7-SDC production execution path, and
production L7-SDC backends do not call `ICustomAccelerator.Execute()`.

Code anchors:

- `HybridCPU_ISE/Core/Accelerators/MatMulAccelerator.cs`
- `HybridCPU_ISE/NonRTL/Core/Diagnostics/InstructionRegistry.Types.cs`
- `HybridCPU_ISE/NonRTL/Core/Diagnostics/InstructionRegistry.Accelerators.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcLegacyQuarantineTests.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcMatMulNoLegacyExecuteTests.cs`

## Metadata-only provider

`MatMulCapabilityProvider` registers `matmul.fixture.v1` metadata: operation name,
supported datatypes, rank-2 shape envelope, and conservative resource model. Query
results expose metadata only; guard-backed acceptance is still required before token
admission.

Capability metadata is not authority by itself. Current `ACCEL_QUERY_CAPS` can
return a bounded metadata summary through the scoped runtime/register ABI path
when guard-backed capability acceptance succeeds, but metadata alone does not
authorize submit admission, memory publication, arbitrary backend execution, or
compiler/backend production lowering.

Code anchors:

- `HybridCPU_ISE/NonRTL/Core/Execution/ExternalAccelerators/Capabilities/MatMulCapabilityProvider.cs`
- `HybridCPU_ISE/NonRTL/Core/Execution/ExternalAccelerators/Capabilities/AcceleratorCapabilityRegistry.cs`
- `HybridCPU_ISE/NonRTL/Core/Execution/ExternalAccelerators/Capabilities/MatMulResourceModel.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcMatMulCapabilityTests.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcCapabilityIsNotAuthorityTests.cs`

## Descriptor and backend contour

`MatMulDescriptor` validates dimensions, datatype triples, strides, layout flags,
footprints, alias policy, and binding to the guard-accepted command descriptor. The fake
MatMul backend is test-only and stages descriptor-bound result bytes. `DeviceComplete`
from that backend is still not commit.

This contour is not a universal external accelerator command protocol. The
current scoped `SystemDeviceCommandMicroOp.Execute(...)` path can submit guarded
SDC1/MatMul work through the external accelerator runtime and test/reference
backend surfaces, but production L7-SDC backends still do not call
`ICustomAccelerator.Execute()` and legacy custom accelerator fallback remains
quarantined.

Code anchors:

- `HybridCPU_ISE/NonRTL/Core/Execution/ExternalAccelerators/Descriptors/MatMulDescriptor.cs`
- `HybridCPU_ISE/NonRTL/Core/Execution/ExternalAccelerators/Backends/FakeMatMulExternalAcceleratorBackend.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcMatMulDescriptorTests.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcBackendTests.cs`

WhiteBook / Ex1 anchors:

- `Documentation/InstructionsRefactor/WhiteBook/05_NonExecutable_And_Future_Gates.md`
- `Documentation/Stream WhiteBook/ExternalAccelerators/03_Descriptor_ABI_And_Carrier_Cleanliness.md`
- `Documentation/Stream WhiteBook/ExternalAccelerators/06_Backend_Staging_Commit_And_Rollback.md`
- `Documentation/Refactoring/Phases Ex1/10_External_Accelerator_L7_SDC_Gate.md`
- `Documentation/Refactoring/Phases Ex1/11_Compiler_Backend_Lowering_Contract.md`
