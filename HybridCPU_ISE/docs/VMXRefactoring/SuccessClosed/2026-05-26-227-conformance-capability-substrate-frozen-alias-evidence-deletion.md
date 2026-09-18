# Closure 227: Conformance Capability/Substrate/Frozen-Alias Evidence Deletion

Date: 2026-05-26

## Selected Slice

Decouple and delete the capability/substrate extraction and frozen-alias quarantine evidence pool from compiled `Legacy/VMX/Conformance` sources.

This was the first blocker after closure `226`: the full conformance folder move-away probe failed tests on `CapabilityProjectionPlacementServiceSubstrateExtractionContract`, `CoreVmxSubstrateResidualExtractionContract`, and `FinalFrozenAliasQuarantineContract`.

## Direct Dependencies Found

Compiled test dependencies were limited to:

- `HybridCPU_ISE.Tests/VmxRefactoring/VmxProjectionSchemaAndQuarantineTests.cs`, capability projection placement/substrate extraction assertions;
- `HybridCPU_ISE.Tests/VmxRefactoring/VmxProjectionSchemaAndQuarantineTests.cs`, Core VMX substrate residual extraction assertions;
- `HybridCPU_ISE.Tests/VmxRefactoring/VmxProjectionSchemaAndQuarantineTests.cs`, final frozen alias quarantine assertions.

One prior test-local retained-surface inventory entry also referenced `Legacy/VMX/Conformance/AuthorityBoundary/CoreVmxSubstrateResidualExtractionContract.cs` as live conformance evidence; that stale inventory entry was removed because this closure deletes that file.

No production caller used these contracts.

## Changed

Deleted from `Legacy/VMX/Conformance/AuthorityBoundary`:

- `CapabilityProjectionPlacementServiceSubstrateExtractionContract.cs`;
- `CoreVmxSubstrateResidualExtractionContract.cs`;
- `FinalFrozenAliasQuarantineContract.cs`.

Added:

- `HybridCPU_ISE.Tests/VmxRefactoring/VmxCapabilitySubstrateAndAliasEvidenceContracts.cs`.

Updated:

- `HybridCPU_ISE.Tests/VmxRefactoring/VmxRetainedSurfaceAndFreezeEvidenceContracts.cs`, removing the deleted substrate residual contract from its generated debug lifecycle inventory.

The new test file is static path/marker evidence only. It does not create a runtime owner, compatibility backend, VMCS field store, active pointer state, manager, adapter, or VMX execution path.

## Production Authority

Production authority is unchanged:

- neutral `Core/Runtime/*` owners remain the source of truth for memory, I/O, lanes, events, nested state, capabilities, evidence, completion, and retire;
- VMX-shaped aliases remain frozen compatibility projection or denied/read-only vocabulary;
- no generic runtime state moved into `Legacy/VMX`;
- no legacy vocabulary was returned to `Core/VMX`;
- no `VmxExecutionUnit`, `VmcsManager`, or `IVmcsManager` was restored.

## Counts

- `Core/VMX` legacy-marked C# sources: `0`.
- `Legacy/VMX/Compatibility` C# sources: `0`.
- `Legacy/VMX/Conformance` C# sources: `25`.
- Total `Legacy/VMX` C# sources: `25`.
- Heavy carrier files (`VmxExecutionUnit.cs`, `VmcsManager.cs`, `IVmcsManager.cs`): `0`.

## Move-Away Probe

Full `Legacy/VMX/Conformance` move-away probe was repeated after the deletion:

- production build while folder was moved away: exit `0`;
- tests build while folder was moved away: exit `1`;
- folder restored in `finally`: yes.

First reported remaining blockers:

- `NestedDomainProjectionCheckpointOwnerContract`;
- `LegacyIommuDomainBindingReturnContract`;
- `LegacyVmcsMemoryTranslationProjectionRemovalContract`;
- `LegacyShadowVmcsBlockRemovalContract`;
- `LegacyVmxV1ExecutionAdapterSurfaceReturnContract`;
- `LegacyVmcsManagerVmxPublicationAuthorityRemovalContract`.

This proves the capability/substrate/frozen-alias blocker was removed, while whole-folder deletion remains test-blocked by nested-composition and VMCS/shadow/manager evidence.

## Verification

- `dotnet build HybridCPU_ISE/HybridCPU_ISE.csproj --no-restore`: passed, existing warnings only.
- `dotnet build HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-restore`: passed, existing warnings only.
- `dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~VmxProjectionSchemaAndQuarantineTests"`: passed `58/58`.
- `dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~Vmx"`: passed `473/473`.
- Static scan: deleted capability/substrate/frozen-alias contracts no longer exist under `Legacy/VMX/Conformance`.

## Residual Risk

The conformance folder remains compiled by tests. The remaining blockers are nested-composition evidence, IOMMU return evidence, VMCS memory translation/shadow evidence, VMCS manager/publication evidence, and remaining execution-unit proof contracts.

## Next Step

Continue the conformance evidence decoupling pass with the next small pool:

1. `NestedDomainProjectionCheckpointOwnerContract`;
2. related nested-composition contracts;
3. then VMCS/shadow/manager evidence contracts.

The target remains full deletion of `Legacy/VMX/Conformance` without moving legacy authority into actual VMX.
