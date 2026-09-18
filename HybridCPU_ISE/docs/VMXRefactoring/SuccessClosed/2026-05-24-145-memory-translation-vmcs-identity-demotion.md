# Memory translation VMCS identity demotion

Дата: 2026-05-24

## Закрытая задача

Закрыт пункт `29` из `docs/VMXRefactoring/audit.md`: `VmcsIdentity` в memory translation control мог стать authority leak для cache key / invalidation / address-space identity.

## Правило / основание

- VMCS/VMCSv2 не является substrate object.
- VMCS identity может быть только compatibility projection/evidence, не canonical memory-domain authority.
- Address-space identity должна строиться из generic memory-domain fields.

## Что изменено

- `MemoryTranslationAuthorityView` исключает `CompatibilityProjectionIdentity`.
- `MemoryTranslationAuthorityContract` проверяет, что `VmcsIdentity` не используется как authority.
- Existing `CompatibilityProjectionIdentity` оставлен как явный projection-only alias для frozen compatibility surface.

## Как проверено

Выполнена сборка основного проекта после изменения:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Результат сборки

Build succeeded, 54 warning(s), 0 error(s).
