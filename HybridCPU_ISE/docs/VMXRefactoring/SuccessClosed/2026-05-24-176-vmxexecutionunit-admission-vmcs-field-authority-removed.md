# 176 VmxExecutionUnit Admission VMCS Field Authority Removed

Date: 2026-05-24

Status: closed

## Rule / Basis

VMX is a frozen compatibility frontend, not the virtualization architecture. Intercept and event admission must be owned by generic domain descriptors/admission policy, not by a legacy VMX frontend deriving authoritative routing tags from VMCS fields.

## Changed

- Kept `Legacy/VMX/Compatibility/Frontend/Handlers/VmxExecutionUnit.cs` quarantined.
- Removed `ResolveActiveMemoryTranslationControl()` without replacement.
- Removed frontend reads of `VmcsField.SecondaryProcControls`, `VmcsField.GuestCr3`, `VmcsField.EptPointer`, and `VmcsField.Vpid` for intercept/event admission domain construction.
- Left intercept/event compatibility entry points in place, but they no longer derive tagged routing state from VMCS fields; tagged routing now requires a future generic domain-admission source.
- Added `LegacyVmxExecutionUnitAdmissionVmcsFieldAuthorityRemovalContract`.
- Extended `VmxProjectionSchemaAndQuarantineTests` with `LegacyVmxExecutionUnit_DoesNotDeriveAdmissionDomainsFromVmcsFields`.
- Updated `docs/VMXRefactoring/audit.md`.
- Updated `docs/VMXRefactoring/2026-05-24-vmx-current-model-completion-audit.md`.
- Updated `docs/VMXRefactoring/ОСНОВЫ и ПРАВИЛА VMX.md`.

## Verified

- Static conformance proves `VmxExecutionUnit.cs` has no `ResolveActiveMemoryTranslationControl`, `CreateRuntimeProjection`, `VmcsField.SecondaryProcControls`, `VmcsField.GuestCr3`, `VmcsField.EptPointer`, or `VmcsField.Vpid` markers.
- `LegacyVmxQuarantineManifest` still keeps `VmxExecutionUnit.cs` quarantined.
- `CoreVmxAuthorityBoundaryTests` still pass.

## Build / Test Result

- `dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore`
  Result: succeeded, 54 existing warnings, 0 errors.
- `dotnet build "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-restore`
  Result: succeeded, 93 existing warnings, 0 errors.
- `dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-build --filter "FullyQualifiedName~VmxProjectionSchemaAndQuarantineTests"`
  Result: passed 18/18.
- `dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-build --filter "FullyQualifiedName~CoreVmxAuthorityBoundaryTests"`
  Result: passed 1/1.
- `dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-build --filter "FullyQualifiedName~LegacyVmxExecutionUnit"`
  Result: passed 5/5.
- `dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-build --filter "FullyQualifiedName~DoesNotDeriveAdmissionDomainsFromVmcsFields"`
  Result: passed 1/1.

## Residual Risk

- `VmxExecutionUnit` remains quarantined and still owns broad VMX frontend behavior, including VMX instruction resolution/retire, VMCS access routing, intercept exit publication, event delivery calls, and Lane6/Lane7 extended-state paths.
- Intercept/event admission now lacks VMCS-field-derived domain tags; future tagged routing must come from generic domain-admission descriptors instead of restoring the removed frontend helper.
- `VmcsManager` remains the next heavy VMCS state anchor.
