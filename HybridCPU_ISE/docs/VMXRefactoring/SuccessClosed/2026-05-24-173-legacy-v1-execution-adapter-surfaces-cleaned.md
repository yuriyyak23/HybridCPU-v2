# 173 Legacy V1 Execution Adapter Surfaces Cleaned

Дата: 2026-05-24

Статус: closed

## Правило / основание

VMX is a frozen compatibility frontend, not the virtualization architecture. Legacy execution adapter partials must not remain in `Legacy/VMX` as hidden active execution surface. If the code is only current VMX opcode/effect routing over already configured execution and retire boundaries, it can live in the owning Core execution/pipeline files with conformance proving that no VMCS manager, raw VMCS field, IOMMU, or Shadow VMCS authority was imported.

## Что изменено

- Temporarily moved the four remaining `Legacy/VMX` files to `\New folder\LegacyVMXProbe-2026-05-24` and built the project to inspect real compile anchors.
- Restored only `Legacy/VMX/Compatibility/Frontend/Handlers/VmxExecutionUnit.cs` and `Legacy/VMX/Substrate/Runtime/Binding/VmcsManager.cs`.
- Removed the legacy V1 execution adapter partials from `Legacy/VMX`:
  - `Legacy/VMX/Compatibility/Adapters/LegacyVmxV1/ExecutionDispatcherV4.Vmx.cs`
  - `Legacy/VMX/Compatibility/Adapters/LegacyVmxV1/CPU_Core.PipelineExecution.Vmx.cs`
- Added `Core/Execution/ExecutionDispatcherV4.VmxCompatibility.cs`.
- Added `Core/Pipeline/Core/CPU_Core.PipelineExecution.VmxRetire.cs`.
- Added `LegacyVmxV1ExecutionAdapterSurfaceReturnContract`.
- Updated `LegacyVmxQuarantineManifest`.
- Extended `VmxProjectionSchemaAndQuarantineTests` with `ReturnedLegacyVmxV1ExecutionAdapterSurfaces_AreCurrentCoreRoutingOnly`.

## Как проверено

- The live compile probe showed that removing all four files first breaks on `VmxExecutionUnit` and `VmcsManager` type anchors.
- With only the two legacy V1 adapter partials absent, missing symbols were limited to current execution/retire routing methods: `ExecuteVmx`, `CaptureVmxRetireWindowPublications`, `EnqueuePipelineEvent`, `MaterializeLaneVmxEffect`, and `ApplyRetiredVmxEffect`.
- New Core files provide only those routing/materialization methods.
- Legacy V1 adapter partial paths are absent from `Legacy/VMX`.
- `Legacy/VMX` now contains only `VmxExecutionUnit.cs` and `VmcsManager.cs`.
- Conformance verifies the new Core surfaces contain no `VmcsManager`, `IVmcsManager`, raw VMCS field access, `IOMMU.`, or `.ShadowVmcs.` markers.

## Результат сборки

- `dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore`
  Result: succeeded, 54 existing warnings, 0 errors.
- `dotnet build "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-restore`
  Result: succeeded, 93 existing warnings, 0 errors.
- `dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-build --filter "FullyQualifiedName~VmxProjectionSchemaAndQuarantineTests"`
  Result: passed 13/13.
- `dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-build --filter "FullyQualifiedName~CoreVmxAuthorityBoundaryTests"`
  Result: passed 1/1.
- `dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-build --filter "FullyQualifiedName~ReturnedLegacyVmxV1ExecutionAdapterSurfaces"`
  Result: passed 1/1.
- Final `dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore`
  Result: succeeded, 0 warnings, 0 errors.

## Остаточный риск

- `VmxExecutionUnit` remains the largest active heavy legacy frontend handler and still owns broad VMX instruction behavior.
- `VmcsManager` remains the VMCS state anchor and should be demoted toward generated projection handles / runtime descriptors in a later task.
- The new Core routing files still use VMX compatibility vocabulary because the VMX opcode/effect ABI is frozen; this is routing vocabulary, not VMX authority ownership.
