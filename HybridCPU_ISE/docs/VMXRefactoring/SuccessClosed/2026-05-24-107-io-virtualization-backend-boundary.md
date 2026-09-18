# 107. I/O virtualization backend boundary

Date: 2026-05-24

## Rule / basis

- I/O-domain descriptors should own I/O virtualization state and route host mechanics through explicit backend boundaries.
- VMX-shaped IOMMU operations must not be direct authority inside `Core/VMX` substrate descriptors.
- Legacy VMX/IOMMU calls may remain behind compatibility backend adapters until replaced by generic host runtime backends.

## What changed

- Added `IIoVirtualizationBackend`.
- Updated `IoVirtualizationBlock` to delegate domain binding, unbinding, IOTLB invalidation, and epoch observation through the backend boundary.
- Added `Legacy/VMX/Substrate/IO/LegacyVmxIoVirtualizationBackend.cs` as the default compatibility backend over existing IOMMU methods.

## How verified

Command:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Build result

- Main project build: succeeded, 0 errors, 54 warnings.
