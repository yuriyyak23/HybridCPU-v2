# Closed task 168: legacy translation invalidation backend returned through generic host boundary

Date: 2026-05-24

Status: closed

## Rule / basis

- INVEPT/INVVPID compatibility transport must route through generic memory-domain/address-space invalidation.
- `Core/VMX` must not directly call VMX-shaped IOMMU/EPT/VPID authority.
- A file may return from `Legacy/VMX` to `Core/VMX` only with descriptor owner, capability policy, evidence policy, retire/publication boundary, projection tests, and no authoritative VMX state.
- Unsupported or unmapped compatibility state must fail closed.

## Changed

- Moved `Legacy/VMX/Substrate/Memory/Invalidation/LegacyVmxTranslationInvalidationBackend.cs` to `Core/VMX/Compatibility/Adapters/MemoryInvalidation/LegacyVmxTranslationInvalidationBackend.cs`.
- Added `LegacyOriginPath`, `CoreReturnPath`, `RequiredCoreReturnProof`, and `RejectsVmxShapedIommuAuthority`.
- Removed direct `IOMMU.`, `ApplyVmxInvalidation`, `VmxInvalidationScope`, and `InvalidateVmxIotlb` calls from the returned Core adapter.
- Added `Memory/MMU/TranslationInvalidationHostBackend.cs` as the host-side backend behind `ITranslationInvalidationBackend`.
- Added `Memory/MMU/IOMMU.TranslationInvalidation.cs` with a generic `ApplyTranslationInvalidation(...)` entrypoint outside `Core/VMX`.
- Updated `LegacyVmxQuarantineManifest` to treat the legacy invalidation backend as returned-to-Core.
- Added conformance coverage proving the returned adapter requires reverse-import proof, is absent from `Legacy/VMX`, and has no direct VMX/IOMMU authority markers.

## Verified

- `dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore` succeeded with 54 existing warnings, 0 errors.
- `dotnet build "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-restore` succeeded with 93 existing warnings, 0 errors.
- `dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-build --filter "FullyQualifiedName~VmxProjectionSchemaAndQuarantineTests"` passed 8/8.
- `dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-build --filter "FullyQualifiedName~CoreVmxAuthorityBoundaryTests|FullyQualifiedName~RuntimeBoundaryAdmissionTests"` passed 5/5.

## Result

The legacy translation invalidation backend no longer keeps VMX-shaped IOMMU authority inside `Core/VMX`. It is now a compatibility adapter over generic memory-domain invalidation admission and a host memory backend outside the VMX tree.

## Residual risk

The broader memory/IO authority theme remains open: `Legacy/VMX` still contains the VMX-shaped IOMMU binding partial, legacy I/O virtualization backend, and VMCS translation projection. Translation records also still expose NPT/VPID-shaped compatibility vocabulary until the remaining callers move to generic aliases.
