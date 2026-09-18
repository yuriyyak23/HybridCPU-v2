# Shadow VMCS compatibility bridge adapter

Дата: 2026-05-24

## Правило / основание

- `ОСНОВЫ и ПРАВИЛА VMX.md`: VMX/VMCS должны оставаться compatibility frontend / generated projection, а substrate nested model должен двигаться к domain composition.
- `audit.md`, пункт 2: `ShadowVmcsNestedProjectionService` не должен хранить `VmcsV2Descriptor` и напрямую вызывать `descriptor.ShadowVmcs` как потенциальный authority bridge.

## Что изменено

- В `ShadowVmcsNestedProjectionService` введён `IShadowVmcsCompatibilityBridge`.
- Прямой доступ к `VmcsV2Descriptor.ShadowVmcs` локализован в `ShadowVmcsCompatibilityBridge`.
- Сам projection service теперь хранит только bridge-интерфейс и не держит descriptor как состояние.
- Добавлен `ShadowVmcsCompatibilityBridgeContract`, фиксирующий, что ShadowVMCS остаётся compatibility-only bridge.

## Как проверено

- Выполнено: `dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore`

## Результат сборки

- `Build succeeded`
- `54 Warning(s)`
- `0 Error(s)`

