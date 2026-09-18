# Neutral runtime boundary admission service

Дата: 2026-05-24

Статус: closed

## Основание

- `ОСНОВЫ и ПРАВИЛА VMX.md`: источником истины является generic domain/descriptor/capability runtime substrate, а не VMX/VMCS/VMX CSR.
- `audit.md`: пункт 2/30 фиксирует риск, что generic substrate физически остается под `Core/VMX/Substrate`.
- `2026-05-24-vmx-current-model-completion-audit.md`: recommended next task требует staged extraction of neutral runtime substrate from `Core/VMX/Substrate`.

## Что изменено

- Созданы нейтральные runtime папки:
  - `Core/Runtime/Domains`
  - `Core/Runtime/Capabilities`
  - `Core/Runtime/Evidence`
  - `Core/Runtime/Services`
- Добавлены файлы с кратким `Description:` в начале каждого файла:
  - `Core/Runtime/Domains/DomainBoundaryDescriptor.cs`
  - `Core/Runtime/Capabilities/CapabilityBoundaryRequirement.cs`
  - `Core/Runtime/Evidence/EvidenceBoundaryRequirement.cs`
  - `Core/Runtime/Services/RuntimeBoundaryAdmissionService.cs`
- Выбранная кодовая задача: `RuntimeBoundaryAdmissionService`.
- Сервис fail-closed соединяет:
  - execution/memory/io domain descriptor boundary;
  - typed capability grant requirement;
  - evidence policy visibility/migration checks;
  - root runtime authority;
  - запрет direct authoritative mutation from compatibility frontend.
- Добавлены conformance-тесты `RuntimeBoundaryAdmissionTests`.

## Проверка

- `dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore`
- `dotnet build "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-restore`
- `dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-build --filter "FullyQualifiedName~RuntimeBoundaryAdmissionTests"`

## Результат

- Основной проект: succeeded, 0 errors.
- Тестовый проект: succeeded, 0 warnings, 0 errors.
- `RuntimeBoundaryAdmissionTests`: Passed 4/4.

## Остаточный риск

Это первый нейтральный runtime-boundary слой. Большая часть старого generic substrate все еще физически находится под `Core/VMX/Substrate`; полный вынос должен идти отдельными малыми шагами.
