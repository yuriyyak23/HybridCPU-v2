# Memory, I/O, And Lanes

Virtualization spans memory, I/O, and lane subsystems, but VMX does not own any of them. Each subsystem has its own runtime authority and its own projection boundary.

## Memory Translation

Neutral memory translation owns:

- address-space identity;
- nested translation status;
- second-stage composition;
- translation violation information;
- invalidation;
- dirty tracking.

Relevant paths:

- `CloseToHSL/Core/Runtime/Memory/Translation/NestedPageWalker.cs`
- `CloseToHSL/Core/Runtime/Memory/Translation/NestedTranslationResult.cs`
- `CloseToHSL/Core/Runtime/Memory/Translation/MemoryTranslationPolicy.cs`
- `CloseToHSL/Core/Runtime/Memory/Invalidation/TranslationInvalidationService.cs`
- `CloseToHSL/Core/Runtime/Memory/DirtyTracking/DirtyTrackingServiceDescriptor.cs`

VMX-compatible names such as EPT violation, EPT misconfiguration, VPID, and invalidation scopes are projected from these neutral results. They are not direct owners of the memory model.

## I/O And DMA

Neutral I/O owns:

- DMA window descriptors;
- domain bindings;
- IOMMU descriptors;
- IOTLB invalidation;
- DMA authority checks.

Relevant paths:

- `CloseToHSL/Core/Runtime/IO/Dma/DmaWindowDescriptor.cs`
- `CloseToHSL/Core/Runtime/IO/Dma/DmaDomainBinding.cs`
- `CloseToHSL/Core/Runtime/IO/Dma/DmaAuthorityService.cs`
- `CloseToHSL/Core/Runtime/IO/Iotlb/IotlbInvalidationService.cs`
- `CloseToHSL/Core/Runtime/Memory/Iommu/IommuDomainDescriptor.cs`

VMX I/O aliases must remain aliases. They do not bypass domain binding or IOMMU authority.

## Lane 6

Lane 6 runtime owns queue state, token namespace, fences, and host-owned evidence for its lane domain.

Relevant paths:

- `CloseToHSL/Core/Runtime/Lanes/Lane6/**`
- `CloseToHSL/Core/Runtime/Domains/Admission/Lane6/Lane6DomainRuntime.cs`

Compatibility projection can report lane-related evidence only if it passes the evidence and domain boundaries. It cannot use VMX to publish lane state.

## Lane 7

Lane 7 runtime owns accelerator token namespaces, handles, backend binding policy, completion routing, state checkpointing, and host-owned evidence.

Relevant paths:

- `CloseToHSL/Core/Runtime/Lanes/Lane7/**`
- `CloseToHSL/Core/Runtime/Domains/Admission/Lane7/Lane7DomainRuntime.cs`

Lane 7 virtualization is not a VMX feature. VMX can at most project compatibility facts after neutral Lane 7 authority has accepted them.

## Vector Stream

Vector stream virtualization is descriptor-owned:

- vector stream execution extension descriptors;
- vector stream state;
- save/restore projection;
- fault and evidence projections.

Relevant paths:

- `CloseToHSL/Core/Runtime/Lanes/VectorStream/**`
- `CloseToHSL/Core/Runtime/Domains/Admission/VectorStream/VectorStreamDomainRuntime.cs`
- `CloseToHSL/Core/Virtualization/Compatibility/Frontend/Projection/Lanes/**`

The VMCSv2 vector-stream projection is compatibility evidence. The vector stream state remains neutral runtime state.
