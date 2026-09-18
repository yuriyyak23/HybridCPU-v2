# Closed: trap policy service fail-closed boundary

Date: 2026-05-24

## Rule / Basis

- Trap/intercept policy must be owned by the generic runtime/domain substrate, not by VMCS state.
- A compatibility projection may expose trap state, but must not become the authority for trap evaluation.
- Empty substrate services must fail closed until a runtime-owned descriptor admits the requested class.
- Frozen VMX/VMCS ABI names and RTL behavior must not change for this small refactoring.

## Changed

- Added `TrapPolicyEvaluationResult`.
- Added `TrapPolicyService.Evaluate(...)`.
- The service now requires a runtime-owned `TrapPolicyDescriptor`, validated domain state when requested, and an enabled trap class before consulting the trap bitmap.
- Denied or unsupported classes fail closed with a security-policy VM-exit projection instead of silently becoming implicit allow points.
- Kept the change service-local: no VMX instruction handlers, VMCS ABI names, trap bitmap storage, or RTL behavior were changed.

## Verified

- Ran:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Build Result

- Build succeeded.
- 54 existing warnings.
- 0 errors.
