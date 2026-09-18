# 83. Nested capability filter fail-closed

Date: 2026-05-24

## Rule / basis

- VMX/VMCS must remain a frozen compatibility frontend rather than the authority for nested virtualization.
- Nested execution must move toward domain composition with capability descriptors and runtime-owned validation.
- Host-owned evidence must stay outside guest-visible state.
- Lane6/Lane7/vector-stream ownership must not leak through nested VMX compatibility paths.
- Validation must fail closed when descriptor or grant information is absent or insufficient.

## Changed

- Updated `Core/VMX/Substrate/Nested/CapabilityFilter/NestedCapabilityFilter.cs`.
- Added explicit `NestedCapabilityFilterDecision` values for allowed and denied states.
- Added `NestedCapabilityFilterRequest` and `NestedCapabilityFilterResult` as small descriptor-sideband contracts.
- Added `Validate(...)` and `CanPass(...)` so nested capability checks deny missing descriptors, non-runtime authority, missing grants, host-evidence leakage, lane passthrough leakage, and missing domain composition.

## Verification

Command:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Build result

- Build succeeded.
- 0 errors.
- 54 existing warnings.
