# Closed: VMX completion projection service

Date: 2026-05-24

## Source rule

VMX exit reason and qualification are compatibility projections from generic completion records. Unsupported or unknown projected VMX completion state must fail closed.

## Completed change

- Replaced the empty `CompletionProjectionService` placeholder with a minimal projection service.
- Added `VmxCompletionProjection` for VMX-facing exit reason, exit qualification, guest physical address, and EPT qualification aliases.
- Added projection from `CompletionRecord` into VMX compatibility fields.
- Added fail-closed projection of unknown reason codes to `VmExitReason.SecurityPolicyViolation`.

## Validation

- `dotnet build HybridCPU_ISE.csproj --no-restore`
- Result: passed with existing project warnings, `0 Error(s)`.
