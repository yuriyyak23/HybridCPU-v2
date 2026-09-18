# 178 VmxExecutionUnit lane/vector runtime authority removed

Date: 2026-05-25

Status: closed

## Rule / basis

- VMX is a frozen compatibility frontend, not the owner of Lane6/Lane7/vector-stream runtime state.
- Lane/vector ownership belongs to generic descriptors, tokens, fences, completion routing, and runtime admission.
- Heavy legacy cleanup for `VmxExecutionUnit` must proceed by removal-without-replacement, not return-to-Core.

## What changed

- Removed direct Lane7 VMFUNC resolution from `Legacy/VMX/Compatibility/Frontend/Handlers/VmxExecutionUnit.cs`.
- Removed direct vector-stream extended-state validation/save/restore helper use from `VMSAVEX` and `VMRESTX` handling.
- Preserved `VMFUNC CapabilityQuery` as frozen compatibility vocabulary.
- Made non-admitted VMFUNC leaves, `VMSAVEX`, and `VMRESTX` fail closed with `SecurityPolicyViolation` until generic lane/vector runtime admission exists.
- Added `LegacyVmxExecutionUnitLaneVectorAuthorityRemovalContract`.
- Extended `VmxProjectionSchemaAndQuarantineTests` with source-level conformance for the removed Lane7/vector-stream helper paths.

## Verification

- Main build after code change: succeeded, 54 existing warnings, 0 errors.
- Test project build: succeeded, 93 existing warnings, 0 errors.
- `VmxProjectionSchemaAndQuarantineTests`: Passed 20/20.
- `CoreVmxAuthorityBoundaryTests`: Passed 1/1.
- `LegacyVmxExecutionUnit`: Passed 7/7.
- `DoesNotOwnLane7OrVectorStreamRuntimePaths`: Passed 1/1.
- `VmxCapsProjectionBoundaryTests`: Passed 3/3.
- Static marker check found no `TryResolveLane7VmFunc`, `Lane7VmFuncResult`, `TrySaveVectorStreamState`, `TryRestoreVectorStreamState`, `TryValidateVectorStreamExtendedStateMask`, `TryDecodeVectorStreamSaveMask`, `DescriptorMatchesActiveVmcs`, or `VectorStreamSaveMask` in `VmxExecutionUnit.cs`.

## Build result

- Final main project build after documentation update: succeeded, 0 warnings, 0 errors.

## Residual risk

- `VmxExecutionUnit.cs` remains quarantined and still owns broad VMX instruction frontend behavior, VMCS access routing, event delivery calls, and the common VM-exit completion helper.
- `VmcsManager.cs` remains the second heavy legacy anchor.
- Future Lane7/vector-stream functionality must be restored only through generic runtime descriptors/admission, not by reintroducing VMCS helper authority.
