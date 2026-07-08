# Phase 01 — Inventory and Semantic Freeze

## Назначение

Зафиксировать текущее поведение `HybridCPU_Compiler/Core` как наблюдаемую систему, не переписывая его. Эта фаза нужна, чтобы дальнейший рефакторинг не подменил архитектурные смыслы удобными API-ярлыками.

## Основной принцип

Сначала описываем, что compiler уже делает и чего он не имеет права утверждать. Только после этого вводим новые публичные типы.

## Scope

Зоны для первичной инвентаризации:

- `Core/IR/Model` — какие IR-объекты считаются архитектурными, какие только строительными.
- `Core/IR/Construction` — где создаются bundle/carrier/slot представления.
- `Core/IR/Bundling` — где принимаются структурные решения о bundle layout.
- `Core/IR/Scheduling` — где возникают scheduling assumptions.
- `Core/IR/Hazards` — где вычисляются hazard summaries и resource expectations.
- `Core/IR/Admission` — где compiler пытается предварительно оценить admissibility.
- `Core/IR/Analysis` — где создаются derived facts.
- `Core/IR/Telemetry` — где формируется наблюдаемость.
- `Core/Support` — общие helpers, которые могут скрывать authority boundaries.

## Tasks

### 1. Составить карту текущих API-точек

Для каждого public/internal типа зафиксировать:

- имя типа;
- namespace;
- входы;
- выходы;
- создает ли carrier bytes;
- создает ли sideband;
- создает ли descriptor;
- создает ли typed-slot facts;
- возвращает ли boolean success;
- может ли boolean success быть ошибочно понят как runtime legality;
- может ли результат быть ошибочно понят как execution evidence.

### 2. Классифицировать результаты функций

Каждый метод должен быть отнесен к одному из классов:

- `StructuralObservation` — наблюдение о форме IR/bundle.
- `TransportConstruction` — создание переносимого carrier/sideband/descriptor.
- `CompilerEvidence` — доказательство компиляторной проверки.
- `RuntimeBridgeInput` — пакет для передачи runtime.
- `RuntimeAuthorityRequired` — результат неполон без runtime `LegalityDecision`.
- `ParserOnly` — распознавание без права исполнения.
- `HelperOnly` — scoped helper ABI без production lowering.
- `NoEmission` — намеренное отсутствие emission.
- `FutureGated` — будущая возможность, запрещенная сейчас.

### 3. Зафиксировать опасные двусмысленности

Искать имена и возвращаемые значения вроде:

- `IsLegal` без указания compiler/runtime authority;
- `CanExecute` в compiler layer;
- `ValidDescriptor` без класса `ParserOnly`/`TransportOnly`;
- `Success` без `DecisionKind`;
- `Emit` без `EmissionClass`;
- `Lower` без `NoFallbackPolicy`;
- `Capability` без источника authority.

### 4. Ввести временный документ `CURRENT_BEHAVIOR.md`

До изменения кода добавить документ с перечнем текущих возможностей:

- что реально emits carrier;
- что только сохраняет sideband;
- что только строит descriptor;
- что только проверяет typed-slot facts;
- что только парсит;
- что является helper-only;
- что future-gated;
- где production lowering явно отсутствует.

## Deliverables

- `CURRENT_BEHAVIOR.md` в `Core/RefactorPlan` или соседнем каталоге `Core/Docs`.
- Таблица текущих public/internal API-точек.
- Перечень методов, требующих переименования.
- Перечень методов, требующих замены boolean-return на typed decision.
- Перечень мест, где helper/parser success выглядит как production/runtime success.

## Acceptance criteria

Фаза считается завершенной, когда для каждого значимого метода `Core` можно ответить:

1. Что он создает?
2. Какой authority source у результата?
3. Может ли результат быть исполнен без runtime?
4. Может ли результат публиковать память/register state?
5. Является ли это production lowering?

Если хотя бы один ответ неочевиден, код еще не готов к фазе 2.

## Non-goals

- Не менять lowering behavior.
- Не переносить файлы.
- Не вводить новые execution contours.
- Не исправлять scheduling heuristics.
- Не добавлять production backend paths.

## Риски

Главный риск этой фазы — слишком рано начать рефакторинг имен и классов. Нужно сначала получить карту смыслов. Любое переименование без semantic inventory может скрыть нарушение модели `carrier != execution != publication != authority != commit != retire != evidence != production lowering`.