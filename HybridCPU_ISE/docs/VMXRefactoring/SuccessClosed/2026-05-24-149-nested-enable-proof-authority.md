# Nested enablement proof authority

Дата: 2026-05-24

## Закрытая задача

Закрыт пункт `25` из `docs/VMXRefactoring/audit.md`: nested gates оставались только enum masks.

## Правило / основание

- Nested virtualization должна быть domain composition.
- Nested enablement должен ссылаться на typed capability grants, evidence policies, memory composition descriptors and completion route policies.
- Enum masks допустимы только как compact compatibility input, не как финальная authority.

## Что изменено

- Добавлены `NestedCapabilityGrantDescriptor`, `NestedEnablementGateDescriptor` и `NestedEnablementProof`.
- `NestedEnablementRequest.HasRequiredNestedCapabilities` и `HasRequiredGates` теперь идут через `EnablementProof`.
- Добавлен `NestedEnablementProofAuthorityContract`.

## Как проверено

Выполнена сборка основного проекта после изменения:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Результат сборки

Build succeeded, 54 warning(s), 0 error(s).
