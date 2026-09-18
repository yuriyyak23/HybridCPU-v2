# 84. Nested domain runtime admission

Date: 2026-05-24

## Rule / basis

- Nested virtualization must move toward domain composition, not public VMCS12/VMCS02 architecture.
- Runtime-owned legality and validation must be the admission authority.
- Compatibility projection may exist only after descriptor and capability gates allow it.
- VMX handler behavior and frozen ABI names must remain untouched for small substrate refactoring tasks.

## Changed

- Updated `Core/VMX/Substrate/Domains/Nested/NestedDomainRuntime.cs`.
- Added `NestedDomainRuntimeDecision` with explicit fail-closed denial reasons.
- Added `NestedDomainRuntimeRequest` and `NestedDomainRuntimeResult` as runtime admission contracts.
- Added `Validate(...)` and `CanAdmit(...)` so nested runtime admission requires:
  - a descriptor;
  - runtime-authoritative descriptor ownership;
  - validated domain composition;
  - an allowed nested capability filter result;
  - descriptor-authorized compatibility projection when requested.

## Verification

Command:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Build result

- Build succeeded.
- 0 errors.
- 54 existing warnings.
