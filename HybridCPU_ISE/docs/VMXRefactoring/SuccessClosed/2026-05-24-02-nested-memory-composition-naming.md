# Closed: nested memory composition naming

Date: 2026-05-24

## Source rule

Nested mode should be domain/memory composition, not public `VMCS12/VMCS02` or NPT-centered architecture vocabulary. NPT remains an implementation/compatibility detail where the lower translation control still exposes legacy names.

## Completed change

- Renamed `NestedExitMapper.FromNestedNpt` to `NestedExitMapper.FromNestedMemoryComposition`.
- Renamed `NestedExitMapper.Npt.partial.cs` to `NestedExitMapper.MemoryComposition.partial.cs`.
- Renamed public nested composition stage names from `L1Npt`/`L0Npt` to `ChildDomainTranslation`/`HostDomainTranslation`.
- Renamed `NestedMemoryCompositionContext` public control/epoch fields from L1/L0 NPT wording to child/host translation wording.

## Validation

- `dotnet build HybridCPU_ISE.csproj --no-restore`
- Result: passed with existing warnings, `0 Error(s)`.
