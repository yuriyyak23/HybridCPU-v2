# Dirty tracking descriptor authority

Дата: 2026-05-24

## Правило / основание

- `ОСНОВЫ и ПРАВИЛА VMX.md`: VMX не владеет memory state и evidence/security state.
- `deep-research-report (6).md`: `DirtyLogBlock` должен мапиться на `DirtyTrackingServiceDescriptor`; service должен bind-иться к memory domain, а не к VMX.

## Что изменено

- `DirtyTrackingServiceDescriptor` больше не пустая substrate-заготовка.
- Добавлены runtime-owned authority marker, host-owned visibility default, enable flag, range capacity, epoch и fence-before-snapshot policy.
- Добавлены fail-closed helpers: tracking выключен по умолчанию, host-owned evidence не раскрывается, guest summary доступен только при явном runtime-owned включении.

## Как проверено

Запущено:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Результат сборки

Успешно: `Build succeeded`, 0 errors. Остались существующие предупреждения проекта.
