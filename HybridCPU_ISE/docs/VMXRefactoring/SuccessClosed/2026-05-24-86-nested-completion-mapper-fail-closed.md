# 86. Nested completion mapper fail-closed

Date: 2026-05-24

## Rule / basis

- Nested mode must be domain composition with runtime-owned validation.
- Lane6/Lane7 completions belong to substrate routing/descriptor services, not VMX-owned state.
- Nested child domains must not receive native lane tokens, backend bindings, or host-owned completion evidence.
- Compatibility projection must be descriptor-authorized and fail closed.

## Changed

- Updated `Core/VMX/Substrate/Nested/CompletionMapping/NestedCompletionMapper.cs`.
- Added `NestedCompletionMappingDecision`, `NestedCompletionMappingRequest`, and `NestedCompletionMappingResult`.
- Added `Validate(...)` and `CanMap(...)` gates for descriptor authority, completion payload presence, host-owned evidence, Lane6/Lane7 passthrough blocking, nested exit-mapping capability grant, and route descriptor projection authorization.

## Verification

Command:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Build result

- Build succeeded.
- 0 errors.
- 54 existing warnings.
