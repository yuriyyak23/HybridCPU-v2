# Compat alias map canonical schema artifact

Дата: 2026-05-24

## Правило / основание

- `deep-research-report (6).md`: generated/read-only compatibility projection должна иметь evidence chain, а не быть неявной hand-written authority.
- `Оценка рефакторинга VMX.md`: VMCS/VMX ABI aliases допустимы как frozen compatibility frontend, но должны маппиться на descriptor/runtime substrate через generated/conformance boundary.

## Что изменено

- В `CompatAliasMap` добавлен `CompatAliasSchemaArtifact` с canonical schema name/version/source hash/artifact hash/entry count.
- Добавлен `CompatAliasMap.CanonicalSchema`.
- Добавлен `MatchesCanonicalSchema(...)`, чтобы conformance слой мог проверять соответствие таблицы canonical artifact.
- В `CompatSpecArtifactSet` добавлен artifact kind `CompatAliasMap`, требующий generated parity и ABI freeze.

## Как проверено

Команда:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Результат сборки

Сборка основного проекта успешно завершена: `54 Warning(s)`, `0 Error(s)`.
