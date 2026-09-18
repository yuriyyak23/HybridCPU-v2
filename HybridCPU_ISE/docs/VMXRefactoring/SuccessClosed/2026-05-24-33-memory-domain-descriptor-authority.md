# Closed: memory domain descriptor authority

Date: 2026-05-24

## Rule / basis

Memory and second-stage translation authority must belong to memory-domain substrate, not to VMX/VMCS fields. VMCS/NPT controls may remain compatibility inputs or projections.

## Changed

- Added immutable `MemoryDomainDescriptor` state for address space, translation policy, translation control, dirty tracking, and second-stage ownership.
- Added explicit `IsAuthoritativeMemoryStateOwner` classifier.
- Added helper properties for address-space, translation-policy, dirty-tracking, and translation-control availability.
- Added a translation-control replacement helper.
- No MMU/NPT behavior, VMCS ABI field, decoder, encoder, or RTL path was changed.

## Verified

Ran:

```text
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Build result

Build succeeded with existing warnings and `0 Error(s)`.
