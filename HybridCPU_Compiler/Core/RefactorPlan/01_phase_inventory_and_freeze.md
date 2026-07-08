# Phase 01 — Inventory and Semantic Freeze

## Назначение

Зафиксировать текущее поведение `HybridCPU_Compiler/Core` как наблюдаемую систему, не переписывая его. Эта фаза нужна, чтобы дальнейший рефакторинг не подменил архитектурные смыслы удобными API-ярлыками.

## Основной принцип

Сначала описываем, что compiler уже делает и чего он не имеет права утверждать. Только после этого вводим новые публичные типы.

Эта фаза является semantic freeze, а не cosmetic rename. Запрещено начинать перенос поведения в новые классы, пока не зафиксировано, какие текущие APIs уже создают carrier, sideband, descriptor, typed-slot facts, agreement, evidence или bridge input, а какие только возвращают legacy `bool`/`Try*`/`IsLegal`/`HasLegalAssignment`.

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

## Current Core anchors that must be inventoried

Аудит текущего `master` показал, что refactor plan не должен проектировать blank-slate слой: в Core уже есть aggregate artifacts, helper recovery paths, structural legality terms и runtime contract bridge vocabulary. Поэтому `CURRENT_BEHAVIOR.md` должен явно описать минимум следующие anchor areas.

### Artifact aggregate anchor

`HybridCpuCompiledProgram` должен быть описан как существующий aggregate, который уже совмещает:

- `IrProgramSchedule`;
- `IrProgramBundlingResult`;
- lowered `IReadOnlyList<VLIW_Bundle>`;
- serialized `ProgramImage`;
- `ContractVersion`;
- optional `EmissionBaseAddress`;
- `IrAdmissibilityAgreement`;
- `IReadOnlyList<VliwBundleAnnotations>`.

Inventory must state that this object is not wrong by itself, but it needs a compatibility adapter into separated envelopes in later phases. The adapter must not strengthen authority.

### Current lowerer anchor

`HybridCpuBundleLowerer` must be inventoried as the current carrier/sideband/facts production surface:

- `LowerProgram` / `LowerBlock` / `LowerBundle` produce backend-facing `VLIW_Bundle` carrier structures.
- `EmitAnnotationsForProgram` / `EmitAnnotationsForBundle` produce sideband annotations.
- `EmitFactsForBundle` produces typed-slot facts.
- `LowerInstruction` preserves MatrixTile/vector encoded helper instructions.
- Descriptor sideband is copied into `InstructionSlotMetadata` for lane6/lane7 paths.

### Current construction/recovery anchor

`HybridCpuIrBuilder` must be inventoried as the current IR construction and helper recovery surface:

- `BuildProgram` and `BuildInstruction` reconstruct IR from encoded instructions and optional sideband.
- `CompilerMatrixTileEmissionLowerer.TryRecoverFromInstruction` is used as helper/ABI recovery evidence.
- `CompilerVectorTransferEmissionLowerer.TryRecoverFromInstruction` is used as helper/transport recovery evidence.
- `ValidateExplicitAcceleratorIntent` rejects lane6/lane7 descriptor misuse and keeps L7 descriptorless submit fail-closed.

### Current structural legality anchor

`HybridCpuInstructionLegalityChecker`, `HybridCpuSlotModel`, `HybridCpuBundleFormer` and related model types must be inventoried with special care because they currently use names such as:

```text
Legality
IsLegal
LegalSlots
HasLegalAssignment
EvaluateCandidateBundle
EvaluateClusterPreparedLegality
TryMaterialize*
TryBundleProgramGlobally
```

Inventory must classify these as compiler structural admission / structural placement / scheduling search terminology, not runtime Legality A|B.

### Current runtime contract anchor

`CompilerContract` and typed-slot policy vocabulary in runtime/ISE must be listed as a runtime-owned boundary that compiler may observe/reference but not own. The compiler refactor must not duplicate runtime policy authority.

## Mandatory API inventory list

`CURRENT_BEHAVIOR.md` must include a row for every symbol below. The list is a lower bound, not exhaustive.

```text
HybridCpuCompiledProgram.EmitVliwBundleImage
HybridCpuCompiledProgram.ValidateRuntimeContractCompatibility
HybridCpuBundleLowerer.LowerProgram
HybridCpuBundleLowerer.LowerBlock
HybridCpuBundleLowerer.LowerBundle
HybridCpuBundleLowerer.EmitAnnotationsForProgram
HybridCpuBundleLowerer.EmitAnnotationsForBundle
HybridCpuBundleLowerer.EmitFactsForBundle
HybridCpuIrBuilder.BuildProgram
HybridCpuIrBuilder.BuildInstruction
HybridCpuIrBuilder.ValidateExplicitAcceleratorIntent
CompilerMatrixTileEmissionLowerer.TryRecoverFromInstruction
CompilerVectorTransferEmissionLowerer.TryRecoverFromInstruction
HybridCpuInstructionLegalityChecker.AnalyzeCandidateBundle
HybridCpuInstructionLegalityChecker.EvaluateCandidateBundle
HybridCpuInstructionLegalityChecker.EvaluateClusterPreparedLegality
HybridCpuBundleFormer.BundleProgram
HybridCpuBundleFormer.BundleBlock
HybridCpuBundleFormer.TryBundleProgramGlobally
HybridCpuBundleFormer.TryMaterializeBlockGlobalLookahead
HybridCpuBundleFormer.TryMaterializeAdjacentBundleTripletLookahead
HybridCpuBundleFormer.TryMaterializeAdjacentBundlePair
HybridCpuSlotModel.SearchAssignments
HybridCpuSlotModel.SearchProgramAssignments
HybridCpuSlotModel.SearchGlobalBasicBlockAssignments
HybridCpuHazardModel.GetExecutionProfile
TelemetryProfileReader
CompilerContract.ThrowIfVersionMismatch
CompilerTypedSlotPolicyMode
CompilerTypedSlotIngressAction
```

## Required inventory columns

For each row in `CURRENT_BEHAVIOR.md`, include:

```text
Symbol
File path
Namespace
Current return shape
Current success/failure shape
Creates carrier bytes/objects?
Creates sideband?
Creates descriptor?
Creates typed-slot facts?
Creates structural agreement?
Creates evidence/telemetry?
Mentions legality/legal/valid/success/accepted?
Could caller misread it as runtime legality?
Could caller misread it as execution/publication/commit/retire?
Current fallback behavior
Same-contour structural fallback?
Cross-contour fallback?
Required replacement/wrapper type
Required negative test id
Notes
```

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
- `StructuralAdmissionEvidence` — compiler-side structural admissibility, not runtime legality.
- `StructuralPlacementEvidence` — slot/resource placement search result, not runtime legality.
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
- `LegalSlots` без structural-only qualifier;
- `HasLegalAssignment` без structural placement qualifier;
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

### 5. Разделить fallback inventory

Отдельно описать:

- same-contour structural placement fallback, например global placement search -> local materialization внутри bundler;
- forbidden cross-contour fallback, например MatrixTile -> scalar, L7 -> DSC, DSC -> scalar, Stream -> scalar.

Same-contour structural fallback may be allowed later only if it preserves intent, contour, emission class, sideband requirement and authority class. Cross-contour fallback must be empty unless a later phase introduces an explicit and reviewed `FallbackPolicy`.

## Deliverables

- `CURRENT_BEHAVIOR.md` в `Core/RefactorPlan` или соседнем каталоге `Core/Docs`.
- Таблица текущих public/internal API-точек с mandatory columns.
- Перечень методов, требующих переименования.
- Перечень методов, требующих замены boolean-return на typed decision.
- Перечень мест, где helper/parser success выглядит как production/runtime success.
- Перечень current artifact aggregate fields и compatibility adapter requirements.
- Перечень same-contour structural fallbacks и cross-contour fallback prohibitions.

## Acceptance criteria

Фаза считается завершенной, когда для каждого значимого метода `Core` можно ответить:

1. Что он создает?
2. Какой authority source у результата?
3. Может ли результат быть исполнен без runtime?
4. Может ли результат публиковать память/register state?
5. Является ли это production lowering?
6. Является ли успех structural placement/admission, parser/helper recognition, ABI validation, bridge ingress compatibility или runtime legality?
7. Есть ли fallback, и является ли он same-contour structural или forbidden cross-contour?

Если хотя бы один ответ неочевиден, код еще не готов к фазе 2.

## Non-goals

- Не менять lowering behavior.
- Не переносить файлы.
- Не вводить новые execution contours.
- Не исправлять scheduling heuristics.
- Не добавлять production backend paths.

## Риски

Главный риск этой фазы — слишком рано начать рефакторинг имен и классов. Нужно сначала получить карту смыслов. Любое переименование без semantic inventory может скрыть нарушение модели `carrier != execution != publication != authority != commit != retire != evidence != production lowering`.

Дополнительный риск — не заметить уже существующие `Legal*`/`Try*`/`HasLegalAssignment` semantics и затем построить новые records поверх старой ambiguity. Поэтому фаза 1 обязана завершиться не списком каталогов, а machine-reviewable таблицей конкретных symbols.
