# Migration validation policy fail-closed

Дата: 2026-05-24

## Правило / основание

- `deep-research-report (6).md`: incoming migration stream must be treated as hostile and validated fail-closed.
- `Оценка рефакторинга VMX security-centric.md`: host-owned evidence must remain outside guest-visible/restored state.

## Что изменено

- `MigrationValidationPolicy` больше не пустая заготовка.
- Добавлены `MigrationValidationDecision` и `MigrationValidationResult`.
- Policy по умолчанию fail-closed: импорт отклоняется без разрешения `MigrationDescriptor`/`EvidencePolicyDescriptor`; host-owned runtime evidence, scheduler evidence, backend bindings и native tokens не импортируются как authoritative state.

## Как проверено

Запущено:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Результат сборки

Успешно: `Build succeeded`, 0 errors. Остались существующие предупреждения проекта.
