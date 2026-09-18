# 101. VmxCaps projection conformance tests

Дата: 2026-05-24

## Правило / основание

- `VMFUNC CapabilityQuery` должен возвращать projection of `CapabilityDescriptorSet`, а не raw `VmxCaps` CSR.
- Writes to `VmxCaps` должны быть rejected или compatibility-no-effect through `VmxCapsProjection`.
- Актуальные тесты должны использовать новые substrate/projection boundaries, а не legacy VMX-owned test types.

## Что изменено

- Добавлен `HybridCPU_ISE.Tests/VmxRefactoring/VmxCapsProjectionBoundaryTests.cs`.
- Добавлен conformance test, который:
  - задает raw `VmxCaps` CSR шире, чем projected descriptor grants;
  - запускает `VMFUNC CapabilityQuery`;
  - проверяет, что результат равен projected `CapabilityDescriptorSet`, а не raw CSR.
- Добавлен test для `VmxCapsProjection.EvaluateWrite(...)`:
  - strict mode => `Rejected`;
  - compatibility mode => `CompatibilityNoEffect`.

## Как проверено

Команды:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --filter "FullyQualifiedName~VmxCapsProjectionBoundaryTests"
```

## Результат сборки / тестов

- Основной проект: build succeeded, 0 errors, 54 warnings.
- Точечные тесты: passed, 2/2.
