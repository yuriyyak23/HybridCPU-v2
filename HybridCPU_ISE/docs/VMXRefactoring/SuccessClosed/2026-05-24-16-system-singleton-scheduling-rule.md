# Closed: virtualization SystemSingleton scheduling rule

Date: 2026-05-24

## Rule / basis

VMX frontend must be descriptor-gated, VMX instructions normally remain SystemSingleton/lane7 work, and compiler/runtime boundaries must prevent VMX frontend emission from becoming a general lane or descriptor bypass. Typed-slot legality remains runtime-owned.

## Changed

- Replaced the empty `VirtualizationSystemSingletonSchedulingRule` placeholder with a minimal scheduling admission rule.
- Added `VirtualizationSystemSingletonSchedulingRequest` to carry placement, descriptor validation, and bundle-level SystemSingleton count.
- Added explicit fail-closed decisions for unvalidated descriptors, non-SystemSingleton placement, missing hard pinning, non-lane7 pinning, and multiple SystemSingleton carriers in one bundle.

## Verified

Ran:

```text
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Build result

Build succeeded with existing warnings and `0 Error(s)`.
