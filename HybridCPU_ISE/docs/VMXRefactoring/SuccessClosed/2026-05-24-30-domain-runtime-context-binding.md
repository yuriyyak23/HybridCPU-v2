# Closed: domain runtime context binding

Date: 2026-05-24

## Rule / basis

Authoritative virtualization state must move toward generic execution, memory, I/O, and capability descriptors. VMX compatibility frontend should operate over a domain runtime context instead of treating VMCS/VMX CSR state as the semantic center.

## Changed

- Added `DomainRuntimeContext` bindings for `ExecutionDomainDescriptor`, `MemoryDomainDescriptor`, `IoDomainDescriptor`, and `CapabilityDescriptorSet`.
- Added presence helpers for required domain descriptors.
- Added capability query and immutable capability replacement helper.
- Kept descriptor references nullable to preserve incremental adoption and avoid forcing runtime rewiring in this short task.
- No VMX execution logic, VMCS projection, CSR behavior, or RTL path was changed.

## Verified

Ran:

```text
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Build result

Build succeeded with existing warnings and `0 Error(s)`.
