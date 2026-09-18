# Debug Trace Export Policy Fail-Closed

Дата: 2026-05-24

## Правило / основание

- Observability не должен быть VMCS-owned.
- Debug trace export должен использовать generic evidence/observability descriptors.
- Host-owned runtime evidence, scheduler evidence, backend binding evidence и native token evidence не должны экспортироваться в guest-visible trace без явного redaction/gate.
- Пустые сервисы экспорта должны fail-closed.

## Что изменено

- `Core/VMX/Substrate/Evidence/DebugTrace/DebugTraceExportPolicy.cs` больше не является пустой заготовкой.
- Добавлены `DebugTraceExportDecision` и `DebugTraceExportResult`.
- Добавлена политика `DebugTraceExportPolicy` с fail-closed значениями по умолчанию.
- Добавлена проверка `ValidateExport(...)`:
  - guest-visible export требует явного `AllowGuestExport`, `EvidencePolicyDescriptor.CanExposeToGuest(...)` и `ObservabilityDescriptor.CanPublishToGuest(...)`;
  - host-local export требует явного `AllowHostLocalExport` и `ObservabilityDescriptor.CanCaptureHostLocal(...)`;
  - guest export host-owned evidence возвращает `RedactionRequired`, если политика требует redaction.

## Как проверено

Команда:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Результат сборки

Сборка основного проекта успешно завершена. Ошибок: 0. Предупреждения: существующие предупреждения проекта.
