# Compatibility caps projection alias

Дата: 2026-05-24

## Правило / основание

- Capability bitmap может быть compatibility projection/cache, но не должен выглядеть как canonical authority.
- Publication path должен явно читать projection, а typed grants остаются authority boundary.
- Основание: `ОСНОВЫ и ПРАВИЛА VMX.md`; audit.md, пункт 11.

## Что изменено

- В `CapabilityDescriptorSet` добавлен `CompatibilityCapsProjection`.
- `EffectiveCaps` оставлен только как совместимый alias поверх `CompatibilityCapsProjection`.
- `CapabilityPublicationPolicy` и `CapabilityNegotiationService` переключены на `CompatibilityCapsProjection`.
- `CapabilityGrantCollectionAuthorityContract` обновлен на projection terminology.
- Добавлен `CompatibilityCapsProjectionContract`, сверяющий publication mask с typed grant projection.

## Как проверено

- Выполнено: `dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore`.

## Результат сборки

- Build succeeded.
- 54 warnings, 0 errors.

