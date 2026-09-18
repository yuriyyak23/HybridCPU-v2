# Closed: minimal MigrationDescriptor boundary

Date: 2026-05-24

## Source rule

Migration must serialize domain state rather than VMCS projection state. Incoming migration payload is security-sensitive and host-owned evidence, scheduler evidence, backend bindings, and native tokens must be recomputed or rejected fail-closed.

## Completed change

- Replaced the empty `MigrationDescriptor` placeholder with a minimal fail-closed descriptor.
- Added `MigrationPayloadClass` to distinguish guest architectural state, domain descriptor state, compatibility projection metadata, and host-owned runtime payload classes.
- Added serialization, restore, recompute-after-restore, and imported-payload rejection predicates.

## Validation

- `dotnet build HybridCPU_ISE.csproj --no-restore`
- Result: passed with existing project warnings, `0 Error(s)`.
