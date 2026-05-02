# Authority Model

## Authority root

The authority root is owner/domain guard evidence plus mapping and IOMMU-domain epoch
validation. Guard decisions are accepted only when their evidence source is the guard
plane and the descriptor owner binding matches the current owner/domain evidence.

This is current model admission/commit authority, not executable L7 IOMMU-backed
memory execution. IOMMU backend infrastructure, mapping epoch checks, and
no-fallback resolver decisions remain downstream evidence until Phase06 plus the
L7 executable gate and Phase12 migration are complete.

Code anchors:

- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Auth/AcceleratorOwnerDomainGuard.cs`
- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Descriptors/AcceleratorDescriptorParser.cs`
- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Tokens/AcceleratorTokenStore.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcOwnerDomainGuardTests.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcMappingEpochGuardTests.cs`

## Authority vs evidence

Evidence can identify, audit, correlate, or explain a decision. It cannot replace guard
authority. The code explicitly treats raw VT hints, token handles, telemetry, replay or
certificate identity, and registry metadata as evidence-plane sources that are rejected
when supplied as authority.

Code anchors:

- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Auth/AcceleratorOwnerDomainGuard.cs`
- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Capabilities/AcceleratorCapabilityRegistry.cs`
- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Tokens/AcceleratorTokenHandle.cs`
- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Telemetry/AcceleratorTelemetry.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcEvidenceIsNotAuthorityTests.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcTokenHandleIsNotAuthorityTests.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcCapabilityIsNotAuthorityTests.cs`

## Publication authority

Model submit admission, model backend execution, explicit commit helpers, and
fault/status observations each require fresh or validated guard authority.
`DeviceComplete` is only model/backend completion evidence. Architectural memory
visibility is owned by the guarded commit coordinator and is not a pipeline
retire exception path.

L7-SDC faults are guarded observations/results in the current implementation:
`AcceleratorCommitResult.RequiresRetireExceptionPublication` is `false`, backend
results have `CanPublishException = false`, and `SystemDeviceCommandMicroOp`
execution never publishes a retire exception because it throws fail-closed before
backend, queue, fence, or commit work can run.

Code anchors:

- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Tokens/AcceleratorExceptionPublication.cs`
- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Commit/AcceleratorCommitModel.cs`
- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Memory/AcceleratorMemoryModel.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcFaultPublicationTests.cs`
- `HybridCPU_ISE.Tests/tests/L7SdcCommitTests.cs`

WhiteBook / Ex1 anchors:

- `Documentation/Stream WhiteBook/ExternalAccelerators/03_Descriptor_ABI_And_Carrier_Cleanliness.md`
- `Documentation/Stream WhiteBook/ExternalAccelerators/10_Telemetry_And_Evidence.md`
- `Documentation/Refactoring/Phases Ex1/06_Addressing_Backend_And_IOMMU_Integration.md`
- `Documentation/Refactoring/Phases Ex1/10_External_Accelerator_L7_SDC_Gate.md`
