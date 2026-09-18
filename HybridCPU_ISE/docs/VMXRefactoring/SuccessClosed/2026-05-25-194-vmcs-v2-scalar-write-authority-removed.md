# 2026-05-25-194 - VMCSv2 scalar write authority removed

Date: 2026-05-25

Status: closed

## Rule / Basis

VMX is a frozen compatibility frontend, not the virtualization architecture. VMCS/VMCSv2 vocabulary may remain as frozen ABI, generated/read-only projection, schema, and conformance vocabulary only. A VMCS-shaped scalar field dictionary must not be the runtime source of truth for execution domains, grants, evidence, memory domains, completion routing, checkpoint state, or retire publication.

The active audit target was the remaining public `VmcsV2Descriptor.TryWriteScalarField` path after the `VmcsManager`, checkpoint image, vector helper, and `VmcsV2Blocks` helper removals. This closure removes one executable mutable descriptor authority slice; it does not declare VMCSv2 freeze.

## Selected slice

`VmcsV2Descriptor.TryWriteScalarField` public scalar write authority.

Dependency classification:
- Frozen compatibility/projection vocabulary: `VmcsField`, `VmcsFieldProjectionSchema`, `VmcsV2DescriptorProjection`, and VMCS field identifiers remain as ABI/schema/read-only projection vocabulary.
- Mutable VMCS-shaped authority removed: public descriptor scalar writes, descriptor-owned scalar field validation for writes, guest/compatibility scalar field-store behavior, and the old VMWRITE-like success path on the descriptor itself.
- Generic runtime responsibility already owned elsewhere: runtime admission, domain state, evidence policy, completion routing, checkpoint validation, and memory/lane ownership remain with neutral domain/runtime descriptors and services or fail closed.
- Test-only or historical behavior: no production caller required `TryWriteScalarField`; conformance now treats any reintroduction as a retired historical behavior.

## Removed without replacement

Removed from `NonRTL/Core/System/Vmcs/V2/VmcsV2Descriptor.cs`:
- `public bool TryWriteScalarField(...)`
- `private static bool ValidateScalarWrite(...)`
- the descriptor-owned scalar write validation path for `CanWriteViaVmWrite`, boolean field validation, width validation, and reserved-bit validation.

No VMCS scalar field store, VMCS projection runtime manager, `VmcsV2RuntimeManager`, scalar write service, `VmcsManager` adapter, or renamed compatibility runtime owner was introduced.

## Remaining allowed vocabulary

The following names remain only as compatibility/projection/conformance vocabulary:
- `VmcsField`
- `VmcsFieldProjectionSchema`
- `VmcsV2DescriptorProjection`
- `VmcsV2Descriptor`
- `TryReadScalarField`
- `WriteKnownScalar`

`TryReadScalarField` and `WriteKnownScalar` remain only as current read-side projection/cache mechanics used by existing completion and guest-state projection paths. They are not freeze evidence and remain future generated/read-only projection audit scope.

## Generic owners left in place

Live generic responsibilities were not moved into a new VMCS owner:
- `RuntimeBoundaryAdmissionService` remains the neutral admission boundary for runtime-owned operations.
- `ExecutionDomainDescriptor`, `MemoryDomainDescriptor`, `IoDomainDescriptor`, capability descriptors, evidence policy, checkpoint validation, and lane/vector runtimes remain the intended owners for live state.
- Compatibility VMREAD/VMWRITE routing remains fail closed until generated projection/access-policy admission is wired over neutral owners.

## Conformance

Added:
- `Core/VMX/Conformance/AuthorityBoundary/VmcsV2ScalarWriteAuthorityRemovalContract.cs`

Updated:
- `HybridCPU_ISE.Tests/VmxRefactoring/VmxProjectionSchemaAndQuarantineTests.cs`

The focused conformance proves:
- `VmcsV2Descriptor.TryWriteScalarField` is absent by reflection and source scan;
- former scalar write helper markers are absent from `VmcsV2Descriptor`;
- `VmcsV2DescriptorProjection` remains read-only/denied compatibility projection;
- `VmcsFieldProjectionSchema` remains frozen owner/access/evidence vocabulary;
- no scalar field-store, scalar write service, `VmcsV2RuntimeManager`, or projection runtime manager marker was introduced in production sources;
- `Legacy/VMX` remains empty;
- the removed `VmxExecutionUnit` and `VmcsManager` closure contracts remain enforced by targeted tests.

## Verification

Baseline / code verification:
- `rg --files HybridCPU_ISE/Legacy/VMX`: no files.
- `dotnet build HybridCPU_ISE/HybridCPU_ISE.csproj --no-restore`: passed before code changes.
- `dotnet build HybridCPU_ISE/HybridCPU_ISE.csproj --no-restore`: passed after code changes.
- `dotnet build HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-restore`: passed, 0 warnings / 0 errors.

Targeted removal/conformance:
- `dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~VmxProjectionSchemaAndQuarantineTests"`: passed, 36/36.
- `dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~VmcsV2Descriptor_ScalarWriteAuthority"`: passed, 1/1.
- `dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~CoreVmxAuthorityBoundaryTests"`: passed, 1/1.
- `dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~RemovedLegacyVmxExecutionUnit"`: passed, 15/15.
- `dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~LegacyVmcsManager"`: passed, 4/4.
- `dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~VmcsV2"`: passed, 4/4.
- `dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~VmcsFieldProjectionSchema"`: passed, 1/1.
- `dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~Checkpoint"`: passed, 2/2.
- `dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~Migration"`: passed, 8/8.
- `dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~VMWRITE"`: passed, 3/3.
- `dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~VMREAD"`: passed, 9/9.

Final production build:
- `dotnet build HybridCPU_ISE/HybridCPU_ISE.csproj --no-restore`: passed, 0 warnings / 0 errors.

## Known unrelated broad-filter failures

The repository still has broad-filter failures outside this closure scope:
- `FullyQualifiedName~DmaStreamCompute`: existing repository-shape failures around the missing `Core/Execution/DmaStreamCompute/DmaStreamComputeRuntime.cs`, compiler emission text, and native NonRTL runtime source scans.
- `FullyQualifiedName~Retire`: existing repository-shape/documentation and boundary-scan failures around missing `Documentation/operational-semantics.md`, DmaStreamCompute retire publication shape, direct stream helper boundary scans, and retired compatibility policy identifiers.

## Residual risk

This closure does not freeze VMCSv2. Remaining open risks:
- `VmcsV2Descriptor` still has internal scalar projection cache mechanics (`_scalarValues`, `_scalarWritten`, `WriteKnownScalar`) that need generated/read-only projection audit.
- Generated projection still needs a real build-time generator.
- Generic nested-domain projection/checkpoint ownership is still missing.
- `CapabilityDescriptorSet` still has bitmap mask compatibility-cache pressure.
- `MemoryTranslationControl` still exposes NPT/VPID/VMCS-shaped backing names.
- Lane6 host-token evidence and migration rebuild proof remain open.
- Generic substrate still physically lives mostly under `Core/VMX/Substrate`.

## Next heavy step

Audit and reduce the remaining `VmcsV2Descriptor` internal scalar projection cache (`_scalarValues`, `_scalarWritten`, `WriteKnownScalar`) into generated/read-only projection over neutral descriptors, or explicitly deny the cache where no neutral source exists. Do not introduce a VMCS field store, VMCS projection runtime manager, or renamed VMCS runtime owner.
