# Closed: Lane6 domain descriptor authority

Date: 2026-05-24

## Rule / Basis

- Lane6 ownership must live outside VMX compatibility semantics.
- Token, queue, and fence namespaces are substrate resources, not VMCS-owned state.
- VMX may expose compatibility projection, but Lane6 domain authority must remain with runtime/domain descriptors.
- Frozen VMX/VMCS ABI names and RTL behavior must not change for this small refactoring.

## Changed

- Added `Lane6DomainAuthority` as an explicit authority marker.
- Added immutable `Lane6DomainDescriptor` state for lane id, token namespace id, queue namespace id, fence domain id, and compatibility projection permission.
- Added helper classifiers for runtime authority, Lane6 pinning, namespace binding, and compatibility projection.
- Kept the change descriptor-local: no Lane6 execution behavior, queue behavior, token behavior, VMX instruction handlers, VMCS ABI names, or RTL behavior were changed.

## Verified

- Ran:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Build Result

- Build succeeded.
- 54 existing warnings.
- 0 errors.
