# Closed: compatibility alias map metadata

Date: 2026-05-24

## Rule / basis

Every frozen compatibility opcode, CSR, field, completion, or leaf must have a substrate target in the alias map. VMX remains a compatibility frontend; authoritative state belongs to domain runtime operations, descriptors, capability grants, and completion records.

## Changed

- Added `CompatAliasSourceKind`, `CompatAliasTargetKind`, and `CompatAliasMapEntry`.
- Added a small compatibility alias map for VMXON/VMXOFF, VMREAD/VMWRITE, `VmxCaps`, exit CSRs, VMCS fields, completion projection, and function leaves.
- Added lookup helpers for source coverage and frozen ABI classification.
- Kept the change metadata-only; no VMX handler, decoder, CSR address, VMCS projection, or emitted code was changed.

## Verified

Ran:

```text
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Build result

Build succeeded with existing warnings and `0 Error(s)`.
