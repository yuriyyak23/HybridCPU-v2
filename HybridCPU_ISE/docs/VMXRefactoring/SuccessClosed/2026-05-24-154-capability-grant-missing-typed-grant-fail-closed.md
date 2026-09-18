# Capability grant missing typed grant fail-closed

Дата: 2026-05-24

## Правило / основание

- `ОСНОВЫ и ПРАВИЛА VMX.md`: capability authority должен принадлежать typed grants, а bitmap может быть только compatibility projection/cache.
- `audit.md`, пункт 12: fallback в `CreateGrant(...)` создавал grant из `HasEffectiveCapability(...)`.

## Что изменено

- `CapabilityDescriptorSet.CreateGrant(ulong, CapabilityGrantScope)` больше не создаёт mask-derived grant при отсутствии typed grant.
- Отсутствие typed grant возвращает `CapabilityGrant.Denied`.
- Добавлен `CapabilityGrantFailClosedContract`.

## Как проверено

- Выполнено: `dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore`

## Результат сборки

- `Build succeeded`
- `54 Warning(s)`
- `0 Error(s)`

