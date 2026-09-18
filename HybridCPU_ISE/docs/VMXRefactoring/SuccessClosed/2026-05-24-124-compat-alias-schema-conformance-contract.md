# Compat alias schema conformance contract

Дата: 2026-05-24

## Правило / основание

- `deep-research-report (6).md`: generated artifacts должны проверяться через conformance/evidence chain, чтобы checked-in projection tables не становились новым handwritten authority.
- `Оценка рефакторинга VMX.md`: frozen VMX/VMCS ABI names могут жить только как compatibility frontend поверх descriptor/runtime substrate.

## Что изменено

- Добавлен `CompatAliasSchemaConformanceContract`.
- Контракт проверяет:
  - наличие `CompatAliasMap` в `CompatSpecArtifactSet`;
  - обязательность generated parity;
  - обязательность ABI freeze;
  - canonical schema marker и непустые schema/artifact hashes;
  - совпадение текущего alias map с canonical schema artifact;
  - отсутствие пустых alias names;
  - frozen ABI для всех alias entries.

## Как проверено

Команда:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Результат сборки

Сборка основного проекта успешно завершена: `54 Warning(s)`, `0 Error(s)`.
