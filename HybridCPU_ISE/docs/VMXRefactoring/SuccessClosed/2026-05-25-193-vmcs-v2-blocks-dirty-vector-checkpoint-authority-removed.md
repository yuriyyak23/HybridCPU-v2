# 2026-05-25-193 - VMCSv2 blocks dirty/vector/checkpoint authority removed

Date: 2026-05-25

Status: closed

## Rule / Basis

VMX is a frozen compatibility frontend, not the virtualization architecture. VMCS/VMCSv2 names may remain as frozen ABI, generated/read-only projection, schema, and conformance vocabulary only. Dirty tracking, vector-stream runtime state, checkpoint restore, completion routing, evidence, and migration policy must be owned by neutral domain/runtime services or denied.

The active audit target was the remaining `VmcsV2Blocks` dirty/vector/checkpoint helper behavior after task `192`. This closure removes an executable helper authority slice; it does not declare VMCSv2 freeze.

## Selected slice

`VmcsV2Blocks` dirty/vector/checkpoint helper authority.

Dependency classification:
- Frozen compatibility/projection vocabulary: `VectorStreamStateBlock`, `DirtyLogBlock`, `VmxDirtyLogStatus`, and block directory names remain as compatibility/read-only status vocabulary.
- Mutable VMCS-shaped authority removed: VMCSv2 block-owned vector policy configure, stream descriptor binding, vector snapshot capture/restore, checkpoint restore, vector dirty/epoch helpers, vector/stream fault helper publication, dirty-log configure, dirty range mark, snapshot, clear, restore, page-index store, accepted-write accounting, overflow, and generation helpers.
- Generic runtime responsibility already owned elsewhere: vector-stream admission remains with `VectorStreamDomainRuntime`; checkpoint restore validation remains with `DomainCheckpointImage` / `RestoreValidationService`; memory dirty tracking must remain a memory-domain service or fail closed.
- Test-only or historical behavior: no production caller required the removed `VmcsV2Blocks` helper behavior; the only live caller was `VmcsV2Descriptor.ResetForClear` calling `DirtyLog.Configure(...)`, which was removed.

## Removed without replacement

Removed from `Core/VMX/Compatibility/Generated/VmcsProjection/VmcsV2Blocks.cs`:
- `VectorStreamStateBlock.ConfigurePolicy`
- `VectorStreamStateBlock.CanUseSaveMask`
- `VectorStreamStateBlock.BindStreamDescriptorTable`
- `VectorStreamStateBlock.CaptureSnapshot`
- `VectorStreamStateBlock.TryRestoreSnapshot`
- `VectorStreamStateBlock.RestoreCheckpointSnapshot`
- `VectorStreamStateBlock.MarkVectorDirty`
- `VectorStreamStateBlock.AdvanceStreamReplayEpoch`
- `VectorStreamStateBlock.AdvanceStreamQueueEpoch`
- `VectorStreamStateBlock.AdvanceStreamCompletionEpoch`
- `VectorStreamStateBlock.RecordVectorException`
- `VectorStreamStateBlock.RecordStreamDescriptorFault`
- `VectorStreamStateBlock.IsDescriptorAddressInRange`
- `DirtyLogBlock.Configure`
- `DirtyLogBlock.TryMarkDirtyRange`
- `DirtyLogBlock.TrySnapshot`
- `DirtyLogBlock.TryClear`
- `DirtyLogBlock.RestoreSnapshot`
- dirty-log `_dirtyPageIndices` page store and private mutation helpers.

Removed from `NonRTL/Core/System/Vmcs/V2/VmcsV2Descriptor.cs`:
- the `ResetForClear` call to `DirtyLog.Configure(VmxDirtyLogConfiguration.Disabled)`.

No dirty-log manager, vector-stream manager, checkpoint block service, VMCS projection runtime manager, `VmcsV2Runtime`, field store, or renamed VMCS owner was introduced.

## Remaining allowed vocabulary

The following names remain only as compatibility/projection/conformance vocabulary:
- `VmcsV2Blocks`
- `VectorStreamStateBlock`
- `DirtyLogBlock`
- `VmxDirtyLogProjectionTypes`
- `VmcsV2BlockDirectory`
- `VmxDirtyLogStatus`

## Generic owners left in place

Live generic responsibilities were not moved into a new VMCS owner:
- `VectorStreamDomainRuntime` remains the neutral vector-stream admission boundary.
- `DomainCheckpointImage` and `RestoreValidationService` remain neutral checkpoint/restore validation boundaries.
- `IOMMU.VmxCompatibilityAliases.TryAccountNptWriteProtectDirty(...)` remains fail closed.

## Conformance

Added:
- `Core/VMX/Conformance/AuthorityBoundary/VmcsV2BlocksDirtyVectorCheckpointAuthorityRemovalContract.cs`

Updated:
- `HybridCPU_ISE.Tests/VmxRefactoring/VmxProjectionSchemaAndQuarantineTests.cs`

The focused conformance proves:
- former vector/checkpoint helper markers are absent from `VmcsV2Blocks`;
- former dirty-log mutation/page-store helper markers are absent from `VmcsV2Blocks`;
- `VmcsV2Descriptor` no longer calls the removed dirty-log mutator;
- `VmcsV2Blocks` still contains block vocabulary/status projection only;
- neutral vector/checkpoint owners have no `VmcsV2Descriptor` or `VmcsField` dependency;
- the VMX dirty compatibility alias remains fail closed;
- no VMCS dirty/vector/checkpoint runtime owner marker was introduced;
- `Legacy/VMX` remains empty.

## Verification

Baseline / code verification:
- `dotnet build HybridCPU_ISE/HybridCPU_ISE.csproj --no-restore`: passed with existing warnings before code changes.
- `dotnet build HybridCPU_ISE/HybridCPU_ISE.csproj --no-restore`: passed with existing warnings after code changes.
- `dotnet build HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-restore`: passed with existing warnings.

Targeted removal/conformance:
- `dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~VmxProjectionSchemaAndQuarantineTests"`: passed, 35/35.
- `dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~VmcsV2Blocks_DirtyVectorCheckpoint"`: passed, 1/1.
- `dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~CoreVmxAuthorityBoundaryTests"`: passed, 1/1.
- `dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~RemovedLegacyVmxExecutionUnit"`: passed, 15/15.
- `dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~LegacyVmcsManager"`: passed, 4/4.
- `dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~VmcsV2"`: passed, 3/3.
- `dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~Checkpoint"`: passed, 2/2.
- `dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~Migration"`: passed, 8/8.
- `dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~VectorStream"`: passed, 1/1.
- `dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~DirtyLog"`: no matching tests.

Known unrelated broad-filter failures were re-run and remain outside this closure:
- `FullyQualifiedName~DmaStreamCompute`: 5 existing failures, including missing `Core/Execution/DmaStreamCompute/DmaStreamComputeRuntime.cs`, missing compiler emission text, and native NonRTL runtime source boundary scans.
- `FullyQualifiedName~Retire`: 4 existing failures, including missing `Documentation/operational-semantics.md`, existing DmaStreamCompute retire publication shape, direct stream helper boundary scan, and retired compat policy identifier quarantine.

Final production build:
- `dotnet build HybridCPU_ISE/HybridCPU_ISE.csproj --no-restore`: passed, incremental 0 warnings / 0 errors.

## Residual risk

This closure does not freeze VMCSv2. Remaining open risks:
- `VmcsV2Descriptor.TryWriteScalarField` is still a mutable scalar write API and needs separate read-only/denied projection audit.
- `VmxDirtyLogProjectionTypes` remains DTO/projection vocabulary and must not become a dirty tracking owner.
- `MemoryTranslationControl` still exposes NPT/VPID/VMCS-shaped backing names.
- `CapabilityDescriptorSet` still has bitmap mask compatibility-cache pressure.
- Generated projection still needs a real build-time generator.
- Generic nested projection/checkpoint ownership is still missing.
- Lane6 host-token evidence and migration rebuild proof remain open.
- Generic substrate still physically lives mostly under `Core/VMX/Substrate`.

## Next heavy step

Audit and reduce `VmcsV2Descriptor.TryWriteScalarField` / mutable scalar field authority into explicit read-only/denied compatibility projection, without introducing any VMCS manager, VMCS field-store owner, or renamed runtime owner.
