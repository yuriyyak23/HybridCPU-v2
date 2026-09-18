# I/O domain runtime admission

Дата: 2026-05-24

## Правило / основание

- VMX не владеет I/O и DMA state.
- I/O, DMA и IOMMU authority принадлежат `IoDomainDescriptor`.
- VMX compatibility projection не должна становиться обходом DMA/IOMMU authority.

## Что изменено

- Актуализирован `Core/VMX/Substrate/Domains/IO/IoDomainRuntime.cs`.
- Добавлены `IoDomainRuntimeDecision`, `IoDomainRuntimeRequest` и `IoDomainRuntimeResult`.
- `IoDomainRuntime` теперь валидирует наличие I/O descriptor, descriptor authority, DMA authority, IOMMU authority, I/O virtualization block, DMA window descriptor и compatibility projection gate.
- Поведение VMX handlers и RTL-логика не изменялись.

## Как проверено

Команда:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Результат сборки

- Успешно.
- 0 errors.
- 54 warnings, существующие предупреждения проекта.
