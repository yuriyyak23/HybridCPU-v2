# Closed: virtualization golden artifact manifest

Date: 2026-05-24

## Rule / basis

VMX refactoring requires hard architectural gates for ABI snapshots, generated parity, alias-map completeness, host-evidence non-leak, migration replay, and nested composition. These gates must describe compatibility/conformance artifacts without making VMX the authority owner.

## Changed

- Added `VirtualizationGoldenArtifactKind` and `VirtualizationGoldenArtifact`.
- Added manifest entries for compatibility ABI, generated projection parity, alias-map completeness, host-evidence non-leak, migration replay, and nested composition.
- Added helper methods for lookup, hard-gate classification, and generated-source dependency.
- Kept the change metadata-only; no runtime behavior, RTL, ABI names, or generated projection wiring was changed.

## Verified

Ran:

```text
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Build result

Build succeeded with existing warnings and `0 Error(s)`.
