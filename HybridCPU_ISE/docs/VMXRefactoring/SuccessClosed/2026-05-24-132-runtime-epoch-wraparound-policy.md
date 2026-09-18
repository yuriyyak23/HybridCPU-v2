# Runtime epoch wraparound fail-closed policy

Дата: 2026-05-24

## Правило / основание

- Runtime-owned legality and validation должны fail closed.
- Memory/domain runtime state не должен silently wrap authority epochs без явного решения runtime слоя.

## Что изменено

- Добавлен `RuntimeEpochAdvanceDecision` и `RuntimeEpochAdvanceResult`.
- Добавлен `TryAdvanceRuntimeEpochFailClosed()`, который при `ulong.MaxValue` возвращает `DeniedWraparound` и требует domain flush.
- Старый `AdvanceRuntimeEpoch()` оставлен без изменения поведения как compatibility helper для существующих вызовов.

## Как проверено

```text
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Результат сборки

Build succeeded. 54 Warning(s), 0 Error(s).
