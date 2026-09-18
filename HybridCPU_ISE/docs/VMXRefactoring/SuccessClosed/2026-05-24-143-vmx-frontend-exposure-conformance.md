# VMX frontend exposure conformance

Дата: 2026-05-24

## Закрытая задача

Закрыт пункт `24` из `docs/VMXRefactoring/audit.md`: completion/evidence/migration/lane descriptors были заявлены, но не было единого frontend exposure conformance gate.

## Правило / основание

- VMX frontend не должен открывать host-owned evidence.
- VMX frontend не должен публиковать native tokens, backend handles, scheduler evidence, migration-only fields или lane-private state.
- VMX-visible state должен проходить generated projection и access policy.

## Что изменено

- Добавлен `Core/VMX/Conformance/HostEvidenceNonLeak/VmxFrontendExposureConformanceContract.cs`.
- Контракт проверяет generated projection, наличие access policy и запрет guest-visible exposure для host/native/backend/scheduler/migration/lane-private state.

## Как проверено

Выполнена сборка основного проекта после изменения:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Результат сборки

Build succeeded, 54 warning(s), 0 error(s).
