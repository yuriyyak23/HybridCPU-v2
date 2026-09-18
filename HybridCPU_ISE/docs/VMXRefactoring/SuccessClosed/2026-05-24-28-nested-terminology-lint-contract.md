# Closed: nested terminology lint contract

Date: 2026-05-24

## Rule / basis

Nested virtualization must be modeled as domain composition, not as public `VMCS12`/`VMCS02` architecture. Legacy terms may remain only in compatibility glossary or generated projection contexts.

## Changed

- Added `NestedCompositionTermScope` and `NestedLegacyAliasTerm`.
- Added explicit legacy alias terms for `VMCS12`, `VMCS02`, and `ShadowVmcs`.
- Added helpers to classify allowed scope and public architecture violations.
- Kept the change conformance-only; no nested runtime, projection composition, VMCS ABI name, or migration behavior was changed.

## Verified

Ran:

```text
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Build result

Build succeeded with existing warnings and `0 Error(s)`.
