# Closed task 169: legacy I/O virtualization backend returned through generic host boundary

Date: 2026-05-24

Status: closed

## Rule / basis

- VMX is a frozen compatibility frontend over generic domain/capability/evidence/runtime substrate.
- I/O, DMA, IOMMU, and IOTLB authority belong to `IoDomainDescriptor` and runtime services, not to VMX.
- `Core/VMX` must not directly call VMX-shaped IOMMU/IOTLB authority.
- A file may return from `Legacy/VMX` to `Core/VMX` only with descriptor owner, capability policy, evidence policy, retire/publication boundary, projection tests, and no authoritative VMX state.
- Unsupported, missing, or unmapped I/O-domain state must fail closed.

## Changed

- Moved `Legacy/VMX/Substrate/IO/LegacyVmxIoVirtualizationBackend.cs` to `Core/VMX/Compatibility/Adapters/IO/LegacyVmxIoVirtualizationBackend.cs`.
- Added `LegacyOriginPath`, `CoreReturnPath`, `RequiredCoreReturnProof`, and `RejectsVmxShapedIommuAuthority`.
- Removed direct `IOMMU.`, `BindVmx`, `UnbindVmx`, `InvalidateVmx`, `ApplyVmx`, and `TryTranslateVmxDma` calls from the returned Core adapter.
- Added `Memory/MMU/IoVirtualizationHostBackend.cs` as the host-side backend behind `IIoVirtualizationBackend`.
- Added `Memory/MMU/IOMMU.IoDomainBackend.cs` with generic host wrappers for I/O-domain bind/unbind and IOTLB invalidation.
- Updated `IoVirtualizationBlock` default backend to `IoVirtualizationHostBackend.Instance`.
- Updated `LegacyVmxQuarantineManifest` to treat the legacy I/O virtualization backend as returned-to-Core.
- Added conformance coverage proving the returned adapter requires reverse-import proof, is absent from `Legacy/VMX`, and has no direct VMX/IOMMU authority markers.
- Added I/O authority fail-closed coverage for missing I/O domain, missing DMA/IOMMU authority, missing virtualization block, missing DMA window, invalid IOMMU binding, and unsatisfied fence.

## Verified

- `dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore` succeeded with 54 existing warnings, 0 errors.
- `dotnet build "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-restore` succeeded with 93 existing warnings, 0 errors.
- `dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-build --filter "FullyQualifiedName~VmxProjectionSchemaAndQuarantineTests"` passed 9/9.
- `dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-build --filter "FullyQualifiedName~CoreVmxAuthorityBoundaryTests"` passed 1/1.
- `dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-build --filter "FullyQualifiedName~IoDomainAuthorityBoundaryTests"` passed 2/2.

## Result

The legacy I/O virtualization backend no longer keeps VMX-shaped IOMMU/IOTLB authority inside `Core/VMX`. It is now a compatibility adapter over generic host I/O transport, with descriptor-owned IOTLB/DMA admission remaining in the I/O-domain services.

## Residual risk

`Legacy/VMX/Substrate/Memory/Iommu/IOMMU.DomainBinding.partial.cs` remains quarantined and still contains VMX-shaped host mechanics. A later step should demote that partial behind generic host-side I/O-domain storage and DMA translation wrappers without moving VMX authority back into `Core/VMX`.
