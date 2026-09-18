# 186 VmxExecutionUnit VMCALL / VMFUNC capability gates removed

Date: 2026-05-25

Status: closed

## Rule / basis

- VMX is a frozen compatibility frontend, not the owner of runtime capability authority.
- `VMCALL` / `VMFUNC` capability-query behavior must be admitted by generic runtime capability descriptors and typed publication policy.
- Heavy legacy cleanup for `VmxExecutionUnit` must proceed by removal-without-replacement, not return-to-Core.

## What changed

- Removed frontend-owned VMCALL/VMFUNC capability-query gates from `Legacy/VMX/Compatibility/Frontend/Handlers/VmxExecutionUnit.cs`.
- Removed raw VMX CSR and capability projection readback from the VMCALL/VMFUNC path.
- `VMCALL` and `VMFUNC` now resolve and retire as fail-closed `SecurityPolicyViolation` outcomes until generic runtime capability admission owns those compatibility operations.
- The legacy frontend no longer references `VmxV2InstructionCaps.VmCall`, `VmxV2InstructionCaps.VmFunc`, `VmxFunctionLeaf.CapabilityQuery`, `ReadProjectedVmxCaps(...)`, or `IsVmxV2CapabilityEnabled(...)`.
- Added `LegacyVmxExecutionUnitVmCallVmFuncGateRemovalContract`.
- Extended `VmxProjectionSchemaAndQuarantineTests` with source-level conformance and runtime fail-closed coverage for VMCALL/VMFUNC.
- Updated `VmxCapsProjectionBoundaryTests` so capability projection remains tested directly, without relying on VMFUNC as a publication path.

## Verification

- Main build after code change: succeeded, 54 existing warnings, 0 errors.
- Test project build: succeeded, 36 existing warnings, 0 errors.
- `VmxProjectionSchemaAndQuarantineTests`: Passed 32/32.
- `CoreVmxAuthorityBoundaryTests`: Passed 1/1.
- `DoesNotOwnVmCallVmFuncCapabilityGates`: Passed 1/1.
- `VmCallVmFuncFailClosedWithoutCapabilityPublication`: Passed 2/2.
- `VmxCapsProjectionBoundaryTests`: Passed 3/3.
- Static marker check found no `ResolveVmCall`, `ResolveVmFunc`, `ApplyVmCall`, `ApplyVmFunc`, `ReadProjectedVmxCaps`, `IsVmxV2CapabilityEnabled`, `VmxFunctionLeaf.CapabilityQuery`, `CsrAddresses.VmxControl`, `VmxV2ControlBits.VmFuncCapabilityQuery`, `VmxV2InstructionCaps.VmCall`, `VmxV2InstructionCaps.VmFunc`, `VmxRetireEffect.VmCall`, or `VmxRetireEffect.VmFunc` in `VmxExecutionUnit.cs`.

## Build result

- Final main project build after documentation update: succeeded, 54 existing warnings, 0 errors.

## Residual risk

- `VmxExecutionUnit.cs` remains quarantined and still owns a broad frozen opcode compatibility shell and constructor ABI surface.
- `VmcsManager.cs` remains the second heavy legacy anchor.
- Future VMCALL/VMFUNC behavior must be restored only through generic runtime capability admission and typed publication policy, not by reintroducing frontend CSR or capability readback.
