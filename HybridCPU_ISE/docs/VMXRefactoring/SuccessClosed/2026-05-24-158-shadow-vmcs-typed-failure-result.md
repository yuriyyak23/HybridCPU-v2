# Shadow VMCS typed failure result

Дата: 2026-05-24

## Правило / основание

- `ОСНОВЫ и ПРАВИЛА VMX.md`: VMX frontend должен возвращать VMX-compatible results, но внутри использовать generic outcomes.
- `audit.md`, пункт 26: `ShadowVmcsCompatibilityBridge` возвращал только `validationMessage`, теряя typed failure.

## Что изменено

- `IShadowVmcsCompatibilityBridge.TryEnable(...)` теперь возвращает `NestedValidationResult`.
- `ShadowVmcsCompatibilityBridge` мапит VMCS-backed failure в generic `NestedValidationCode.CompatibilityProjectionFailed`.
- `ShadowVmcsNestedProjectionService` больше не реконструирует failure из строки.
- Добавлен `ShadowVmcsTypedFailureContract`; `ShadowVmcsCompatibilityBridgeContract` дополнен проверкой typed failure.

## Как проверено

- Выполнено: `dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore`

## Результат сборки

- `Build succeeded`
- `54 Warning(s)`
- `0 Error(s)`

