# Task 211: final frozen alias quarantine

Date: 2026-05-25
Status: closed

## Rule / basis

- `audit2.md`: the remaining `MemoryTranslationControl`, VMX invalidation/IOTLB, Lane7 VMFUNC/VM-exit, vector-stream VMCS host-evidence, and VMCS field alias surfaces must not own runtime authority.
- `ОСНОВЫ и ПРАВИЛА VMX.md`: VMX is a frozen compatibility frontend; generic runtime/domain descriptors, policies, and host-owned evidence are the source of truth.

## Closed slice

- `MemoryDomainTranslationControl` and `TranslationInvalidationService` are neutral runtime memory owners under `Core/Runtime/Memory`. `MemoryTranslationControl` is retained in explicit VMX compatibility projection as read-only vocabulary and no longer exposes domain conversion or epoch-mutation helpers.
- VMX invalidation/IOTLB aliases are explicit denied/no-effect compatibility surfaces: `IommuVmxCompatibilityAliases`, `LegacyVmxTranslationInvalidationBackend`, and `LegacyVmxIoVirtualizationBackend` do not delegate VMX-named operations into neutral host mutation.
- `Lane7StateBlock` is neutral runtime state without VMFUNC leaf maps, VMFUNC policy/result APIs, or VM-exit-reason state. Its checkpoint path does not save or restore VMFUNC policy state.
- Lane7 and vector-stream VMCS host-evidence helper names remain only as false-only compatibility projection methods; neutral save/restore and evidence rebuild paths use no VMCS vocabulary.
- Every canonical VMCS field projection schema entry is read-only, and `VmcsFieldAliasProjection` denies all write access even if a caller supplies `AllowWrite`. The build-time lineage verifier regenerates and checks that rule.
- The last nested compatibility projection files were classified out of `Core/VMX/Substrate`, leaving that directory with no C# sources.

## Not moved as runtime owner

- No `VmcsManager`, `IVmcsManager`, `VmxExecutionUnit`, VMCS field store, active VMCS pointer, projection runtime manager, or VMX-owned memory/I/O/lane/evidence authority was created.
- Live generic responsibilities remain with neutral runtime owners: memory translation/invalidation under `Core/Runtime/Memory`, Lane7 and vector-stream state under `Core/Runtime/Lanes`, and already established domain/evidence/completion/nested services under `Core/Runtime`.

## Compatibility vocabulary retained

- `MemoryTranslationControl` field aliases remain read-only ABI/projection vocabulary.
- VMX IOTLB/invalidation API spellings remain denied compatibility aliases.
- Lane7/vector-stream VMCS host-evidence spellings remain false-only projection vocabulary.
- VMCS field names and generated alias/schema artifacts remain read-only compatibility lineage.

## Conformance

- Added `FinalFrozenAliasQuarantineContract`.
- Extended projection/quarantine tests to prove neutral placement, absent substrate paths, denied VMX adapters, absent Lane7 VMFUNC/VM-exit authority, denied VMCS alias writes, and empty `Legacy/VMX`.
- Updated existing extraction/authority contracts to track the explicit compatibility quarantine and neutral runtime paths.
- Generated schema lineage now enforces read-only VMCS field output at build time.

## Verification

- `dotnet build HybridCPU_ISE.csproj --no-restore`: passed with projection lineage verified; 54 existing warnings, 0 errors.
- `dotnet build HybridCPU_ISE.Tests.csproj --no-restore`: passed with projection lineage verified; 93 existing warnings, 0 errors.
- `FullyQualifiedName~VmxProjectionSchemaAndQuarantineTests`: passed 54/54.
- `FullyQualifiedName~CoreVmxAuthorityBoundaryTests`: passed 1/1.
- `FullyQualifiedName~FinalFrozenAliasQuarantine|FullyQualifiedName~VmcsV2|FullyQualifiedName~MemoryTranslation|FullyQualifiedName~VectorStream|FullyQualifiedName~Lane7HostOwnedEvidence`: passed 8/8.
- `FullyQualifiedName~RemovedLegacyVmxExecutionUnit|FullyQualifiedName~LegacyVmcsManager`: passed 19/19.
- `FullyQualifiedName~Checkpoint`: passed 3/3.
- `FullyQualifiedName~Migration`: passed 8/8.
- `FullyQualifiedName~DirtyLog`: no matching tests in the built test assembly.
- Static scans: `Core/VMX/Substrate` contains no C# sources; `Legacy/VMX` remains empty; neutral memory/Lane7/vector-stream runtime scan contains no VMX/VMCS authority markers from this slice.

## Known unrelated broad-filter failures

- Broad `Retire` was not rerun in task `211`; task `210` already recorded existing repository-shape/documentation scan failures unrelated to this authority-removal slice.
- No targeted failure is attributed to task `211`.

## Residual risk / next heavy step

- Do not declare VMX freeze yet. Perform a final freeze-readiness inventory of all compiled compatibility/frontend/generated/debug/lifecycle vocabulary, including remaining `VmcsLifecycleResults`, `VmxDebugTracePlane`, generated `ShadowVmcs` bridges, frozen opcode fail-closed coverage, and known repository-shape test debt.
