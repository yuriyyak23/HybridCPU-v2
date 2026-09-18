# Closed: event queue descriptor authority

Date: 2026-05-24

## Rule / Basis

- Event delivery and queueing must be owned by the generic runtime substrate, not by VMCS state.
- VMX remains a compatibility frontend over runtime-owned event delivery policy.
- Queue capacity, delivery ordering, and coalescing policy must be descriptor-visible and fail closed.
- Frozen VMX/VMCS ABI names and RTL behavior must not change for this small refactoring.

## Changed

- Added `EventQueueAuthority` as an explicit authority marker.
- Added immutable `EventQueueDescriptor` state for runtime authority, maximum pending events, priority delivery preference, coalescing permission, and compatibility projection permission.
- Added helper classifiers for runtime authority, compatibility projection, capacity acceptance, and capacity updates.
- Kept the change descriptor-local: no posted event queue behavior, interrupt delivery behavior, VMX instruction handlers, VMCS ABI names, or RTL behavior were changed.

## Verified

- Ran:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Build Result

- Build succeeded.
- 54 existing warnings.
- 0 errors.
