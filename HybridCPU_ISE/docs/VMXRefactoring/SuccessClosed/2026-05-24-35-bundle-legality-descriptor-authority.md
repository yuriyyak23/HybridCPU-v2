# Closed: bundle legality descriptor authority

Date: 2026-05-24

## Rule / basis

Runtime-owned legality must remain authoritative. Compiler evidence may be accepted as evidence, but it must not become the source of truth for virtualization bundle admission.

## Changed

- Added `BundleLegalityAuthority`.
- Added immutable `BundleLegalityDescriptor` state for authority, runtime validation, compiler evidence, and compatibility projection.
- Added classifiers for runtime authority, compiler evidence authority rejection, and compatibility projection.
- Kept the change metadata-only; no scheduler, compiler emission, decoder, ABI, or RTL behavior was changed.

## Verified

Ran:

```text
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Build result

Build succeeded with existing warnings and `0 Error(s)`.
