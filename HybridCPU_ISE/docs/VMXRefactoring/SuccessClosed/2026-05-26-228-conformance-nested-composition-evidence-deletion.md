# Closure 228: Conformance Nested-Composition Evidence Deletion

Date: 2026-05-26

## Selected Slice

Decouple and delete the nested-composition evidence pool from compiled `Legacy/VMX/Conformance` sources.

This was the next blocker after closure `227`: the full conformance folder move-away probe reported `NestedDomainProjectionCheckpointOwnerContract` first, followed by VMCS/shadow/manager and related evidence contracts.

## Direct Dependencies Found

Compiled references to `NestedDomainProjectionCheckpointOwnerContract` were limited to:

- `HybridCPU_ISE.Tests/VmxRefactoring/VmxProjectionSchemaAndQuarantineTests.cs`, nested projection/checkpoint owner path and marker assertions;
- `HybridCPU_ISE.Tests/VmxRefactoring/VmxProjectionSchemaAndQuarantineTests.cs`, host-owned checkpoint restore rejection assertion;
- `HybridCPU_ISE.Tests/VmxRefactoring/VmxProjectionSchemaAndQuarantineTests.cs`, vector accelerator nested identity pressure guard.

`NestedCompositionContract` and `ShadowVmcsBridgeRetirementContract` had no remaining compiled callers outside their own deleted files. Remaining mentions are string evidence only:

- `Core/VMX/Conformance/GoldenArtifacts/VirtualizationGoldenArtifactManifest.cs` keeps the artifact name `NestedCompositionContract`;
- `HybridCPU_ISE.Tests/VmxRefactoring/VmxRetainedSurfaceAndFreezeEvidenceContracts.cs` keeps `ShadowVmcsBridgeRetirementContract` as a forbidden marker string.

No production caller used these contracts.

## Changed

Deleted from `Legacy/VMX/Conformance/NestedComposition`:

- `NestedDomainProjectionCheckpointOwnerContract.cs`;
- `NestedCompositionContract.cs`;
- `ShadowVmcsBridgeRetirementContract.cs`.

Added:

- `HybridCPU_ISE.Tests/VmxRefactoring/VmxNestedCompositionEvidenceContracts.cs`.

The new test file is static path/marker and denial evidence only. It does not create a runtime owner, compatibility backend, VMCS field store, active pointer state, manager, adapter, or VMX execution path.

## Production Authority

Production authority is unchanged:

- neutral nested ownership stays under `Core/Runtime/Nested/*` and `Core/Runtime/Migration/*`;
- Shadow VMCS remains generated/read-only and denied/fail-closed compatibility projection;
- no generic runtime state moved into `Legacy/VMX`;
- no legacy vocabulary was returned to `Core/VMX`;
- no `VmxExecutionUnit`, `VmcsManager`, or `IVmcsManager` was restored.

## Counts

- `Core/VMX` legacy-marked C# sources: `0`.
- `Legacy/VMX/Compatibility` C# sources: `0`.
- `Legacy/VMX/Conformance` C# sources: `22`.
- Total `Legacy/VMX` C# sources: `22`.
- Heavy carrier files (`VmxExecutionUnit.cs`, `VmcsManager.cs`, `IVmcsManager.cs`): `0`.

## Verification

Static checks completed:

- deleted nested-composition source files are absent from `Legacy/VMX/Conformance`;
- static reference scan now resolves `NestedDomainProjectionCheckpointOwnerContract` only in the test-local evidence helper and the tests that consume it;
- `NestedCompositionContract` and `ShadowVmcsBridgeRetirementContract` remain only as string evidence.

Build/test verification is currently blocked by unrelated non-VMX compile errors:

- `CloseToHSL/Core/ISA/Instructions/NonVmx/Lane06DmaStream/QueueLifecycle/DscPollInstruction.cs`;
- `DscCancelInstruction.cs`;
- `DscWaitInstruction.cs`;
- `DscFenceInstruction.cs`;
- `DscCommitInstruction.cs`.

The reported errors are duplicate member definitions such as `Mnemonic`, `EvidenceBoundary`, `RequiresQueueAuthority`, `HasScalarOpcodeAllocation`, `IsExecutable`, and `CompilerHelperAllowed`. These are outside VMX and outside this legacy-conformance deletion slice.

Because production build is currently blocked before VMX validation, a fresh full `Legacy/VMX/Conformance` move-away probe was not meaningful after this slice.

## Residual Risk

The conformance folder remains compiled by tests once the unrelated non-VMX build blocker is cleared. Expected next evidence blockers are VMCS/shadow/manager, IOMMU-return, reverse-import, runtime-epoch, and remaining execution-unit removal contracts.

## Next Step

First clear or isolate the unrelated non-VMX QueueLifecycle duplicate-definition baseline. Then continue with the next conformance evidence pool:

1. `LegacyIommuDomainBindingReturnContract`;
2. `LegacyVmcsMemoryTranslationProjectionRemovalContract`;
3. `LegacyShadowVmcsBlockRemovalContract`;
4. `LegacyVmcsManagerVmxPublicationAuthorityRemovalContract`.

The target remains full deletion of `Legacy/VMX/Conformance` without moving legacy authority into actual VMX.
