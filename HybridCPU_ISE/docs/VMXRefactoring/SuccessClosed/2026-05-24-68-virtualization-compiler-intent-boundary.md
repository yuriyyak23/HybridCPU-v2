# Virtualization Compiler Intent Boundary

Дата: 2026-05-24

## Правило / основание

- VMX frontend должен быть compatibility frontend поверх substrate, а не прямым compiler bypass.
- Compiler IR не должен выражать прямую мутацию authoritative substrate state.
- Compiler-emitted VMX path не должен публиковать host-owned evidence или native lane tokens.
- VMX compiler boundary должен быть descriptor-gated.

## Что изменено

- `Core/VMX/CompilerBoundary/IR/VirtualizationCompilerIntent.cs` больше не является пустой заготовкой.
- Добавлены `VirtualizationCompilerIntentKind`, `VirtualizationCompilerIntentDecision`, `VirtualizationCompilerIntentRequest` и `VirtualizationCompilerIntentResult`.
- Добавлена fail-closed проверка `Evaluate(...)`, которая:
  - требует явное compiler intent;
  - допускает только compatibility frontend intent;
  - требует descriptor validation;
  - запрещает direct substrate mutation;
  - запрещает host-owned evidence emission;
  - запрещает native lane token emission.

## Как проверено

Команда:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Результат сборки

Сборка основного проекта успешно завершена. Ошибок: 0. Предупреждения: существующие предупреждения проекта.
