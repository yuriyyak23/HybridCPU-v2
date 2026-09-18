# 174 VmxExecutionUnit Invalidation Authority Removed

Date: 2026-05-24

Status: closed

## Rule / Basis

VMX is a frozen compatibility frontend, not the virtualization architecture. `VmxExecutionUnit` must not own host invalidation authority, IOMMU authority, EPT/VPID epoch state, or runtime legality. Heavy legacy must be deleted in small authority slices without returning the legacy handler to Core.

## Changed

- Kept `Legacy/VMX/Compatibility/Frontend/Handlers/VmxExecutionUnit.cs` quarantined.
- Removed frontend-owned `EptInvalidationEpoch` and `VpidInvalidationEpoch`.
- Removed the old `ResolveInvalidation(...)` path that decoded INVEPT/INVVPID payloads into a host invalidation retire effect.
- Removed retire-time `IOMMU.ApplyVmxInvalidation(...)` from `VmxExecutionUnit`.
- Made INVEPT/INVVPID resolve and retire fail closed with `VmExitReason.SecurityPolicyViolation`.
- Added `LegacyVmxExecutionUnitInvalidationRemovalContract`.
- Extended `VmxProjectionSchemaAndQuarantineTests` with static source checks and runtime fail-closed checks for INVEPT/INVVPID.
- Updated `docs/VMXRefactoring/audit.md`.
- Updated `docs/VMXRefactoring/2026-05-24-vmx-current-model-completion-audit.md`.

## Verified

- Static conformance proves `VmxExecutionUnit.cs` has no `IOMMU.`, `ApplyVmxInvalidation`, `EptInvalidationEpoch`, `VpidInvalidationEpoch`, `AdvanceRuntimeEpoch(`, `ResolveInvalidation(`, `VmxRetireEffect.Invalidation`, or `DecodeInvalidationScope` markers.
- Runtime conformance proves INVEPT and INVVPID resolve to `SecurityPolicyViolation` and retire as faulted outcomes.
- `LegacyVmxQuarantineManifest` still keeps `VmxExecutionUnit.cs` quarantined.
- `CoreVmxAuthorityBoundaryTests` still pass.

## Build / Test Result

- `dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore`
  Result: succeeded, 54 existing warnings, 0 errors.
- `dotnet build "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-restore`
  Result: succeeded, 36 existing warnings, 0 errors.
- `dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-build --filter "FullyQualifiedName~VmxProjectionSchemaAndQuarantineTests"`
  Result: passed 16/16.
- `dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-build --filter "FullyQualifiedName~CoreVmxAuthorityBoundaryTests"`
  Result: passed 1/1.
- `dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-build --filter "FullyQualifiedName~LegacyVmxExecutionUnit"`
  Result: passed 3/3.

## Residual Risk

- `VmxExecutionUnit` remains quarantined and still owns broad VMX frontend behavior, VMCS access routing, intercept/event handling, and compatibility translation snapshot construction.
- INVEPT/INVVPID semantics are intentionally fail-closed until they can be routed through generic runtime-owned memory/IOTLB invalidation admission.
- `VmcsManager` remains the next heavy VMCS state anchor.
