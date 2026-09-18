# Lane7 handle namespace authority

Дата: 2026-05-24

## Правило / основание

- VMX не владеет Lane7 state.
- Lane7 descriptor должен владеть virtual accelerator handles и backend binding policy.
- Backend/native handles не должны быть guest-visible или VMX-owned.

## Что изменено

- Актуализирован `Core/VMX/Substrate/Lanes/Lane7/Handles/Lane7HandleNamespace.cs`.
- Добавлены `Lane7HandleNamespaceAuthority` и `Lane7HandleVisibility`.
- `Lane7HandleNamespace` теперь описывает runtime-authoritative namespace, namespace binding, guest-safe visibility и compatibility projection boundary.
- Projection разрешается только для runtime-authoritative namespace без раскрытия native backend handles.
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
