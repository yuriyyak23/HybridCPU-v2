# Отчёт об исправленной проблеме сборки

**Проект:** `C:\Users\Yuriy Kurnosov\Desktop\HybridCPU v2\`  
**Каталог отчёта:** `Documentation`  
**Дата:** 2026-04-25

## 1. Суть проблемы

После `clean.bat` в копии проекта воспроизводился каскад ошибок сборки (вплоть до тысяч), вызванный не дефектами C#-кода, а инфраструктурой MSBuild для ссылки на `HybridCPU_ISE`.

Первичные симптомы:
- `CS0006` (не найдена зависимая сборка/метаданные)
- `NETSDK1004` (проблема с `project.assets.json`)

Вторичные каскадные симптомы:
- `CS0234`
- `CS0246`

Подтверждённый первичный механизм сбоя:
- потребители ожидали `HybridCPU_ISE.dll` по пути `bin\Debug\testsupport\HybridCPU_ISE.dll`;
- фактический вывод `HybridCPU_ISE` шёл в `bin\Debug\testsupport\net10.0\HybridCPU_ISE.dll`;
- из-за несовпадения пути возникал `CS0006`, затем запускался каскад производных ошибок.

## 2. Подтверждённая целевая конфигурация

Проверены проекты, где есть `ProjectReference` на `HybridCPU_ISE`:
- `HybridCPU_ISE.Tests`
- `TestAssemblerConsoleApps`
- `HybridCPU_Compiler`
- `CpuInterfaceBridge`

Подтверждено соответствие шаблону test-support:
- нет фиксированного `Configuration=Debug` в `AdditionalProperties`
- используется `OutputPath=bin\$(Configuration)\testsupport\`
- используется `BaseIntermediateOutputPath=obj\testsupport\`
- используется `MSBuildProjectExtensionsPath=obj\` (для корректного `project.assets.json`)
- отсутствуют абсолютные пути в этой конфигурации

Дополнительно зафиксировано в конфигурации:
- в `Directory.Build.props` задан централизованный шаблон `HybridCpuIseProjectReferenceProperties`;
- в Debug включён дефолт `DefineTestSupport=true` (если не задан явно);
- в `HybridCPU_ISE.csproj` для test-support режима задано:
  - `OutputPath=bin\$(Configuration)\testsupport\`
  - `AppendTargetFrameworkToOutputPath=false`
  (чтобы путь вывода соответствовал ожидаемому `ProjectReference`).

## 3. Валидация результата

Выполнены проверки сборки:
1. Повторное воспроизведение сценария: `clean.bat`.
2. Полная сборка workspace после clean — успешно.
3. Изолированные проверки зависимых проектов — успешно (с предупреждениями).

Итог:
- блокирующие инфраструктурные ошибки `CS0006/NETSDK1004` после `clean.bat` не воспроизводятся;
- каскадные ошибки типа `CS0234/CS0246`, связанные с отсутствующей зависимостью, устранены.

## 4. Что изменялось

Изменения C#-кода не выполнялись.  
Проблема классифицирована как конфигурационная (связка `ProjectReference`/MSBuild-свойств и фактического `OutputPath`).

Выполненные правки:
1. `Directory.Build.props`
   - сохранён единый шаблон `HybridCpuIseProjectReferenceProperties` для `ProjectReference` на `HybridCPU_ISE`;
   - зафиксирован Debug-дефолт `DefineTestSupport=true` (при пустом значении).
2. `HybridCPU_ISE.csproj`
   - для режима `EnableInternalTestHooks=true` задан `OutputPath=bin\$(Configuration)\testsupport\`;
   - добавлен `AppendTargetFrameworkToOutputPath=false`.

## 5. Рекомендации

Для предотвращения регрессий:
1. Использовать единый шаблон `AdditionalProperties` для всех ссылок на `HybridCPU_ISE`.
2. Сохранять `MSBuildProjectExtensionsPath=obj\` при `BaseIntermediateOutputPath=obj\testsupport\`.
3. Для test-support выхода `HybridCPU_ISE` сохранять согласованный путь без TFM-подкаталога (через `AppendTargetFrameworkToOutputPath=false`).
4. В CI применять подход first-failure: сначала устранять `CS0006/NETSDK1004`, затем анализировать остальные ошибки.
