# Closed: compatibility ABI freeze contract

Date: 2026-05-24

## Rule / basis

VMX8 ABI, CSR aliases, VMCS field aliases, opcode ids, operand forms, retire kinds, and exit reasons are compatibility ABI and must remain stable. VMX remains a frontend/projection layer; new substrate authority must not silently expand the frozen compatibility surface.

## Changed

- Replaced the empty `CompatAbiFreezeContract` placeholder with a minimal conformance helper.
- Added frozen opcode ranges for the VMX core and extension opcodes.
- Added frozen CSR alias validation for `VmxEnable`, `VmxCaps`, `VmxControl`, `VmxExitReason`, and `VmxExitQual`.
- Added VMCS field alias validation against the frozen `VmcsField` enum.

## Verified

Ran:

```text
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Build result

Build succeeded with existing warnings and `0 Error(s)`.
