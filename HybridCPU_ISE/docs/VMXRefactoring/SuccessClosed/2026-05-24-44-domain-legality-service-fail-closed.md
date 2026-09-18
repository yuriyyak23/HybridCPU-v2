# Closed: domain legality service fail-closed boundary

Date: 2026-05-24

## Rule / Basis

- Runtime-owned legality and validation must remain authoritative.
- VMX compatibility projection must not become the owner of bundle legality.
- Empty substrate services must fail closed instead of becoming implicit allow points.
- Frozen VMX/VMCS ABI names and RTL behavior must not change for this small refactoring.

## Changed

- Added `DomainLegalityService.Validate(...)`.
- The service now requires a complete domain runtime context, validates operation capability/mutation boundaries, and requires a runtime-owned `BundleLegalityDescriptor`.
- Added explicit validation failure reasons for missing legality descriptor, missing runtime authority, and denied compatibility projection.
- Kept the change service-local: no VMX instruction handlers, VMCS ABI names, scheduler logic, or RTL behavior were changed.

## Verified

- Ran:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Build Result

- Build succeeded.
- 54 existing warnings.
- 0 errors.
