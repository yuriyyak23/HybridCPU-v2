# Success Closure 234: VMCSv2 Header And Child Intent Authority Removal

Date: 2026-05-27

## Selected Slice

Close the residual forbidden-authority projection carrier pool:

- `Core/VMX/Compatibility/Generated/VmcsProjection/VmcsV2Header.cs`;
- `Core/VMX/Compatibility/Frontend/Projection/Nested/ChildDomainIntentDescriptor.cs`.

## What Changed

- Removed `VmcsV2Header.MarkLaunched`, `ResetLaunchState`, and `AdvanceInvalidationEpoch`.
- Reduced `VmcsV2Header` launch/epoch state to read-only compatibility metadata: `IsLaunched => false`, `InvalidationEpoch => 0`.
- Removed descriptor retire-publication side effects that advanced `Header.InvalidationEpoch`.
- Removed `ChildDomainIntentDescriptor` field dictionary, generation counter, write API, raw field accessor, field snapshot DTO, snapshot creation, and snapshot restore.
- Kept `ChildDomainIntentAccessPolicy.DefaultNestedL1Visible` as immutable compatibility policy data.
- Kept `ChildDomainIntentDescriptor.TryReadIntentField` only as fail-closed compatibility probing; it does not synthesize child intent state.
- Updated projection inventory classification to `30` files total: `4` generated-lineage, `23` contract-only, `3` denied-only, and `0` forbidden-authority.
- Added reflection fences for header epoch authority and child-intent state removal.

## Production Authority

Production authority is not moved into actual VMX. This slice only removes projection-owned mutation and keeps VMX as frozen compatibility metadata/fail-closed vocabulary.

This slice did not introduce:

- `VmxExecutionUnit`;
- `VmcsManager` or `IVmcsManager`;
- VMCS field store;
- active pointer state;
- VMX runtime manager or adapter;
- admitted VMX backend path.

If child-intent state is needed later, it must be introduced under neutral `Core/Runtime/Nested/*` and projected back through compatibility vocabulary.

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
- Forbidden-authority projection targets remaining: `0`.

Build/test:

- `dotnet build HybridCPU_ISE/HybridCPU_ISE.csproj --no-restore`: passed, `0` warnings, `0` errors.
- `dotnet build HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-restore`: passed, `0` warnings, `0` errors.
- `dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~VmxCompatibilityProjectionInventoryTests"`: passed, `4/4`.
- `dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~VmcsV2MutableHelperAuthorityTests"`: passed, `9/9`.
- `dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~VmxProjectionSchemaAndQuarantineTests"`: passed, `58/58`.
- `dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~Vmx"`: passed, `509/509`.

## Residual Risk

- The first admitted VMX compatibility path is still absent and must route through neutral runtime admission.
- `Core/Runtime/Events/Traps` still interops with frozen VMX compatibility exit vocabulary from the prior extraction slice; split neutral trap results before using it as a first-admitted runtime path.
- Existing build warnings are unrelated nullability/analyzer debt and were not introduced by this closure.

## Next Step

Design the first low-risk admitted VMX compatibility path through `RuntimeBoundaryAdmissionService`, or split neutral trap results from VMX-compatible exit projection before admitting trap/intercept behavior.
