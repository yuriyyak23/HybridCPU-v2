# Compat alias generator pipeline provenance

Дата: 2026-05-24

## Правило / основание

- `ОСНОВЫ и ПРАВИЛА VMX.md`: generated/read-only VMX projections должны иметь canonical schema и conformance parity, чтобы handwritten tables не становились source of truth.
- `audit.md`, пункт 9: `CompatAliasMap` имел hashes и schema artifact, но не был закреплён как build-time generated pipeline contract.

## Что изменено

- Добавлен `CompatAliasGeneratorPipelineContract`.
- Зафиксированы schema path, generator name, canonical schema, generated artifact hash, generated parity requirement и ABI freeze requirement.
- Pipeline contract использует `CompatAliasSchemaConformanceContract` как parity gate.

## Как проверено

- Выполнено: `dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore`

## Результат сборки

- `Build succeeded`
- `54 Warning(s)`
- `0 Error(s)`

