# 180 VmxExecutionUnit VMCS access routing removed

Date: 2026-05-25

Status: closed

## Rule / basis

- VMX is a frozen compatibility frontend, not the owner of VMCS field authority.
- `VMREAD`/`VMWRITE` must be admitted by generated projection access policy, descriptor ownership, evidence policy, and no-emission rules.
- Heavy legacy cleanup for `VmxExecutionUnit` must proceed by removal-without-replacement, not return-to-Core.

## What changed

- Removed direct VMCS field read/write routing from `Legacy/VMX/Compatibility/Frontend/Handlers/VmxExecutionUnit.cs`.
- Removed nested VMCS read/write routing from the legacy frontend.
- Made `VMREAD` and `VMWRITE` fail closed with `SecurityPolicyViolation` until generated projection/access-policy admission owns the path.
- Added `LegacyVmxExecutionUnitVmcsAccessRoutingRemovalContract`.
- Extended `VmxProjectionSchemaAndQuarantineTests` with source-level conformance for the removed VMCS access helper paths.

## Verification

- Main build after code change: succeeded, 54 existing warnings, 0 errors.
- Test project build: succeeded, 93 existing warnings, 0 errors.
- `VmxProjectionSchemaAndQuarantineTests`: Passed 23/23.
- `CoreVmxAuthorityBoundaryTests`: Passed 1/1.
- `DoesNotOwnVmcsAccessRouting`: Passed 1/1.
- `LegacyVmxExecutionUnit`: Passed 10/10.
- Static marker check found no `ReadFieldValue`, `WriteFieldValue`, `TryNestedVmRead`, `TryNestedVmWrite`, `ApplyNestedVmcsAccessFailure`, `VmxRetireEffect.VmcsRead`, or `VmxRetireEffect.VmcsWrite` in `VmxExecutionUnit.cs`.

## Build result

- Final main project build after documentation update: succeeded, 0 warnings, 0 errors.

## Residual risk

- `VmxExecutionUnit.cs` remains quarantined and still owns broad VMX instruction frontend behavior plus VMXON/VMXOFF, VM-entry, VMPTR/VMCLEAR pointer lifecycle, and some trace/fail publication paths.
- `VmcsManager.cs` remains the second heavy legacy anchor.
- Future VMCS field access must be restored only through generated projection access policy, not by reintroducing VMCS helper authority inside the frontend.
