# 177 VmxExecutionUnit Intercept Exit Publication Removed

Date: 2026-05-25

Status: closed

## Rule / Basis

VMX is a frozen compatibility frontend, not the virtualization architecture. Intercept exits must map to generic domain-trap publication and common VM-exit completion; the heavy legacy VMX frontend must not own standalone VMCS/trace publication for intercept exits.

## Changed

- Kept `Legacy/VMX/Compatibility/Frontend/Handlers/VmxExecutionUnit.cs` quarantined.
- Removed the standalone `_vmcs.RecordInterceptExit(...)` call from `ApplyInterceptExit(...)`.
- Removed the standalone `VmxEventKind.InterceptExit` trace emission from `ApplyInterceptExit(...)`.
- Kept VMX instruction behavior on the existing `CompleteQualifiedVmExit(...)` common retire path.
- Added `LegacyVmxExecutionUnitInterceptPublicationRemovalContract`.
- Extended `VmxProjectionSchemaAndQuarantineTests` with `LegacyVmxExecutionUnit_DoesNotPublishStandaloneInterceptExits`.
- Updated `docs/VMXRefactoring/audit.md`.
- Updated `docs/VMXRefactoring/2026-05-24-vmx-current-model-completion-audit.md`.
- Updated `docs/VMXRefactoring/ОСНОВЫ и ПРАВИЛА VMX.md`.

## Verified

- Static conformance proves `VmxExecutionUnit.cs` has no `RecordInterceptExit` or `VmxEventKind.InterceptExit` markers.
- `LegacyVmxQuarantineManifest` still keeps `VmxExecutionUnit.cs` quarantined.
- `CoreVmxAuthorityBoundaryTests` still pass.

## Build / Test Result

- `dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore`
  Result: succeeded, 54 existing warnings, 0 errors.
- `dotnet build "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-restore`
  Result: succeeded, 93 existing warnings, 0 errors.
- `dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-build --filter "FullyQualifiedName~VmxProjectionSchemaAndQuarantineTests"`
  Result: passed 19/19.
- `dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-build --filter "FullyQualifiedName~CoreVmxAuthorityBoundaryTests"`
  Result: passed 1/1.
- `dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-build --filter "FullyQualifiedName~LegacyVmxExecutionUnit"`
  Result: passed 6/6.
- `dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-build --filter "FullyQualifiedName~DoesNotPublishStandaloneInterceptExits"`
  Result: passed 1/1.

## Residual Risk

- `VmxExecutionUnit` remains quarantined and still owns broad VMX frontend behavior, including VMX instruction resolution/retire, VMCS access routing, event delivery calls, and Lane6/Lane7 extended-state paths.
- Intercept exit completion still flows through the legacy common VM-exit completion helper until a generic domain-trap publication/completion service is wired.
- `VmcsManager` remains the next heavy VMCS state anchor.
