# Closed: VMX CSR alias set

Date: 2026-05-24

## Rule / basis

VMX/VMCS ABI names may remain frozen at the frontend boundary, but capability and completion data must be treated as compatibility projections over descriptor/capability/completion substrate instead of VMCS-owned authority.

## Changed

- Added `VmxCsrAliasKind` and `VmxCsrAlias`.
- Added a frozen CSR alias table for VMX enable, capability, control, and completion projection CSRs.
- Marked `VmxCaps` as a read-only capability projection.
- Added lookup helpers without changing CSR addresses, RTL behavior, or emitted code.

## Verified

Ran:

```text
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Build result

Build succeeded with existing warnings and `0 Error(s)`.
