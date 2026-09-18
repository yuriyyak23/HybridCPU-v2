# Legacy VMX quarantine manifest

Дата: 2026-05-24

## Правило / основание

- `ОСНОВЫ и ПРАВИЛА VMX.md`: legacy VMX behavior не должен возвращаться в `Core/VMX` без descriptor owner, capability policy, evidence policy, retire boundary и projection tests.
- `audit.md`, пункт 1: `Legacy/VMX` остаётся главным риском, если heavy-файлы будут возвращаться без conversion to substrate boundary.

## Что изменено

- Добавлен `LegacyVmxQuarantineManifest`.
- Формально перечислены основные heavy legacy зоны: `VmxExecutionUnit`, `VmcsManager`, ShadowVMCS block, CSR fallback projection, VMCS memory translation, IOMMU/invalidation/IO backends и legacy adapters.
- Для каждого entry указано условие, без которого файл должен оставаться в карантине.
- `CanReturnToCore(...)` связан с `LegacyReverseImportContract`.

## Как проверено

- Выполнено: `dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore`

## Результат сборки

- `Build succeeded`
- `54 Warning(s)`
- `0 Error(s)`

