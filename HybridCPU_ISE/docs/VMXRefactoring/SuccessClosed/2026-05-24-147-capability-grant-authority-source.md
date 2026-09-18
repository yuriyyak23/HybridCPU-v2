# Capability grant authority source

Дата: 2026-05-24

## Закрытая задача

Закрыт пункт `6` из `docs/VMXRefactoring/audit.md`: `CapabilityGrant` выглядел производным объектом от bitmap masks.

## Правило / основание

- Capability authority должна быть descriptor/grant model.
- `VmxCaps` и bitmap words должны быть compatibility projection/cache, а не источник истины.
- Проверки grants должны предпочитать typed authority.

## Что изменено

- `CapabilityDescriptorSet.CreateGrant(...)` сначала ищет grant в `TypedGrants`.
- Fallback на mask-derived grant сохранён только для совместимости с существующими callers.
- `CapabilityGrantCollectionAuthorityContract` проверяет typed metadata каждого grant и соответствие effective compatibility mask.

## Как проверено

Выполнена сборка основного проекта после изменения:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Результат сборки

Build succeeded, 54 warning(s), 0 error(s).
