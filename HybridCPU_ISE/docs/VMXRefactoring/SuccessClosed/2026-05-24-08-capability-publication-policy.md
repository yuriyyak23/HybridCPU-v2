# Closed: fail-closed CapabilityPublicationPolicy

Date: 2026-05-24

## Source rule

`VmxCaps` must be a pure compatibility alias over `CapabilityDescriptorSet`. Compatibility publication is observational only, and writes to capability publication aliases must fail closed or have no architectural effect.

## Completed change

- Replaced the empty `CapabilityPublicationPolicy` placeholder with a minimal fail-closed policy object.
- Added explicit compatibility alias publication gating.
- Added `CapabilityWriteDisposition` for reject vs compatibility no-effect write behavior.
- Added projection and single-capability publication predicates driven by `CapabilityDescriptorSet.EffectiveCaps`.

## Validation

- `dotnet build HybridCPU_ISE.csproj --no-restore`
- Result: passed with existing project warnings, `0 Error(s)`.
