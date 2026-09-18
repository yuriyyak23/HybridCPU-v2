# Shadow VMCS bridge retirement contract

Дата: 2026-05-24

## Закрытая задача

Закрыт пункт `17` из `docs/VMXRefactoring/audit.md`: `ShadowVmcsNestedProjectionService` мог закрепиться как разрешённая legacy-дыра.

## Правило / основание

- Nested mode должен двигаться к `NestedDomainDescriptor` / `NestedProjectionService`, а не к VMCS12/ShadowVMCS authority.
- Legacy names допустимы только в compatibility projection.
- VMCS-backed bridge должен быть fenced compatibility-only и не иметь substrate/migration authority.

## Что изменено

- Добавлен `Core/VMX/Conformance/NestedComposition/ShadowVmcsBridgeRetirementContract.cs`.
- Контракт проверяет allowed generated projection path, generated fence, отсутствие substrate reachability, запрет migration serialization и наличие replacement `NestedProjectionService` boundary.
- `ShadowVmcsNestedProjectionService` получил явный `CompatibilityBridgePath` и `IsRetirementFenced`.

## Как проверено

Выполнена сборка основного проекта после изменения:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Результат сборки

Build succeeded, 54 warning(s), 0 error(s).
