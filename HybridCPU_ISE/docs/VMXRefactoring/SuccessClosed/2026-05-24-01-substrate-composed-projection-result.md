# Closed: substrate composed projection result naming

Date: 2026-05-24

## Source rule

`VMCS12/VMCS02` must not remain normative substrate terminology. VMCS terms are allowed on the compatibility/projection boundary, but the authoritative nested model is domain composition.

## Completed change

- Renamed `ComposedDomainProjectionResult.Vmcs02` to `ComposedDomainProjectionResult.ComposedProjection`.
- Updated the compatibility `ShadowVmcsBlock` bridge to consume `ComposedProjection` while preserving its VMCS-facing compatibility members.

## Validation

- `dotnet build HybridCPU_ISE.csproj --no-restore`
- Result: passed with existing warnings, `0 Error(s)`.
