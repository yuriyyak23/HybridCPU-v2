# Closure 225: VMCSv2 Mutable Helper Authority Audit

Date: 2026-05-25

## Slice

Selected slice: audit `VmcsV2Descriptor.cs` and `VmcsV2Blocks.cs`, then remove the smallest dead owner-like helper pool: descriptor guest-state capture/materialization and host-evidence record/discard/reset helpers.

This is not a VMX runtime implementation. It reduces VMCSv2 descriptor authority and leaves VMX as a frozen compatibility frontend over neutral runtime owners.

## Method Classification

### Removed Without Replacement

- `VmcsV2Descriptor.CaptureGuestStateEager`
- `VmcsV2Descriptor.BeginLazyGuestStateSave`
- `VmcsV2Descriptor.MaterializeLazyGuestRegisters`
- `VmcsV2Descriptor.MaterializeVmExitGuestState`
- `VmcsV2Descriptor.RecordHostEvidence`
- `VmcsV2Descriptor.GuestVisibleStateContainsHostEvidence`
- `VmcsV2Descriptor.DiscardHostEvidenceAfterRestore`
- `VmcsV2Descriptor.ResetForClear`
- `VirtualCpuBlock.CaptureEager`
- `VirtualCpuBlock.BeginLazySave`
- `VirtualCpuBlock.TryMaterializeLazyRegisters`
- `VirtualCpuBlock.SnapshotGuestIntegerRegisters`
- descriptor `_hostEvidence` backing field

### Denied/Fail-Closed Compatibility ABI

- `VmcsV2Descriptor.TryReadScalarField`
- `VmcsV2Descriptor.ValidateMigrationReadiness`
- `VmcsV2Descriptor.ValidateNestedEnablementReadiness`
- generated VMCS field write aliases that remain denied by access policy

### Retire-Publication-Only Compatibility Projection

- `VmcsV2Descriptor.RecordVectorExceptionExit`
- `VmcsV2Descriptor.RecordStreamDescriptorFaultExit`
- `VmcsV2Descriptor.RecordStreamReplayRequiredExit`
- `ExitInfoBlock.RecordVectorException`
- `ExitInfoBlock.RecordStreamDescriptorFault`
- `ExitInfoBlock.RecordStreamReplayRequired`

These methods only publish compatibility exit information from neutral vector/stream identity facts. They do not own vector runtime, stream descriptors, nested admission, or completion routing.

### Generated/Read-Only Projection

- VMCS field schema/directory metadata
- default `VirtualCpuBlock` status properties after capture helpers were removed
- `VmxPreemptionTimerBlock`
- `InterceptBitmapBlock`
- `LaneCompletionRoutingBlock`
- dirty-log status projection without mutation helpers

### Runtime-Owner-To-Extract Or Dead Shell To Delete

- `VmxRootControlBlock.BindRootDescriptor`
- `VmxRootControlBlock.AdvanceEpoch`
- `VmxNptBlock.BindControl`
- `BundleExecutionBlock.BindBundle`
- `EventInjectionBlock.ConfigureInterruptRemap`
- `EventInjectionBlock.RemoveInterruptRemap`
- `EventInjectionBlock.ClearInterruptRemaps`
- `EventInjectionBlock.TryQueue`
- `EventInjectionBlock.TryDeliver`
- `EventInjectionBlock.RestoreSnapshot`
- `DebugTraceBlock.ConfigureExport`
- `DebugTraceBlock.RecordEvent`
- `DebugTraceBlock.RecordFail`
- `DebugTraceBlock.RecordAbort`
- `DebugTraceBlock.RecordInvalidation`
- `DebugTraceBlock.RecordDroppedPostedEvents`
- `DebugTraceBlock.ResetCounters`
- `DebugTraceBlock.DiscardTraceHandles`

These are not accepted as production VMX authority. The next slice must either move real behavior to neutral runtime/event/observability owners or delete/deny the helpers if no admitted caller exists.

## What Changed

- Edited `HybridCPU_ISE/NonRTL/Core/System/Vmcs/V2/VmcsV2Descriptor.cs`.
- Edited `HybridCPU_ISE/Core/VMX/Compatibility/Generated/VmcsProjection/VmcsV2Blocks.cs`.
- Added `HybridCPU_ISE.Tests/VmxRefactoring/VmcsV2MutableHelperAuthorityTests.cs`.

## Authority Boundary

Production authority was narrowed.

- No VMX execution unit was restored.
- No VMCS manager, adapter, active pointer state, field store, or VMX runtime manager was introduced.
- No legacy-marked C# source was returned to `Core/VMX`.
- No runtime state was moved into `Legacy/VMX`.
- Remaining VMCSv2 vocabulary is compatibility projection/status vocabulary unless explicitly classified as next extraction/deletion work.

## Verification

Builds and tests:

- production build: passed;
- tests build: passed;
- `VmcsV2MutableHelperAuthorityTests`: `2/2` passed;
- `VmxProjectionSchemaAndQuarantineTests`: `58/58` passed;
- broad `FullyQualifiedName~Vmx`: `258/258` passed.

Static results:

```text
Removed descriptor/helper marker scan: no production C# matches.
CoreVmxLegacyMarkedCs=0
LegacyVmxCs=30
LegacyVmxCompatibilityCs=0
LegacyVmxConformanceCs=30
VmxExecutionUnit.cs/VmcsManager.cs/IVmcsManager.cs: absent
```

## Residual Risk

`EventInjectionBlock` and `DebugTraceBlock` still contain real mutable queues/counters. They currently have no admitted production caller, but their shape is runtime-like. They must not be treated as frozen VMCS authority.

`VmxRootControlBlock`, `VmxNptBlock`, and `BundleExecutionBlock` also still expose owner-like bind/epoch helpers. They are smaller than event/debug, but still need deletion or neutral extraction before a fully clean VMCSv2 projection story.

## Next Step

Take the next honest VMCSv2 helper slice:

1. event injection queue/remap/delivery/restore extraction or deletion; or
2. debug trace configure/record/reset extraction or deletion; or
3. root/NPT/bundle bind helper deletion if they remain caller-free.
