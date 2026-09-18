# Closed: DMA authority window boundary

Date: 2026-05-24

## Rule / Basis

- I/O and DMA authority must belong to the generic `IoDomainDescriptor`, not VMX-owned state.
- DMA access must be descriptor-gated and fail closed when the authoritative domain/window/binding is absent.
- Non-coherent DMA fence requirements must be modeled outside VMX compatibility state.
- Frozen VMX/VMCS ABI names and RTL behavior must not change for this small refactoring.

## Changed

- Added `DmaWindowAuthority` and immutable `DmaWindowDescriptor` state for address range, permissions, fence requirement, runtime authority, and compatibility projection.
- Added `DmaAuthorityService.ValidateAccess(...)` with explicit fail-closed decisions for missing I/O domain, missing DMA window, invalid IOMMU binding, missing authority, permission denial, range denial, and unsatisfied fence.
- Returned typed `DmaFault` evidence through `DmaAuthorityResult` without exposing VMX as the source of truth.
- Kept the change substrate-local: no DMA execution path, IOMMU translation behavior, VMX instruction handlers, VMCS ABI names, or RTL behavior were changed.

## Verified

- Ran:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Build Result

- Build succeeded.
- 54 existing warnings.
- 0 errors.
