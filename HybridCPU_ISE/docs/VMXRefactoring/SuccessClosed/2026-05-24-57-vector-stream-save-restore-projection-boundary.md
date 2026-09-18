# Vector-stream save/restore projection boundary

Дата: 2026-05-24

## Правило / основание

- `ОСНОВЫ и ПРАВИЛА VMX.md`: VMSAVEX/VMRESTX должны вызывать save/restore projection over `ExecutionDomainDescriptor` / vector-stream descriptors.
- `Оценка рефакторинга VMX security-centric.md`: VMX requests must pass through descriptor lookup, authority validation and evidence policy checks.

## Что изменено

- `VectorStreamSaveRestoreProjection` больше не пустая заготовка.
- Добавлены `VectorStreamProjectionDecision` и `VectorStreamProjectionResult`.
- Projection проверяет execution extension descriptor, vector-stream descriptor authority, compatibility projection gate, save/restore mask, vector length, replay epoch and host-evidence non-leak before restore.

## Как проверено

Запущено:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Результат сборки

Успешно: `Build succeeded`, 0 errors. Остались существующие предупреждения проекта.
