# 181 VmxExecutionUnit common VM-exit completion helper removed

Date: 2026-05-25

Status: closed

## Rule / basis

- VMX is a frozen compatibility frontend, not the owner of completion routing or VM-exit publication.
- VM-exit completion must be owned by generic domain-trap/domain-fault routing, retire publication, and completion-route descriptors.
- Heavy legacy cleanup for `VmxExecutionUnit` must proceed by removal-without-replacement, not return-to-Core.

## What changed

- Removed `CompleteQualifiedVmExit(...)` from `Legacy/VMX/Compatibility/Frontend/Handlers/VmxExecutionUnit.cs`.
- Removed common qualified VM-exit publication through `_vmcs.RecordQualifiedVmExit(...)`, `VmxExitCnt`, and frontend `VmxEventKind.VmExit` shortcuts.
- Former helper callers now fail closed with `SecurityPolicyViolation` until generic domain-trap/domain-fault completion routing owns the path.
- Added `LegacyVmxExecutionUnitExitCompletionRemovalContract`.
- Extended `VmxProjectionSchemaAndQuarantineTests` with source-level conformance for the removed common VM-exit completion helper.

## Verification

- Main build after code change: succeeded, 54 existing warnings, 0 errors.
- Test project build: succeeded, 93 existing warnings, 0 errors.
- `VmxProjectionSchemaAndQuarantineTests`: Passed 23/23.
- `CoreVmxAuthorityBoundaryTests`: Passed 1/1.
- `DoesNotOwnCommonVmExitCompletion`: Passed 1/1.
- `LegacyVmxExecutionUnit`: Passed 10/10.
- Static marker check found no `CompleteQualifiedVmExit`, `RecordQualifiedVmExit`, `CompleteVmExit`, `VmxExitCnt`, or `VmxEventKind.VmExit` in `VmxExecutionUnit.cs`.

## Build result

- Final main project build after documentation update: succeeded, 0 warnings, 0 errors.

## Residual risk

- `VmxExecutionUnit.cs` remains quarantined and still owns broad VMX instruction frontend behavior plus VMXON/VMXOFF, VM-entry, VMPTR/VMCLEAR pointer lifecycle, and some trace/fail publication paths.
- `VmcsManager.cs` remains the second heavy legacy anchor.
- Future VM-exit completion/publication must be restored only through generic domain-trap/domain-fault routing, not by reintroducing the legacy frontend helper.
