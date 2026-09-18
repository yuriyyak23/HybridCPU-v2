# Lane6 queue namespace authority

Дата: 2026-05-24

## Правило / основание

- VMX не владеет Lane6 state.
- Lane6 descriptor должен владеть DMA queues и runtime namespace binding.
- Guest-visible projection не должна раскрывать native queue handles.

## Что изменено

- Актуализирован `Core/VMX/Substrate/Lanes/Lane6/Queues/Lane6QueueNamespace.cs`.
- Добавлены `Lane6QueueNamespaceAuthority` и `Lane6QueueVisibility`.
- `Lane6QueueNamespace` теперь описывает runtime-authoritative namespace, namespace binding, guest-safe visibility и compatibility projection boundary.
- Native queue handles явно отделены от projection-safe виртуальных queue ids.
- Поведение VMX handlers и RTL-логика не изменялись.

## Как проверено

Команда:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Результат сборки

- Успешно.
- 0 errors.
- 54 warnings, существующие предупреждения проекта.
