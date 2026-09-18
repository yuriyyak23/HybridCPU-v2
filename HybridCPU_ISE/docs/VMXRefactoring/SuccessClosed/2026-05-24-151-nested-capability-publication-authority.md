# Nested capability publication authority

Дата: 2026-05-24

## Правило / основание

- `ОСНОВЫ и ПРАВИЛА VMX.md`: capability grants должны быть authority, а VMX CSR/capability words должны быть compatibility projection.
- `audit.md`, пункт 4: `NestedEnablementRequest` был VMX-caps-shaped; `HasExplicitVmxCapability` опирался на `VmxV2InstructionCaps.NestedVmx` напрямую.

## Что изменено

- Добавлены `NestedCompatibilityCapabilityRequirement` и `NestedCapabilityPublication`.
- `PublishedVmxCaps` сохранён как frozen compatibility input, но публикация теперь проходит через typed `CapabilityGrant`.
- `HasExplicitVmxCapability` переведён на `CapabilityPublication.CanPublishRequiredCapability`.
- Добавлен `NestedCapabilityPublicationContract`.

## Как проверено

- Выполнено: `dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore`

## Результат сборки

- `Build succeeded`
- `54 Warning(s)`
- `0 Error(s)`

