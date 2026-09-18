# Memory translation address-space root authority

Дата: 2026-05-24

## Закрытая задача

Закрыт пункт `28` из `docs/VMXRefactoring/audit.md`: `MemoryTranslationControl` нёс `GuestCr3` как слишком guest/VM-shaped substrate term.

## Правило / основание

- Translation state принадлежит memory-domain substrate.
- VPID/NPT/EPT/guest vocabulary не должен становиться substrate authority vocabulary.
- Compatibility fields могут оставаться frozen backing/projection, но active authority должна использовать generic naming.

## Что изменено

- В `MemoryTranslationControl` добавлены `AddressSpaceRoot`, `MemoryTranslationAuthorityView` и generic factory `CreateSecondStageControl(...)`.
- Compatibility helper `NestedPageWalker.TranslateNested(...)` переведён на generic factory с `addressSpaceRoot` / `secondStageRoot`.
- Добавлен `MemoryTranslationAuthorityContract`, запрещающий использовать `GuestCr3` как canonical authority.

## Как проверено

Выполнена сборка основного проекта после изменения:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Результат сборки

Build succeeded, 54 warning(s), 0 error(s).
