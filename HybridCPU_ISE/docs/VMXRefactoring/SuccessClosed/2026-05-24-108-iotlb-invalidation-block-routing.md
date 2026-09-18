# 108. IOTLB invalidation block routing

Date: 2026-05-24

## Rule / basis

- IOTLB invalidation should be routed through the I/O-domain descriptor surface and backend boundary.
- `Core/VMX` IOTLB services should validate descriptor authority and avoid direct VMX-shaped IOMMU calls.
- Compatibility backend details belong to `Legacy/VMX` while generic substrate boundaries are being completed.

## What changed

- Updated `IotlbInvalidationService` so `IotlbInvalidationScope.All` routes through `IoVirtualizationBlock.InvalidateIotlbAll()`.
- Removed the direct `IOMMU.InvalidateVmxIotlbAll()` call and the no-longer-needed IOMMU namespace import from the service.

## How verified

Command:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Build result

- Main project build: succeeded, 0 errors, 54 warnings.
