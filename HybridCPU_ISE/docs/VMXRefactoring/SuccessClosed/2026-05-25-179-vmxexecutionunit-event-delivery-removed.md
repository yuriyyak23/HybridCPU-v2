# 179 VmxExecutionUnit event delivery removed

Date: 2026-05-25

Status: closed

## Rule / basis

- VMX is a frozen compatibility frontend, not the owner of event delivery, completion routing, or event-queue state.
- Event delivery belongs to generic runtime descriptors, queue admission, remap policy, and completion/event routing.
- Heavy legacy cleanup for `VmxExecutionUnit` must proceed by removal-without-replacement, not return-to-Core.

## What changed

- Removed direct VMCS-backed pending virtual-event delivery from `Legacy/VMX/Compatibility/Frontend/Handlers/VmxExecutionUnit.cs`.
- Kept `TryDeliverPendingVirtualEventAtSafeBoundary(...)` as a frozen ABI compatibility hook, but made it fail closed with no delivery and no state mutation.
- Removed auto-delivery after VM-entry retire.
- Removed frontend-owned `VmxEventKind.EventDelivered` publication from the legacy frontend.
- Added `LegacyVmxExecutionUnitEventDeliveryRemovalContract`.
- Extended `VmxProjectionSchemaAndQuarantineTests` with source-level conformance for the removed event-delivery path.

## Verification

- Main build after code change: succeeded, 54 existing warnings, 0 errors.
- Test project build: succeeded, 93 existing warnings, 0 errors.
- `VmxProjectionSchemaAndQuarantineTests`: Passed 21/21.
- `CoreVmxAuthorityBoundaryTests`: Passed 1/1.
- `LegacyVmxExecutionUnit`: Passed 8/8.
- `DoesNotOwnVirtualEventDelivery`: Passed 1/1.
- `VmxCapsProjectionBoundaryTests`: Passed 3/3.
- `VmLaunchResumeTests`: Passed 12/12.
- `VmEntryExitCycleTests`: Passed 2/2.
- Static marker check found no `TryDeliverVirtualEvent`, `VmxEventKind.EventDelivered`, or private `TryDeliverPendingVirtualEvent(...)` path in `VmxExecutionUnit.cs`.

## Build result

- Final main project build after documentation update: succeeded, 0 warnings, 0 errors.

## Residual risk

- `VmxExecutionUnit.cs` remains quarantined and still owns broad VMX instruction frontend behavior, VMCS access routing, and the common VM-exit completion helper.
- `VmcsManager.cs` remains the second heavy legacy anchor.
- Future virtual-event delivery must be restored only through generic runtime event-delivery admission, not by reintroducing VMCS helper authority inside the frontend.
