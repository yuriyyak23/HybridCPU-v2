# Closed: evidence sideband envelope

Date: 2026-05-24

## Rule / basis

Host-owned runtime evidence is never guest ABI. Evidence visibility, restore lifetime, recomputation, and guest exposure belong to `EvidencePolicy`, not VMCS/VMX projection state. Sideband transport must be fail-closed by default.

## Changed

- Replaced the empty `EvidenceSidebandEnvelope` placeholder with a minimal immutable sideband envelope.
- Added subject, sequence, payload hash, visibility class, restore policy, and host-owned markers.
- Added fail-closed guest exposure through `EvidencePolicyDescriptor`.
- Added restore recomputation checks for host-owned/runtime evidence.

## Verified

Ran:

```text
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Build result

Build succeeded with existing warnings and `0 Error(s)`.
