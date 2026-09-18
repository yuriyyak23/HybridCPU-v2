# VMX Compatibility Projection Service Boundary

Дата: 2026-05-24

## Правило / основание

- VMX должен быть compatibility frontend поверх generic substrate, а не владельцем state.
- Compatibility projection должна опираться на frozen ABI alias map.
- Projection layer не должен напрямую мутировать authoritative substrate state.
- Descriptor/evidence validation обязательны перед публикацией VMX-visible projection.

## Что изменено

- `Core/VMX/Compatibility/Frontend/Projection/VmxCompatProjectionService.cs` больше не является пустой заготовкой.
- Добавлены `VmxCompatProjectionDecision`, `VmxCompatProjectionRequest` и `VmxCompatProjectionResult`.
- Добавлен `ValidateProjection(...)`, который:
  - требует наличие source alias в `CompatAliasMap`;
  - требует frozen ABI alias;
  - требует descriptor validation;
  - требует evidence validation;
  - запрещает прямую authoritative mutation из compatibility projection.

## Как проверено

Команда:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Результат сборки

Сборка основного проекта успешно завершена. Ошибок: 0. Предупреждения: существующие предупреждения проекта.
