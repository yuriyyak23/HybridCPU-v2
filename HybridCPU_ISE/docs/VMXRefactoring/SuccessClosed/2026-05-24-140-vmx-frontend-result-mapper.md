# VMX frontend result mapper

Дата: 2026-05-24

## Закрытая задача

Закрыт пункт `27` из `docs/VMXRefactoring/audit.md`: не было явного separation между generic validation result и VMX frontend result.

## Правило / основание

- Substrate должен возвращать generic domain/security/authority outcomes.
- VMX compatibility frontend должен отдельно преобразовывать результаты в VMX-compatible `VMFailValid`, `VMFailInvalid`, `VMExit` или `VMAbort`.
- VMCS/VMX-specific status не должен быть substrate-owned result type.

## Что изменено

- Добавлен `Core/VMX/Compatibility/Frontend/Projection/VmxFrontendResultMapper.cs`.
- Введены `VmxFrontendResultKind` и `VmxFrontendResult`.
- Добавлен mapper из `NestedValidationResult` в VMX-compatible completion/result boundary.

## Как проверено

Выполнена сборка основного проекта:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Результат сборки

Build succeeded, 54 warning(s), 0 error(s).
