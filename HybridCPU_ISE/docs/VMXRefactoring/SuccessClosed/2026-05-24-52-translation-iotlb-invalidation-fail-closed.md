# Translation and IOTLB invalidation fail-closed boundary

Дата: 2026-05-24

## Правило / основание

- `ОСНОВЫ и ПРАВИЛА VMX.md`: INVEPT/INVVPID должны быть VMX aliases over generic translation/domain invalidation.
- `Оценка рефакторинга VMX security-centric.md`: security должно проверяться на descriptor transition boundary, а VMX request не должен быть bypass.

## Что изменено

- `TranslationInvalidationService` больше не пустая заготовка: добавлены generic scope/decision/result типы и проверка memory-domain descriptor, address-space authority, translation policy, range и fence.
- `IotlbInvalidationService` больше не пустая заготовка: добавлены generic scope/decision/result типы и проверка I/O-domain descriptor, IOMMU authority, virtualization block и обязательных identifiers.
- Низкоуровневые VMX-named invalidation calls оставлены как compat plumbing; публичная service-boundary модель использует generic descriptor/runtime authority.

## Как проверено

Запущено:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Результат сборки

Успешно: `Build succeeded`, 0 errors. Остались существующие предупреждения проекта.
