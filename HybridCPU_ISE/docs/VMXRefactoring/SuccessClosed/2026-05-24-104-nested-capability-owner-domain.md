# Nested capability owner domain

Дата: 2026-05-24

## Правило / основание

- Nested grants должны получать owner domain из runtime context, а не из default compatibility owner.
- Default runtime owner не должен быть неявной финальной authority model.
- Основание: `ОСНОВЫ и ПРАВИЛА VMX.md`; audit.md, пункт 28.

## Что изменено

- `NestedCompatibilityCapabilityRequirement.Create(...)` требует явный `ownerDomainId`.
- `NestedEnablementRequest` несет `OwnerDomainId`.
- `NestedCapabilityPublication.FromCompatibilityAlias(...)` принимает owner domain и передает его в grant requirement.
- `NestedDomainCapabilityProjectionContract` проверяет, что owner domain задан и не равен no-owner marker.

## Как проверено

- Выполнено: `dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore`.

## Результат сборки

- Build succeeded.
- 54 warnings, 0 errors.

