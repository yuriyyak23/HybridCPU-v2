# Closed: I/O domain descriptor authority

Date: 2026-05-24

## Rule / basis

I/O, DMA, and IOMMU authority must belong to I/O-domain substrate, not to VMX as an architectural axis. VMX compatibility surfaces may project into that domain boundary without owning the state.

## Changed

- Added immutable `IoDomainDescriptor` state for I/O virtualization block, DMA window, DMA authority, IOMMU authority, and compatibility projection flag.
- Added explicit `IsAuthoritativeIoStateOwner` classifier.
- Added helper properties for virtualization block, DMA window, and required I/O authority.
- Added a compatibility projection toggle helper.
- No DMA, IOMMU, VMX instruction, CSR, VMCS projection, or RTL behavior was changed.

## Verified

Ran:

```text
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Build result

Build succeeded with existing warnings and `0 Error(s)`.
