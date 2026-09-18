# VmxCaps bit schema typed grant source

Дата: 2026-05-24

Статус: closed

Правило/основание: `ОСНОВЫ и ПРАВИЛА VMX.md` требует, чтобы `VmxCaps` был pure compatibility alias over `CapabilityDescriptorSet`, а каждый compatibility bit имел typed grant source, publication/projection policy и guest-visible evidence boundary.

Что изменено:
- `CapabilityBitSchemaEntry` расширен `TypedGrantSource`, `CapabilityFrontendProjectionPolicy` и `CapabilityEvidenceVisibility`.
- Каждый compatibility `VmxCaps` bit теперь указывает typed grant source.
- `VmxCapsBitSchemaConformanceContract` запрещает missing typed grant source, missing projection policy и host-only evidence projection.
- Добавлен тест `VmxCapsBitSchema_RequiresTypedGrantSourceAndGuestVisibleProjectionPolicy`.

Как проверено:
- `dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore`
- `dotnet build "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-restore`
- `dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-build --filter "FullyQualifiedName~VmxProjectionSchemaAndQuarantineTests"`

Результат сборки: build succeeded; targeted tests passed 4/4.
