# Closed: scheduling budget descriptor authority

Date: 2026-05-24

## Rule / Basis

- VMX must move toward a generic domain/descriptor/capability/evidence/runtime substrate.
- Runtime-owned scheduling and legality must remain authoritative.
- Lane6/Lane7/vector-stream ownership must stay outside VMX compatibility semantics.
- Frozen VMX/VMCS ABI names and RTL behavior must not change for this small refactoring.

## Changed

- Added `SchedulingBudgetAuthority` as an explicit authority marker.
- Added immutable `SchedulingBudgetDescriptor` state for runtime authority, per-epoch operation budget, system-singleton lane requirement, and pinned lane id.
- Added budget and lane helper classifiers so consumers can reason about scheduling policy through the execution-domain descriptor instead of VMX-owned state.
- Kept the change descriptor-local: no VMX instruction handlers, VMCS ABI names, scheduler logic, VLIW bundle logic, or RTL behavior were changed.

## Verified

- Ran:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Build Result

- Build succeeded.
- 54 existing warnings.
- 0 errors.
