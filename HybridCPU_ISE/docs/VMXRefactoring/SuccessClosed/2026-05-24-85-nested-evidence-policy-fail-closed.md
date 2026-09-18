# 85. Nested evidence policy fail-closed

Date: 2026-05-24

## Rule / basis

- Nested mode must be domain composition, not VMCS12/VMCS02 as architecture.
- Host-owned runtime evidence must never become guest ABI.
- EvidencePolicy and Observability must stay descriptor-owned, not VMCS-owned.
- Nested validation must fail closed when descriptor authority or evidence gates are missing.

## Changed

- Updated `Core/VMX/Substrate/Nested/Policies/NestedEvidencePolicy.cs`.
- Added `NestedEvidencePolicyDecision`, `NestedEvidencePolicyRequest`, and `NestedEvidencePolicyResult`.
- Added `Validate(...)` and `CanExpose(...)` gates for runtime descriptor authority, host-evidence exclusion, host-owned evidence classes, evidence policy, and observability policy.

## Verification

Command:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Build result

- Build succeeded.
- 0 errors.
- 54 existing warnings.
