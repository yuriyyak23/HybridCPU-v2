# Closed: completion route descriptor authority

Date: 2026-05-24

## Rule / Basis

- Completion routing must be owned by the domain/runtime substrate, not by VMCS state.
- Lane6/Lane7/vector-stream completion ownership must stay outside VMX compatibility semantics.
- VMX may expose a compatibility projection, but routing authority must remain with runtime/domain descriptors.
- Frozen VMX/VMCS ABI names and RTL behavior must not change for this small refactoring.

## Changed

- Added `CompletionRouteAuthority` as an explicit authority marker.
- Added immutable `CompletionRouteDescriptor` state for route authority, enabled completion sources, posted-event queue requirement, and compatibility projection permission.
- Added helper classifiers for runtime authority, enabled routes, compatibility projection, and source membership.
- Kept the change descriptor-local: no completion router behavior, posted event queue behavior, VMX instruction handlers, VMCS ABI names, or RTL behavior were changed.

## Verified

- Ran:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Build Result

- Build succeeded.
- 54 existing warnings.
- 0 errors.
