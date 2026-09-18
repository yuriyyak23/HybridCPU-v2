# 171 Legacy VMCS Memory Translation Projection Removed

Дата: 2026-05-24

Статус: closed

## Правило / основание

VMX is a frozen compatibility frontend, not the virtualization architecture. A legacy VMCS-to-memory-translation projection must not be reintroduced as Core authority. If the legacy layer is only an adapter over frozen VMCS fields, it should be removed without replacement, with reusable construction expressed through generic runtime/memory-domain inputs.

## Что изменено

- Removed `Legacy/VMX/Substrate/Memory/Translation/LegacyVmcsMemoryTranslationControlProjection.cs`.
- Added `MemoryTranslationControl.CreateRuntimeProjection(...)` as a generic factory over second-stage/address-space/domain generation inputs.
- Updated quarantined `VmxExecutionUnit` to use `ResolveActiveMemoryTranslationControl()` instead of `LegacyVmcsMemoryTranslationControlProjection.FromVmcs(...)`.
- Extended `LegacyVmxQuarantineManifest` with a `RemovedWithoutReplacement` disposition.
- Added `LegacyVmcsMemoryTranslationProjectionRemovalContract`.
- Extended `VmxProjectionSchemaAndQuarantineTests` with a static removal check.

## Как проверено

- Legacy path is absent from `Legacy/VMX`.
- `VmxExecutionUnit` no longer references `LegacyVmcsMemoryTranslationControlProjection` or `.FromVmcs(...)`.
- `MemoryTranslationControl.cs` contains the generic factory and does not contain `IVmcsManager`, `VmcsField`, or `ReadFieldValue`.
- Manifest marks the legacy projection as removed without replacement, not returned to Core.

## Результат сборки

- `dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore`
  Result: succeeded, 54 existing warnings, 0 errors.
- `dotnet build "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-restore`
  Result: succeeded, 93 existing warnings, 0 errors.
- `dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-build --filter "FullyQualifiedName~VmxProjectionSchemaAndQuarantineTests"`
  Result: passed 11/11.
- `dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-build --filter "FullyQualifiedName~CoreVmxAuthorityBoundaryTests"`
  Result: passed 1/1.
- `dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-build --filter "FullyQualifiedName~RemovedLegacyVmcsMemoryTranslationProjection"`
  Result: passed 1/1.

## Остаточный риск

- `VmxExecutionUnit` still reads frozen VMCS fields to construct compatibility memory-translation snapshots. This remains quarantined and should disappear with the frontend split.
- `MemoryTranslationControl` still carries VMCS/NPT/VPID-shaped compatibility backing names; generic aliases and factories exist, but full substrate renaming remains open.
- Remaining heavy legacy topics: `VmxExecutionUnit`, `VmcsManager`, `ShadowVmcsBlock`, and the legacy V1/V2 execution adapter partials.
