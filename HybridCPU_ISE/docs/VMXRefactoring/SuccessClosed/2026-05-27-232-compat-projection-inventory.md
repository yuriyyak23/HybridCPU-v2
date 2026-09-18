# Success Closure 232: Compatibility Projection Inventory

Date: 2026-05-27

## Selected Slice

Inventory every `.cs` source under:

- `Core/VMX/Compatibility/Generated/*`;
- `Core/VMX/Compatibility/Frontend/Projection/*`.

The classification buckets are:

- generated-lineage;
- contract-only;
- denied-only;
- forbidden-authority.

## What Changed

- Added `VmxCompatibilityProjectionInventoryTests` with an exact test-local inventory of the generated/frontend projection scope.
- The inventory covers `32` files and fails if a new source appears in the scope without classification.
- Generated-lineage entries are cross-checked against `GeneratedProjectionLineageBuildContract.RequiredGeneratedOutputs`.
- Runtime manager/store/backend authority markers are denied across the whole inventory scope.

## Classification Result

- Generated-lineage: `4`.
- Contract-only: `22`.
- Denied-only: `2`.
- Forbidden-authority: `4`.

Forbidden-authority entries:

- `Core/VMX/Compatibility/Generated/VmcsProjection/VmcsV2Header.cs` for launch/invalidation epoch mutation.
- `Core/VMX/Compatibility/Frontend/Projection/Events/SchedulingBudgetTimer.cs` for armed timer state and expiry mutation.
- `Core/VMX/Compatibility/Frontend/Projection/Events/TrapPolicyBitmap.cs` for mutable trap/intercept bitmap state.
- `Core/VMX/Compatibility/Frontend/Projection/Nested/ChildDomainIntentDescriptor.cs` for mutable child-intent field dictionary and snapshot/restore shell.

## What Was Left

This is an inventory closure, not an extraction closure. The four forbidden-authority files remain in place but are now explicit next-step targets. They cannot silently expand or be joined by a new unclassified generated/projection file.

## Production Authority

No production authority moved into VMX. This closure did not introduce:

- `VmxExecutionUnit`;
- `VmcsManager` or `IVmcsManager`;
- VMCS field store;
- active pointer state;
- VMX runtime manager or adapter;
- admitted VMX backend path.

Runtime authority remains with neutral `Core/Runtime/*` owners. The new artifact is test-local evidence only.

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
- Forbidden manager/execution-unit files: `0`.
- Inventory scope: `32` `.cs` files, all classified.

Build/test:

- `dotnet build HybridCPU_ISE/HybridCPU_ISE.csproj --no-restore`: passed, `0` warnings, `0` errors.
- `dotnet build HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-restore`: passed, existing warnings only, `0` errors.
- `dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~VmxCompatibilityProjectionInventoryTests"`: passed, `3/3`.
- `dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~VmxProjectionSchemaAndQuarantineTests"`: passed, `58/58`.
- `dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~Vmx"`: passed, `508/508`.

## Residual Risk

- The four forbidden-authority projection carriers still need extraction, deletion, or reduction to denied/read-only wrappers.
- The first admitted VMX compatibility path is still absent and must be routed through neutral runtime admission.
- Existing build warnings are unrelated nullability/analyzer debt and were not introduced by this closure.

## Next Step

Extract/delete the forbidden-authority projection carriers, starting with `SchedulingBudgetTimer.cs` and `TrapPolicyBitmap.cs`, then audit `VmcsV2Header` launch/invalidation epoch mutation.
