# 104. Memory translation VMCS projection relocated

Date: 2026-05-24

## Rule / basis

- Memory translation control in `Core/VMX` must be a generic runtime/domain descriptor snapshot, not a VMCS reader.
- VMCS field reads belong to compatibility or legacy projection code until promoted through generated alias/access-policy boundaries.
- VMX/VMCS naming remains acceptable only at frozen ABI or compatibility projection edges.

## What changed

- Removed `MemoryTranslationControl.FromVmcs(...)` from `Core/VMX/Substrate/Memory/Translation/MemoryTranslationControl.cs`.
- Reworded the control summary from VMX/NPT/VPID ownership to generic memory translation runtime ownership.
- Added `Legacy/VMX/Substrate/Memory/Translation/LegacyVmcsMemoryTranslationControlProjection.cs` with the old VMCS-to-translation-control adapter.
- Updated legacy `VmxExecutionUnit` calls to use `LegacyVmcsMemoryTranslationControlProjection.FromVmcs(...)`.

## How verified

Command:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Build result

- Main project build: succeeded, 0 errors, 54 warnings.
