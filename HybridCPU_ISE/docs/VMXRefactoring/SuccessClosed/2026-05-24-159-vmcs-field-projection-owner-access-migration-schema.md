# VMCS field projection owner/access/migration schema

Дата: 2026-05-24

Статус: closed

Правило/основание: `ОСНОВЫ и ПРАВИЛА VMX.md` требует, чтобы каждый VMCS field проходил через generated projection с явным substrate owner, access policy, evidence policy и migration policy; unsupported/unsafe state должен fail closed.

Что изменено:
- `VmcsFieldProjectionSchemaEntry` расширен явными `VmcsFieldProjectionAccessPolicy` и `VmcsFieldProjectionMigrationPolicy`.
- Для каждого frozen `VmcsField` задан owner, evidence class, access policy и migration policy.
- `VmcsFieldProjectionSchemaConformanceContract` теперь запрещает writable completion fields, writable host aliases, host-owned evidence exposure и projection-only guest architectural state.
- Добавлен тест `VmcsFieldProjectionSchema_DeclaresOwnerAccessEvidenceAndMigrationPolicy`.

Как проверено:
- `dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore`
- `dotnet build "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-restore`
- `dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-build --filter "FullyQualifiedName~VmxProjectionSchemaAndQuarantineTests"`

Результат сборки: build succeeded; targeted tests passed 4/4.
