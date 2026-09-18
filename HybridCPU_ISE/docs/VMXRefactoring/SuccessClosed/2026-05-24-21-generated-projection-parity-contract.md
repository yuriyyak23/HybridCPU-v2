# Closed: generated projection parity contract

Date: 2026-05-24

## Rule / basis

VMCS/VMCSv2 are generated compatibility projections over domain descriptors, not substrate authority. Every VMX-visible field must map to a substrate descriptor field, projection service, or explicit unsupported/denied result. Host-owned evidence must not be embedded into generated compatibility projection artifacts.

## Changed

- Replaced the empty `GeneratedProjectionParityContract` placeholder with a minimal conformance helper.
- Added `GeneratedProjectionParityRequest` to bind a generated projection artifact to a validated descriptor sideband source.
- Added explicit fail-closed parity violations for missing descriptor authority, missing generated-projection marker, source hash mismatch, missing artifact hash, and host evidence projection attempts.

## Verified

Ran:

```text
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Build result

Build succeeded with existing warnings and `0 Error(s)`.
