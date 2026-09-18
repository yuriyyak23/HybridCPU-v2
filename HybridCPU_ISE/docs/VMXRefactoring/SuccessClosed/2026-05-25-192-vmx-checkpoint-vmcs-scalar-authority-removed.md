# 2026-05-25-192 - VMX checkpoint VMCS scalar authority removed

Date: 2026-05-25

Status: closed

## Rule / Basis

VMX is a frozen compatibility frontend, not the virtualization architecture. Migration/checkpoint authority belongs to neutral domain checkpoint and migration policy surfaces, not to VMCS/VMCSv2 image writers, scalar field snapshots, VMCS blocks, or descriptor-owned restore helpers.

The active audit target was the remaining `VmxCheckpointImage` plus `VmcsV2Descriptor` scalar/checkpoint restore authority after task `191`. This closure removes a dead executable authority slice; it does not declare VMCSv2 freeze.

## Selected slice

VMCS-shaped checkpoint/scalar image authority.

Dependency classification:
- Frozen compatibility/projection vocabulary: VMCS/VMCSv2 names remain in field schema, projection, conformance, and evidence vocabulary. `VmcsV2HostEvidenceKind` remains as a vocabulary-only enum used by existing guardrails.
- Mutable VMCS-shaped model authority removed: VMCS-shaped checkpoint image writer/reader, `ScalarVmcsFields` block serialization, scalar field snapshot DTO, descriptor-owned guest checkpoint creation, and descriptor-owned scalar restore helpers.
- Generic runtime responsibility already owned elsewhere: neutral checkpoint validation remains with `DomainCheckpointImage` and `RestoreValidationService`; migration policy remains with the existing migration/evidence policy classes.
- Test-only or historical behavior: no production or test caller required the deleted VMCS-shaped checkpoint image authority.

## Removed without replacement

Deleted:
- `NonRTL/Core/System/Migration/VmxCheckpointImage.cs`
- `NonRTL/Core/System/Vmcs/V2/VmcsV2Checkpoint.cs`

Removed from `VmcsV2Descriptor`:
- `TryCreateGuestCheckpoint`
- `SnapshotMigratableScalarFields`
- `RestoreGuestStateForMigration`
- `RestoreScalarFieldForMigration`

Split out as vocabulary only:
- `NonRTL/Core/System/Vmcs/V2/VmcsV2HostEvidenceKind.cs`

No checkpoint manager, VMCS checkpoint service, projection runtime manager, scalar field restore adapter, new VMCS field store, or renamed runtime owner was introduced.

## Remaining allowed vocabulary

The following names may remain only as compatibility/projection/conformance/evidence vocabulary while their mutable authority remains under audit:
- `VmcsV2Descriptor`
- `VmcsV2Blocks`
- `VmcsV2BlockDirectory`
- `VmcsV2DescriptorProjection`
- `VmcsV2HostEvidenceKind`
- `VmxDirtyLogProjectionTypes`

## Generic owners left in place

Live generic responsibilities were not moved into a new VMCS owner:
- `DomainCheckpointImage` remains the neutral domain checkpoint image boundary.
- `RestoreValidationService` continues to deny compatibility projection metadata as authoritative restore state.
- `MigrationValidationPolicy` and `EvidenceRestorePolicy` remain existing policy boundaries for migration import and evidence restore.

## Conformance

Added:
- `Core/VMX/Conformance/AuthorityBoundary/VmxCheckpointVmcsScalarAuthorityRemovalContract.cs`

Updated:
- `HybridCPU_ISE.Tests/VmxRefactoring/VmxProjectionSchemaAndQuarantineTests.cs`

The focused conformance proves:
- the VMCS-shaped checkpoint image and checkpoint DTO files are absent;
- former descriptor checkpoint/scalar restore helper markers are absent;
- production sources do not reintroduce `VmxCheckpointImage`, `VmxCheckpointWriter`, `VmxCheckpointReader`, `VmxCheckpointBlockKind`, `VmcsV2Checkpoint`, `VmcsV2ScalarFieldSnapshot`, or scalar restore helper markers;
- neutral `DomainCheckpointImage` / `RestoreValidationService` remain independent of `VmcsV2Descriptor` and `VmcsField`;
- `Legacy/VMX` remains empty.

## Verification

Baseline before code change:
- `dotnet build HybridCPU_ISE/HybridCPU_ISE.csproj --no-restore`: passed.

After removal/conformance:
- `dotnet build HybridCPU_ISE/HybridCPU_ISE.csproj --no-restore`: passed with existing warnings.
- `dotnet build HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-restore`: passed with existing warnings.
- `dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~VmxProjectionSchemaAndQuarantineTests"`: passed.
- `dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~CoreVmxAuthorityBoundaryTests"`: passed.
- `dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~RemovedLegacyVmxExecutionUnit"`: passed.
- `dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~LegacyVmcsManager"`: passed.
- `dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~VmxCheckpointVmcsScalarAuthority"`: passed.
- `dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~VmcsV2"`: passed.
- `dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~Checkpoint"`: passed.
- `dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~Migration"`: passed.
- `dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~VectorStream"`: passed.
- `dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~DirtyLog"`: no matching tests.
- Final `dotnet build HybridCPU_ISE/HybridCPU_ISE.csproj --no-restore`: passed.

Known unrelated broad filters were re-run and remain outside this closure:
- `FullyQualifiedName~DmaStreamCompute`: existing failures expect missing `Core/Execution/DmaStreamCompute/DmaStreamComputeRuntime.cs`, compiler emission text, or reject native NonRTL runtime sources.
- `FullyQualifiedName~Retire`: existing failures include stream-retire boundary scans, retired compat policy scans, and missing `Documentation/operational-semantics.md`.

## Residual risk

This closure does not freeze VMCSv2. Remaining open risks:
- `VmcsV2Descriptor.TryWriteScalarField` is still a mutable scalar write API and needs separate read-only/denied projection audit.
- `VmcsV2Blocks` still exposes dirty/vector/checkpoint block behavior and restore helpers.
- `VmxDirtyLogProjectionTypes` remains DTO/projection vocabulary and must not become a dirty tracking owner.
- `MemoryTranslationControl` still exposes NPT/VPID/VMCS-shaped backing names.
- `CapabilityDescriptorSet` still has bitmap mask compatibility-cache pressure.
- Generated projection still needs a real build-time generator.
- Generic nested projection/checkpoint ownership is still missing.
- Generic substrate still physically lives mostly under `Core/VMX/Substrate`.

## Next heavy step

Audit and reduce `VmcsV2Blocks` dirty/vector/checkpoint helper behavior or `VmcsV2Descriptor.TryWriteScalarField` mutable scalar authority. The preferred shape is explicit read-only/denied compatibility projection backed by neutral domain/checkpoint/dirty/vector owners, without introducing any VMCS manager or renamed runtime owner.
