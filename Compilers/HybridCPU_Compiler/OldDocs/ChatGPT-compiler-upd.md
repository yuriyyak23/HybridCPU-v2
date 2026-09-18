
Но перед реализацией я бы внёс **5 существенных корректировок**, две из них считаю обязательными для корректного modulo scheduling.

## Сводная проверка

| Моя рекомендация | RefPlan3 | Вердикт |
|---|---|---|
| Единый static machine model | Phase 01 + 03 | ✅ Полное соответствие |
| Не дублировать packer | Phase 02 использует `SearchStructuralAssignments` | ✅ |
| Cycle membership и placement должны рассматриваться совместно | Phase 02 — да; Phase 14 — lazy decomposition | ⚠️ Усилить contract |
| First-class scheduling-region layer | Phase 04 | ✅ |
| Loop IR / natural loops | Phase 13 | ✅ |
| `IterationDistance` для loop-carried dependencies | `IrLoopDependenceEdge(latency,distance,...)` | ✅ |
| RecMII/resource MII | Phase 13 | ✅ |
| Floyd–Warshall upper bound числа pipeline stages | отсутствует | ❌ Добавить |
| Modulo scheduler отдельной strategy, не замена всего scheduler | Phase 14 + deterministic fallback | ✅ |
| Solver не должен владеть runtime legality | глобальные invariants + Phase 14 | ✅ Очень хорошо |
| SafetyVerifier остаётся final authority | README, 01, 03, 14, 21–22 | ✅ |
| Compiler WAR/WAW сначала сохранять | Phase 05/06/13 | ✅ |
| Rename-aware SWP только после отдельного proof | Phase 06 | ✅, но есть MVE-gap |
| Prolog/kernel/epilog | Phase 14 | ⚠️ Недостаточно разделены solve/CFG-transform |
| Solver backend abstraction | Phase 12 | ✅ для oracle; неоднозначно для Phase 14 |
| Deterministic solver | README + 12/14 | ✅ Отлично |
| Wall-clock не должен влиять на codegen | глобальный invariant | ✅ Отлично |
| UNSAT/conflict diagnostics | Phase 12 + cuts Phase 14 | ⚠️ Усилить Phase 14 |
| FSP как secondary objective, не correctness | Phase 18 | ✅ Отлично |
| FSP не смешивать с VDSA | 18–23 | ✅ Отлично |
| VDSA compiler intent advisory only | Phase 23 | ✅ |
| Profile влияет только на profitability | повсеместно | ✅ |
| Runtime FSP/Safety state не сериализовать из compiler | 19–22 | ✅ |

---

# 1. Самая важная недостающая часть: Floyd–Warshall stage bound

Это единственное прямое расхождение с моей предыдущей рекомендацией по paper.

Phase 13 сейчас строит:

```text
natural loop
  ↓
distance dependence graph
  ↓
RecMII
SlotMII
PortMII
RegGroupMII
MemMII
  ↓
ChosenII
```

Это правильно.

Но в paper после выбора конкретного `II` используется ещё одна важная операция: определяется **конечная верхняя граница schedule horizon / количества pipeline stages**.

Для зависимости:

$$
u \rightarrow v,\quad latency=l,\quad distance=d
$$

строится reverse constraint edge примерно вида:

$$
v\rightarrow u,\quad weight=d\cdot II-l
$$

после чего all-pairs shortest paths дают bound на расстояние операций и, следовательно, на число stages.

В `RefPlan3` я не нашёл ни `Floyd`, ни `Warshall`, ни отдельного `StageUpperBound`.

Сейчас Phase 14 говорит:

```text
SDC temporal candidate
→ lazy resource cuts
→ exact W=8 kernel placement
```

но математический horizon расписания явно не сформулирован.

### Что добавить

В Phase 13 или начале Phase 14:

```text
HybridCpuModuloStageBoundAnalyzer

ModuloStageBoundReport
    InitiationInterval
    MaxOperationSeparation
    MaxStageCount
    Witness
    BoundKind
```

и алгоритм:

```text
distance DAG
   +
candidate II
   ↓
difference-constraint graph
   ↓
Floyd-Warshall / equivalent APSP
   ↓
maximum legal operation separation
   ↓
MaxStageCount(II)
```

Я бы предпочёл добавить это именно в Phase 13 как **analysis-only primitive**, примерно рядом с MII:

```text
MII          = lower bound on II
StageBound   = upper bound on horizon for selected II
```

Это две разные величины.

Это **обязательная правка**, если цель — содержательно интегрировать алгоритм paper, а не только общий принцип constraint scheduling.

---

# 2. Phase 14 нужно сильнее зафиксировать как joint cycle + lane scheduling

Phase 02 сделан очень правильно.

Там есть:

```text
ready nodes
 → alternative membership
 → exact SearchStructuralAssignments
 → placement witness
 → winning membership/witness
 → BundleFormer materializes same membership
```

Это именно тот structural refactoring, который я рекомендовал.

В Phase 14 формулировка чуть слабее:

```text
SDC temporal candidate
 → lazy discrete resource validation
 → conflict cut
 → exact W=8 kernel placement
```

и backlog:

> `Reuse exact slot assignment for each kernel cycle.`

Такая decomposition **может быть полностью корректным joint solver**, но только при одном условии:

> failure exact placement должен становиться constraint/nogood для temporal solve, а успешный placement должен возвращаться как часть окончательной модели.

Иначе получится:

```text
solver chooses Cycle(op)
        ↓
later packer tries Slot(op)
```

что как раз является архитектурой, от которой я рекомендовал уйти.

### Я бы изменил contract Phase 14 на

```text
IrModuloSchedule
    II
    StageCount

    Operations[]
        InstructionId
        ModuloCycle
        Stage
        PhysicalSlot / exact placement witness

    KernelCycles[]
        Membership
        PlacementWitness
```

И обязательно:

```text
Modulo scheduler produces:
    membership + exact placement witness

BundleFormer:
    verifies/materializes prescribed witness

BundleFormer must NOT:
    search for a semantically different placement
    move an op to another cycle
    repair infeasible modulo membership
```

То есть перенести сильный invariant из Phase 02 также в Phase 14.

### Почему это особенно важно для HybridCPU

У вас topology не является просто:

```text
8 identical slots
```

а:

```text
ALU            lanes 0..3
LSU            lanes 4..5
DMA/MTILE      lane 6 shared
Branch/System  lane 7 shared
hard-pinned
class-flexible
```

Поэтому:

```text
temporal SAT
```

ещё не означает:

```text
HybridCPU schedule SAT
```

пока не существует exact W=8 witness.

---

# 3. Есть потенциальный correctness gap: modulo variable expansion / register rotation

Это второй действительно серьёзный вопрос.

Phase 06 правильно запрещает преждевременно удалять WAR/WAW:

```text
No WAR/WAW removal
until compiler-local allocation proof
```

Phase 13 тоже правильно говорит:

> preserve anti/output edges unless optional Phase 06 proves safe local allocation and rename.

Это полностью соответствует моей рекомендации.

Но затем Phase 14 говорит:

> Construct and validate prologue/epilogue, live-range rotation requirements and carried values.

Проблема: **`live-range rotation requirements` пока только упомянуты, но architecture mechanism для них не определён.**

Phase 06 — не полноценное modulo variable expansion. Она специально ограничена:

```text
compiler-created temporaries only
BB-only initially
no general SSA
no global RA
no ABI-visible renaming
```

Для software pipelining этого может оказаться недостаточно.

Представим значение:

```text
r5(i)
```

живущее более одного `II`.

После pipelining одновременно оказываются живыми:

```text
r5(i)
r5(i+1)
r5(i+2)
```

Классический SWP должен либо иметь:

```text
rotating registers
```

либо:

```text
modulo variable expansion
```

например:

```text
r5_0
r5_1
r5_2
```

Runtime rename сам по себе не должен автоматически считаться доказательством корректности такого преобразования.

### Для первой версии я рекомендую не решать всё сразу

Добавить в Phase 14 строгую eligibility gate:

```text
ModuloValueVersioningRequirement
```

с состояниями:

```text
NoneRequired
StaticallyAllocatable
Unsupported
```

И первая production версия принимает только:

```text
NoneRequired
```

или, позже:

```text
StaticallyAllocatable
```

при наличии отдельного allocation proof.

То есть:

```text
if overlapping iteration lifetimes require
more architectural versions than proven available:
    reject modulo scheduling
    fallback to block schedule
```

Это существенно безопаснее.

Позднее можно ввести отдельный:

```text
HybridCpuModuloValueExpansionPlanner
```

который уже будет использовать infrastructure Phase 05/06.

---

# 4. Solve и CFG transformation лучше разделить явно

Сейчас Phase 14 одновременно охватывает:

```text
modulo solving
+
prologue/kernel/epilogue
```

Я бы сохранил это в одной release phase, но разделил на два компонента.

Например:

```text
HybridCpuModuloScheduler
```

возвращает только:

```text
IrModuloSchedule
```

Математический объект:

```text
II
Stages
op -> cycle
op -> stage
op -> slot
dependence witnesses
```

А затем:

```text
HybridCpuModuloLoopExpander
```

выполняет:

```text
IrCanonicalLoopView
+
IrModuloSchedule
        ↓
prologue blocks
kernel blocks
epilogue blocks
        ↓
new CFG
        ↓
rebuild dependence analysis
        ↓
rebuild resource analysis
        ↓
normal bundling/lowering/relocation
```

Это важное разделение.

Не стоит позволять constraint solver'у самому становиться владельцем:

```text
IrBasicBlock
ControlFlowGraph
branch relocation
label rewriting
```

Solver должен знать математику расписания, но не compiler CFG ABI.

Существующий relocation layer затем естественно исправит physical branch addresses.

---

# 5. Название `lazy-SMT` я бы поменял

В HybridCPU аббревиатура `SMT` уже имеет очень сильное архитектурное значение:

```text
Simultaneous Multithreading

MicroOpScheduler.SMT
SmtEligibility
SafetyVerifier.SmtLegality
...
```

А Phase 14 использует:

```text
lazy-SMT
```

в значении:

```text
Satisfiability Modulo Theories
```

Это почти гарантированная терминологическая авария.

В документации появятся фразы вида:

```text
SMT scheduling improves SMT scheduling
```

где первая `SMT` — solver, а вторая — virtual threads.

### Я бы назвал Phase 14

```text
Bounded Constraint Modulo Scheduling
```

или:

```text
Bounded SDC / Lazy-Constraint Modulo Scheduling
```

А solver backend:

```text
ConstraintSolverBackend
```

не:

```text
SmtScheduler
```

Моё прежнее предложение:

```text
HybridCpuConstraintModuloScheduler
```

для этого плана подходит практически идеально.

---

# Что в плане сделано особенно правильно
---

# Phase 12 Exact Oracle — правильное место для Z3

Я раньше рекомендовал solver abstraction.

В RefPlan3 она фактически появляется именно здесь:

```text
IExactScheduleOracle
+
solver backend isolated behind
optional test/research assembly/process boundary
```

Это хорошее архитектурное решение.

Я бы лишь добавил ещё один интерфейс ниже oracle:

```text
IConstraintSolverBackend
```

примерно:

```text
IExactScheduleOracle
       │
       ▼
ConstraintProblem
       │
       ▼
IConstraintSolverBackend
       ├── Z3
       ├── CVC5
       └── ExhaustiveTinyReference
```

Это позволит не привязывать schema oracle к Z3 AST.

---

# UNSAT diagnostics стоит перенести частично и в production modulo scheduler

Phase 12 уже содержит:

> Extract unsat/conflict explanations by resource.

Это хорошо.

Но Phase 14 сейчас главным образом говорит про:

```text
ModuloConflictCut
first deterministic conflict
```

Я бы ввёл общий identity:

```text
ModuloConstraintId
```

например:

```text
dep.raw.i17.i21.d1
resource.lsu.cycle2
lane6.alias.cycle0
hardpin.i31.lane7
serialization.i42
bank3.cycle1
```

Тогда независимо от того, используется ли Z3 или custom SDC, диагностический output будет:

```text
II=3 rejected

conflict:
  dep.raw.i17.i21.d1
  resource.lsu.modcycle1
  hardpin.i9.lane4
```

Это очень полезно для ISE/ISA design.

---

# Я бы немного изменил dependency graph фаз

Текущий paper path:

```text
03 + 04 + 05 + 08
       ↓
      13
       ↓
      14
```

Для полного RefPlan3 это нормально.

Но **Phase 05 не является фундаментальным prerequisite для самого Loop IR**.

Для:

```text
natural loop
distance DAG
RecMII
SlotMII
```

достаточно:

```text
CFG
dependencies
machine topology
memory-distance analysis
```

Liveness нужен главным образом для:

```text
pressure
value expansion
register requirements
```

Поэтому я бы split Phase 13:

```text
13A Loop IR + distance DAG
    depends 03,04,08

13B register/pressure-aware MII
    additionally depends 05
```

Тогда research-paper path становится короче:

```text
00
 ↓
01
 ↓
03
 ↓
04
 ↓
08
 ↓
13A
 ↓
13B optional enrichment
 ↓
14
```

А не ждёт весь register-pressure infrastructure до появления first-class loop representation.

Это скорее оптимизация плана, чем correctness requirement.

---


---

# Как бы я поправил Phase 14

Целевая архитектура после правок:

```text
IrCanonicalLoopView
+
IrLoopDependenceGraph
+
MachineResourceModel
+
ChosenII
       │
       ▼
ModuloStageBoundAnalyzer
    Floyd-Warshall
       │
       ├── MaxStageCount
       ▼
HybridCpuConstraintModuloScheduler
       │
       ├── Cycle(op)
       ├── Stage(op)
       ├── exact Slot(op) / placement witness
       ├── dependence constraints
       ├── alias-resource constraints
       └── deterministic conflict cuts
       │
       ▼
IrModuloSchedule
       │
       ▼
ModuloValueVersioningCheck
       │
   ┌───┴──────────┐
   │              │
 supported     unsupported
   │              │
   ▼              └──→ legacy schedule
HybridCpuModuloLoopExpander
       │
       ├── prologue
       ├── kernel
       └── epilogue
       │
       ▼
rebuild CFG/dependencies/resources
       │
       ▼
normal compiler structural validation
       │
       ▼
BundleFormer
  prescribed placement materialization
       │
       ▼
lowering / relocation / serialization
       │
       ▼
runtime
Stage A → SafetyVerifier → Stage B
```

Это практически полностью соединяет RefPlan3 с моей предыдущей рекомендацией.

---

## Итоговый вердикт

**План можно использовать как основу рефакторинга. Перестраивать его заново не нужно.**

Архитектурно самые сильные части — Phases **01–03, 13, 18–23** и глобальные authority/determinism invariants. Они совпадают с выводами предыдущего анализа.

Перед началом modulo implementation я бы внёс ровно такие обязательные изменения:

1. **Добавить Floyd–Warshall/equivalent `ModuloStageBoundAnalyzer(II)`** между MII и modulo solve.
2. **Зафиксировать Phase 14 как complete joint temporal+placement solution**: окончательный `IrModuloSchedule` обязан содержать exact placement witness.
3. **Добавить explicit `ModuloValueVersioningRequirement`** и в первой версии reject loops, которым нужна недоказанная register rotation/MVE.
4. **Разделить `ModuloScheduler` и `ModuloLoopExpander`**, чтобы solver не владел CFG transformation.
5. **Переименовать `lazy-SMT modulo scheduling`**, чтобы не конфликтовать с SMT=Simultaneous Multithreading, и добавить solver-neutral backend/constraint IDs.

После этих изменений соответствие моему рекомендованному architecture path будет практически полным. Самая важная техническая дыра сейчас — не SafetyVerifier и не FSP, а связка **`stage bound + modulo value versioning + prescribed joint placement`**.