# Nested domain descriptor authority

Дата: 2026-05-24

## Правило / основание

- Nested model должен двигаться к domain composition, а не к публичной архитектуре VMCS12/VMCS02.
- `NestedDomainDescriptor` / `NestedProjectionService` являются целевой публичной моделью nested composition.
- Host-owned evidence не должно попадать в child-visible state, а Lane6/Lane7 passthrough должен быть заблокирован без явной политики.

## Что изменено

- Актуализирован `Core/VMX/Substrate/Nested/Descriptors/NestedDomainDescriptor.cs`.
- Добавлены `NestedDomainAuthority`, parent/child domain binding, nested capability mask, domain composition gate, compatibility projection gate, host evidence exclusion и lane passthrough blocking.
- Добавлены вычисляемые свойства `CanComposeDomain` и `CanProjectToCompatibilityFrontend`.
- Поведение VMX handlers и RTL-логика не изменялись.

## Как проверено

Команда:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Результат сборки

- Успешно.
- 0 errors.
- 54 warnings, существующие предупреждения проекта.
