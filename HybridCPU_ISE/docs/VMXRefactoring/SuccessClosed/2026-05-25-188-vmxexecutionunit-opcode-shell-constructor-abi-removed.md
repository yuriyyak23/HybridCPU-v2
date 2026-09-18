# 188 VmxExecutionUnit opcode shell and constructor ABI removed

Date: 2026-05-25

Status: closed

## Rule / basis

- VMX is a frozen compatibility frontend, not the owner of execution or retire authority.
- A heavy frontend class that retains only a frozen opcode shell and constructor ABI is removed without replacement, never returned to `Core/VMX`.
- Frozen opcode identity may remain only as typed fail-closed compatibility metadata and effects until generic runtime admission supplies policy-checked behavior.

## What changed

- Deleted `Legacy/VMX/Compatibility/Frontend/Handlers/VmxExecutionUnit.cs` without replacement or Core return.
- Removed production `VmxExecutionUnit`, `VmxUnit`, and `_vmxUnit` dependencies from current dispatcher, micro-op, retire, and CPU-core state routing.
- `ExecutionDispatcherV4.VmxCompatibility`, `VmxMicroOp`, and `CPU_Core.PipelineExecution.VmxRetire` now materialize and apply only typed fail-closed `SecurityPolicyViolation` effects for published frozen VMX opcodes.
- Changed the `LegacyVmxQuarantineManifest` entry to `RemovedWithoutReplacement` and added `LegacyVmxExecutionUnitRemovalContract`.
- Converted current conformance and metadata tests away from the deleted constructor ABI; obsolete success-path shell tests are retained only as non-executable historical specification text.
- `Legacy/VMX` now contains only `Substrate/Runtime/Binding/VmcsManager.cs`.

## Verification

- Main build after production removal: succeeded, 54 existing warnings, 0 errors.
- Test project build: succeeded, 36 existing warnings, 0 errors.
- `VmxProjectionSchemaAndQuarantineTests`: Passed 29/29.
- `CoreVmxAuthorityBoundaryTests`, `VmxCapsProjectionBoundaryTests`, and `VmxInstructionMetadataConsistencyTests`: Passed 30/30.
- `RemovedLegacyVmxExecutionUnit`: Passed 15/15.
- Static inventory check found only `Legacy/VMX/Substrate/Runtime/Binding/VmcsManager.cs` under `Legacy/VMX`.

## Build result

- Final main project build after documentation update: succeeded, 0 errors.

## Residual risk

- `VmcsManager.cs` is now the only remaining heavy legacy file and still must be demoted to a projection handle/generic runtime binding or removed without restoring VMCS authority.
- Generic nested-domain projection/checkpoint service, capability authority cleanup, VMX-shaped translation vocabulary, generator pipeline, and physical `Core/VMX/Substrate` relocation remain open.
- Frozen VMX behavior remains fail closed until generic policy-checked runtime operations are admitted.
