# 98. VMX invalidation scope admission policy

Дата: 2026-05-24

## Правило / основание

- `INVEPT` / `INVVPID` не должны владеть MMU model.
- VMX invalidation scopes должны быть frozen ABI aliases поверх generic translation/domain invalidation.
- Translation authority и runtime-owned invalidation validation должны быть обязательными fail-closed gates.

## Что изменено

- Актуализирован `Core/VMX/Compatibility/FrozenAbi/OperandForms/VmxInvalidationScope.cs`.
- Добавлены `VmxInvalidationScopeAdmissionDecision`, request/result-модели и `VmxInvalidationScopeAdmissionPolicy`.
- Policy fail-closed проверяет:
  - scope входит в frozen ABI alias set;
  - descriptor validation;
  - capability validation;
  - runtime-owned invalidation validation;
  - translation-domain authority validation;
  - запрет direct MMU mutation.

## Как проверено

Команда:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Результат сборки

Сборка успешно завершена: 0 errors, 54 warnings.
