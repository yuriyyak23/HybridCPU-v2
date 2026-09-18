# Closed: VMX opcode alias set

Date: 2026-05-24

## Rule / basis

VMX instruction names are frozen compatibility ABI surface. The VMX frontend may keep VMX/VMCS naming, while authority must remain outside handwritten VMCS/RTL logic and compatibility artifacts must be explicit and auditable.

## Changed

- Added `VmxOpcodeAliasKind` and `VmxOpcodeAlias`.
- Added `VmxOpcodeAliasSet.FrozenAliases` as a frozen compatibility opcode table.
- Added lookup helpers for containment and alias retrieval.
- Kept the change as metadata-only compatibility substrate; no decoder, encoder, RTL, or execution behavior was changed.

## Verified

Ran:

```text
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Build result

Build succeeded with existing warnings and `0 Error(s)`.
