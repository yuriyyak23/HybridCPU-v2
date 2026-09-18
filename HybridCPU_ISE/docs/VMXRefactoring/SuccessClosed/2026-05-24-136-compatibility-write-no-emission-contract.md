# Compatibility write no-emission contract

Дата: 2026-05-24

## Правило / основание

- Запись в VMX compatibility projection не должна становиться authority mutation/emission path.
- `VmxCaps` write в strict mode должен fail closed; compatibility no-effect допустим только при explicit fence.

## Что изменено

- Добавлен `CompatibilityWriteNoEmissionContract`.
- Контракт разрешает `VmxCaps` write только как `Rejected` либо как `CompatibilityNoEffect` при включенном `CompatibilityWriteFenceEnabled` и no-effect publication policy.
- Unsupported write result, missing fence и missing no-effect policy возвращают deny decision.

## Как проверено

```text
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
rg -n "CompatibilityWriteNoEmissionContract|ValidateVmxCapsWrite" Core\VMX\Conformance\NoEmission
```

## Результат сборки

Build succeeded. 54 Warning(s), 0 Error(s).
