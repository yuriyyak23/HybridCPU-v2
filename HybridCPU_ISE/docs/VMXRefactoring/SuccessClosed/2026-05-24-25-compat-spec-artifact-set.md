# Closed: compatibility spec artifact set

Date: 2026-05-24

## Rule / basis

Generated compatibility projection should be explicit and covered by conformance/golden artifacts. VMX may remain a compatibility frontend, while architecture authority stays in descriptor/capability/evidence/runtime substrate.

## Changed

- Added `CompatSpecArtifactKind` and `CompatSpecArtifact`.
- Added an explicit registry for frozen opcode aliases, frozen CSR aliases, VMCS field aliases, capability projection, and completion projection artifacts.
- Added helpers for generated parity and ABI freeze classification.
- Kept the change metadata-only; no generator wiring, emitted code, RTL logic, or frozen ABI names were changed.

## Verified

Ran:

```text
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Build result

Build succeeded with existing warnings and `0 Error(s)`.
