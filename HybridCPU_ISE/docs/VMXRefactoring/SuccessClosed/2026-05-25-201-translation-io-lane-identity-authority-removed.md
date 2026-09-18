# 2026-05-25-201 Translation/I-O/Lane Identity Authority Removed

Date: 2026-05-25

Status: closed

## Rule / Basis

VMX is a frozen compatibility frontend, not the memory, I/O, DMA, lane, or nested translation architecture. Live identity and legality belong to neutral memory/I/O/lane-domain descriptors. VMID, VPID, NPT, and VMCS-shaped names may remain only as explicit compatibility aliases or projection vocabulary.

This closure follows `audit2.md` and the VMX principles requirement to remove remaining compatibility pressure in `MemoryTranslationControl`, `NestedTranslationResult`, IOTLB/DMA/Lane keys, and dead nested projection surfaces without creating a VMX-owned runtime replacement.

## Closed Slice

- Changed live IOMMU/IOTLB binding and invalidation keys from `Vmid` to `IoDomainTag` / `IoDomain`.
- Changed active domain-bound IOMMU translation to consume `MemoryDomainTranslationControl` directly.
- Removed dead compatibility conversion/factory helpers from `MemoryTranslationControl`: `CreateSecondStageControl`, `CreateRuntimeProjection`, `FromDomainControl`, and `ToCompatibilityControl`.
- Changed canonical nested translation outcome names to `SecondStageViolation` and `SecondStageMisconfiguration`; removed NPT-shaped canonical outcomes and `CausesVmExit`.
- Changed Lane6 queue/token and DMA validation identity to `IoDomainTag`.
- Changed Lane7 state/checkpoint and lane-completion routing identity to `ExecutionDomainTag` / `AddressSpaceTag`.
- Deleted dead `ComposedDomainProjectionComposer` without replacement; it was the remaining unreferenced VMCS-shaped nested projection composer.

## Intentionally Not Moved

No `VmcsManager`, `VmxExecutionUnit`, nested VMCS projection manager, VMX IOTLB runtime, VMX DMA runtime, or VMX-owned lane runtime was introduced. Live responsibilities continue in existing neutral memory/I/O/lane substrate types.

`MemoryTranslationControl` remains compiled as frozen read-side compatibility vocabulary, and `InvalidateVmxIotlbByVmid(...)` remains an explicit compatibility alias delegating to neutral I/O-domain invalidation. Neither is a live identity owner.

## Live Generic Responsibilities

- Memory-domain translation control: `MemoryDomainTranslationControl`.
- Host I/O-domain binding and IOTLB invalidation: `IOMMU.DomainBinding`, `IotlbTag`, `IoVirtualizationBlock`, and `IotlbInvalidationService`.
- Lane6 execution identity: neutral `IoDomainTag` queue/token descriptors and existing DMA runtime.
- Lane7 execution/completion identity: neutral `ExecutionDomainTag` / `AddressSpaceTag` descriptors and completion routing.
- Nested translation results: neutral second-stage outcomes and existing nested memory composition service.

## Conformance

Added `TranslationIoLaneIdentityAuthorityRemovalContract`, with tests proving:

- executable IOMMU/IOTLB and lane identity surfaces do not expose VMID/VPID keys;
- active IOMMU translation consumes neutral memory-domain control;
- nested translation result vocabulary cannot return NPT-shaped canonical outcomes or VM-exit authority;
- dead compatibility translation factory helpers are absent;
- the dead VMCS-shaped nested projection composer is absent;
- the frozen VMX IOTLB alias delegates into neutral I/O-domain invalidation.

## Verification

- `dotnet build HybridCPU_ISE.csproj --no-restore`: passed with existing warnings; projection lineage verifier ran.
- `dotnet build HybridCPU_ISE.Tests.csproj --no-restore`: passed with existing warnings; projection lineage verifier ran.
- `dotnet test HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~TranslationIoLaneIdentity|FullyQualifiedName~IoDomainAuthorityBoundaryTests|FullyQualifiedName~AddressSpaceCanonicalIdentity"`: passed, 4/4.
- `dotnet test HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~VmxProjectionSchemaAndQuarantineTests"`: passed, 42/42.
- `dotnet test HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~CoreVmxAuthorityBoundaryTests"`: passed, 1/1.
- `dotnet test HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~RemovedLegacyVmxExecutionUnit"`: passed, 15/15.
- `dotnet test HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~LegacyVmcsManager"`: passed, 4/4.
- `dotnet test HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~VmcsV2"`: passed, 5/5.
- `dotnet test HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~Checkpoint|FullyQualifiedName~Migration|FullyQualifiedName~VectorStream"`: passed, 11/11.
- `dotnet test HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~MemoryTranslation|FullyQualifiedName~NestedPageWalker|FullyQualifiedName~NestedMemory"`: passed, 3/3.
- `rg --files HybridCPU_ISE/Legacy/VMX`: no files.
- Wider affected-surface filter `MemoryTranslation|NestedPageWalker|NestedMemory|Lane6|Lane7`: 174 tests passed; two failures are existing repository-shape/native DMA checks (existing `NonRTL` production-source scan and missing `Core\Decoder\DecodedBundleTransportProjector.cs` expectation), not identity-contract regressions.
- Broad `DmaStreamCompute`: 172 passed, 5 known unrelated repository-shape/native NonRTL failures, including the absent expected `Core\Execution\DmaStreamCompute\DmaStreamComputeRuntime.cs`.
- Broad `Retire`: 590 passed, 4 known unrelated documentation/stream/compat/native-DMA boundary failures, including the absent expected `Documentation\operational-semantics.md`.
- Final mandatory `dotnet build HybridCPU_ISE.csproj --no-restore` after closure updates: passed; projection lineage verifier ran; 0 warnings, 0 errors.

## Residual Risk

Architectural freeze remains blocked by Lane6 host-token evidence and restore/rebuild proof, event/trap compatibility identity, a genuine generic nested-domain projection/checkpoint service, epoch overflow/migration evidence conformance, and physical extraction of neutral substrate from `Core/VMX/Substrate`.

## Next Heavy Step

Close the Lane6 host-owned evidence boundary: remove active compatibility-shaped `_hostTokens` ownership or route it through a neutral lane evidence namespace with fail-closed epoch and migration rebuild proof, then audit the remaining event/trap VMID/VPID projection aliases.
