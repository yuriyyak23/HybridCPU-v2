# Virtualization Lowering Boundary Fail-Closed

Дата: 2026-05-24

## Правило / основание

- VMX lowering должен выпускать только compatibility projection, а не прямые VMX handler/substrate calls.
- Lowering должен быть descriptor-gated, capability-gated и runtime-gated.
- No-emission regression gate должен оставаться обязательным для compiler boundary.
- VMX frontend не должен становиться compiler bypass к authoritative substrate.

## Что изменено

- `Core/VMX/CompilerBoundary/Lowering/VirtualizationLoweringBoundary.cs` больше не является пустой заготовкой.
- Добавлены `VirtualizationLoweringDecision`, `VirtualizationLoweringRequest` и `VirtualizationLoweringResult`.
- Добавлена проверка `ValidateLowering(...)`, которая:
  - требует allowed compiler intent;
  - требует descriptor validation;
  - требует capability validation;
  - требует runtime validation;
  - требует успешный no-emission gate;
  - запрещает direct VMX handler emission.

## Как проверено

Команда:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Результат сборки

Сборка основного проекта успешно завершена. Ошибок: 0. Предупреждения: существующие предупреждения проекта.
