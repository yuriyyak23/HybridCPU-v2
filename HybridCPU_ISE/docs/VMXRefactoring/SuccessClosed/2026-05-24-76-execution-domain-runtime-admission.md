# Execution domain runtime admission

Дата: 2026-05-24

## Правило / основание

- VMX не владеет execution state.
- Execution state принадлежит `ExecutionDomainDescriptor`.
- Runtime-owned legality and validation должны fail-closed до compatibility projection или mutation.

## Что изменено

- Актуализирован `Core/VMX/Substrate/Domains/Execution/ExecutionDomainRuntime.cs`.
- Добавлены `ExecutionDomainRuntimeDecision`, `ExecutionDomainRuntimeRequest` и `ExecutionDomainRuntimeResult`.
- `ExecutionDomainRuntime` теперь валидирует наличие execution descriptor, descriptor authority, bundle-legality descriptor, scheduling-budget descriptor и compatibility projection gate.
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
