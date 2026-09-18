# 184 VmxExecutionUnit VMX root-switch authority removed

Date: 2026-05-25

Status: closed

## Rule / basis

- VMX is a frozen compatibility frontend, not the owner of runtime root mode.
- `VMXON` / `VMXOFF` must map to generic runtime-domain admission and compatibility activation policy.
- Heavy legacy cleanup for `VmxExecutionUnit` must proceed by removal-without-replacement, not return-to-Core.

## What changed

- Removed frontend-owned VMXON/VMXOFF root-switch authority from `Legacy/VMX/Compatibility/Frontend/Handlers/VmxExecutionUnit.cs`.
- `VMXON` and `VMXOFF` now fail closed with `SecurityPolicyViolation`.
- Resolve no longer creates `VmxRetireEffect.VmxOnRootDescriptor(...)`.
- Retire no longer calls `_vmcs.ActivateRootDescriptor(...)`.
- Retire no longer writes `CsrAddresses.VmxEnable`.
- The legacy frontend no longer emits `VmxEventKind.VmxOn` or `VmxEventKind.VmxOff`.
- `ResolveFinalPipelineState(...)` no longer drives `PipelineTransitionTrigger.VmxOff`.
- Added `LegacyVmxExecutionUnitVmxRootSwitchRemovalContract`.
- Extended `VmxProjectionSchemaAndQuarantineTests` with source-level conformance for the removed VMX root-switch authority.

## Verification

- Main build after code change: succeeded, 54 existing warnings, 0 errors.
- Test project build: succeeded, 36 existing warnings, 0 errors.
- `VmxProjectionSchemaAndQuarantineTests`: Passed 27/27.
- `CoreVmxAuthorityBoundaryTests`: Passed 1/1.
- `DoesNotOwnVmxRootSwitchAuthority`: Passed 1/1.
- `LegacyVmxExecutionUnit`: Passed 14/14.
- Static marker check found no `ResolveVmxOn`, `ResolveVmxOff`, `VmxOnRootDescriptor`, `ActivateRootDescriptor`, `_csr.Write(CsrAddresses.VmxEnable`, `VmxEventKind.VmxOn`, `VmxEventKind.VmxOff`, or `PipelineTransitionTrigger.VmxOff` in `VmxExecutionUnit.cs`.

## Build result

- Final main project build after documentation update: succeeded, 54 existing warnings, 0 errors.

## Residual risk

- `VmxExecutionUnit.cs` remains quarantined and still owns broad VMX instruction frontend compatibility behavior, VMCALL/VMFUNC capability-query gates, guest-intercept request construction, and VMCS-backed legacy guard checks.
- `VmcsManager.cs` remains the second heavy legacy anchor.
- Future VMXON/VMXOFF behavior must be restored only through generic runtime-domain admission and compatibility activation policy, not by reintroducing root descriptor / `VmxEnable` authority inside the frontend.
