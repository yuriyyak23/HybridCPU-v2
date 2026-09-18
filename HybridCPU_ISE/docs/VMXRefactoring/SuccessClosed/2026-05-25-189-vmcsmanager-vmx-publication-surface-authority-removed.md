# 189 VmcsManager VMX publication surface authority removed

Date: 2026-05-25

Status: closed

## Rule / basis

- VMX is a frozen compatibility frontend, not an owner of execution, event, evidence, observability, or retire-publication state.
- Production-dead VMX authority is removed without replacement.
- `VmcsManager.cs` cannot be declared removed while Core still consumes its VMCS-shaped lane/vector/dirty/DMA responsibilities.

## Dependency classification

- Production-dead VMX/frontend authority removed in this slice: nested/intercept/qualified-exit publication, standalone virtual-event queue/delivery, and public VMX debug/fail/abort/invalidation observability APIs.
- Live but incorrectly placed generic responsibilities still remaining in the quarantined carrier: lane completion routing, vector-stream dirty tracking, dirty memory tracking, Lane7/DMA descriptor lookup and validation paths.
- Frozen compatibility/projection vocabulary retained: generated VMCS projection schema and VMCS aliases.
- Historical direct-construction tests were not treated as evidence that deleted publication APIs are required by production.

## What changed

- Removed from `Legacy/VMX/Substrate/Runtime/Binding/VmcsManager.cs` and `NonRTL/Core/System/IVmcsManager.cs`:
  - `RecordNestedTranslationExit`
  - `TryResolveIntercept`
  - `RecordInterceptExit`
  - `RecordQualifiedVmExit`
  - standalone `TryQueueVirtualEvent` and `TryDeliverVirtualEvent`
  - `SnapshotDebugTraceCounters`, `ResetDebugTraceCounters`, `RecordVmxInvalidationForObservability`, `RecordVmxFailForObservability`, and `RecordVmxAbortForObservability`
- Added `LegacyVmcsManagerVmxPublicationAuthorityRemovalContract`.
- Updated the quarantine manifest to state the removed slice while retaining `VmcsManager.cs` as quarantined.
- Added focused conformance proving the removed markers stay absent while the live lane/vector/dirty dependencies remain visible.

## Intentionally not moved to Core

- No adapter, projection-runtime manager, event publisher, or observability replacement was introduced for the removed VMX authority.
- No VMCS-owned state carrier was returned to `Core/VMX`.

## Verification

- Static check: removed publication/event/observability markers are absent from both `VmcsManager.cs` and `IVmcsManager.cs`.
- Static check: live `TryRouteLaneCompletion`, `TryMarkVectorStreamDirty`, `TryMarkDirtyRange`, and `ActiveV2Descriptor` markers remain and prevent an incorrect full-removal claim.
- Main project build after code removal: succeeded, 54 existing warnings, 0 errors.
- Test project build after final conformance addition: succeeded, 36 existing warnings, 0 errors.
- `VmxProjectionSchemaAndQuarantineTests`: Passed 30/30.
- `CoreVmxAuthorityBoundaryTests`: Passed 1/1.
- `RemovedLegacyVmxExecutionUnit`: Passed 15/15.
- `LegacyVmcsManager`: Passed 1/1.

## Residual risk

- `VmcsManager.cs` remains physically present as the only file under `Legacy/VMX`.
- Core still constructs it and routes lane completion, vector dirty, memory dirty, Lane7, and DMA flows through VMCS-shaped state.
- Pointer lifecycle, field storage/access, VM-entry/exit, checkpoint/migration, and remaining descriptor-owned authority require further independent classification/removal slices.

## Next heavy step

- Extract or fail-close the live lane completion, vector-stream dirty, memory dirty, Lane7, and DMA workflows through neutral runtime-domain owners, then remove the remaining VMCS carrier without replacement.
