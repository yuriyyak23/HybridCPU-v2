# Capability projection placement and service substrate extraction

Date: 2026-05-25

Status: closed

## Rule / basis

- `audit2.md`: `Core/VMX/Substrate` must not remain the final home for generic runtime/domain substrate.
- `OSNOVY i PRAVILA VMX`: VMX is a frozen compatibility frontend; generated projection artifacts may stay under `Core/VMX/Compatibility/Generated`, but runtime authority must live in neutral runtime/domain owners.
- VMX/VMCS names are allowed only as frozen ABI, generated/read-only projection, explicit quarantine, or conformance vocabulary.

## Closed slice

- Moved generated capability-bit schema placement out of substrate:
  `Core/VMX/Substrate/Capabilities/DescriptorSet/CapabilityDescriptorSetSchema.cs`
  -> `Core/VMX/Compatibility/Generated/CapabilityProjection/CapabilityDescriptorSetSchema.cs`.
- Updated the build-time lineage verifier and conformance source paths so generated VmxCaps schema drift is checked at the new compatibility-generated location.
- Moved selected neutral services/descriptors from `Core/VMX/Substrate` to `Core/Runtime/*`:
  memory address-space / nested translation identity and policy, I/O DMA/IOTLB services, migration/checkpoint/restore policy, completion routing, event injection/fabric/queue services, and Lane6 state/queue/token/fence surfaces.

## Intentionally not moved as runtime owners

- No `VmcsManager`, `IVmcsManager`, `VmxExecutionUnit`, VMCS field store, active VMCS pointer, projection runtime manager, or renamed VMX runtime owner was created.
- `MemoryTranslationControl`, `CompletionProjectionService`, VM-exit/trap vocabulary, Lane7 VMFUNC/VM-exit paths, vector-stream VMCS host-evidence helpers, nested compatibility mappers, and capability mask ingress/grant services remain explicit compatibility/quarantine surfaces.
- `CapabilityGrantCollection.FromCompatibilityMasks(...)` remains compatibility seed materialization only; it was not promoted as generic capability authority.

## Names that remain compatibility vocabulary

- `VmxCaps`, `VmxCompatibility`, `MemoryTranslationControl`, `VmExitReason`, `VmxExitQualification`, `VmxFunctionLeaf`, `Vmcs.V2.VmcsV2HostEvidenceKind`, VMX IOTLB/INVVPID aliases, and VMCS field aliases remain frozen projection/ABI vocabulary only.

## Live generic responsibility

- Memory/I/O/migration/completion/event/Lane6 responsibilities moved in this slice are now under neutral `Core/Runtime/*` placement.
- Existing domain/nested runtime responsibility remains under `Core/Runtime/Domains/*` and `Core/Runtime/Nested/*`.
- Generated capability projection remains under `Core/VMX/Compatibility/Generated/CapabilityProjection` because it is frozen compatibility metadata, not runtime authority.

## Conformance

- Added `CapabilityProjectionPlacementServiceSubstrateExtractionContract`.
- Added `CapabilityProjectionPlacementServiceSubstrate_MovesNeutralServicesAndQuarantinesCompatVocabulary`.
- The contract proves:
  - old capability generated substrate path is absent;
  - new generated capability projection path is used by lineage proof;
  - selected neutral runtime service files are outside `Core/VMX/Substrate`;
  - moved neutral files do not contain VMX/VMCS/VMFUNC/VMExit/legacy owner markers;
  - remaining VMX-shaped service files are explicitly quarantined compatibility surfaces;
  - `Legacy/VMX` remains empty.

## Verification

- Baseline production build before edits: passed.
- `dotnet build HybridCPU_ISE.csproj --no-restore`: passed, 54 existing warnings, 0 errors.
- `dotnet build HybridCPU_ISE.Tests.csproj --no-restore`: passed, 93 existing warnings, 0 errors.
- `VmxProjectionSchemaAndQuarantineTests`: passed 50/50.
- `CapabilityProjectionPlacementServiceSubstrate`: passed 1/1.
- `CoreVmxAuthorityBoundaryTests`: passed 1/1.
- `RemovedLegacyVmxExecutionUnit`: passed 15/15.
- `LegacyVmcsManager`: passed 4/4.
- `GeneratedProjectionLineage`: passed 1/1.
- `TranslationIoLaneIdentity`: passed 1/1.
- `EventTrapDomainIdentity`: passed 1/1.
- `AddressSpaceCanonicalIdentity`: passed 1/1.
- `Lane6HostOwnedEvidence`: passed 1/1.
- `VmxCheckpointVmcsScalar`: passed 1/1.
- `NestedDomainProjectionCheckpoint`: passed 1/1.
- `Capability`: passed 44/44.
- `VmcsV2`: passed 5/5.
- `VmcsV2BlocksDirtyVectorCheckpoint`: no matching test filter name; covered by full `VmxProjectionSchemaAndQuarantineTests`.
- `rg --files Legacy/VMX`: no files.

## Broad-filter status

- Broad `DmaStreamCompute` and `Retire` filters were not rerun for this slice.
- Existing unrelated broad-filter risks remain as previously classified: repository-shape/documentation checks around missing `Core/Execution/DmaStreamCompute/DmaStreamComputeRuntime.cs`, native NonRTL runtime source expectations, missing `Documentation/operational-semantics.md`, and existing stream/compat/native-DMA boundary scans.

## Residual risk

- `Core/VMX/Substrate` still contains residual capability grant/descriptor services, vector-stream state/save-restore surfaces, Lane7 VMFUNC/VM-exit compatibility state, nested compatibility mapper/composition surfaces, evidence/debug trace policy files, retire evidence, and explicit frozen compatibility aliases.
- `audit.md` remains absent in the dirty worktree from prior state; this closure updated `audit2.md`, current-model completion audit, and VMX principles doc.

## Next heavy step

Extract or quarantine the remaining capability grant/descriptor services, vector-stream state/save-restore placement, Lane7 compatibility state, nested mapper/composition services, evidence/debug trace policy, and retire evidence so `Core/VMX/Substrate` trends toward frozen compatibility/projection vocabulary only.
