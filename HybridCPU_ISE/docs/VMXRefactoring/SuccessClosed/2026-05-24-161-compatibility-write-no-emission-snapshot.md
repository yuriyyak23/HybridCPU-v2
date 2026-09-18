# Compatibility write no-emission snapshot

Дата: 2026-05-24

Статус: closed

Правило/основание: `ОСНОВЫ и ПРАВИЛА VMX.md` требует, чтобы writes в compatibility projection были reject/no-effect и не меняли descriptor, grants, evidence, completion, memory-domain generation, lane state или migration state.

Что изменено:
- Добавлен `CompatibilityWriteNoEmissionSnapshot`.
- `CompatibilityWriteNoEmissionContract` получил `ValidateNoMutation(...)` и `IsNoMutationSatisfied(...)`.
- Contract fail-closed различает mutation descriptor/grants/evidence/completion/memory generation/lane/migration fingerprints.
- Добавлен тест `CompatibilityWriteNoEmissionSnapshot_DeniesAnySubstrateMutation`.

Как проверено:
- `dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore`
- `dotnet build "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-restore`
- `dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-build --filter "FullyQualifiedName~VmxProjectionSchemaAndQuarantineTests"`

Результат сборки: build succeeded; targeted tests passed 4/4.
