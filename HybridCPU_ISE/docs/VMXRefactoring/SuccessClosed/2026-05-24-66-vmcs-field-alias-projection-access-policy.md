# VMCS Field Alias Projection Access Policy

Дата: 2026-05-24

## Правило / основание

- VMREAD/VMWRITE должны работать только через access policy.
- VMREAD/VMWRITE не должны открывать host-owned evidence, scheduler evidence, native tokens или backend binding evidence.
- VMCS fields должны быть generated/declared alias map over descriptors; unsupported fields должны fail closed.
- VMCS/VMCSv2 остаётся frozen compatibility projection, а не substrate authority.

## Что изменено

- `Core/VMX/Compatibility/Generated/VmcsProjection/VmcsFieldAliasProjection.cs` больше не является пустой заготовкой.
- Добавлены `VmcsFieldAliasAccess`, `VmcsFieldAliasDecision`, `VmcsFieldAliasRequest` и `VmcsFieldAliasResult`.
- Добавлена проверка `ValidateAccess(...)`, которая:
  - отклоняет undefined `VmcsField`;
  - требует declared generated alias;
  - требует descriptor validation;
  - отклоняет host-owned evidence;
  - проверяет `EvidencePolicyDescriptor.CanExposeToGuest(...)`;
  - отдельно gate-ит write access через `AllowWrite`.

## Как проверено

Команда:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Результат сборки

Сборка основного проекта успешно завершена. Ошибок: 0. Предупреждения: существующие предупреждения проекта.
