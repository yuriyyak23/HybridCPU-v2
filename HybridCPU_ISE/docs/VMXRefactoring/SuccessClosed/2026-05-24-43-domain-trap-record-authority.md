# Closed: domain trap record authority

Date: 2026-05-24

## Rule / Basis

- VMX must be a compatibility frontend over domain traps, not the owner of trap authority.
- Trap/exit records must carry domain descriptor and evidence metadata so host-owned evidence stays outside guest-visible VMCS authority.
- Compatibility projection may expose VMX-shaped exit information, but runtime/domain trap records remain authoritative.
- Frozen VMX/VMCS ABI names and RTL behavior must not change for this small refactoring.

## Changed

- Added `DomainTrapRecordAuthority` as an explicit authority marker.
- Added immutable `DomainTrapRecord` state for sequence, domain id, `TrapDecision`, descriptor epoch, evidence hash, and compatibility projection permission.
- Added helper classifiers for runtime authority, domain binding, evidence hash presence, VMX compatibility projection, and exit state.
- Kept the change record-local: no trap evaluation behavior, VM-exit materialization behavior, VMX instruction handlers, VMCS ABI names, or RTL behavior were changed.

## Verified

- Ran:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Build Result

- Build succeeded.
- 54 existing warnings.
- 0 errors.
