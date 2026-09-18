# Success Closure 233: Projection Timer And Trap Bitmap Extraction

Date: 2026-05-27

## Selected Slice

Extract/delete the first forbidden-authority projection carriers:

- `Core/VMX/Compatibility/Frontend/Projection/Events/SchedulingBudgetTimer.cs`;
- `Core/VMX/Compatibility/Frontend/Projection/Events/TrapPolicyBitmap.cs`.

`VmcsV2Header` launch/invalidation epoch authority and `ChildDomainIntentDescriptor` child-intent state are intentionally left for the next iterations.

## What Changed

- Deleted the two mutable carrier files from the VMX frontend projection scope.
- Added runtime-owned carrier files under `Core/Runtime/Events/Traps/`:
  - `SchedulingBudgetTimer.cs`;
  - `TrapPolicyBitmap.cs`.
- Kept the existing `SchedulingBudgetTimer.Snapshot.partial.cs` under the same runtime trap owner.
- Updated `VmxCompatibilityProjectionInventoryTests` so the generated/frontend projection inventory now covers `30` files instead of `32`.
- Added test-local extraction evidence proving the old projection paths remain absent and the runtime owner paths contain the expected mutable timer/trap markers.
- Updated VMX evidence contracts so deleted projection paths are no longer treated as live compatibility quarantine sources.

## Production Authority

Production authority is not moved into actual VMX. The mutable timer/trap bitmap state left `Core/VMX/Compatibility/Frontend/Projection/*` and now sits under the neutral runtime trap area.

This slice did not introduce:

- `VmxExecutionUnit`;
- `VmcsManager` or `IVmcsManager`;
- VMCS field store;
- active pointer state;
- VMX runtime manager or adapter;
- admitted VMX backend path.

Residual note: the extracted runtime trap carriers still interoperate with existing frozen VMX compatibility vocabulary such as `TrapDecision`, `VmExitReason`, and `VmxOperationKind`. That is a physical authority extraction, not a neutral vocabulary redesign. A future slice should split neutral trap results from compatibility exit projection if this boundary becomes a first-admitted runtime path.

## Legacy Count And Move-Away Status

- `Legacy/VMX` C# count: `0`.
- `Legacy/VMX/Compatibility` C# count: `0`.
- `Legacy/VMX/Conformance` C# count: `0`.
- Physical `Legacy/VMX` is absent.
- Physical `Legacy/VMX-v2` is absent.
- Move-away probe for `Legacy/VMX/Conformance`: not rerun because the folder is already physically absent; static absence plus production/test builds remain green.

## Verification

Static checks:

- `Core/VMX` legacy-marked C# sources: `0`.
- `Legacy/VMX` C# sources: `0`.
- Forbidden manager/execution-unit files: `0`.
- Projection inventory scope: `30` `.cs` files, all classified.
- Forbidden-authority projection targets remaining: `2` (`VmcsV2Header.cs`, `ChildDomainIntentDescriptor.cs`).

Build/test:

- `dotnet build HybridCPU_ISE/HybridCPU_ISE.csproj --no-restore`: passed, `0` warnings, `0` errors.
- `dotnet build HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-restore`: passed, `0` warnings, `0` errors.
- `dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~VmxCompatibilityProjectionInventoryTests"`: passed, `4/4`.
- `dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~VmxProjectionSchemaAndQuarantineTests"`: passed, `58/58`.
- `dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~Vmx"`: passed, `509/509`.
- Final `dotnet build HybridCPU_ISE/HybridCPU_ISE.csproj --no-restore`: passed, `0` warnings, `0` errors.

## Residual Risk

- `VmcsV2Header.cs` still carries launch/invalidation epoch mutation under generated VMCS projection.
- `ChildDomainIntentDescriptor.cs` still carries mutable child-intent dictionary and snapshot/restore state under VMX frontend projection.
- The first admitted VMX compatibility path is still absent and must route through neutral runtime admission.
- Existing build warnings are unrelated nullability/analyzer debt and were not introduced by this closure.

## Next Step

Audit and extract/delete `VmcsV2Header` launch/invalidation epoch authority, then close `ChildDomainIntentDescriptor` child-intent state.
