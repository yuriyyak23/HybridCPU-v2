# 102. VmxExecutionUnit no direct VmxCaps read contract

Дата: 2026-05-24

## Правило / основание

- `VmxCaps` не должен быть authority source внутри VMX handler.
- `VmxExecutionUnit` должен обращаться к capability surface через projection/source boundary.
- No-regression tests должны фиксировать отсутствие возврата к direct CSR reads.

## Что изменено

- Актуализирован `HybridCPU_ISE.Tests/VmxRefactoring/VmxCapsProjectionBoundaryTests.cs`.
- Добавлен regression test `VmxExecutionUnit_DoesNotReadVmxCapsCsrDirectly`.
- Тест статически проверяет, что `Core/VMX/Compatibility/Frontend/Handlers/VmxExecutionUnit.cs` не содержит:
  - `CsrAddresses.VmxCaps`;
  - `DirectRead(CsrAddresses.VmxCaps`;
  - `Read(CsrAddresses.VmxCaps`.

## Как проверено

Команды:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --filter "FullyQualifiedName~VmxCapsProjectionBoundaryTests"
```

## Результат сборки / тестов

- Основной проект: build succeeded, 0 errors, 0 warnings.
- Точечные тесты: passed, 3/3.
