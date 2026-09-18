# Nested substrate generic validation result

Дата: 2026-05-24

## Правило / основание

- VMX/VMCS должны оставаться frozen compatibility frontend и generated/read-only projection.
- Substrate-слой не должен возвращать VMCS-owned validation vocabulary.
- Nested model должен двигаться к domain composition и nested projection service, а не к публичной архитектуре VMCS12/ShadowVMCS.

## Что изменено

- Введен generic nested validation result: `NestedValidationResult` / `NestedValidationCode`.
- `INestedProjectionService` и `NestedDomainController` переведены с `VmcsV2ValidationResult` на `NestedValidationResult`.
- `NestedDomainController` больше не использует `VmcsV2ValidationCode` и `VmcsV2BlockDirectory.ShadowVmcsBlockFieldId` в substrate path.
- Compatibility projection (`NestedDomainControllerCompatibilityProjection`) оставляет VMCS validation mapping только на VMCS-facing boundary.

## Как проверено

```text
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Результат сборки

Build succeeded. 54 Warning(s), 0 Error(s).
