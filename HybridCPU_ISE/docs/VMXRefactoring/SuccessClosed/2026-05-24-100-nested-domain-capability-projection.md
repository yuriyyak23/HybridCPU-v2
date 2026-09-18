# Nested domain capability projection

Дата: 2026-05-24

## Правило / основание

- VMX compatibility bits не должны быть substrate authority.
- Generic nested authority должна выражаться typed domain capability, а VMX mask должен оставаться frontend projection.
- Основание: `ОСНОВЫ и ПРАВИЛА VMX.md`; audit.md, пункт 6.

## Что изменено

- Введены `NestedDomainCapability` и `NestedDomainCapabilityProjection`.
- `NestedCompatibilityCapabilityRequirement` теперь хранит typed capability projection, а `VmxV2InstructionCaps.NestedVmx` остается frontend mask внутри projection.
- Добавлен conformance contract `NestedDomainCapabilityProjectionContract`.

## Как проверено

- Выполнено: `dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore`.

## Результат сборки

- Build succeeded.
- 54 warnings, 0 errors.

