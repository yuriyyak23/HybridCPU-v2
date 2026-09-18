# 2026-05-25-197 VmxCaps and generated artifact lineage build check

Date: 2026-05-25

Status: closed

## Rule / Basis

VMX remains a frozen compatibility frontend. Generated/projection artifacts may not self-report generation without executable lineage proof. Compatibility projection vocabulary is allowed, but it cannot become capability authority, evidence authority, VMCS field-store authority, completion authority, or runtime ownership.

This closure follows `audit2.md` and `ОСНОВЫ и ПРАВИЛА VMX.md`: generated means schema-to-output regeneration verified by build, or the surface must be explicitly projection-contract only.

## Slice Closed

Extended the existing build-time lineage proof to:

- `docs/VMXRefactoring/schemas/vmxcaps-capability-bit-schema.v1.json`
- `Core/VMX/Substrate/Capabilities/DescriptorSet/CapabilityDescriptorSetSchema.cs`
- `docs/VMXRefactoring/schemas/compat-spec-artifact-schema.v1.json`
- `Core/VMX/Compatibility/Generated/SpecArtifacts/CompatSpecArtifactSet.cs`

## What Changed

- Added concrete VmxCaps capability-bit entries to `vmxcaps-capability-bit-schema.v1.json`.
- Added `compat-spec-artifact-schema.v1.json`.
- Extended `tools/VMXProjectionLineage/VerifyProjectionLineage.ps1` with:
  - `New-VmxCapsCapabilityBitSchemaSource`;
  - `New-CompatSpecArtifactSetSource`;
  - regenerated-output comparison for `CapabilityDescriptorSetSchema.cs`;
  - regenerated-output comparison for `CompatSpecArtifactSet.cs`.
- Extended `CompatSpecArtifactSet` with `CompatSpecArtifactLineageKind`, `LineageSource`, and `HasExecutableLineage(...)`.
- Kept executable generated lineage only for current artifacts with build proof:
  - VMCS field aliases via `VmcsFieldProjectionSchema`;
  - VmxCaps capability projection metadata via `CapabilityDescriptorSetSchema`;
  - `CompatAliasMap`.
- Marked `CompletionProjectionService` as `ProjectionContractOnly`, not executable generated output.
- Extended `GeneratedProjectionLineageBuildContract` and focused projection/quarantine tests.

## What Was Not Created

No runtime owner was created. No VMX/VMCS manager, adapter, runtime generator, field store, capability manager, or replacement carrier was introduced.

Specifically not introduced:

- `VmcsManager`
- `IVmcsManager`
- `VmcsManagerAdapter`
- `VmcsProjectionRuntimeManager`
- `VmcsV2RuntimeManager`
- VMCS scalar field store
- VmxCaps capability authority service
- Completion projection runtime owner

## Authority Boundary

This slice proves generated lineage for compatibility metadata only. It does not make `VmxCaps`, `CapabilityDescriptorSetSchema`, or `CompatSpecArtifactSet` the source of authority.

Live authority remains with existing generic or neutral boundaries:

- typed capability grants and capability admission policy;
- `CapabilityDescriptorSet` as current transitional descriptor/cache surface;
- runtime boundary admission services;
- completion records and completion routing;
- evidence policies.

The remaining capability-mask risk is unchanged: bitmap masks still need a future grant-first authority cleanup before VMX freeze.

## Conformance / Static Checks

The updated conformance verifies:

- the MSBuild target still runs before `CoreCompile`;
- the verifier script includes VmxCaps and compat spec artifact generators;
- VmxCaps schema has concrete entries including `VmPtrSt` and `NestedVmx`;
- the compat spec schema distinguishes `ExecutableGeneratedSource` and `ProjectionContractOnly`;
- checked-in generated outputs do not perform runtime file/process generation;
- `VmcsFieldAliases`, `CapabilityProjection`, and `CompatAliasMap` have executable lineage;
- `CompletionProjectionService` is ABI-frozen but not falsely claimed as executable generated output;
- no lineage-specific VMCS runtime owner replacement appears in production source.

## Verification

Build:

- `dotnet build HybridCPU_ISE.csproj --no-restore`: passed; verifier printed parity for VMCS field schema, compat alias map, VmxCaps bit schema, and compat spec artifact manifest; 54 existing warnings, 0 errors.
- `dotnet build HybridCPU_ISE.Tests.csproj --no-restore`: passed; verifier ran; 93 existing warnings, 0 errors.

Targeted tests:

- `FullyQualifiedName~GeneratedProjectionLineage`: passed, 1/1.
- `FullyQualifiedName~VmxProjectionSchemaAndQuarantineTests`: passed, 38/38.
- `FullyQualifiedName~VmxCaps`: passed, 6/6.
- `FullyQualifiedName~CoreVmxAuthorityBoundaryTests`: passed, 1/1.
- `FullyQualifiedName~RemovedLegacyVmxExecutionUnit`: passed, 15/15.
- `FullyQualifiedName~LegacyVmcsManager`: passed, 4/4.
- `FullyQualifiedName~VmcsV2`: passed, 5/5.

Known unrelated broad-filter failures:

- `FullyQualifiedName~DmaStreamCompute`: failed 5 existing repository-shape/native NonRTL checks, including missing `Core/Execution/DmaStreamCompute/DmaStreamComputeRuntime.cs`.
- `FullyQualifiedName~Retire`: failed 4 existing documentation/stream/compat/native-DMA boundary checks, including missing `Documentation/operational-semantics.md`.

## Residual Risk

- Capability authority is still mask-first/cache-shaped and needs grant-first cleanup.
- Memory translation still exposes VMX/NPT/VPID-shaped vocabulary.
- Lane6 host-token evidence still needs migration/rebuild proof.
- Generic nested-domain projection/checkpoint ownership is still missing.
- Generic substrate still physically lives under `Core/VMX/Substrate`.
- Future generated/projection artifacts must either join the build-time verifier or remain explicitly projection-contract only.

## Next Heavy Step

Close capability-mask pressure: move `CapabilityDescriptorSet` toward a grant-first canonical typed grant collection, leaving `GlobalHardwareCaps`, `RuntimeEnabledCaps`, `DomainGrantedCaps`, `EffectiveCaps`, and `KnownVmxV2CompatibilityMask` as compatibility projection/cache only.
