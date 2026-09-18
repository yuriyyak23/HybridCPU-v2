# VMX Closure 230: VMCSv2 Block Mutable Helper Deletion

Date: 2026-05-26

## Selected Slice

VMCSv2 mutable helper authority audit for the next high-risk `VmcsV2Blocks` pool:

- root descriptor binding and epoch advance;
- NPT control binding;
- bundle binding;
- interrupt fabric exposure;
- event injection queue/remap/delivery/snapshot helpers;
- debug trace configure/record/reset helpers.

## What Changed

Removed without replacement from `Core/VMX/Compatibility/Generated/VmcsProjection/VmcsV2Blocks.cs`:

- `VmxRootControlBlock.BindRootDescriptor`;
- `VmxRootControlBlock.AdvanceEpoch`;
- `VmxNptBlock.BindControl`;
- `BundleExecutionBlock.BindBundle`;
- `VirtualInterruptFabricBlock.Fabric`;
- `VmxEventInjectionBlockSnapshot`;
- `EventInjectionBlock.ConfigureInterruptRemap`;
- `EventInjectionBlock.RemoveInterruptRemap`;
- `EventInjectionBlock.ClearInterruptRemaps`;
- `EventInjectionBlock.TryQueue`;
- `EventInjectionBlock.TryDeliver`;
- `EventInjectionBlock.CreateSnapshot`;
- `EventInjectionBlock.RestoreSnapshot`;
- `DebugTraceBlock.ConfigureExport`;
- `DebugTraceBlock.Record*`;
- `DebugTraceBlock.SnapshotCounters`;
- `DebugTraceBlock.ResetCounters`;
- `DebugTraceBlock.DiscardTraceHandles`.

The affected blocks now expose read-only/default compatibility projection state only.

## Authority Boundary

No production authority moved into VMX:

- no `VmxExecutionUnit`;
- no `VmcsManager` or `IVmcsManager`;
- no VMCS field store;
- no active pointer state;
- no adapter/manager replacement;
- no successful VMX backend path.

Real event queue/remap/delivery mechanics remain under neutral `Core/Runtime/Events/Injection`. Debug/observability mutation was not extracted in this slice; it remains denied until a neutral runtime owner exists.

## Tests And Fences

Updated:

- `VmcsV2MutableHelperAuthorityTests` now fences the removed root/NPT/bundle/event/debug methods, backing fields, snapshot type, and read-only default behavior.
- `EventTrapDomainIdentityAuthorityRemovalContract` now forbids VMCSv2 compatibility event projection from exposing `TryQueue`, `TryDeliver`, interrupt remap mutation, or snapshot restore helpers.
- `VmxProjectionSchemaAndQuarantineTests` now expects the VMCSv2 event block to be read-only projection rather than a neutral-identity executable helper.

## Counts

- `Core/VMX` legacy-marked C# sources: `0`.
- `Legacy/VMX` C# sources: `0`.
- Physical `Legacy/VMX`: absent.
- `VmxExecutionUnit.cs`, `VmcsManager.cs`, `IVmcsManager.cs`: absent.

## Verification

Passed:

- `dotnet build HybridCPU_ISE.csproj --no-restore`;
- `dotnet build HybridCPU_ISE.Tests.csproj --no-restore`;
- `dotnet test HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~VmcsV2MutableHelperAuthorityTests"`: `5/5`;
- `dotnet test HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~VmxProjectionSchemaAndQuarantineTests"`: `58/58`;
- `dotnet test HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~Vmx"`: `505/505`;
- static `Core/VMX` legacy-marker scan: `0`;
- static `Legacy/VMX` C# source inventory: `0`;
- physical carrier scan for `VmxExecutionUnit.cs`, `VmcsManager.cs`, `IVmcsManager.cs`: absent.

## Residual Risk

`ExitInfoBlock.Record*` and descriptor `Record*Exit` helpers remain allowed only as retire-publication compatibility projection. `VectorStreamStateBlock`, `DirtyLogBlock`, `SecurityIsolationBlock`, and `CapabilityNegotiationBlock` still need a private-set state surface audit.

## Next Step

Continue the residual VMCSv2 block authority audit:

- classify `VectorStreamStateBlock` private-set state after prior vector helper deletion;
- classify `DirtyLogBlock` read-only status/counters and overflow fields;
- classify `SecurityIsolationBlock` and `CapabilityNegotiationBlock` epoch shells;
- keep `ExitInfoBlock.Record*` fenced as retire-publication-only or route future publication through neutral domain completion first.
