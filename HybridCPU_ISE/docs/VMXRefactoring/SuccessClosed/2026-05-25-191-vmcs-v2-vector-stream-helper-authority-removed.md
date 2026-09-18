# 2026-05-25-191 - VMCSv2 vector-stream helper authority removed

Date: 2026-05-25

Status: closed

## Rule / Basis

VMX is a frozen compatibility frontend, not the virtualization architecture. VMCS/VMCSv2 names may remain only as frozen ABI vocabulary, generated/read-only projection, explicit legacy quarantine, or conformance vocabulary. Lane/vector-stream authority belongs to generic runtime/domain descriptors and runtime admission, not to `VmcsV2Descriptor`, VMCS blocks, or compatibility helper classes.

The `audit2.md` risk being reduced here is the remaining VMCSv2/checkpoint/vector compatibility surface after `VmxExecutionUnit` and `VmcsManager` were deleted. The goal is not a freeze declaration; it is one smaller proof that VMCSv2-shaped executable authority does not silently survive under a helper name.

## Selected slice

Slice C: VMCSv2 vector-stream helper authority.

Dependency classification:
- Frozen compatibility/projection vocabulary: `VmcsV2Descriptor`, `VmcsV2Blocks`, `VmcsV2BlockDirectory`, `VmcsFieldProjectionSchema`, `VmcsV2DescriptorProjection`, `VmxDirtyLogProjectionTypes`, `VmcsCompatMapper`, and VMCS/VMX schema aliases remain vocabulary/projection/checkpoint surfaces, not a freeze claim.
- Mutable VMCS-shaped authority removed in this slice: descriptor-owned vector save/restore, vector dirty marking, VMCS-shaped stream validation evidence, and guest stream descriptor parsing formerly exposed by vector-stream helper classes.
- Generic runtime responsibility already owned elsewhere: vector-stream runtime admission remains with `VectorStreamDomainRuntime`; native Lane6/Lane7 behavior remains with the existing generic runtimes and compatibility guest admission stays fail-closed before backend/token/result effects.
- Test-only or historical behavior: old success-path VMX/vector helper behavior is not treated as production necessity.

## Removed without replacement

Deleted:
- `NonRTL/Core/Execution/VectorStream/VmxVectorStreamStateManager.cs`
- `NonRTL/Core/Execution/VectorStream/VmxStreamDescriptorValidator.cs`

This also removes the helper-only request/result/evidence/parser surface from the validator file:
- `VmxStreamDescriptorValidationRequest`
- `VmxStreamDescriptorValidationResult`
- `VmxStreamDescriptorValidationEvidence`
- `VmxGuestStreamDescriptorParser`
- `VmxGuestStreamDescriptor`

No replacement manager, adapter, renamed runtime owner, active VMCS pointer store, VMCS field store, descriptor-owned vector dirty sink, or stream validation evidence bridge was introduced.

## Remaining allowed vocabulary

The following names may still exist only as compatibility/projection/checkpoint/conformance vocabulary while their mutable authority remains under audit:
- `VmcsV2Descriptor`
- `VmcsV2Blocks`
- `VmcsV2BlockDirectory`
- `VmcsV2DescriptorProjection`
- `VmxCheckpointImage`
- `VmxDirtyLogProjectionTypes`
- `VmxStreamDescriptorFaultInfo`
- `VmcsLifecycleResults`

## Generic owners left in place

Live generic responsibilities were not moved into a new VMCS owner:
- `VectorStreamDomainRuntime` remains the neutral vector-stream runtime/admission owner and has no `VmcsV2Descriptor` dependency.
- `DmaStreamComputeRuntime`, `ExternalAcceleratorRuntime`, and `RuntimeBoundaryAdmissionService` remain existing generic runtime boundaries for their domains.

## Conformance

Added:
- `Core/VMX/Conformance/AuthorityBoundary/VmcsV2VectorStreamProjectionAuthorityRemovalContract.cs`

Updated:
- `HybridCPU_ISE.Tests/VmxRefactoring/VmxProjectionSchemaAndQuarantineTests.cs`

The focused conformance proves:
- the removed helper source files are absent;
- `Legacy/VMX` remains empty;
- `VmxExecutionUnit`, `VmcsManager`, and `IVmcsManager` remain absent through existing tests;
- `VmcsV2DescriptorProjection` remains read-only/authoritative-mutation-denied vocabulary;
- production sources do not reintroduce the removed vector helper markers;
- no new VMCSv2 runtime/projection manager marker is introduced;
- `VectorStreamDomainRuntime` remains independent of `VmcsV2Descriptor`.

## Verification

Baseline before the code change:
- `rg --files HybridCPU_ISE/Legacy/VMX`: no files.
- `dotnet build HybridCPU_ISE/HybridCPU_ISE.csproj --no-restore`: passed with existing warnings.

After removal/conformance:
- `dotnet build HybridCPU_ISE/HybridCPU_ISE.csproj --no-restore`: passed with existing warnings.
- `dotnet build HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-restore`: passed with existing warnings.
- `dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~VmxProjectionSchemaAndQuarantineTests"`: passed.
- `dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~VmcsV2VectorStreamProjectionAuthorityHelpers"`: passed.
- `dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~VmcsV2"`: passed.
- `dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~VectorStream"`: passed.
- `dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~CoreVmxAuthorityBoundaryTests"`: passed.
- `dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~RemovedLegacyVmxExecutionUnit"`: passed.
- `dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~LegacyVmcsManager"`: passed.
- `dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~Migration"`: passed.
- `dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~Checkpoint"`: no matching tests.
- `dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~DirtyLog"`: no matching tests.

## Residual risk

This closure does not freeze VMCSv2. Remaining open risks:
- `VmcsV2Descriptor` scalar writes and migration restore helpers still require read-only/denied projection audit.
- `VmcsV2Blocks` still exposes VMCS-shaped vector/dirty/checkpoint block vocabulary.
- `VmxCheckpointImage` still serializes/restores VMCS-shaped scalar/vector/dirty/checkpoint state and must be reduced or denied.
- `MemoryTranslationControl` still exposes NPT/VPID/VMCS-shaped backing names.
- `CapabilityDescriptorSet` still has bitmap mask compatibility-cache pressure.
- Generated projection still needs a real build-time generator.
- Generic nested projection/checkpoint ownership is still missing.
- Generic substrate still physically lives mostly under `Core/VMX/Substrate`.

## Next heavy step

Audit and reduce `VmxCheckpointImage` plus `VmcsV2Descriptor` scalar/checkpoint restore authority: either convert VMCS-shaped checkpoint/scalar blocks into explicit read-only/denied compatibility projection over neutral checkpoint descriptors, or remove dead scalar/checkpoint helper authority without replacement.
