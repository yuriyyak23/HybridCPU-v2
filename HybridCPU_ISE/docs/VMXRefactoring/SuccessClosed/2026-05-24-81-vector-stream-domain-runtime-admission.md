# Vector-stream domain runtime admission

Дата: 2026-05-24

## Правило / основание

- Lane6/Lane7/vector-stream ownership должен быть вне VMX.
- VMSAVEX/VMRESTX не должны владеть extended state; они должны вызывать save/restore projection over execution-domain/vector-stream descriptors.
- Runtime-owned legality and validation должны fail-closed.

## Что изменено

- Актуализирован `Core/VMX/Substrate/Domains/VectorStream/VectorStreamDomainRuntime.cs`.
- Добавлены `VectorStreamDomainRuntimeDecision`, `VectorStreamDomainRuntimeRequest` и `VectorStreamDomainRuntimeResult`.
- `VectorStreamDomainRuntime` теперь валидирует execution-extension descriptor, runtime authority, vector-stream descriptor, enabled state, save/restore mask, vector length, stream descriptor table binding и compatibility projection gate.
- Поведение VMX handlers и RTL-логика не изменялись.

## Как проверено

Команда:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Результат сборки

- Успешно.
- 0 errors.
- 54 warnings, существующие предупреждения проекта.
