# Root authority descriptor boundary

Дата: 2026-05-24

## Правило / основание

- `ОСНОВЫ и ПРАВИЛА VMX.md`: `VMXON` не должен быть архитектурным root switch; VMX frontend должен активироваться через domain runtime authority.
- `Оценка рефакторинга VMX security-centric.md`: authority must be checked at descriptor transition boundary.

## Что изменено

- `RootAuthorityDescriptor` больше не пустая заготовка.
- Добавлены root authority class, epoch, granted capability mask и deny-by-default gates для compatibility frontend activation и authoritative state mutation.
- VMX/VMCS ABI names не менялись.

## Как проверено

Запущено:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Результат сборки

Успешно: `Build succeeded`, 0 errors. Остались существующие предупреждения проекта.
