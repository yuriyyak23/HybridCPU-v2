# Capability grant authority contract

Дата: 2026-05-24

## Правило / основание

- `Оценка рефакторинга VMX security-centric.md`: capability authority должен быть typed grant model, а bitmap должен оставаться compatibility projection / fast mask.
- `deep-research-report (6).md`: capabilities должны принадлежать descriptor/grant substrate и не возвращаться к CSR/VMX-owned authority.

## Что изменено

- Добавлен `CapabilityGrantAuthorityContract`.
- Контракт fail-closed проверяет:
  - granted mask не пустой;
  - grant несёт typed authority metadata;
  - domain grant имеет owner domain;
  - delegation/revocation/migration/evidence/projection policies не пустые;
  - compatibility projection grant не публикуется без `ProjectIfCompatible`.

## Как проверено

Команда:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Результат сборки

Сборка основного проекта успешно завершена: `54 Warning(s)`, `0 Error(s)`.
