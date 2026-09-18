# Legacy VMX reverse import contract

Дата: 2026-05-24

## Закрытая задача

Закрыт пункт `21` из `docs/VMXRefactoring/audit.md`: не было machine-readable запрета на обратный импорт legacy behavior.

## Правило / основание

- Legacy VMX остаётся только на ABI-границе.
- Файл из `Legacy/VMX` не должен возвращаться в `Core/VMX` без снятия VMX authority.
- Возврат требует descriptor owner, capability policy, evidence policy, retire boundary и projection tests.

## Что изменено

- Добавлен `Core/VMX/Conformance/AuthorityBoundary/LegacyReverseImportContract.cs`.
- Контракт fail-closed проверяет origin из legacy, отсутствие authoritative VMX state, descriptor owner, capability policy, evidence policy, retire boundary и projection tests.

## Как проверено

Выполнена сборка основного проекта после изменения:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Результат сборки

Build succeeded, 54 warning(s), 0 error(s).
