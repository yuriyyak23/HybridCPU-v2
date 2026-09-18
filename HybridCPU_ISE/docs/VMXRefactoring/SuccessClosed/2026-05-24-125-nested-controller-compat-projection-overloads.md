# Nested controller compatibility projection overloads

Дата: 2026-05-24

## Правило / основание

- `Оценка рефакторинга VMX.md`: nested model должен двигаться к domain composition, а VMCS12/ShadowVMCS vocabulary должен оставаться compatibility projection vocabulary.
- `deep-research-report (6).md`: substrate-controller не должен напрямую создавать VMCS/ShadowVMCS bridge; VMX/VMCS остаются frontend/projection layer.

## Что изменено

- `NestedDomainController` сделан `partial`.
- Из substrate-файла `Core/VMX/Substrate/Nested/Policies/NestedDomainController.cs` вынесены overloads, принимающие `VmcsV2Descriptor`.
- Compatibility overloads перенесены в `Core/VMX/Compatibility/Generated/VmcsProjection/NestedDomainControllerCompatibilityProjection.cs`.
- Substrate-facing path теперь явно остаётся на `NestedDomainDescriptor + INestedProjectionService`.

## Как проверено

Команда:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Результат сборки

Сборка основного проекта успешно завершена: `54 Warning(s)`, `0 Error(s)`.
