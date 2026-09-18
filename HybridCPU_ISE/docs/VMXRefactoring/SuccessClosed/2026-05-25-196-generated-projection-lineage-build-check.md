# 2026-05-25-196 generated projection lineage build check

Date: 2026-05-25

Status: closed

## Rule / Basis

VMX is a frozen compatibility frontend, not the virtualization architecture. Generated VMCS/compat artifacts may remain only as schema, ABI, projection, or conformance vocabulary. A checked-in generated file is not enough evidence by itself: the build must prove source schema to generated output lineage and fail on drift.

This closure follows `audit2.md` and `ОСНОВЫ и ПРАВИЛА VMX.md`: do not trust `generated`, `projection`, or `descriptor-backed` claims without executable verification.

## Slice Closed

Closed the generated projection lineage proof slice for:

- `Core/VMX/Compatibility/Generated/VmcsProjection/VmcsFieldProjectionSchema.cs`
- `Core/VMX/Compatibility/Generated/AliasMaps/CompatAliasMap.cs`
- `docs/VMXRefactoring/schemas/vmcs-field-projection-schema.v1.json`
- `docs/VMXRefactoring/schemas/compat-alias-schema.v1.json`

## What Changed

- Added concrete VMCS field projection entries to `vmcs-field-projection-schema.v1.json`.
- Added `tools/VMXProjectionLineage/VerifyProjectionLineage.ps1`.
- Added the `VerifyVmxProjectionLineage` MSBuild target before `CoreCompile`.
- The verifier regenerates expected C# for `VmcsFieldProjectionSchema` and `CompatAliasMap`, compares it against checked-in generated sources, writes expected artifacts under `obj`, and fails the build on drift.
- Added `GeneratedProjectionLineageBuildContract`.
- Added focused conformance in `VmxProjectionSchemaAndQuarantineTests`.

## What Was Not Moved Or Recreated

No runtime owner was created. No manager, adapter, VMCS field store, descriptor scalar cache, or runtime generator was introduced.

Specifically not introduced:

- `VmcsManager`
- `IVmcsManager`
- `VmcsManagerAdapter`
- `VmcsProjectionRuntimeManager`
- `VmcsV2RuntimeManager`
- VMCS region dictionary
- active VMCS pointer
- VMCS scalar field store

## Remaining VMCS / VMCSv2 Names

The remaining VMCS/VMCSv2 names in this slice are compatibility vocabulary only:

- JSON schema identifiers.
- generated/read-only C# projection tables.
- frozen alias names.
- conformance contract and test vocabulary.
- build-time verifier path names.

They do not own execution-domain authority, VM-entry/VM-exit authority, capability authority, evidence policy, completion routing, memory-domain state, lane state, checkpoint state, or retire publication.

## Live Generic Responsibilities

Live runtime responsibilities remain with existing neutral owners:

- `ExecutionDomainDescriptor` for execution state.
- `MemoryDomainDescriptor` and memory-domain services for memory state.
- `VectorStreamDomainRuntime` for vector-stream execution.
- `DomainCheckpointImage` / `RestoreValidationService` for checkpoint validation.
- typed capability/evidence/runtime boundary services for authority checks.

This task did not add a new neutral runtime owner because the slice is build-time projection lineage proof only.

## Conformance / Static Checks Added

- `GeneratedProjectionLineageBuildContract` declares the required build property, target, script, schema inputs, generated outputs, verifier functions, and drift failure markers.
- `VmxProjectionSchemaAndQuarantineTests.GeneratedProjectionLineage_BuildTargetRegeneratesVmcsFieldSchemaAndCompatAliases` verifies:
  - MSBuild target exists and runs before `CoreCompile`;
  - verifier script regenerates VMCS field projection and compat alias outputs;
  - drift failure markers are present;
  - schema entries are concrete, including `GuestPc`, `ExitReason`, `EptViolationQualification`, `VMREAD`, `VMWRITE`, and `VmxCompletion`;
  - checked-in generated C# has static entry tables, not runtime file/process generation;
  - no lineage-specific VMCS runtime owner replacement appears in production source.

## Verification

Baseline:

- `rg --files ...\HybridCPU_ISE\Legacy\VMX`: no files.
- `dotnet build HybridCPU_ISE.csproj --no-restore`: passed; verifier printed `VMX projection lineage verified`.

After code step:

- `dotnet build HybridCPU_ISE.csproj --no-restore`: passed, 54 existing warnings, 0 errors.
- `dotnet build HybridCPU_ISE.Tests.csproj --no-restore`: passed, 93 existing warnings, 0 errors.

Targeted tests:

- `FullyQualifiedName~VmxProjectionSchemaAndQuarantineTests`: passed, 38/38.
- `FullyQualifiedName~GeneratedProjectionLineage`: passed, 1/1.
- `FullyQualifiedName~CoreVmxAuthorityBoundaryTests`: passed, 1/1.
- `FullyQualifiedName~RemovedLegacyVmxExecutionUnit`: passed, 15/15.
- `FullyQualifiedName~LegacyVmcsManager`: passed, 4/4.
- `FullyQualifiedName~VmcsV2`: passed, 5/5.
- `FullyQualifiedName~Checkpoint`: passed, 2/2.
- `FullyQualifiedName~Migration`: passed, 8/8.
- `FullyQualifiedName~VectorStream`: passed, 1/1.
- `FullyQualifiedName~DirtyLog`: no matching tests.

Known unrelated broad-filter failures:

- `FullyQualifiedName~DmaStreamCompute`: failed 5 existing repository-shape/native NonRTL runtime checks, including missing `Core/Execution/DmaStreamCompute/DmaStreamComputeRuntime.cs`.
- `FullyQualifiedName~Retire`: failed 4 existing broad checks, including missing `Documentation/operational-semantics.md` and existing stream/compat/native-DMA boundary scans.

## Residual Risk

This closes build-time lineage proof only for `VmcsFieldProjectionSchema` and `CompatAliasMap`.

Still open before VMX freeze:

- VmxCaps bit schema needs equivalent executable regeneration/drift proof.
- Capability authority still has mask/cache pressure and needs grant-first ownership.
- Memory translation vocabulary still exposes VMX/NPT/VPID-shaped backing names.
- Lane6 host-token evidence still needs migration/rebuild proof.
- Generic nested-domain checkpoint/projection ownership is still missing.
- Generic substrate still physically lives under `Core/VMX/Substrate`.

## Next Heavy Step

Extend generated lineage proof to VmxCaps capability-bit schema and any remaining generated projection artifacts, or close the capability-mask pressure by moving `CapabilityDescriptorSet` toward a grant-first canonical source with bitmap masks as projection/cache only.
