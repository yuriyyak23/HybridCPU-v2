# Lane7 domain runtime admission

Дата: 2026-05-24

## Правило / основание

- VMX не владеет Lane7 state.
- Lane7 descriptor должен владеть virtual accelerator handles, token namespace, backend binding policy, quota/pressure policy и completion routing.
- Backend/native handles и bindings не должны становиться guest-visible evidence.

## Что изменено

- Актуализирован `Core/VMX/Substrate/Domains/Lane7/Lane7DomainRuntime.cs`.
- Добавлены `Lane7DomainRuntimeDecision`, `Lane7DomainRuntimeRequest` и `Lane7DomainRuntimeResult`.
- `Lane7DomainRuntime` теперь валидирует runtime authority, pinning к Lane7, backend binding, handle namespace binding, token namespace binding, completion route binding и compatibility projection gate.
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
