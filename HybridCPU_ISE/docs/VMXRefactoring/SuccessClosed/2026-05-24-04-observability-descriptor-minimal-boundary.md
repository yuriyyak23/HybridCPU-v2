# Closed: minimal ObservabilityDescriptor boundary

Date: 2026-05-24

## Source rule

`Observability` must be domain/substrate-owned, not VMCS-owned. Host-owned runtime evidence, scheduler evidence, backend binding evidence, and native token evidence must not become guest-visible compatibility state.

## Completed change

- Replaced the empty `ObservabilityDescriptor` placeholder with a minimal fail-closed descriptor.
- Added explicit guest publication predicates for guest architectural state and compatibility aliases.
- Added a host-local capture predicate for host-owned runtime, scheduler, backend binding, and native token evidence.
- Added guest redaction predicate so observability has an explicit non-leak boundary outside VMCS projection.

## Validation

- `dotnet build HybridCPU_ISE.csproj --no-restore`
- Result: passed with existing project warnings, `0 Error(s)`.
