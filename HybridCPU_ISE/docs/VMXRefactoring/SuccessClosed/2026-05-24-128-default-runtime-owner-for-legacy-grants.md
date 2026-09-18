# Default runtime owner for legacy grants

Дата: 2026-05-24

## Правило / основание

- `Оценка рефакторинга VMX security-centric.md`: domain capability grant должен иметь owner domain; mask-only grant не должен оставаться authoritative model.
- `ОСНОВЫ и ПРАВИЛА VMX.md`: compatibility API можно сохранять, но substrate authority должен быть descriptor/grant-owned.

## Что изменено

- В `CapabilityGrant` добавлены constants:
  - `NoOwnerDomainId`;
  - `DefaultRuntimeOwnerDomainId`.
- Legacy-compatible constructor `CapabilityGrant(ulong, CapabilityGrantScope, bool)` теперь назначает `DefaultRuntimeOwnerDomainId` для granted `DomainGranted` scope.
- Legacy-compatible overload `CapabilityNegotiationService.Negotiate(...)` теперь делегирует к owner-aware path с `DefaultRuntimeOwnerDomainId`.
- Owner-aware overload сохраняет явный `ownerDomainId` caller-а и остаётся fail-closed через conformance contract.

## Как проверено

Команда:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Результат сборки

Сборка основного проекта успешно завершена: `54 Warning(s)`, `0 Error(s)`.
