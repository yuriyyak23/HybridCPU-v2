# Closed: nested substrate capability naming

Date: 2026-05-24

## Source rule

Nested mode is domain composition, not public `VMCS12/VMCS02` or NPT-centered architecture. VMCS/NPT names may remain in compatibility projection or implementation-specific translation controls, but substrate capability and gate names should use domain/memory composition vocabulary.

## Completed change

- Renamed `NestedEnablementGate.VmcsPolicyEnabled` to `NestedCompatibilityPolicyEnabled`.
- Renamed `NestedEnablementGate.NestedNptReady` to `NestedMemoryCompositionReady`.
- Renamed `NestedCapabilityGrantMask.Vmcs02Composition` to `ComposedDomainProjection`.
- Renamed `NestedCapabilityGrantMask.NestedNptComposition` to `NestedMemoryComposition`.
- Updated `RequiredForPhase7` masks to use the generic substrate names.

## Validation

- `dotnet build HybridCPU_ISE.csproj --no-restore`
- Result: passed with existing project warnings, `0 Error(s)`.
