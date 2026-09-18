# Closed: Lane7 accelerator descriptor authority

Date: 2026-05-24

## Rule / Basis

- Lane7/external accelerator ownership must live outside VMX compatibility semantics.
- Backend bindings, handle namespaces, token namespaces, and completion routes are substrate resources, not VMCS-owned state.
- VMX may expose compatibility projection, but Lane7 authority must remain with runtime/domain descriptors.
- Frozen VMX/VMCS ABI names and RTL behavior must not change for this small refactoring.

## Changed

- Added `Lane7AcceleratorAuthority` as an explicit authority marker.
- Added immutable `Lane7AcceleratorDescriptor` state for lane id, backend binding id, handle namespace id, token namespace id, completion route id, runtime backend binding requirement, and compatibility projection permission.
- Added helper classifiers for runtime authority, Lane7 pinning, backend binding, namespace binding, and compatibility projection.
- Kept the change descriptor-local: no Lane7 backend behavior, handle behavior, token behavior, completion routing behavior, VMX instruction handlers, VMCS ABI names, or RTL behavior were changed.

## Verified

- Ran:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Build Result

- Build succeeded.
- 54 existing warnings.
- 0 errors.
