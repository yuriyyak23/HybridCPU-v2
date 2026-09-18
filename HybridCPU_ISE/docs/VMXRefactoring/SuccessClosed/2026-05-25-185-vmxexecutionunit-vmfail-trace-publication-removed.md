# 185 VmxExecutionUnit VMFail / trace publication removed

Date: 2026-05-25

Status: closed

## Rule / basis

- VMX is a frozen compatibility frontend, not the owner of fault, trace, or VMX CSR publication.
- `VMFail` must map to generic descriptor/projection validation failure and domain-fault publication.
- Heavy legacy cleanup for `VmxExecutionUnit` must proceed by removal-without-replacement, not return-to-Core.

## What changed

- Removed frontend-owned common VMFail / trace / VMX CSR publication from `Legacy/VMX/Compatibility/Frontend/Handlers/VmxExecutionUnit.cs`.
- Fault retire now returns `VmxRetireOutcome.Fault(...)` without writing VMX exit CSRs.
- Unsupported invalidation retire no longer writes `CsrAddresses.VmxExitReason` or calls `_vmcs.RecordVmxFailForObservability(...)`.
- Non-guest VMCALL retire no longer writes `CsrAddresses.VmxExitQual`.
- The legacy frontend no longer keeps a trace sink field and no longer calls trace publication APIs.
- Updated invalidation fail-closed coverage so it asserts denied paths do not publish `VmxExitReason`.
- Added `LegacyVmxExecutionUnitFailTracePublicationRemovalContract`.
- Extended `VmxProjectionSchemaAndQuarantineTests` with source-level conformance for the removed VMFail / trace publication authority.

## Verification

- Main build after code change: succeeded, 54 existing warnings, 0 errors.
- Test project build: succeeded, 36 existing warnings, 0 errors.
- `VmxProjectionSchemaAndQuarantineTests`: Passed 27/27.
- `CoreVmxAuthorityBoundaryTests`: Passed 1/1.
- `DoesNotOwnVmFailOrTracePublication`: Passed 1/1.
- `LegacyVmxExecutionUnit`: Passed 14/14.
- Static marker check found no `RecordVmxFailForObservability`, `RecordVmxEvent`, `HardwareWrite`, `CsrAddresses.VmxExitReason`, `CsrAddresses.VmxExitQual`, or `VmxEventKind.` in `VmxExecutionUnit.cs`.

## Build result

- Final main project build after documentation update: succeeded, 54 existing warnings, 0 errors.

## Residual risk

- `VmxExecutionUnit.cs` remains quarantined and still owns broad VMX instruction frontend compatibility behavior, VMCALL/VMFUNC capability-query gates, guest-intercept request construction, and VMCS-backed legacy guard checks.
- `VmcsManager.cs` remains the second heavy legacy anchor.
- Future VMFail/trace visibility must be restored only through generic retire/domain-fault publication, not by reintroducing frontend CSR or trace shortcuts.
