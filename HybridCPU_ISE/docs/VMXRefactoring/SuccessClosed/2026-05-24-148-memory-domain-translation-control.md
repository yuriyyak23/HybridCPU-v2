# Memory-domain translation control

Дата: 2026-05-24

## Закрытая задача

Закрыт пункт `13` из `docs/VMXRefactoring/audit.md`: memory translation substrate оставался NPT/VPID/VMCS-shaped backing model.

## Правило / основание

- Translation state принадлежит `MemoryDomainDescriptor` / generic memory-domain substrate.
- NPT/VPID/VMCS vocabulary не должен быть canonical substrate vocabulary.
- Compatibility backing может сохраняться как projection, но новые paths должны иметь generic domain form.

## Что изменено

- Добавлен `MemoryDomainTranslationControl` с generic fields: `TranslationEnabled`, `AddressSpaceRoot`, `SecondStageRoot`, `DomainTag`, `AddressSpaceTag`, `AddressSpaceGeneration`.
- `MemoryTranslationControl` получил `DomainControl` и `FromDomainControl(...)`.
- `NestedPageWalker.TranslateNested(...)` переведён на создание generic `MemoryDomainTranslationControl` перед compatibility projection.
- `MemoryTranslationAuthorityContract` расширен проверкой domain control parity.

## Как проверено

Выполнена сборка основного проекта после изменения:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Результат сборки

Build succeeded, 54 warning(s), 0 error(s).
