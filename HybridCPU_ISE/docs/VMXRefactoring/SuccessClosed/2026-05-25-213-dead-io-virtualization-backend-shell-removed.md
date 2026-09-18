# Task 213: dead I/O virtualization backend shell removed

Date: 2026-05-25
Status: closed

## Rule / basis

- `Legacy/VMX` is compiled physical quarantine, not an authority home and not an automatic ABI-retention entitlement.
- A compatibility source may remain only when a production frozen ABI/projection path requires it or its behavior is a necessary fail-closed vocabulary boundary.
- Generic I/O, IOTLB, DMA, evidence, completion, checkpoint, and retire authority must remain outside `Legacy/VMX`.

## Selected compiled quarantine slice

- Removed without replacement:
  `Legacy/VMX/Compatibility/Adapters/IO/LegacyVmxIoVirtualizationBackend.cs`.
- Before removal it implemented `IIoVirtualizationBackend` only as a denied/no-effect shell.
- Reachability found no production caller, constructor use, or runtime/emitted-code dependency. Its references were limited to `LegacyVmxQuarantineManifest`, `FinalFrozenAliasQuarantineContract`, and `VmxProjectionSchemaAndQuarantineTests`.

## Production dependency classification

### Required frozen ABI / projection

- `Legacy/VMX/Compatibility/Frontend/Decode/VmxInstructionPayload.cs` is consumed by `VmxCompatDecodeBoundary`, `InstructionIR`, and `MicroOp.IO`; it remains frozen opcode/decode payload vocabulary.
- `Legacy/VMX/Compatibility/Frontend/Retire/VmxRetireModel.cs` is consumed by current dispatcher, micro-op, pipeline retire, retire-boundary, and evidence-boundary paths; it remains the typed fail-closed compatibility effect/result carrier.
- `Legacy/VMX/Compatibility/Generated/VmcsProjection/ShadowVmcsNestedProjectionService.cs` is constructed by `NestedDomainControllerCompatibilityProjection`; it remains a generated compatibility bridge whose admission path rejects enablement rather than becoming nested authority.

### Dead compiled compatibility shell

- `LegacyVmxIoVirtualizationBackend.cs` is deleted in this task.
- `LegacyVmxTranslationInvalidationBackend.cs`, `LegacyCsrBackedVmxCapabilityDescriptorSource.cs`, `LegacyVmxV1AdapterBoundary.cs`, and `LegacyVmxV2AdapterBoundary.cs` have no production caller found in this audit. They remain quarantine candidates for deletion-or-necessity proof; their test use alone is not ABI necessity.

### Neutral live authority

- I/O descriptor/backend routing remains in `Core/Runtime/Domains/Descriptors/IoDomain/IoVirtualizationBlock.cs`.
- IOTLB invalidation admission remains in `Core/Runtime/IO/Iotlb/IotlbInvalidationService.cs`.
- Admitted generic host backend mechanics remain in `Memory/MMU/IoVirtualizationHostBackend.cs`.
- Existing neutral owners for typed grants, evidence, lanes, completion, migration/checkpoint, nested admission, and retire policy are unchanged.

### Test-only / historical evidence

- `Legacy/VMX/Conformance/**` remains evidence vocabulary, including the new `LegacyVmxIoVirtualizationBackendRemovalContract`.
- Manifest records both historical removed origins and physically retained compatibility sources; the removed I/O adapter entry is now `RemovedWithoutReplacement`.

## Conformance

- Added `LegacyVmxIoVirtualizationBackendRemovalContract` under the physical legacy quarantine because its subject carries legacy vocabulary.
- Replaced the former no-effect-instance test with a removal contract test that proves:
  - the compiled I/O backend shell is absent;
  - manifest status is `RemovedWithoutReplacement`;
  - neutral I/O authority paths remain present outside quarantine;
  - remaining production compatibility sources contain no direct typed-grant, host-evidence, lane-state, memory/IOTLB/DMA, completion-routing, or checkpoint-restore mutation markers.
- Updated `FinalFrozenAliasQuarantineContract` so the removed I/O adapter is an absent path rather than a retained denied adapter.

## Inventory and authority status

- Production `.cs` sources under `Legacy/VMX/Compatibility`: reduced from `8` to `7`.
- Total `.cs` sources under `Legacy/VMX`: `38` after the step; one dead production file was replaced in the count by one focused conformance contract.
- `Core/VMX/**/*.cs` continues to contain no `legacy` marker.
- `VmxExecutionUnit.cs`, `VmcsManager.cs`, and `IVmcsManager.cs` were not restored.
- No VMX-owned field store, pointer state, I/O/DMA/IOTLB authority, evidence, completion queue, checkpoint state, nested composition authority, or retire authority was introduced.

## Verification

- Baseline production build before removal: passed with projection lineage verified; `54` existing warnings, `0` errors.
- Production build after removal: passed with projection lineage verified; `54` existing warnings, `0` errors.
- Tests build after removal: passed with projection lineage verified; `93` existing warnings, `0` errors.
- `FullyQualifiedName~VmxProjectionSchemaAndQuarantineTests`: passed `55/55`.
- `FullyQualifiedName~CoreVmxAuthorityBoundaryTests`: passed `1/1`.
- `FullyQualifiedName~RemovedLegacyVmxExecutionUnit|FullyQualifiedName~LegacyVmcsManager`: passed `19/19`.
- `FullyQualifiedName~LegacyVmxIoVirtualizationBackendRemoval`: passed `1/1`.
- Static scans: zero legacy-marked `.cs` under `Core/VMX`; the removed I/O compatibility backend has no production source; removed heavy carrier absence remains covered by focused tests.

## Known unrelated broad-filter failures

- No broad filter was rerun for this slice. The repository-shape/documentation broad-filter failures already recorded in tasks `209` and `210` remain unrelated to this removal.

## Residual risk / next heavy step

- Do not declare VMX freeze. The next heavy removal candidate is `LegacyVmxTranslationInvalidationBackend`, which has no production caller in this inventory but is still cited by earlier conformance tests/contracts.
- After that, independently classify or remove the CSR-backed capability stub and v1/v2 adapter boundaries before final compiled-surface freeze-readiness inventory.
