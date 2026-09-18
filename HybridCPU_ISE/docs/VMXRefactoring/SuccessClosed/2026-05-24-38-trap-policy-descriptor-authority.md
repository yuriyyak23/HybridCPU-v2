# Closed: trap policy descriptor authority

Date: 2026-05-24

## Rule / Basis

- VMX must be a compatibility frontend, not the owner of trap/intercept policy.
- Trap policy belongs to the generic domain/runtime substrate and must fail closed until a validated domain descriptor enables policy classes.
- Compatibility projection may expose policy state, but must not become authoritative.
- Frozen VMX/VMCS ABI names and RTL behavior must not change for this small refactoring.

## Changed

- Added `TrapPolicyAuthority` as an explicit authority marker.
- Added immutable `TrapPolicyDescriptor` state for runtime authority, enabled trap classes, validated-domain requirement, and compatibility projection permission.
- Added helper classifiers for runtime authority, active trap classes, compatibility projection, and class membership.
- Kept the change descriptor-local: no VMX instruction handlers, trap evaluation logic, intercept bitmap behavior, VMCS ABI names, or RTL behavior were changed.

## Verified

- Ran:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Build Result

- Build succeeded.
- 54 existing warnings.
- 0 errors.
