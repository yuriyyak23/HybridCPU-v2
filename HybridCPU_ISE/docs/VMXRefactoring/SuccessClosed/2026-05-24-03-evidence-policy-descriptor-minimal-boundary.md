# Closed: minimal EvidencePolicyDescriptor boundary

Date: 2026-05-24

## Source rule

Evidence visibility, recomputation, zeroization, and host-owned runtime facts belong to `EvidencePolicy`, not VMCS/VMX projection state. The default must be fail-closed.

## Completed change

- Replaced the empty `EvidencePolicyDescriptor` placeholder with a minimal fail-closed policy object.
- Added `EvidenceVisibilityClass` to distinguish guest architectural state, compatibility aliases, host-owned runtime evidence, scheduler evidence, backend binding evidence, and native token evidence.
- Added `EvidenceRestorePolicy` and small policy predicates for guest exposure, migration serialization, and restore-time recomputation.

## Validation

- `dotnet build HybridCPU_ISE.csproj --no-restore`
- Result: passed with existing warnings, `0 Error(s)`.
