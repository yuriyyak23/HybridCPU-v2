# Retire Evidence Publication Boundary

Дата: 2026-05-24

## Правило / основание

- VMX effects не должны становиться видимыми до корректного retire boundary.
- Retire publication должна быть evidence-safe и observability-gated.
- Host-owned runtime evidence, scheduler evidence, backend binding evidence и native token evidence не должны публиковаться как guest-visible state.
- Evidence visibility принадлежит generic `EvidencePolicyDescriptor` / `ObservabilityDescriptor`, а не VMCS-owned состоянию.

## Что изменено

- `Core/VMX/Substrate/Runtime/RetireEvidence/RetireEvidenceBoundary.cs` больше не является пустой заготовкой.
- Добавлены `RetireEvidenceDecision`, `RetireEvidenceResult` и `RetireEvidencePublicationRequest`.
- Добавлена fail-closed проверка `ValidatePublication(...)`:
  - требует валидный `VmxRetireEffect`;
  - guest-visible publication разрешается только через `EvidencePolicyDescriptor.CanExposeToGuest(...)` и `ObservabilityDescriptor.CanPublishToGuest(...)`;
  - host-owned evidence запрещается для guest-visible publication;
  - host-local capture разрешается только через `ObservabilityDescriptor.CanCaptureHostLocal(...)`.

## Как проверено

Команда:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Результат сборки

Сборка основного проекта успешно завершена. Ошибок: 0. Предупреждения: существующие предупреждения проекта.
