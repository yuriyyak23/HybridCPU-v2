# 93. VMX compat frontend admission

Дата: 2026-05-24

## Правило / основание

- VMX должен быть frozen compatibility frontend, а не архитектурной осью виртуализации.
- VMX frontend должен быть descriptor-gated, capability-gated, evidence-safe и runtime-authority gated.
- VMCS/VMX ABI names сохраняются только на compatibility boundary; VMCS не должен становиться authoritative state owner.

## Что изменено

- Актуализирован `Core/VMX/Compatibility/Frontend/Handlers/VmxCompatFrontend.cs`.
- Добавлены `VmxCompatFrontendAdmissionDecision`, request/result-модели и fail-closed admission surface.
- `ValidateAdmission` / `CanAdmit` теперь требуют:
  - decode-boundary validation;
  - projection-boundary validation;
  - retire-boundary validation;
  - descriptor validation;
  - capability validation;
  - runtime-owned authority validation;
  - запрет direct VMCS authority.

## Как проверено

Команда:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Результат сборки

Сборка успешно завершена: 0 errors, 54 warnings.
