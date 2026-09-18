# Second-stage translation generic aliases

Дата: 2026-05-24

## Правило / основание

- `ОСНОВЫ и ПРАВИЛА VMX.md`: substrate-типы должны двигаться к generic domain/descriptor/capability/evidence/runtime naming, а VMX/EPT/NPT/VPID vocabulary оставаться там, где это compatibility/frozen ABI.
- `Оценка рефакторинга VMX.md`: memory authority принадлежит memory domain descriptors / second-stage translation, а не VMX/NPT/EPT-named state.

## Что изменено

- В `MemoryTranslationControl` добавлены generic aliases:
  - `SecondStageTranslationEnabled`;
  - `AddressSpaceTaggingEnabled`;
  - `SecondStageRoot`;
  - `AddressSpaceTag`.
- В `NestedTranslationStatus` добавлены generic aliases:
  - `SecondStageViolation`;
  - `SecondStageMisconfiguration`.
- В `NestedTranslationResult` добавлены:
  - `IsSecondStageFault`;
  - фабрики `SecondStageViolation(...)` и `SecondStageMisconfiguration(...)`.
- Старые NPT/VPID-compatible имена сохранены без изменения поведения.

## Как проверено

Команда:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Результат сборки

Сборка основного проекта успешно завершена: `54 Warning(s)`, `0 Error(s)`.
