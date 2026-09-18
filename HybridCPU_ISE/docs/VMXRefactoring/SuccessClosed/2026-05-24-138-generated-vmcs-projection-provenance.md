# Generated VMCS projection provenance

Дата: 2026-05-24

## Закрытая задача

Закрыт пункт `23` из `docs/VMXRefactoring/audit.md`: generated VMCS projection могла оставаться handwritten semantics без отдельного provenance/parity gate.

## Правило / основание

- Generated projection vocabulary должен быть доказуемым conformance artifact, а не только именем папки.
- VMCS projection не должна становиться скрытым authority source.
- Drift между schema, alias map и generated projection должен fail closed через conformance boundary.

## Что изменено

- Добавлен `Core/VMX/Conformance/GeneratedParity/GeneratedVmcsProjectionProvenanceContract.cs`.
- Контракт связывает VMCS field schema parity с общим generated projection parity contract.
- Добавлена проверка совпадения artifact hash между canonical VMCS field schema и VMCS projection provenance descriptor.

## Как проверено

Выполнена сборка основного проекта:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Результат сборки

Build succeeded, 54 warning(s), 0 error(s).
