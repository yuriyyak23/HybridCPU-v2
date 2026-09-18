# Lane6 token namespace authority

Дата: 2026-05-24

## Правило / основание

- VMX не владеет Lane6 state.
- `TokenNamespace` должен владеть virtual tokens.
- Native tokens не должны быть guest-visible или VMX-owned.

## Что изменено

- Актуализирован `Core/VMX/Substrate/Lanes/Lane6/Tokens/Lane6TokenNamespace.cs`.
- Добавлены `Lane6TokenNamespaceAuthority` и `Lane6TokenVisibility`.
- `Lane6TokenNamespace` теперь описывает runtime-authoritative namespace, namespace binding, guest-safe visibility и compatibility projection boundary.
- Projection разрешается только для runtime-authoritative namespace без раскрытия native tokens.
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
