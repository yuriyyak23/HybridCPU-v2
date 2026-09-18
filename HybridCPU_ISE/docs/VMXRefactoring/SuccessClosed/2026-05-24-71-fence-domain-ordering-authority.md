# Fence domain ordering authority

Дата: 2026-05-24

## Правило / основание

- VMX не владеет Lane6/Lane7 state; lane execution, queues, tokens, fences и completions принадлежат lane descriptors и runtime namespaces.
- `FenceDomain` должен владеть ordering semantics.
- Fence semantics должны покрывать CPU, DMA, IOMMU, Lane6, Lane7, memory visibility и completion publication.

## Что изменено

- Актуализирован `Core/VMX/Substrate/Lanes/Lane6/Fences/FenceDomain.cs`.
- Добавлены `FenceOrderingScope` и `FenceDomainAuthority`.
- `FenceDomain` теперь описывает runtime-authoritative fence domain, domain binding, ordering scope и compatibility projection boundary.
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
