# 2026-05-25-195 - VMCSv2 scalar projection cache denied

Date: 2026-05-25

Status: closed

## Rule / Basis

VMX is a frozen compatibility frontend, not the virtualization architecture. VMCS/VMCSv2 vocabulary may remain as frozen ABI, generated/read-only projection, schema, and conformance vocabulary only. A VMCS-shaped scalar cache must not become the runtime source of truth for execution domains, memory domains, completion routing, checkpoint state, evidence policy, grants, or retire publication.

The active audit target was the residual internal scalar projection cache in `VmcsV2Descriptor` after task `194` removed the public scalar write API. This closure removes the descriptor-owned scalar cache; it does not declare VMCSv2 freeze.

## Selected slice

`VmcsV2Descriptor` internal scalar projection cache:
- `_scalarValues`
- `_scalarWritten`
- `WriteKnownScalar(...)`
- `HasScalarFieldValue(...)`
- `TryGetScalarFieldValue(...)`

Dependency classification:
- Frozen compatibility/projection vocabulary: `VmcsField`, `VmcsFieldProjectionSchema`, `VmcsV2DescriptorProjection`, and `TryReadScalarField` remain compatibility/schema/read-only-or-denied vocabulary.
- Mutable VMCS-shaped authority removed: descriptor-owned scalar arrays, scalar cache write helper, scalar cache read helpers, and VMCS-shaped scalar read fallback.
- Generic runtime responsibility already owned elsewhere: execution-domain state, memory-domain state, completion records, checkpoint validation, evidence policy, and lane/vector runtime state remain owned by neutral descriptors/services or fail closed.
- Test-only or historical behavior: no production caller required the cache helpers; conformance now treats any cache/store reintroduction as retired historical behavior.

## Removed / Denied Without Replacement

Removed from `NonRTL/Core/System/Vmcs/V2/VmcsV2Descriptor.cs`:
- `_scalarValues`
- `_scalarWritten`
- `WriteKnownScalar(...)`
- `HasScalarFieldValue(...)`
- `TryGetScalarFieldValue(...)`
- cache clearing in `ResetForClear(...)`

Changed:
- `TryReadScalarField(...)` now remains only as compatibility ABI and fails closed with an explicit denial: scalar VMREAD requires generated read-only projection over neutral descriptors.
- Existing descriptor update paths still advance the descriptor invalidation epoch when typed blocks change, but they no longer publish through a VMCS scalar cache.

No VMCS scalar projection store, scalar read cache, field store, projection runtime manager, `VmcsV2RuntimeManager`, `VmcsManager` adapter, or renamed compatibility runtime owner was introduced.

## Remaining Allowed Vocabulary

The following names remain only as compatibility/projection/conformance vocabulary:
- `VmcsField`
- `VmcsFieldProjectionSchema`
- `VmcsV2DescriptorProjection`
- `VmcsV2Descriptor`
- `TryReadScalarField`

`TryReadScalarField` is explicitly denied until a generated read-only projection over neutral owners is available. It is not a runtime state owner.

## Generic Owners Left In Place

Live generic responsibilities were not moved into a new VMCS owner:
- `ExecutionDomainDescriptor` remains the intended owner for execution state.
- `MemoryDomainDescriptor` remains the intended owner for memory state.
- `CompletionRecord` / `CompletionProjectionService` remain the intended completion projection boundary.
- `DomainCheckpointImage` / `RestoreValidationService` remain checkpoint validation boundaries.
- `RuntimeBoundaryAdmissionService` remains the neutral runtime admission boundary.

## Conformance

Added:
- `Core/VMX/Conformance/AuthorityBoundary/VmcsV2ScalarProjectionCacheAuthorityRemovalContract.cs`

Updated:
- `HybridCPU_ISE.Tests/VmxRefactoring/VmxProjectionSchemaAndQuarantineTests.cs`

The focused conformance proves:
- `_scalarValues` and `_scalarWritten` are absent by reflection and source scan;
- `WriteKnownScalar`, `HasScalarFieldValue`, and `TryGetScalarFieldValue` are absent by reflection and source scan;
- `TryReadScalarField` remains present only as compatibility ABI and returns an explicit cache-removed `AccessDenied` result;
- the VMCS field schema remains compatibility owner/access/evidence vocabulary;
- no scalar projection cache/store, scalar read store, VMCS field store, projection runtime manager, or `VmcsV2RuntimeManager` marker was introduced in production sources;
- `Legacy/VMX` remains empty.

## Verification

Baseline / code verification:
- `rg --files HybridCPU_ISE/Legacy/VMX`: no files.
- `dotnet build HybridCPU_ISE/HybridCPU_ISE.csproj --no-restore`: passed before code changes.
- `dotnet build HybridCPU_ISE/HybridCPU_ISE.csproj --no-restore`: passed after code changes with existing warnings.
- `dotnet build HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-restore`: passed with existing warnings.

Targeted removal/conformance:
- `dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~VmxProjectionSchemaAndQuarantineTests"`: passed, 37/37.
- `dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~VmcsV2Descriptor_ScalarProjectionCache"`: passed, 1/1.
- `dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~VmcsV2Descriptor_ScalarWriteAuthority"`: passed, 1/1.
- `dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~CoreVmxAuthorityBoundaryTests"`: passed, 1/1.
- `dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~RemovedLegacyVmxExecutionUnit"`: passed, 15/15.
- `dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~LegacyVmcsManager"`: passed, 4/4.
- `dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~VmcsV2"`: passed, 5/5.
- `dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~VmcsFieldProjectionSchema"`: passed, 1/1.
- `dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~Checkpoint"`: passed, 2/2.
- `dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~Migration"`: passed, 8/8.
- `dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~VMWRITE"`: passed, 3/3.
- `dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~VMREAD"`: passed, 9/9.

Final production build:
- `dotnet build HybridCPU_ISE/HybridCPU_ISE.csproj --no-restore`: passed, incremental 0 warnings / 0 errors.

## Known Unrelated Broad-Filter Failures

The repository still has broad-filter failures outside this closure scope:
- `FullyQualifiedName~DmaStreamCompute`: existing repository-shape failures around the missing `Core/Execution/DmaStreamCompute/DmaStreamComputeRuntime.cs`, compiler emission text, and native NonRTL runtime source scans.
- `FullyQualifiedName~Retire`: existing repository-shape/documentation and boundary-scan failures around missing `Documentation/operational-semantics.md`, DmaStreamCompute retire publication shape, direct stream helper boundary scans, and retired compatibility policy identifiers.

## Residual Risk

This closure does not freeze VMCSv2. Remaining open risks:
- Generated projection still needs a real build-time generator.
- Generic nested-domain projection/checkpoint ownership is still missing.
- `CapabilityDescriptorSet` still has bitmap mask compatibility-cache pressure.
- `MemoryTranslationControl` still exposes NPT/VPID/VMCS-shaped backing names.
- Lane6 host-token evidence and migration rebuild proof remain open.
- Generic substrate still physically lives mostly under `Core/VMX/Substrate`.

## Next Heavy Step

Prove generated projection lineage with a real build-time generator or executable regeneration check for `VmcsFieldProjectionSchema` / compatibility alias artifacts, or close the next authority risk in `CapabilityDescriptorSet` by making typed grants primary and bitmap masks projection/cache-only.
