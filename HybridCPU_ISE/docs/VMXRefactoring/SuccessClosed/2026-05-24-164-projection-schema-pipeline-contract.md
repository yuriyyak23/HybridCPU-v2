# Projection schema pipeline contract

Дата: 2026-05-24

Статус: closed

## Основание

- `ОСНОВЫ и ПРАВИЛА VMX.md`: compatibility frontend may expose generated/read-only projections, while authority remains in generic descriptors, typed grants, evidence policy and runtime-owned legality.
- `Оценка рефакторинга VMX security-centric.md`: generated projection must be machine-checkable and fail closed for unknown, unsupported or host-owned state.
- `audit.md`: пункт 21 требует отличать checked-in generated scaffolding from generated-by-pipeline evidence.

## Что изменено

- Добавлен `Core/VMX/Conformance/GeneratedParity/ProjectionSchemaPipelineContract.cs`.
- Контракт проверяет для VMCS field projection и VmxCaps bit projection:
  - canonical schema path;
  - generator name;
  - source hash;
  - generated artifact hash;
  - non-zero entry count;
  - build-generation requirement;
  - conformance parity requirement;
  - ABI freeze requirement.
- Расширен `HybridCPU_ISE.Tests/VmxRefactoring/VmxProjectionSchemaAndQuarantineTests.cs`.
- Новый тест `ProjectionSchemaPipeline_HasCanonicalArtifactsForGeneratedTables` проверяет pipeline contracts и физическое наличие schema artifacts.

## Проверка

- `dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore`
- `dotnet build "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-restore`
- `dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-build --filter "FullyQualifiedName~VmxProjectionSchemaAndQuarantineTests"`

## Результат

- Основной проект собирается: succeeded, 0 errors.
- Тестовый проект собирается: succeeded, 0 errors.
- Таргетный conformance набор: Passed 5/5.

## Остаточный риск

Контракт делает pipeline expectations machine-checkable. Реальная build-time генерация C# из schema artifacts все еще должна быть добавлена отдельным шагом.
