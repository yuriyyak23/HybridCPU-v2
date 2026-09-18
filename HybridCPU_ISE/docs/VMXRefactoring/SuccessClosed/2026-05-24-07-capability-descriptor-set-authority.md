# Closed: minimal CapabilityDescriptorSet authority

Date: 2026-05-24

## Source rule

Capabilities belong to `CapabilityDescriptorSet`; `VmxCaps` is only an observational compatibility alias and must not become grant state, policy state, migration state, or scheduler evidence.

## Completed change

- Replaced the empty `CapabilityDescriptorSet` placeholder with a minimal authority model.
- Added separate `GlobalHardwareCaps`, `RuntimeEnabledCaps`, and `DomainGrantedCaps` masks.
- Added `EffectiveCaps` as the intersection of hardware, runtime, and domain grant authority.
- Replaced the empty `CapabilityGrant` placeholder with a typed grant object and `CapabilityGrantScope`.

## Validation

- `dotnet build HybridCPU_ISE.csproj --no-restore`
- Result: passed with existing project warnings, `0 Error(s)`.
