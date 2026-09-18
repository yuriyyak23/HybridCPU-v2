# Closed: VmxCaps capability schema mask

Date: 2026-05-24

## Source rule

Every `VmxCaps` bit must map to a `CapabilityDescriptorSet` source. Unknown capability bits must not leak through compatibility projection.

## Completed change

- Replaced the empty `CapabilityDescriptorSetSchema` placeholder with a minimal schema object.
- Added `KnownVmxV2CompatibilityMask` for the frozen VMX-v2 compatibility capability bits.
- Added helpers to filter projected compatibility capabilities and detect unknown compatibility bits.
- Updated `VmxCapsProjection` to filter reads through the schema and deny publication of unknown capability masks.

## Validation

- `dotnet build HybridCPU_ISE.csproj --no-restore`
- Result: passed with existing project warnings, `0 Error(s)`.
