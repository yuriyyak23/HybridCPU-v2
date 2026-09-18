# Vector-stream extension descriptor authority

Дата: 2026-05-24

## Правило / основание

- `ОСНОВЫ и ПРАВИЛА VMX.md`: VMX не владеет Lane6/Lane7/vector-stream state; lane execution and extension state belong to descriptors/runtime namespaces.
- `deep-research-report (6).md`: `VectorStreamStateBlock` maps to execution extension/vector-stream descriptors, not VMCS authority.

## Что изменено

- `ExecutionExtensionDescriptor` больше не пустая заготовка: добавлен runtime-owned authority, vector-stream descriptor binding, epoch и compatibility projection gate.
- `VectorStreamExecutionExtensionDescriptor` больше не пустая заготовка: добавлен runtime-owned authority, enable flag, allowed save mask, max vector length, stream table bounds and replay epoch.
- Save/restore policy по умолчанию deny-by-default.

## Как проверено

Запущено:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Результат сборки

Успешно: `Build succeeded`, 0 errors. Остались существующие предупреждения проекта.
