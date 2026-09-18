# 90. IOMMU domain descriptor authority

Date: 2026-05-24

## Rule / basis

- I/O and memory translation authority must live in generic domain descriptors, not VMX-owned state.
- VMX CSR/VMCS compatibility projection may only mirror descriptor-authorized state.
- IOMMU/IOTLB operations must be runtime-owned and fail closed when binding or permissions are missing.

## Changed

- Updated `Core/VMX/Substrate/Memory/Iommu/IommuDomainDescriptor.cs`.
- Added `IommuDomainAuthority`.
- Added descriptor fields for runtime authority, `IommuDomainBinding`, required permissions, IOTLB invalidation ownership, and compatibility projection.
- Added gates for valid binding, permission grants, IOTLB invalidation authority, and compatibility projection.

## Verification

Command:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Build result

- Build succeeded.
- 0 errors.
- 54 existing warnings.
