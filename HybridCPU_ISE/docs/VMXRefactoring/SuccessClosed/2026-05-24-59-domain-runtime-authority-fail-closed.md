# Domain Runtime Authority Fail-Closed Boundary

Дата: 2026-05-24

## Правило / основание

- VMX frontend не должен быть источником полномочий выполнения.
- Активация compatibility frontend, проекции и мутации authoritative state должны проходить через runtime/domain descriptor authority.
- Capability grants должны проверяться поверх generic runtime context/root authority, а не через VMX-owned CSR state.
- Неявное разрешение на runtime operation должно fail-closed.

## Что изменено

- `Core/VMX/Substrate/Runtime/Authority/DomainRuntimeAuthority.cs` больше не является пустой заготовкой.
- Добавлена fail-closed модель результата `DomainRuntimeAuthorityResult` и причин отказа `DomainRuntimeAuthorityDecision`.
- Добавлена проверка `Validate(...)` поверх:
  - `RootAuthorityDescriptor`;
  - `DomainRuntimeContext`;
  - `DomainRuntimeOperation`;
  - effective capability mask контекста;
  - root granted capability mask;
  - gate активации compatibility frontend;
  - gate read-only projection;
  - gate мутации authoritative runtime state.

## Как проверено

Команда:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Результат сборки

Сборка основного проекта успешно завершена. Ошибок: 0. Предупреждения: существующие предупреждения проекта.
