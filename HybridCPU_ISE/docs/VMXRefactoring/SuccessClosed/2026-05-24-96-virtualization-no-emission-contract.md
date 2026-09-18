# 96. Virtualization no-emission contract

Дата: 2026-05-24

## Правило / основание

- VMX compatibility frontend не должен эмитить host-owned evidence, native lane tokens или direct substrate state.
- Compatibility-visible state должен проходить через generated projection и no-emission regression gate.
- Golden/conformance artifacts должны фиксировать отсутствие неавторизованной emission surface.

## Что изменено

- Актуализирован `Core/VMX/Conformance/NoEmission/VirtualizationNoEmissionContract.cs`.
- Добавлены `VirtualizationNoEmissionContractDecision`, request/result-модели и conformance validation surface.
- `Validate` / `IsSatisfied` теперь требуют:
  - captured golden artifact;
  - generated compatibility projection;
  - запрет host-owned evidence emission;
  - запрет native lane token emission;
  - положительное решение `VirtualizationNoEmissionRegressionGate`.

## Как проверено

Команда:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Результат сборки

Сборка успешно завершена: 0 errors, 0 warnings.
