# Closed task 167: legacy CSR-backed VmxCaps source returned fail closed

Date: 2026-05-24

Status: closed

## Rule / basis

- VMX is a frozen compatibility frontend over generic domain/capability/evidence/runtime substrate.
- `VmxCaps` is a read-only compatibility projection, not a source of capability authority.
- Raw CSR masks must not grant typed capabilities.
- A file may return from `Legacy/VMX` to `Core/VMX` only with descriptor owner, capability policy, evidence policy, retire/publication boundary, projection tests, and no authoritative VMX state.
- Unsupported or authority-looking compatibility state must fail closed.

## Changed

- Moved `Legacy/VMX/Compatibility/Generated/CsrProjection/LegacyCsrBackedVmxCapabilityDescriptorSource.cs` to `Core/VMX/Compatibility/Generated/CsrProjection/LegacyCsrBackedVmxCapabilityDescriptorSource.cs`.
- Preserved the legacy constructor ABI that accepts `CsrFile`.
- Removed CSR-backed `VmxCaps` reads as capability authority.
- Made the source return `CapabilityDescriptorSet.Empty`.
- Added `LegacyOriginPath`, `CoreReturnPath`, `RequiredCoreReturnProof`, and `RejectsCsrBackedAuthority`.
- Updated `LegacyVmxQuarantineManifest` so the source is treated as returned-to-Core, not still-quarantined.
- Added conformance coverage that writes `CsrAddresses.VmxCaps` and verifies the returned source still publishes no grants/caps.

## Verified

- `dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore` succeeded with 0 warnings, 0 errors.
- `dotnet build "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-restore` succeeded with 93 existing warnings, 0 errors.
- `dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-build --filter "FullyQualifiedName~VmxProjectionSchemaAndQuarantineTests"` passed 7/7.
- `dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-build --filter "FullyQualifiedName~CoreVmxAuthorityBoundaryTests"` passed 1/1.
- `dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-build --filter "FullyQualifiedName~VmxCapsProjectionBoundaryTests"` passed 3/3.

## Result

The legacy CSR-backed VmxCaps bridge no longer acts as a capability authority source. It is now a frozen compatibility/projection stub in `Core/VMX` that rejects CSR-backed authority by construction and is covered by quarantine/projection conformance.

## Residual risk

This closes only the CSR-backed legacy source return. The broader capability model still needs the remaining bitmap/cache pressure in `CapabilityDescriptorSet` reduced until typed grants are canonical and masks are projection-only.
