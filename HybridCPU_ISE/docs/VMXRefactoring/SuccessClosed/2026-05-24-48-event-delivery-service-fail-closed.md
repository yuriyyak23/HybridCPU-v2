# Closed: event delivery service fail-closed boundary

Date: 2026-05-24

## Rule / Basis

- Event delivery must be owned by runtime event queue descriptors, not by VMX/VMCS state.
- Compatibility projection may observe or expose event state, but must not route or admit events as authority.
- Remap/coalescing policy must remain runtime-side and fail closed on invalid descriptors or queue pressure.
- Frozen VMX/VMCS ABI names and RTL behavior must not change for this small refactoring.

## Changed

- Added `EventDeliveryResult`.
- Added `EventDeliveryService.Post(...)`.
- The service now requires a runtime-owned `EventQueueDescriptor`, validates event descriptors, applies optional interrupt remap policy, honors descriptor-level queue capacity, and handles posted-event coalescing through the existing queue.
- Kept the change service-local: no event queue internals, interrupt fabric behavior, VMX handlers, VMCS ABI names, or RTL behavior were changed.

## Verified

- Ran:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Build Result

- Build succeeded.
- 54 existing warnings.
- 0 errors.
