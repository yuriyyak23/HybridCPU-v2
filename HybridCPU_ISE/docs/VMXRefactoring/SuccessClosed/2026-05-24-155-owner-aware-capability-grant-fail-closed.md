# Owner-aware capability grant fail-closed

Дата: 2026-05-24

## Правило / основание

- `ОСНОВЫ и ПРАВИЛА VMX.md`: owner/delegation/revocation/evidence metadata не должны легитимизировать grant, если typed grant отсутствует.
- `audit.md`, пункт 13: owner-aware overload `CreateGrant(...)` всё ещё использовал `HasEffectiveCapability(capabilityMask)`.

## Что изменено

- Owner-aware `CreateGrant(...)` сначала требует существующий typed grant.
- При отсутствии typed grant создаётся denied grant с переданным metadata, но `isGranted: false`.
- Bitmap intersection больше не выдаёт owner-aware grant.

## Как проверено

- Выполнено: `dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore`

## Результат сборки

- `Build succeeded`
- `54 Warning(s)`
- `0 Error(s)`

