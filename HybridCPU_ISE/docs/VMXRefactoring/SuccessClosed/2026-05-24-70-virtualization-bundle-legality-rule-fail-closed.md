# Virtualization bundle legality rule fail-closed

Дата: 2026-05-24

## Правило / основание

- VMX compatibility frontend не должен становиться архитектурной осью виртуализации.
- VLIW/EPIC typed-slot legality и system-singleton scheduling должны уважаться до emission.
- Bundle legality является runtime-owned authority; compiler evidence может сопровождать lowering, но не может быть источником истины.
- Compatibility projection допустим только поверх runtime-authoritative descriptor.

## Что изменено

- Актуализирован `Core/VMX/CompilerBoundary/Bundling/VirtualizationBundleLegalityRule.cs`.
- Добавлены `VirtualizationBundleLegalityDecision`, `VirtualizationBundleLegalityRequest` и `VirtualizationBundleLegalityResult`.
- Добавлена fail-closed проверка `Evaluate(...)`, которая требует:
  - наличия execution-domain `BundleLegalityDescriptor`;
  - предварительно валидированного descriptor;
  - runtime-authoritative bundle legality;
  - runtime validation, если она требуется descriptor;
  - запрета compiler evidence как authority;
  - разрешенной compatibility projection только из runtime-authoritative descriptor;
  - успешного lane-binding решения;
  - успешного system-singleton scheduling решения.
- Добавлен helper `CanBundle(...)`.

## Как проверено

Команда:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Результат сборки

- Успешно.
- 0 errors.
- 54 warnings, существующие предупреждения проекта.
