# Task 210: VMX completion/trap/nested/retire compatibility authority quarantined

Date: 2026-05-25

Status: closed

## Rule / basis

- `audit2.md`: remaining VMX completion, trap, nested, and retire compatibility surfaces must not own retire publication, completion routing, nested mapping, event/trap authority, host-owned evidence, or runtime legality.
- `ОСНОВЫ и ПРАВИЛА VMX.md`: VMX is a frozen compatibility frontend. The source of truth is the generic runtime/domain substrate, typed capability/evidence policy, completion routing, migration/checkpoint policy, and runtime-owned legality.

## Closed slice

- Removed remaining VMCSv2 helper authority for this slice:
  - `VmcsV2Descriptor.RecordNestedTranslationExit`
  - `VmcsV2Descriptor.RecordInterceptExit`
  - `VmcsV2Descriptor.RecordQualifiedExit`
  - `ExitInfoBlock.RecordNestedTranslationFault`
  - `ExitInfoBlock.RecordInterceptExit`
  - `ExitInfoBlock.RecordQualifiedExit`
- Converted VMCSv2 timer/intercept/completion helper blocks to read-only compatibility projection:
  - no `SchedulingBudgetTimer Timer`
  - no `TrapPolicyBitmap Bitmap`
  - no `LaneCompletionRouter Router`
  - no `ConfigureRoute`, `DisableRoute`, `TryRouteCompletion`
- Split neutral `CompletionRecord` from VMX projection helpers. VMX exit conversion now lives only in `CompletionRecordCompatibilityProjection`.
- Removed `TrapDecision` from neutral `DomainTrapRecord`; VMX exit reason/qualification encoding remains compatibility frontend vocabulary.
- Moved completion/trap/nested/retire compatibility surfaces out of `Core/VMX/Substrate` into explicit compatibility frontend quarantine paths.

## Intentionally not moved into Core as a runtime owner

- No `VmcsManager`, `IVmcsManager`, `VmcsV2Runtime`, `VmcsProjectionRuntimeManager`, VMCS field store, active VMCS pointer, or renamed VMX runtime owner was created.
- VMCS/VMCSv2 names remain schema/projection/conformance vocabulary only for this slice.
- Retire publication, completion routing, nested mapping, and event/trap authority remain with neutral runtime descriptors and services:
  - `CompletionRouteDescriptor`
  - `CompletionRoutingService`
  - `TrapPolicyDescriptor`
  - `DomainTrapRecord`
  - `NestedDomainDescriptor`
  - `EvidencePolicyDescriptor`
  - `ObservabilityDescriptor`
  - `HostOwnedEvidenceBoundary`

## Conformance

- Added `VmxCompletionTrapNestedRetireQuarantineContract`.
- Added `VmxCompletionTrapNestedRetireSurfaces_QuarantineCompatibilityAndUseNeutralAuthority`.
- Updated residual substrate and event/trap contracts to point at the new compatibility quarantine locations.
- The contract proves old substrate paths are absent, neutral runtime paths have no VMX/VMCS owner markers, VMCSv2 projection helper markers are absent, read-only projection markers remain, and `Legacy/VMX` stays empty.

## Verification

- `dotnet build HybridCPU_ISE.csproj --no-restore`: passed, existing warnings.
- `dotnet build HybridCPU_ISE.Tests.csproj --no-restore`: passed, existing warnings.
- `VmxProjectionSchemaAndQuarantineTests`: passed, 53/53.
- `VmxCompletionTrapNestedRetire`: passed, 1/1.
- `CoreVmxAuthorityBoundaryTests`: passed, 1/1.
- `RemovedLegacyVmxExecutionUnit`: passed, 15/15.
- `LegacyVmcsManager`: passed, 4/4.
- `VmcsV2`: passed, 5/5.
- `Completion`: passed, 13/13.
- `Trap`: passed, 184/184.
- `Nested`: passed, 14/14.
- `Event`: passed, 323/323.
- `rg --files Legacy/VMX`: no files.

## Known unrelated broad-filter failures

- `Retire`: failed on existing repository-shape/documentation checks:
  - `DmaStreamComputeRetirePublicationPhase04Tests.Phase04_PublicationSurfaceRemainsExplicitFutureSeamNotNormalLane6Path`
  - `Phase09DirectStreamHelperBoundaryTests.StreamEngineCaptureRetireWindowPublications_ProductionCallersStayInsideCpuCoreTestSupport`
  - `Phase12RetiredCompatPolicyBitBoundaryTests.RetiredCompatPolicyBit_IdentifiersRemainQuarantinedToIngressBoundaries`
  - missing `Documentation/operational-semantics.md`

These are the already known broad-filter style failures, not regressions from this VMX completion/trap/nested/retire quarantine slice.

## Residual risk

- `MemoryTranslationControl`, VMX invalidation aliases, Lane7 VMFUNC/VM-exit compatibility state, vector-stream VMCS host-evidence helper vocabulary, and final frozen alias quarantine remain compiled and need final semantic review before VMX freeze.
- Some VMX compatibility projection DTOs still compile by design; they are frozen frontend vocabulary, not runtime authority.

## Next heavy step

Final frozen alias quarantine: review remaining `MemoryTranslationControl`, VMX invalidation/IOTLB aliases, Lane7 VMFUNC/VM-exit compatibility state, vector-stream VMCS host-evidence helper vocabulary, and VMCS field aliases to prove they are read-only/generated compatibility projections or remove/deny any remaining executable helper authority.
