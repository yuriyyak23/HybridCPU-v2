# VMCS field projection schema parity

Дата: 2026-05-24

## Закрытая задача

Закрыты пункты `10` и `11` из `docs/VMXRefactoring/audit.md`: coarse-grained VMCS aliasing и отсутствие field-level VMCS projection schema parity.

## Правило / основание

- VMX/VMCS должны оставаться frozen compatibility frontend и generated/read-only projection там, где это возможно.
- VMCS fields должны быть generated/declared alias map over descriptors; unsupported fields должны fail closed.
- Runtime/domain descriptors являются authority, VMCS не должен быть runtime-owned source of truth.

## Что изменено

- Добавлен `Core/VMX/Compatibility/Generated/VmcsProjection/VmcsFieldProjectionSchema.cs`.
- Для каждого frozen `VmcsField` объявлен field-level owner, evidence visibility class, read/write policy и generated-alias marker.
- Добавлен `Core/VMX/Conformance/GeneratedParity/VmcsFieldProjectionSchemaConformanceContract.cs`.
- Контракт проверяет canonical artifact, отсутствие дублей, наличие generated marker, наличие owner и покрытие всех frozen VMCS fields.

## Как проверено

Выполнена сборка основного проекта:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Результат сборки

Build succeeded, 54 warning(s), 0 error(s).
