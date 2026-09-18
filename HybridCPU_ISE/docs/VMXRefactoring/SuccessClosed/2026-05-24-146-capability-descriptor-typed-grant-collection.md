# Capability descriptor typed grant collection

Дата: 2026-05-24

## Закрытая задача

Закрыт пункт `5` из `docs/VMXRefactoring/audit.md`: `CapabilityDescriptorSet` оставался bitmap-first без canonical typed grant collection.

## Правило / основание

- Capabilities принадлежат `CapabilityDescriptorSet`.
- Capabilities должны быть typed grants, не просто bitmap.
- Bitmap допустим как compatibility projection/cache, но authority должна иметь typed grant форму.

## Что изменено

- Добавлен `CapabilityGrantCollection`.
- `CapabilityDescriptorSet` теперь публикует `TypedGrants`.
- Коллекция строится из canonical `CapabilityDescriptorSetSchema.VmxCompatibilityBits` и хранит typed grants с owner, delegation, revocation, migration, evidence и frontend projection metadata.
- Добавлен `CapabilityGrantCollectionAuthorityContract`.

## Как проверено

Выполнена сборка основного проекта после изменения:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Результат сборки

Build succeeded, 54 warning(s), 0 error(s).
