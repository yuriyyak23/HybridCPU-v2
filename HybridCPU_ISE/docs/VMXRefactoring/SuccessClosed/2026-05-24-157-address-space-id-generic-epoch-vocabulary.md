# AddressSpaceId generic epoch vocabulary

Дата: 2026-05-24

## Правило / основание

- `ОСНОВЫ и ПРАВИЛА VMX.md`: VPID/NPT/EPT vocabulary не должен быть substrate vocabulary.
- `audit.md`, пункт 19: `ToAddressSpaceId(ulong eptEpoch, ulong vpidEpoch)` использовал EPT/VPID имена в substrate API.

## Что изменено

- Параметры `ToAddressSpaceId(...)` переименованы в `secondStageEpoch` и `addressSpaceTagEpoch`.
- `NestedPageWalker.TranslateNestedDetailed(...)` и named arguments переведены на generic epoch vocabulary.
- Добавлен `AddressSpaceIdVocabularyContract`.

## Как проверено

- Выполнено: `dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore`

## Результат сборки

- `Build succeeded`
- `54 Warning(s)`
- `0 Error(s)`

