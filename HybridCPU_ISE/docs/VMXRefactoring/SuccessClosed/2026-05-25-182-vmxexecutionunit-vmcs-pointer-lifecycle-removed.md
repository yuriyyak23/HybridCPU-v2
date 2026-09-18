# 182 VmxExecutionUnit VMCS pointer lifecycle removed

Date: 2026-05-25

Status: closed

## Rule / basis

- VMX is a frozen compatibility frontend, not the owner of execution-domain binding or projection-handle authority.
- `VMCLEAR`, `VMPTRLD`, and `VMPTRST` must bind/read projection handles only through generic execution-domain binding admission.
- Heavy legacy cleanup for `VmxExecutionUnit` must proceed by removal-without-replacement, not return-to-Core.

## What changed

- Removed frontend-owned VMCS pointer lifecycle from `Legacy/VMX/Compatibility/Frontend/Handlers/VmxExecutionUnit.cs`.
- `VMCLEAR`, `VMPTRLD`, and `VMPTRST` no longer resolve VMCS pointer operands into `VmxRetireEffect.VmcsPointerEffect(...)` or `VmxRetireEffect.VmPtrSt(...)`.
- Retire no longer calls `_vmcs.ClearPointer(...)`, `_vmcs.LoadPointer(...)`, or `_vmcs.StorePointer(...)`.
- Retire no longer emits `VmxEventKind.VmClear` or `VmxEventKind.VmPtrLd` from the legacy frontend.
- The compatibility opcodes now fail closed with `SecurityPolicyViolation` until generic execution-domain binding/projection-handle admission owns the path.
- Added `LegacyVmxExecutionUnitVmcsPointerLifecycleRemovalContract`.
- Extended `VmxProjectionSchemaAndQuarantineTests` with source-level conformance for the removed pointer lifecycle authority.

## Verification

- Main build after code change: succeeded, 54 existing warnings, 0 errors.
- Test project build: succeeded, 93 existing warnings, 0 errors.
- `VmxProjectionSchemaAndQuarantineTests`: Passed 24/24.
- `CoreVmxAuthorityBoundaryTests`: Passed 1/1.
- `DoesNotOwnVmcsPointerLifecycle`: Passed 1/1.
- `LegacyVmxExecutionUnit`: Passed 11/11.
- Static marker check found no `VmcsPointerEffect`, `VmxRetireEffect.VmPtrSt`, `ClearPointer`, `LoadPointer`, `StorePointer`, `VmcsPointerResult`, `VmxEventKind.VmClear`, or `VmxEventKind.VmPtrLd` in `VmxExecutionUnit.cs`.

## Build result

- Final main project build after documentation update: succeeded, 0 warnings, 0 errors.

## Residual risk

- `VmxExecutionUnit.cs` remains quarantined and still owns broad VMX instruction frontend behavior plus VMXON/VMXOFF, VM-entry, and some fail/trace publication paths.
- `VmcsManager.cs` remains the second heavy legacy anchor.
- Future VMCS pointer/projection-handle behavior must be restored only through generic execution-domain binding admission, not by reintroducing VMCS pointer helper authority inside the frontend.
