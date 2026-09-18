# VMX Closure 229: Legacy VMX Conformance Folder Deletion

Date: 2026-05-26

## Selected Slice

Conformance evidence decoupling and deletion for the remaining `Legacy/VMX/Conformance` pool:

- VMCS/shadow/manager evidence;
- IOMMU-return and reverse-import evidence;
- V1 execution adapter return evidence;
- remaining `LegacyVmxExecutionUnit*RemovalContract` evidence;
- `RuntimeEpochAdvanceFailClosedContract`.

## What Changed

Deleted from `Legacy/VMX/Conformance`:

- `LegacyIommuDomainBindingReturnContract.cs`;
- `LegacyReverseImportContract.cs`;
- `LegacyVmcsMemoryTranslationProjectionRemovalContract.cs`;
- `LegacyShadowVmcsBlockRemovalContract.cs`;
- `LegacyVmcsManagerRemovalContract.cs`;
- `LegacyVmcsManagerVmxPublicationAuthorityRemovalContract.cs`;
- `LegacyVmxV1ExecutionAdapterSurfaceReturnContract.cs`;
- all remaining `LegacyVmxExecutionUnit*RemovalContract.cs`;
- `RuntimeEpochAdvanceFailClosedContract.cs`.

Added test-local static/file evidence:

- `HybridCPU_ISE.Tests/VmxRefactoring/VmxVmcsShadowManagerEvidenceContracts.cs`.

After the remaining source files were deleted, the tests were updated to allow the full physical `Legacy/VMX` tree to be absent while still failing on any restored C# carrier. The empty `Legacy/VMX` and `Legacy/VMX-v2` directory trees were then removed physically.

## Authority Boundary

No production authority moved into VMX:

- no `VmxExecutionUnit`;
- no `VmcsManager` or `IVmcsManager`;
- no adapter/manager replacement;
- no VMCS field store;
- no active pointer state;
- no successful VMX backend path.

The new test-local file preserves path and marker assertions only. It is not a runtime owner and does not add production compatibility vocabulary.

## Counts

- `Core/VMX` legacy-marked C# sources: `0`.
- `Legacy/VMX/Compatibility` C# sources: `0`.
- `Legacy/VMX/Conformance` C# sources: `0`.
- Total `Legacy/VMX` C# sources: `0`.
- Physical `Legacy/VMX`: absent.
- Physical `Legacy/VMX-v2`: absent.

## Move-Away Probe

Full `Legacy/VMX/Conformance` move-away probe passed before deletion:

- production build exit: `0`;
- tests build exit: `0`;
- probe restored the folder in `finally`: yes.

This satisfied the prior no-wholesale-delete precondition.

## Verification

Passed:

- `dotnet build HybridCPU_ISE.csproj --no-restore`;
- `dotnet build HybridCPU_ISE.Tests.csproj --no-restore`;
- `dotnet test HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~VmxProjectionSchemaAndQuarantineTests"`: `58/58`;
- `dotnet test HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~Vmx"`: `505/505`;
- static `Core/VMX` legacy-marker scan: `0`;
- static `Legacy/VMX` C# source inventory: `0`;
- physical carrier scan for `VmxExecutionUnit.cs`, `VmcsManager.cs`, `IVmcsManager.cs`: absent.

## Residual Risk

Historical VMX/VMCS/removal vocabulary remains in test-local evidence helpers and docs. That vocabulary is allowed only as static/file evidence for no-reintroduction tests; it must not become runtime authority or new `Core/VMX` ownership.

`VmcsV2Blocks` still contains mutable helper candidates that need the next authority-hardening pass.

## Next Step

Return to the VMCSv2 mutable helper authority audit:

- `EventInjectionBlock.TryQueue/TryDeliver/RestoreSnapshot`;
- interrupt remap configuration helpers;
- `DebugTraceBlock.Record*/ResetCounters`;
- root/NPT/bundle binding helpers.

Each remaining helper should become neutral runtime-owned, denied/fail-closed, or deleted.
