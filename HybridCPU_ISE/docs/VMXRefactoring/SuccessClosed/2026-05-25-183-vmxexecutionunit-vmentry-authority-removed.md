# 183 VmxExecutionUnit VM-entry authority removed

Date: 2026-05-25

Status: closed

## Rule / basis

- VMX is a frozen compatibility frontend, not the owner of execution-domain entry.
- `VMLAUNCH` and `VMRESUME` must map to generic `DomainEnter` admission, descriptor policy, evidence policy, and completion routing.
- Heavy legacy cleanup for `VmxExecutionUnit` must proceed by removal-without-replacement, not return-to-Core.

## What changed

- Removed frontend-owned VM-entry authority from `Legacy/VMX/Compatibility/Frontend/Handlers/VmxExecutionUnit.cs`.
- `VMLAUNCH` and `VMRESUME` now fail closed with `SecurityPolicyViolation`.
- Retire no longer calls `_vmcs.BeginVmEntry(...)`.
- The legacy frontend no longer consumes `VmEntryTransitionResult` or restores guest PC/SP from VMCS.
- The legacy frontend no longer emits `VmxEventKind.VmEntry` or `VmxEventKind.VmResume`.
- `ResolveFinalPipelineState(...)` no longer moves `VmLaunch` / `VmResume` through frontend-owned `VmLaunch`, `VmResume`, `EntryOk`, or `EntryFail` pipeline triggers.
- Added `LegacyVmxExecutionUnitVmEntryAuthorityRemovalContract`.
- Extended `VmxProjectionSchemaAndQuarantineTests` with source-level conformance for the removed VM-entry authority.

## Verification

- Main build after code change: succeeded, 54 existing warnings, 0 errors.
- Test project build: succeeded, 93 existing warnings, 0 errors.
- `VmxProjectionSchemaAndQuarantineTests`: Passed 25/25.
- `CoreVmxAuthorityBoundaryTests`: Passed 1/1.
- `DoesNotOwnVmEntryAuthority`: Passed 1/1.
- `LegacyVmxExecutionUnit`: Passed 12/12.
- Static marker check found no `BeginVmEntry`, `VmEntryTransitionResult`, `VmxEventKind.VmEntry`, `VmxEventKind.VmResume`, `GuestPc`, `GuestSp`, `PipelineTransitionTrigger.VmLaunch`, `PipelineTransitionTrigger.VmResume`, `PipelineTransitionTrigger.EntryOk`, `PipelineTransitionTrigger.EntryFail`, `ResolveVmLaunch`, `ResolveVmResume`, or `ApplyVmEntry` in `VmxExecutionUnit.cs`.

## Build result

- Final main project build after documentation update: succeeded, 0 warnings, 0 errors.

## Residual risk

- `VmxExecutionUnit.cs` remains quarantined and still owns VMXON/VMXOFF plus some fail/trace publication paths.
- `VmcsManager.cs` remains the second heavy legacy anchor.
- Future VM-entry behavior must be restored only through generic `DomainEnter` admission/completion routing, not by reintroducing VMCS entry helper authority inside the frontend.
