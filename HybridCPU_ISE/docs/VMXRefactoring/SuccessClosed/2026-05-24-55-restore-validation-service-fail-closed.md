# Restore validation service fail-closed

Дата: 2026-05-24

## Правило / основание

- `Оценка рефакторинга VMX security-centric.md`: restore/import boundary must validate authority before state becomes effective.
- `ОСНОВЫ и ПРАВИЛА VMX.md`: migration/checkpoint state belongs to domain checkpoint model, not VMCS.

## Что изменено

- `RestoreValidationService` больше не пустая заготовка.
- Добавлены `RestoreValidationDecision` и `RestoreValidationResult`.
- Сервис отклоняет пустой checkpoint, compatibility projection metadata, epoch mismatch и любые ошибки `MigrationValidationPolicy`.

## Как проверено

Запущено:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Результат сборки

Успешно: `Build succeeded`, 0 errors. Остались существующие предупреждения проекта.
