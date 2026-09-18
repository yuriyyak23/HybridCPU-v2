# Lane6 domain runtime admission

Дата: 2026-05-24

## Правило / основание

- VMX не владеет Lane6 state.
- Lane6 execution, queues, tokens, fences и completions принадлежат lane descriptors и runtime namespaces.
- Compatibility projection не должна становиться владельцем native Lane6 resources.

## Что изменено

- Актуализирован `Core/VMX/Substrate/Domains/Lane6/Lane6DomainRuntime.cs`.
- Добавлены `Lane6DomainRuntimeDecision`, `Lane6DomainRuntimeRequest` и `Lane6DomainRuntimeResult`.
- `Lane6DomainRuntime` теперь валидирует runtime authority, pinning к Lane6, token namespace binding, queue namespace binding, fence-domain binding и compatibility projection gate.
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
