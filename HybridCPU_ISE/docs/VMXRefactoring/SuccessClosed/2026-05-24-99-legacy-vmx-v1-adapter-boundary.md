# 99. Legacy VMX v1 adapter boundary

Дата: 2026-05-24

## Правило / основание

- Legacy VMX остается только на ABI-границе.
- Старый VMX v1 путь не должен владеть authoritative state и должен публиковать effects через typed retire boundary.
- VMX frontend должен быть descriptor-gated, capability-gated и evidence-safe.

## Что изменено

- Добавлен `Core/VMX/Compatibility/Adapters/LegacyVmxV1/LegacyVmxV1AdapterBoundary.cs`.
- Добавлены `LegacyVmxV1AdapterDecision`, request/result-модели и fail-closed validation surface.
- `Validate` / `CanAdapt` теперь требуют:
  - frozen ABI surface;
  - routing through compatibility frontend;
  - typed retire boundary publication;
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

Сборка успешно завершена: 0 errors, 54 warnings.
