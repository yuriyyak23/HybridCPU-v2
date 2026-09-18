# VMX Compatibility Retire Boundary Fail-Closed

Дата: 2026-05-24

## Правило / основание

- VMX retire effects должны быть typed: success, VMFailValid, VMFailInvalid, VMExit, VMAbort.
- VMX effect не должен становиться visible до descriptor/evidence/completion validation.
- VMExit/VMFail/VMAbort являются compatibility projection generic runtime outcome, а не внутренней runtime authority model.
- Retire publication должна fail-closed.

## Что изменено

- `Core/VMX/Compatibility/Frontend/Retire/VmxCompatRetireBoundary.cs` больше не является пустой заготовкой.
- Добавлены `VmxCompatRetireDecision`, `VmxCompatRetireResult` и `VmxCompatRetireRequest`.
- Добавлена проверка `ValidatePublication(...)`:
  - требует валидный `VmxRetireEffect`;
  - требует descriptor validation;
  - требует evidence validation;
  - требует validated completion route для VMExit/exit-on-retire;
  - запрещает faulted retire effect без typed VMFail/VMAbort completion.

## Как проверено

Команда:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Результат сборки

Сборка основного проекта успешно завершена. Ошибок: 0. Предупреждения: существующие предупреждения проекта.
