# 87. Nested projection service admission

Date: 2026-05-24

## Rule / basis

- Nested compatibility projection must be a generated/read-only frontend view over domain composition.
- Runtime-owned legality, capability grants, evidence policy, and completion mapping must be checked before projection.
- Default or implicit success must fail closed for substrate admission contracts.

## Changed

- Updated `Core/VMX/Substrate/Nested/Projection/NestedProjectionService.cs`.
- Added `NestedProjectionDecision`, `NestedProjectionRequest`, and `NestedProjectionResult`.
- Added `Validate(...)` and `CanProject(...)` admission gates over nested runtime admission, capability filter, evidence policy, optional completion mapping, and descriptor-authorized compatibility projection.
- Required explicit non-empty allow reasons so default record structs do not accidentally pass as valid projection admission.

## Verification

Command:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Build result

- Build succeeded.
- 0 errors.
- 54 existing warnings.
