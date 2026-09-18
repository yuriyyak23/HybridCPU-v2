# Closed: capability negotiation service boundary

Date: 2026-05-24

## Rule / Basis

- Capabilities must be owned by `CapabilityDescriptorSet`, not by VMX/VMCS/CSR state.
- VMX capability publication must be a compatibility projection over substrate capabilities.
- Capability negotiation must fail closed when hardware, runtime, or domain grants are missing.
- Frozen VMX/VMCS ABI names and RTL behavior must not change for this small refactoring.

## Changed

- Added `CapabilityNegotiationDecision` to make grant/deny reasons explicit.
- Added `CapabilityNegotiationResult` as a typed result carrying both decision and `CapabilityGrant`.
- Implemented `CapabilityNegotiationService.Negotiate` so grants are derived only from `CapabilityDescriptorSet` hardware/runtime/domain checks.
- Added `NegotiateGrant` convenience helper and `ProjectEffectiveCompatibilityCaps` for read-only compatibility projection.
- Kept the change substrate-local: no VMX instruction handlers, VMX CSR behavior, VMCS ABI names, generated projection surface, or RTL behavior were changed.

## Verified

- Ran:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Build Result

- Build succeeded.
- 54 existing warnings.
- 0 errors.
