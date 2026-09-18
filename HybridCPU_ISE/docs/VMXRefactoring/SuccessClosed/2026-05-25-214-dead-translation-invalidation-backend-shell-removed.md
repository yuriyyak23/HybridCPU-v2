# Task 214: dead translation invalidation backend shell removed

Date: 2026-05-25
Status: closed

## Selected slice

- Deleted without replacement: `Legacy/VMX/Compatibility/Adapters/MemoryInvalidation/LegacyVmxTranslationInvalidationBackend.cs`.
- Reachability found no production constructor, interface wiring, emitted-code dependency, or frozen ABI caller. References were conformance/manifest/test evidence only.
- The former no-effect implementation did not justify continued compiled production presence.

## Authority result

- Live invalidation remains owned by `Core/Runtime/Memory/Invalidation/TranslationInvalidationService.cs` and `Memory/MMU/TranslationInvalidationHostBackend.cs`.
- `FinalFrozenAliasQuarantineContract` and older residual-extraction contracts now classify the deleted adapter as absent, not retained compatibility.
- No memory generation, IOTLB/DMA, host-evidence, completion, checkpoint, nested, lane, or retire authority moved into `Legacy/VMX`.

## Conformance and inventory

- Added `LegacyVmxTranslationInvalidationBackendRemovalContract` and replaced instance-based testing with absence/manifest/neutral-owner proof.
- Manifest status: `RemovedWithoutReplacement`.
- After the full tasks `214`-`217` wave, production `Legacy/VMX/Compatibility` count is `3`; total `Legacy/VMX` count is `38` because removal contracts are compiled evidence.

## Verification

- Production build passed with projection lineage verified: `54` existing warnings, `0` errors.
- Tests build passed: `93` existing warnings, `0` errors.
- `VmxProjectionSchemaAndQuarantineTests`: passed `56/56`.
- New removal filters for tasks `214`-`217`: passed `4/4`.
- `CoreVmxAuthorityBoundaryTests`: passed `1/1`; prior heavy-carrier and I/O removal filters passed `19/19` and `1/1`.
- Static scan: zero legacy-marked `.cs` under `Core/VMX`; deleted source, `VmxExecutionUnit.cs`, and `VmcsManager.cs` are absent.

## Next step

- Continue with the final necessity/authority inventory of the three retained production compatibility surfaces before any VMX freeze declaration.
