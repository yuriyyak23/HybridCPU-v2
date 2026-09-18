# 2026-05-25-200 Address-Space Canonical Identity Authority Removed

Date: 2026-05-25

Status: closed

## Rule / Basis

VMX is a frozen compatibility frontend, not the memory translation architecture. Canonical translation identity belongs to neutral memory-domain descriptors and runtime-owned address-space/second-stage tags, not `VMID`, `VPID`, `NPT`, `EPT`, VMCS fields, or a VMX-aware host overload.

This closure follows the `audit2.md` blocker requiring `AddressSpaceId`, nested translation cache identity, and active memory translation consumers to move toward `DomainTag`, `AddressSpaceTag`, and `SecondStage*` authority without creating a renamed VMX owner.

## Closed Slice

- Replaced `AddressSpaceId` canonical fields `Vmid`, `Vpid`, `NptRootIdentity`, `EptEpoch`, `VpidEpoch`, and `Generation` with neutral domain/address-space/second-stage identity fields.
- Replaced `NestedTlbTag` and `TLB` nested invalidation key names with `SecondStageRootIdentity`, `AddressSpaceTag`, and `DomainTag`.
- Changed `MemoryDomainDescriptor.TranslationControl` to own `MemoryDomainTranslationControl`.
- Added neutral `MemoryDomainTranslationControl.ToAddressSpaceId(...)`; compatibility `MemoryTranslationControl.ToAddressSpaceId(...)` now delegates through that neutral projection.
- Changed nested memory composition to consume `MemoryDomainTranslationControl`, `ChildAddressSpaceTag`, and second-stage invalidation/walk vocabulary.
- Changed nested page walking and the domain-bound IOMMU translation path to use `MemoryDomainTranslationControl` after compatibility ingress.
- Removed the unused `IOMMU.TranslateAndValidateAccess(... vmxActive, guestCR3, eptPointer)` overload without replacement.

## Intentionally Not Moved

No `VmcsManager`, `VmxExecutionUnit`, memory translation manager, VMX/NPT/VPID runtime service, VMCS field store, or renamed runtime owner was introduced.

`MemoryTranslationControl` remains compiled as compatibility translation vocabulary. It no longer constructs canonical cache identity directly, but its remaining compatibility fields require later projection/ABI cleanup.

## Remaining Vocabulary

VMX/NPT/VPID compatibility names remain only where not closed by this slice: compatibility `MemoryTranslationControl`, compatibility translation status aliases, IOTLB/DMA/Lane surfaces, and nested compatibility projection. None of those residuals is claimed frozen or complete.

## Live Generic Responsibilities

- Canonical address-space identity: `AddressSpaceId`.
- Memory translation owner descriptor: `MemoryDomainDescriptor` with `MemoryDomainTranslationControl`.
- Runtime second-stage cache tag and invalidation keys: `NestedTlbTag` and `TLB`.
- Neutral nested translation execution: `NestedPageWalker` and `NestedMemoryCompositionService`.

## Conformance

Added `AddressSpaceCanonicalIdentityAuthorityRemovalContract`, with tests proving:

- neutral domain control produces canonical address-space identity using only generic tags and epochs;
- `AddressSpaceId`, `NestedTlbTag`, and `TLB` do not expose returned `Vmid` / `Vpid` / `NptRootIdentity` / `EptEpoch` / `VpidEpoch` identity markers;
- canonical identity files do not expose VMCS compatibility identity or host-owned evidence markers;
- nested composition has no returned `L2Vpid`, NPT raw-walk, or VPID invalidation authority markers;
- `MemoryDomainDescriptor` owns `MemoryDomainTranslationControl`;
- domain-bound IOMMU and page walking consume neutral control after compatibility ingress;
- the unused VMX-shaped host IOMMU overload is absent.

## Verification

- `dotnet build HybridCPU_ISE.csproj --no-restore`: passed with existing warnings.
- `dotnet build HybridCPU_ISE.Tests.csproj --no-restore`: passed with existing warnings.
- `dotnet test HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~AddressSpaceCanonicalIdentity"`: passed, 1/1.
- `dotnet test HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~VmxProjectionSchemaAndQuarantineTests"`: passed, 41/41.
- `dotnet test HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~MemoryTranslation|FullyQualifiedName~NestedPageWalker|FullyQualifiedName~NestedMemory"`: passed, 3/3.
- `dotnet test HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~CoreVmxAuthorityBoundaryTests"`: passed, 1/1.
- `dotnet test HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~RemovedLegacyVmxExecutionUnit"`: passed, 15/15.
- `dotnet test HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~LegacyVmcsManager"`: passed, 4/4.
- `dotnet test HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~VmcsV2"`: passed, 5/5.
- `dotnet test HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~VmcsV2|FullyQualifiedName~Checkpoint|FullyQualifiedName~Migration|FullyQualifiedName~VectorStream"`: passed, 14/14.
- `rg --files HybridCPU_ISE/Legacy/VMX`: no files.
- `dotnet test HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~DmaStreamCompute"`: failed on 5 known unrelated repository-shape/native NonRTL checks.
- `dotnet test HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~Retire"`: failed on 4 known unrelated documentation/stream/compat/native-DMA boundary checks.

## Residual Risk

Architectural freeze remains blocked by compatibility `MemoryTranslationControl` and status vocabulary, IOTLB/DMA/Lane VMID cleanup, generic nested projection/checkpoint ownership, Lane6 host-token evidence/migration proof, and neutral substrate directory/namespace placement.

## Next Heavy Step

Audit and remove remaining translation compatibility pressure in `MemoryTranslationControl`, IOTLB/DMA/Lane tags, and nested projection surfaces, converting live authority to neutral memory/I/O-domain descriptors without reintroducing a VMX runtime owner.
