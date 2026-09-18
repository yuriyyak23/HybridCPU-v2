# Closure 226: Conformance Retained/Freeze Evidence Deletion

Date: 2026-05-26

## Selected Slice

Decouple the retained compatibility surface and freeze-readiness evidence from compiled `Legacy/VMX/Conformance` sources.

This was the first open legacy-removal blocker after closure `224`: the full conformance folder move-away probe still failed tests on `LegacyVmxRetainedCompatibilitySurfaceInventoryContract`, `LegacyVmxRetainedCompatibilityInventoryEntry`, `LegacyVmxFreezeReadinessCertificationContract`, and `LegacyVmxFreezeReadinessInventoryEntry`.

## Direct Dependencies Found

Compiled test dependencies were limited to:

- `HybridCPU_ISE.Tests/VmxRefactoring/VmxProjectionSchemaAndQuarantineTests.cs`, retained-surface assertions;
- `HybridCPU_ISE.Tests/VmxRefactoring/VmxProjectionSchemaAndQuarantineTests.cs`, freeze-readiness assertions.

No production caller used those contracts. No other C# source outside the two deleted conformance files and the test evidence path referenced the retained/freeze contract types.

## Changed

Deleted from `Legacy/VMX/Conformance/AuthorityBoundary`:

- `LegacyVmxRetainedCompatibilitySurfaceInventoryContract.cs`;
- `LegacyVmxFreezeReadinessCertificationContract.cs`.

Added:

- `HybridCPU_ISE.Tests/VmxRefactoring/VmxRetainedSurfaceAndFreezeEvidenceContracts.cs`.

The new file is test-local static path/marker evidence. It does not create a runtime owner, compatibility backend, VMCS field store, active pointer state, manager, adapter, or VMX execution path.

## Production Authority

Production authority is unchanged:

- `Core/Runtime/*` remains the source of truth for domains, capabilities, memory, I/O, lanes, nested state, completion, retire, and evidence policy.
- `Core/VMX/Compatibility/*` remains frozen compatibility vocabulary and generated/read-only or denied/fail-closed projection.
- No legacy vocabulary was returned to `Core/VMX`.
- No `VmxExecutionUnit`, `VmcsManager`, or `IVmcsManager` was restored.

## Counts

- `Core/VMX` legacy-marked C# sources: `0`.
- `Legacy/VMX/Compatibility` C# sources: `0`.
- `Legacy/VMX/Conformance` C# sources: `28`.
- Total `Legacy/VMX` C# sources: `28`.
- Heavy carrier files (`VmxExecutionUnit.cs`, `VmcsManager.cs`, `IVmcsManager.cs`): `0`.

## Move-Away Probe

Full `Legacy/VMX/Conformance` move-away probe was repeated after the deletion:

- production build while folder was moved away: exit `0`;
- tests build while folder was moved away: exit `1`;
- folder restored in `finally`: yes.

First reported remaining blockers:

- `CapabilityProjectionPlacementServiceSubstrateExtractionContract`;
- `CoreVmxSubstrateResidualExtractionContract`;
- `FinalFrozenAliasQuarantineContract`.

This proves the retained/freeze blocker was removed, while whole-folder deletion remains test-blocked by the next evidence pool.

## Verification

- `dotnet build HybridCPU_ISE/HybridCPU_ISE.csproj --no-restore`: passed, existing warnings only.
- `dotnet build HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-restore`: passed, existing warnings only.
- `dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~VmxProjectionSchemaAndQuarantineTests"`: passed `58/58`.
- `dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~Vmx"`: passed `473/473`.
- Static scan: deleted retained/freeze contracts no longer exist under `Legacy/VMX/Conformance`.

## Residual Risk

The conformance folder remains compiled by tests. The remaining blockers are now capability/substrate extraction evidence, frozen-alias quarantine evidence, nested composition evidence, VMCS/shadow evidence, and remaining execution-unit removal proof contracts.

## Next Step

Continue the conformance evidence decoupling pass with the next small pool:

1. `CapabilityProjectionPlacementServiceSubstrateExtractionContract`;
2. `CoreVmxSubstrateResidualExtractionContract`;
3. `FinalFrozenAliasQuarantineContract`.

The target remains full deletion of `Legacy/VMX/Conformance` without moving legacy authority into actual VMX.
