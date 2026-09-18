# 92. Nested memory domain composer admission

Дата: 2026-05-24

## Правило / основание

- Nested model должен двигаться к domain composition, а не к публичной архитектуре VMCS12/VMCS02.
- Memory translation authority должен принадлежать memory-domain descriptors и runtime validation.
- Nested memory composition требует явного capability grant и валидных child/host translation controls.
- Compatibility projection разрешается только после descriptor-authorized composition.

## Что изменено

- Актуализирован `Core/VMX/Substrate/Nested/MemoryComposition/NestedMemoryDomainComposer.cs`.
- Добавлены `NestedMemoryDomainCompositionDecision`, request/result-модели и fail-closed validation surface.
- `Validate` / `CanCompose` теперь проверяют:
  - наличие nested descriptor;
  - runtime authority;
  - domain composition gates;
  - `NestedMemoryComposition` capability grant;
  - child/host memory descriptors;
  - ownership second-stage translation;
  - валидные translation controls;
  - валидный composition context;
  - разрешение compatibility projection.

## Как проверено

Команда:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
```

## Результат сборки

Сборка успешно завершена: 0 errors, 54 warnings.
