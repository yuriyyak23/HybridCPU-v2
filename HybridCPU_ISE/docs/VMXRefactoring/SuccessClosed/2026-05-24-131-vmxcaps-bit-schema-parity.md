# VmxCaps capability bit schema parity

Дата: 2026-05-24

## Правило / основание

- Capability descriptor/grant model должен быть authority, а VMX capability CSR должен быть projection.
- Generated/declared compatibility artifacts должны иметь conformance evidence, чтобы избежать drift между VMX bit aliases и substrate capabilities.

## Что изменено

- В `CapabilityDescriptorSetSchema` добавлен canonical VmxCaps bit schema artifact.
- Каждому VMX compatibility bit сопоставлен `CapabilityBitSchemaEntry` с mask и projection scope.
- Добавлена проверка `MatchesCanonicalVmxCompatibilityBitSchema()`.
- Добавлен conformance contract `VmxCapsBitSchemaConformanceContract`.

## Как проверено

```text
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Результат сборки

Build succeeded. 54 Warning(s), 0 Error(s).
