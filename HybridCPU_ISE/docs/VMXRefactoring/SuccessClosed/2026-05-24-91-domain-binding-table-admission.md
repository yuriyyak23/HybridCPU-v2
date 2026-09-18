# 91. Domain binding table admission

Date: 2026-05-24

## Rule / basis

- Runtime-owned legality and validation must bind execution, memory, and I/O descriptors before compatibility projection.
- VMX frontend must not own authoritative domain binding state.
- Compatibility projection must be descriptor/runtime authorized and fail closed by default.

## Changed

- Updated `Core/VMX/Substrate/Runtime/Binding/DomainBindingTable.cs`.
- Added `DomainBindingAuthority`, `DomainBindingDecision`, `DomainBindingEntry`, `DomainBindingRequest`, and `DomainBindingResult`.
- Added `Validate(...)` and `CanBind(...)` gates for binding presence, runtime authority, runtime context presence, required domain descriptors, expected domain id, and compatibility projection authorization.

## Verification

Command:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Build result

- Build succeeded.
- 0 errors.
- 54 existing warnings.
