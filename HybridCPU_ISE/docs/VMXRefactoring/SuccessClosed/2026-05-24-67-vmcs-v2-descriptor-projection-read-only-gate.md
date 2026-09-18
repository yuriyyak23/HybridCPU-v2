# VMCSv2 Descriptor Projection Read-Only Gate

Дата: 2026-05-24

## Правило / основание

- `VmcsV2Descriptor` / VMCSv2 projection не должен быть substrate object.
- VMCSv2 должен быть generated compatibility projection поверх generic domain descriptors.
- Projection не должен владеть legality, evidence, migration semantics или runtime state.
- Ручное writable projection-поведение должно fail-closed.

## Что изменено

- `Core/VMX/Compatibility/Generated/VmcsProjection/VmcsV2DescriptorProjection.cs` больше не является пустой заготовкой.
- Добавлены `VmcsV2DescriptorProjectionDecision`, `VmcsV2DescriptorProjectionRequest` и `VmcsV2DescriptorProjectionResult`.
- Добавлена проверка `ValidateProjection(...)`, которая:
  - требует generated alias map;
  - требует descriptor validation;
  - требует evidence validation;
  - требует read-only projection mode;
  - запрещает authoritative substrate mutation;
  - публикует только ABI metadata `VmcsV2Header` при успешной проверке.

## Как проверено

Команда:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Результат сборки

Сборка основного проекта успешно завершена. Ошибок: 0. Предупреждения: существующие предупреждения проекта.
