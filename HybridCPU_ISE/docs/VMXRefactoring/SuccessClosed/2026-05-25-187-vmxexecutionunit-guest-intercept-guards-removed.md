# 187 VmxExecutionUnit guest-intercept guards removed

Date: 2026-05-25

Status: closed

## Rule / basis

- VMX is a frozen compatibility frontend, not the owner of guest-intercept trap construction.
- Guest-intercept routing must be admitted by generic domain-trap descriptors and completion routing, not by VMCS-backed frontend guard checks.
- Heavy legacy cleanup for `VmxExecutionUnit` must proceed by removal-without-replacement, not return-to-Core.

## What changed

- Removed guest-intercept request construction from `Legacy/VMX/Compatibility/Frontend/Handlers/VmxExecutionUnit.cs`.
- Removed VMCS-backed guest-intercept guard checks and intercept resolution from the frontend route.
- The legacy frontend no longer keeps `_csr`, `_vmcs`, `_vmxCapsProjection`, or `_capabilityDescriptorSource` fields.
- `TryExecuteGuestIntercept(...)` remains as a frozen compatibility ABI hook but now fails closed without constructing a trap request or consulting VMCS state.
- Removed the old helper family around `TryResolveGuestIntercept*`, `TryBuild*Csr/Memory/Lane*InterceptRequest`, `TrapRequest.For*`, and `ReadVmxSourceOperand`.
- Added `LegacyVmxExecutionUnitGuestInterceptRemovalContract`.
- Extended `VmxProjectionSchemaAndQuarantineTests` with source-level conformance and runtime fail-closed coverage for the safe-boundary hook.

## Verification

- Main build after code change: succeeded, 54 existing warnings, 0 errors.
- Test project build: succeeded, 36 existing warnings, 0 errors.
- `VmxProjectionSchemaAndQuarantineTests`: Passed 32/32.
- `CoreVmxAuthorityBoundaryTests`: Passed 1/1.
- `DoesNotBuildGuestInterceptRequests`: Passed 1/1.
- `GuestInterceptHookFailsClosedWithoutVmcsResolution`: Passed 1/1.
- `LegacyVmxExecutionUnit`: Passed 19/19.
- Static marker check found no `TryResolveGuestVmxOperationIntercept`, `TryResolveGuestIntercept`, `TryResolveInterceptRequest`, `TryBuildCsrInterceptRequest`, `TryBuildMemoryInterceptRequest`, `TryBuildLaneInterceptRequest`, `TrapRequest.For`, `_vmcs.`, `_csr.`, `HasActiveVmcs`, `CsrAddresses.VmxEnable`, `MemoryTranslationControl.Disabled`, or `ReadVmxSourceOperand` in `VmxExecutionUnit.cs`.

## Build result

- Final main project build after documentation update: succeeded, 54 existing warnings, 0 errors.

## Residual risk

- `VmxExecutionUnit.cs` remains quarantined and still owns a broad frozen opcode compatibility shell and constructor ABI surface.
- `VmcsManager.cs` remains the second heavy legacy anchor.
- Future guest-intercept behavior must be restored only through generic domain-trap admission/routing, not by reintroducing frontend VMCS-backed guard checks or `TrapRequest.For*` construction.
