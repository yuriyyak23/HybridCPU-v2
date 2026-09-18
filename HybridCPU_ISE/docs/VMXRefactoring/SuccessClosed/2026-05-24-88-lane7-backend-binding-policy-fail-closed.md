# 88. Lane7 backend binding policy fail-closed

Date: 2026-05-24

## Rule / basis

- Lane7 is a substrate-owned accelerator resource, not a VMX subdevice.
- Lane7 backend bindings and native backend handles must not become guest ABI.
- VMX compatibility projection may expose only descriptor-authorized virtual state.
- Backend binding policy must fail closed when descriptor authority, lane pinning, or namespace safety is missing.

## Changed

- Updated `Core/VMX/Substrate/Lanes/Lane7/Handles/Lane7BackendBindingPolicy.cs`.
- Added `Lane7BackendBindingDecision`, `Lane7BackendBindingRequest`, and `Lane7BackendBindingResult`.
- Added `Validate(...)` and `CanBind(...)` gates for runtime descriptor authority, Lane7 pinning, required backend binding, handle namespace authority, native backend handle exposure, and compatibility projection authorization.

## Verification

Command:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Build result

- Build succeeded.
- 0 errors.
- 54 existing warnings.
