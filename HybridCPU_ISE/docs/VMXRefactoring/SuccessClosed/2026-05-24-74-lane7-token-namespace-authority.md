# Lane7 token namespace authority

Дата: 2026-05-24

## Правило / основание

- VMX не владеет Lane7 state.
- Lane7 descriptor должен владеть token namespace.
- Native accelerator tokens не должны быть guest-visible или VMX-owned.

## Что изменено

- Актуализирован `Core/VMX/Substrate/Lanes/Lane7/Tokens/Lane7TokenNamespace.cs`.
- Добавлены `Lane7TokenNamespaceAuthority` и `Lane7TokenVisibility`.
- `Lane7TokenNamespace` теперь описывает runtime-authoritative namespace, namespace binding, guest-safe visibility и compatibility projection boundary.
- Projection разрешается только для runtime-authoritative namespace без раскрытия native accelerator tokens.
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
