# VMX Compatibility Decode Boundary Fail-Closed

Дата: 2026-05-24

## Правило / основание

- VMX должен оставаться frozen compatibility frontend, а не архитектурной осью virtualization.
- VMX frontend должен быть descriptor-gated, capability-gated, scheduling-gated и no-emission-gated.
- Frozen VMX opcode ABI сохраняется, но decode boundary не должен становиться bypass к substrate.
- Unknown/unsupported VMX opcode должен fail-closed.

## Что изменено

- `Core/VMX/Compatibility/Frontend/Decode/VmxCompatDecodeBoundary.cs` больше не является пустой заготовкой.
- Добавлены `VmxCompatDecodeDecision`, `VmxCompatDecodeRequest` и `VmxCompatDecodeResult`.
- Добавлена проверка `Decode(...)`:
  - допускает только frozen VMX opcodes;
  - требует descriptor validation;
  - требует capability validation;
  - требует scheduling validation;
  - требует no-emission validation;
  - возвращает `VmxInstructionPayload` только после прохождения всех gates.

## Как проверено

Команда:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Результат сборки

Сборка основного проекта успешно завершена. Ошибок: 0. Предупреждения: существующие предупреждения проекта.
