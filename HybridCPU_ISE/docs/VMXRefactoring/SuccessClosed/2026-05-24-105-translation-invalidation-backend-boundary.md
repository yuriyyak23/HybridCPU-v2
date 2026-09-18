# 105. Translation invalidation backend boundary

Date: 2026-05-24

## Rule / basis

- `Core/VMX` substrate services should validate generic memory-domain descriptors and delegate host/backend mechanics through explicit runtime boundaries.
- INVEPT/INVVPID-shaped compatibility transport must not be the substrate authority surface.
- Legacy VMX/IOMMU calls may remain only behind compatibility backend adapters until replaced by generic memory-domain and address-space invalidation backends.

## What changed

- Added `TranslationInvalidationBackendRequest`.
- Added `ITranslationInvalidationBackend`.
- Updated `TranslationInvalidationService` to call the backend boundary after descriptor, policy, range, permission, and fence validation.
- Moved the direct `IOMMU.ApplyVmxInvalidation(...)` call into `Legacy/VMX/Substrate/Memory/Invalidation/LegacyVmxTranslationInvalidationBackend.cs`.

## How verified

Command:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Build result

- Main project build: succeeded, 0 errors, 54 warnings.
