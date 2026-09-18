# Closed: domain validation result fail-closed shape

Date: 2026-05-24

## Rule / basis

Runtime-owned legality must be fail-closed. VMX compatibility access must not mutate authoritative state unless the domain context, capability grants, and operation class allow it.

## Changed

- Added `DomainValidationFailureReason`.
- Added immutable `DomainValidationResult` success/fail shape.
- Added domain-context validation for execution, memory, and I/O descriptor presence.
- Added operation validation for capability grants and non-mutating projection/replay paths.
- Kept the change as substrate validation scaffolding; no VMX execution handler, CSR behavior, VMCS projection, or RTL path was changed.

## Verified

Ran:

```text
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Build result

Build succeeded with existing warnings and `0 Error(s)`.
