# 97. VMFUNC leaf admission policy

Дата: 2026-05-24

## Правило / основание

- `VMFUNC` не должен становиться bypass и допустим только как restricted fast path через prevalidated capability grants.
- Lane7 ownership должен оставаться вне VMX; VMX leaf names сохраняются как frozen ABI aliases.
- Capability grants и descriptor validation должны быть authority boundary для VMX frontend.

## Что изменено

- Актуализирован `Core/VMX/Compatibility/FrozenAbi/FunctionLeaves/VmxFunctionLeaf.cs`.
- Добавлены `VmxFunctionLeafAdmissionDecision`, request/result-модели и `VmxFunctionLeafAdmissionPolicy`.
- Policy fail-closed проверяет:
  - leaf входит в frozen compatibility alias set;
  - surface является frozen ABI;
  - descriptor validation;
  - capability validation;
  - явный Lane7 grant для Lane7 leaves;
  - prevalidated capability grants для fast path.

## Как проверено

Команда:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Результат сборки

Сборка успешно завершена: 0 errors, 54 warnings.
