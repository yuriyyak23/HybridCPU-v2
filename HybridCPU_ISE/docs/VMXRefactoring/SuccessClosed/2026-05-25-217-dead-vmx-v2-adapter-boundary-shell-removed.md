# Task 217: dead VMX v2 adapter boundary shell removed

Date: 2026-05-25
Status: closed

## Selected slice

- Deleted without replacement: `Legacy/VMX/Compatibility/Adapters/LegacyVmxV2/LegacyVmxV2AdapterBoundary.cs`.
- No production caller used its admission/result policy vocabulary. The real generated nested compatibility route constructs `ShadowVmcsNestedProjectionService` directly.

## Authority result

- The retained Shadow VMCS bridge remains an admitted generated compatibility caller path that fails closed with `CompatibilityProjectionFailed`; it is not nested runtime authority.
- Neutral nested projection/checkpoint ownership, typed capability grants, evidence, memory/I/O, lanes, completion, and retire legality remain outside `Legacy/VMX`.
- `VmxExecutionUnit`, `VmcsManager`, and `IVmcsManager` remain absent without replacement.

## Final removal-wave inventory

- Manifest marks the v2 boundary and the translation, CSR, and v1 slices as `RemovedWithoutReplacement`.
- Production `.cs` under `Legacy/VMX/Compatibility`: `3`, exactly `VmxInstructionPayload.cs`, `VmxRetireModel.cs`, and `ShadowVmcsNestedProjectionService.cs`.
- Total `.cs` under `Legacy/VMX`: `38`; focused conformance evidence replaces deleted shells in the count without expanding production runtime surface.

## Verification

- Production build: passed, projection lineage verified, `54` existing warnings, `0` errors.
- Tests build: passed, `93` existing warnings, `0` errors.
- `VmxProjectionSchemaAndQuarantineTests`: passed `56/56`.
- New tasks `214`-`217` removal filter: passed `4/4`.
- `CoreVmxAuthorityBoundaryTests`: passed `1/1`.
- Removed `VmxExecutionUnit` / `VmcsManager` filters: passed `19/19`.
- Prior I/O removal filter: passed `1/1`.
- Static scan: zero legacy-marked `.cs` under `Core/VMX`; all selected removed production paths and heavy carriers are absent.

## Residual risk / next heavy step

- VMX freeze is not declared. Perform final necessity and authority-mutation inventory for the three retained production compatibility files and the remaining generated/debug/lifecycle conformance surface.
