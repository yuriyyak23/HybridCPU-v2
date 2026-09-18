# Memory address-space and translation policy authority

Дата: 2026-05-24

## Правило / основание

- `ОСНОВЫ и ПРАВИЛА VMX.md`: VMX не владеет memory state; memory virtualization принадлежит `MemoryDomainDescriptor`.
- `deep-research-report (6).md`: VMX должен быть compatibility frontend, а memory domain descriptors должны быть частью generic substrate.

## Что изменено

- `AddressSpaceDescriptor` больше не пустая заготовка: добавлены runtime-owned authority marker, `AddressSpaceId`, bounded range и fail-closed `AllowsRange`.
- `MemoryTranslationPolicy` больше не пустая заготовка: добавлены runtime-owned authority marker, явные permissions для invalidation и deny-by-default policy.
- VMX/VMCS ABI names не переименовывались; изменения ограничены substrate memory descriptors.

## Как проверено

Запущено:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Результат сборки

Успешно: `Build succeeded`, 0 errors. Остались существующие предупреждения проекта.
