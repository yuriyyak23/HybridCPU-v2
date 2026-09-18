# Domain checkpoint image authority

Дата: 2026-05-24

## Правило / основание

- `ОСНОВЫ и ПРАВИЛА VMX.md`: VMX не владеет migration/checkpoint state.
- `deep-research-report (6).md`: state migration serializes domain/device model, not host evidence or internal VMCS authority.

## Что изменено

- `DomainCheckpointImage` больше не пустая заготовка.
- Добавлен domain-authoritative checkpoint manifest: authority, epoch, payload/evidence masks и compatibility projection metadata marker.
- Добавлены fail-closed restore checks через `MigrationValidationPolicy`; host-owned evidence в checkpoint image отклоняется.

## Как проверено

Запущено:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Результат сборки

Успешно: `Build succeeded`, 0 errors. Остались существующие предупреждения проекта.
