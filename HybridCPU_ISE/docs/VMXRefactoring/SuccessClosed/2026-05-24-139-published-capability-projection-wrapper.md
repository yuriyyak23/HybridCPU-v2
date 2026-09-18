# Published capability projection wrapper

Дата: 2026-05-24

## Закрытая задача

Закрыт пункт `26` из `docs/VMXRefactoring/audit.md`: `PublishedCapabilityWord` оставался слабым generic alias над VMX-shaped `ulong`.

## Правило / основание

- `VmxCaps` и VMX capability words должны быть compatibility projection, а не authority.
- Capability authority должен двигаться к descriptor/grant model.
- Bitmap/word формы допустимы только как frozen frontend projection или fast compatibility cache.

## Что изменено

- В `Core/VMX/Substrate/Nested/Policies/NestedDomainController.cs` добавлен `PublishedCapabilityProjection`.
- `NestedEnablementRequest` получил typed projection facade `PublishedCapabilities`.
- Проверка nested VMX capability переведена через `PublishedCapabilities.Contains(...)` и marker `IsProjectionOnly`.

## Как проверено

Выполнена сборка основного проекта:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Результат сборки

Build succeeded, 54 warning(s), 0 error(s).
