# Host-Owned Evidence Boundary Fail-Closed

Дата: 2026-05-24

## Правило / основание

- Host-owned evidence не должен становиться guest-visible state.
- EvidencePolicy и Observability должны быть generic substrate authority, а не VMCS-owned state.
- Restore/migration не должны сохранять host-local runtime, scheduler, backend binding и native token evidence как гостевое состояние.
- Неявный экспорт evidence должен fail-closed.

## Что изменено

- `Core/VMX/Substrate/Evidence/HostOwned/HostOwnedEvidenceBoundary.cs` больше не является пустой заготовкой.
- Добавлены `HostOwnedEvidenceBoundaryDecision` и `HostOwnedEvidenceBoundaryResult`.
- Добавлены проверки:
  - `ValidateGuestProjection(...)` через `EvidencePolicyDescriptor.CanExposeToGuest(...)` и `ObservabilityDescriptor.CanPublishToGuest(...)`;
  - `ValidateHostLocalCapture(...)` через `ObservabilityDescriptor.CanCaptureHostLocal(...)`;
  - `ValidateRestore(...)` через `EvidencePolicyDescriptor.MustRecomputeAfterRestore(...)` и `CanSerializeAcrossMigration(...)`.

## Как проверено

Команда:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Результат сборки

Сборка основного проекта успешно завершена. Ошибок: 0. Предупреждения: существующие предупреждения проекта.
