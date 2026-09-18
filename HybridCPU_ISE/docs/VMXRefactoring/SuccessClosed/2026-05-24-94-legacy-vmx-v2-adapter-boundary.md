# 94. Legacy VMX v2 adapter boundary

Дата: 2026-05-24

## Правило / основание

- Legacy VMX остается только на ABI-границе.
- VMX v2 compatibility adapters должны маршрутизироваться через compatibility frontend и generated/read-only projection.
- Legacy adapter не должен владеть authoritative substrate state и не должен раскрывать host-owned evidence.

## Что изменено

- Актуализирован `Core/VMX/Compatibility/Adapters/LegacyVmxV2/LegacyVmxV2AdapterBoundary.cs`.
- Добавлены `LegacyVmxV2AdapterDecision`, request/result-модели и fail-closed validation surface.
- `Validate` / `CanAdapt` теперь требуют:
  - frozen ABI surface;
  - routing through compatibility frontend;
  - generated/read-only projection;
  - descriptor validation;
  - capability validation;
  - запрет host-owned evidence exposure;
  - запрет direct authoritative state mutation.

## Как проверено

Команда:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Результат сборки

Сборка успешно завершена: 0 errors, 0 warnings.
