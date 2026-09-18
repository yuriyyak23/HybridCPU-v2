# 95. Migration replay contract fail-closed

Дата: 2026-05-24

## Правило / основание

- Migration/checkpoint state не должен принадлежать VMX/VMCS.
- Host-owned runtime evidence, scheduler evidence, backend binding evidence и native token evidence должны recompute/deny on restore.
- Golden artifacts и replay conformance должны проверять deterministic restore и descriptor-authorized state.

## Что изменено

- Актуализирован `Core/VMX/Conformance/MigrationReplay/MigrationReplayContract.cs`.
- Добавлены `MigrationReplayContractDecision`, request/result-модели и fail-closed replay validation surface.
- `ValidateReplay` / `CanReplay` теперь требуют:
  - captured golden artifact;
  - descriptor authority validation;
  - successful migration validation policy;
  - запрет replay/import host-owned evidence payloads;
  - deterministic replay.

## Как проверено

Команда:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Результат сборки

Сборка успешно завершена: 0 errors, 0 warnings.
