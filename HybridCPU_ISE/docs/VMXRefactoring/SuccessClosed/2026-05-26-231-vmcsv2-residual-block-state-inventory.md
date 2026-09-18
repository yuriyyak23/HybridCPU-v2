# Success Closure 231: VMCSv2 Residual Block-State Inventory

Date: 2026-05-26

## Selected Slice

Residual VMCSv2 block-state inventory for:

- `VectorStreamStateBlock`;
- `DirtyLogBlock`;
- `SecurityIsolationBlock`;
- `CapabilityNegotiationBlock`;
- `ExitInfoBlock.Record*` publication helpers.

## What Changed

- `VectorStreamStateBlock`, `DirtyLogBlock`, `SecurityIsolationBlock`, and `CapabilityNegotiationBlock` are now read-only/default compatibility projection shells with no instance backing fields.
- Vector-stream and dirty-log status vocabulary remains visible only as generated/read-only compatibility projection. No VMCS-owned descriptor table, epoch, dirty-page, security-isolation, or capability-negotiation state remains in those blocks.
- `ExitInfoBlock.RecordVectorException`, `RecordStreamDescriptorFault`, and `RecordStreamReplayRequired` are internal, not public API.
- Public exit publication remains descriptor-mediated through `VmcsV2Descriptor.RecordVectorExceptionExit`, `RecordStreamDescriptorFaultExit`, and `RecordStreamReplayRequiredExit`; this is retire-publication-only projection, not runtime ownership.
- `VmxProjectionSchemaAndQuarantineTests` was tightened to reject `LastSnapshot` private-set/assignment backing state without rejecting read-only expression-bodied projection.
- `VmcsV2MutableHelperAuthorityTests` now fences residual no-backing-state shells and internal-only `ExitInfoBlock.Record*` helpers.

## What Was Left

- `ExitInfoBlock` still carries the exit-publication projection state itself. That mutable state is intentionally scoped to retire-facing VM-exit projection.
- `VirtualCpuBlock` still has private-set guest PC/SP/GPR persistence projection fields from earlier slices; this closure did not change that block.
- `TryReadScalarField`, migration readiness, and nested readiness remain denied/fail-closed until generated projection over neutral owners exists.

## Production Authority

No production authority moved into VMX. This closure did not introduce:

- `VmxExecutionUnit`;
- `VmcsManager` or `IVmcsManager`;
- VMCS field store;
- active pointer state;
- VMX runtime manager or adapter;
- admitted VMX backend path.

Runtime authority remains with neutral `Core/Runtime/*` owners. The edited VMCSv2 blocks remain compatibility projection vocabulary only.

## Legacy Count And Move-Away Status

- `Legacy/VMX` C# count: `0`.
- `Legacy/VMX/Compatibility` C# count: `0`.
- `Legacy/VMX/Conformance` C# count: `0`.
- Physical `Legacy/VMX` is absent.
- Physical `Legacy/VMX-v2` is absent.
- Move-away probe for `Legacy/VMX/Conformance`: not rerun in this slice because the folder is already physically absent from closure `229`; static absence plus production/test builds remain green.

## Verification

Static checks:

- `Core/VMX` legacy-marked C# sources: `0`.
- Forbidden manager/execution-unit files: `0`.
- `VmcsV2Blocks.cs` `private set` lines: `8`, all in `ExitInfoBlock` and `VirtualCpuBlock`; none in `VectorStreamStateBlock`, `DirtyLogBlock`, `SecurityIsolationBlock`, or `CapabilityNegotiationBlock`.

Build/test:

- `dotnet build HybridCPU_ISE/HybridCPU_ISE.csproj --no-restore`: passed, `0` warnings, `0` errors.
- `dotnet build HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-restore`: passed, `36` existing warnings, `0` errors.
- `dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~VmcsV2MutableHelperAuthorityTests"`: passed, `7/7`.
- `dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~VmxProjectionSchemaAndQuarantineTests"`: passed, `58/58`.
- `dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~Vmx"`: passed, `505/505`.
- Final `dotnet build HybridCPU_ISE/HybridCPU_ISE.csproj --no-restore`: passed, `0` warnings, `0` errors.

## Residual Risk

- The first admitted VMX compatibility path is still absent and must be routed through neutral runtime admission, not VMCS/VMX ownership.
- Generated/read-only compatibility projection surfaces still need a broader artifact inventory so contract-only and denied-only carriers are explicitly classified.
- Existing test-project warnings are unrelated nullability/xUnit analyzer debt and were not introduced by this closure.

## Next Step

Inventory `Core/VMX/Compatibility/Generated/*` and `Core/VMX/Compatibility/Frontend/Projection/*` as generated-lineage, contract-only, denied-only, or forbidden-authority, then design the first low-risk admitted VMX compatibility path through neutral runtime admission.
