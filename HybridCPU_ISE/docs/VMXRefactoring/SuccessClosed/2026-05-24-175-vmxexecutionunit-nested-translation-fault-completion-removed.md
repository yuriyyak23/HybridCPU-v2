# 175 VmxExecutionUnit Nested Translation Fault Completion Removed

Date: 2026-05-24

Status: closed

## Rule / Basis

VMX is a frozen compatibility frontend, not the virtualization architecture. VMExit projection must map to generic `DomainTrap` / `DomainFault` / `DomainAssist` outcomes; nested translation faults must not be completed or published by a heavy legacy VMX frontend handler.

## Changed

- Kept `Legacy/VMX/Compatibility/Frontend/Handlers/VmxExecutionUnit.cs` quarantined.
- Removed the unused public `CompleteNestedTranslationFault(...)` method without replacement.
- Removed the legacy frontend path that directly called `_vmcs.RecordNestedTranslationExit(...)`.
- Removed the legacy frontend path that completed nested translation VM-exit publication through `_vmcs.CompleteVmExit(...)`, CSR exit writes, and VMX event tracing.
- Added `LegacyVmxExecutionUnitNestedTranslationFaultRemovalContract`.
- Extended `VmxProjectionSchemaAndQuarantineTests` with `LegacyVmxExecutionUnit_DoesNotCompleteNestedTranslationFaults`.
- Updated `docs/VMXRefactoring/audit.md`.
- Updated `docs/VMXRefactoring/2026-05-24-vmx-current-model-completion-audit.md`.
- Updated `docs/VMXRefactoring/ОСНОВЫ и ПРАВИЛА VMX.md`.

## Verified

- Static conformance proves `VmxExecutionUnit.cs` has no `CompleteNestedTranslationFault`, `RecordNestedTranslationExit`, `NestedTranslationResult`, `NestedTranslationStatus`, `translation.CausesVmExit`, `EptMisconfiguration`, or `EptViolation` markers.
- `LegacyVmxQuarantineManifest` still keeps `VmxExecutionUnit.cs` quarantined.
- `CoreVmxAuthorityBoundaryTests` still pass.

## Build / Test Result

- `dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore`
  Result: succeeded, 54 existing warnings, 0 errors.
- `dotnet build "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-restore`
  Result: succeeded, 93 existing warnings, 0 errors.
- `dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-build --filter "FullyQualifiedName~VmxProjectionSchemaAndQuarantineTests"`
  Result: passed 17/17.
- `dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-build --filter "FullyQualifiedName~CoreVmxAuthorityBoundaryTests"`
  Result: passed 1/1.
- `dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-build --filter "FullyQualifiedName~LegacyVmxExecutionUnit"`
  Result: passed 4/4.
- `dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-build --filter "FullyQualifiedName~LegacyVmxExecutionUnit_DoesNotCompleteNestedTranslationFaults"`
  Result: passed 1/1.

## Residual Risk

- `VmxExecutionUnit` remains quarantined and still owns broad VMX frontend behavior, including VMX instruction resolution/retire, VMCS access routing, intercept/event admission, and compatibility translation snapshot construction.
- Nested translation fault behavior now requires a future generic nested-domain/domain-fault routing path; the removed legacy VMX frontend completion API must not be restored as the shortcut.
- `VmcsManager` remains the next heavy VMCS state anchor.
