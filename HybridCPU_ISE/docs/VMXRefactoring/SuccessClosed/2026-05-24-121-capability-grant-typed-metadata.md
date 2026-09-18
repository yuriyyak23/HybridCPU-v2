# Capability grant typed metadata

Дата: 2026-05-24

## Правило / основание

- `ОСНОВЫ и ПРАВИЛА VMX.md`: VMX не должен быть источником истины для capabilities; authority переносится в generic capability descriptor/grant substrate.
- `Оценка рефакторинга VMX security-centric.md`: capability model должен хранить owner/scope/delegation/revocation/migration/evidence/projection policy как typed authority, а bitmap оставлять compatibility projection / fast mask.

## Что изменено

- В `Core/VMX/Substrate/Capabilities/Grants/CapabilityGrant.cs` добавлены typed policy enums:
  - `CapabilityDelegationPolicy`;
  - `CapabilityRevocationPolicy`;
  - `CapabilityMigrationClass`;
  - `CapabilityEvidenceVisibility`;
  - `CapabilityFrontendProjectionPolicy`.
- `CapabilityGrant` расширен owner/policy metadata без удаления существующего bitmap API.
- Старые конструкторы сохранены как compatibility path и делегируют к typed defaults.
- Добавлен `HasTypedAuthority`.
- `IsPublishableCompatibilityGrant` теперь учитывает `FrontendProjectionPolicy`.

## Как проверено

Команда:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Результат сборки

Сборка основного проекта успешно завершена: `0 Error(s)`.
