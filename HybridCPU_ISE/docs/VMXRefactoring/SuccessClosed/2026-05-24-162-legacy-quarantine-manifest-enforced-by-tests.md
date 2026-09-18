# Legacy quarantine manifest enforced by tests

Дата: 2026-05-24

Статус: closed

Правило/основание: `ОСНОВЫ и ПРАВИЛА VMX.md` и audit требуют machine-enforced запрет обратного импорта legacy VMX files без descriptor owner, capability policy, evidence policy, retire boundary, no-emission/projection tests и projection-only behavior.

Что изменено:
- Добавлен тест, который проверяет, что все entries `LegacyVmxQuarantineManifest` указывают на существующие files в `Legacy/VMX`.
- Тест проверяет fail-closed reverse import без доказательств.
- Тест проверяет, что manifest допускает return только при полном projection-only proof.

Как проверено:
- `dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore`
- `dotnet build "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-restore`
- `dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-build --filter "FullyQualifiedName~VmxProjectionSchemaAndQuarantineTests"`

Результат сборки: build succeeded; targeted tests passed 4/4.
