# Closed: virtualization lane-binding policy

Date: 2026-05-24

## Rule / basis

VMX frontend must be descriptor-gated and must not own Lane6/Lane7 resources. Lane6/Lane7 execution, queues, tokens, fences, handles, and completions belong to lane descriptors and runtime namespaces. VMX compatibility frontend may remain a SystemSingleton/lane7 compatibility carrier.

## Changed

- Replaced the empty `VirtualizationLaneBindingPolicy` placeholder with a minimal lane-binding policy.
- Added canonical compatibility frontend placement as `SystemSingleton` hard-pinned to lane 7.
- Added canonical Lane6 descriptor placement as `DmaStreamClass` hard-pinned to lane 6.
- Added fail-closed decisions for unvalidated descriptors, VMX-owned lane-resource attempts, wrong slot class, and wrong pinned lane.

## Verified

Ran:

```text
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Build result

Build succeeded with existing warnings and `0 Error(s)`.
