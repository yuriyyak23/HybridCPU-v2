# Closed: guest-visible evidence projection gate

Date: 2026-05-24

## Rule / basis

EvidencePolicy is the central security object for evidence visibility. Observability is substrate/domain-owned, not VMCS-owned. Host-owned runtime facts, scheduler evidence, backend binding evidence, and native token evidence must not become guest-visible compatibility state.

## Changed

- Replaced the empty `GuestVisibleEvidenceProjection` placeholder with a minimal projection gate.
- Added `GuestVisibleEvidenceProjectionDecision` for explicit fail-closed outcomes.
- Added `GuestVisibleEvidenceRecord` for the guest-visible projected fact.
- Required both `EvidencePolicyDescriptor` and `ObservabilityDescriptor` approval before projection.
- Rejected host-owned, host-handled, and empty evidence envelopes.

## Verified

Ran:

```text
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Build result

Build succeeded with existing warnings and `0 Error(s)`.
