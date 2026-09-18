# Memory domain runtime admission

Дата: 2026-05-24

## Правило / основание

- VMX не владеет memory state.
- Memory virtualization принадлежит `MemoryDomainDescriptor`.
- INVEPT/INVVPID/NPT/EPT-like compatibility paths должны быть aliases over generic memory-domain translation authority.

## Что изменено

- Актуализирован `Core/VMX/Substrate/Domains/Memory/MemoryDomainRuntime.cs`.
- Добавлены `MemoryDomainRuntimeDecision`, `MemoryDomainRuntimeRequest` и `MemoryDomainRuntimeResult`.
- `MemoryDomainRuntime` теперь валидирует наличие memory descriptor, descriptor authority, address-space descriptor, translation policy, translation control, second-stage translation authority и dirty-tracking descriptor.
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
