# Closed: domain scheduling admission fail-closed boundary

Date: 2026-05-24

## Rule / Basis

- Scheduling admission must be runtime/domain descriptor-owned, not VMX-owned.
- Lane binding for virtualization operations must stay outside VMX compatibility semantics.
- Empty substrate services must fail closed until the execution-domain descriptor admits the operation.
- Frozen VMX/VMCS ABI names and RTL behavior must not change for this small refactoring.

## Changed

- Added `DomainSchedulingAdmission.Admit(...)`.
- The service now requires an execution-domain `SchedulingBudgetDescriptor`, runtime authority, accepted lane binding, and available per-epoch budget.
- Added explicit validation failure reasons for missing scheduling descriptor, rejected lane, and exhausted budget.
- Kept the change service-local: no scheduler behavior, VMX instruction handlers, VMCS ABI names, or RTL behavior were changed.

## Verified

- Ran:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Build Result

- Build succeeded.
- 54 existing warnings.
- 0 errors.
