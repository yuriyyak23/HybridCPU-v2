# Добавление инструкции в HybridCPU ISE

> **Статус документа:** repository workflow manual, не ISA specification и не архитектурная authority. При расхождении этого manual с Markdown paper решение принимает paper; ArchitectureAuthorityRefactorSummary задаёт migration/validation context, а исходный код показывает текущие integration seams. См. [модель authority](#2-модель-authority).

## 1. Назначение, scope и non-goals

Этот manual задаёт практический порядок добавления одной инструкции: от декларации статических ISA-фактов до canonical decode, compiler transport, runtime materialization/execution, replay, fault и retirement. Он предназначен для изменения, которое сохраняет bundle-first ingress, typed `W=8` topology, runtime legality и существующих владельцев архитектурного состояния [paper §3][paper-3] [paper §4][paper-4].

Manual применяется к:

- новой encoded ISA-инструкции;
- новому alias существующей инструкции;
- расширению operand/sideband contour существующего opcode;
- compatibility-only или test-only contour, если требуется доказать, что он **не** стал вторым ISA ingress.

Manual не:

- специфицирует новую семантику вместо paper;
- переоткрывает RF-12, закрытую на RF-12.12h, или объявляет следующую RF-фазу завершённой [ArchitectureAuthorityRefactorSummary overview][ArchitectureAuthorityRefactorSummary-overview] [RF-13 ledger][rf13-ledger];
- заменяет runtime legality compiler metadata;
- обещает universal typed-slot path, ROB, precise exceptions, coherence, полную memory model или backend-global rewind — эти claims прямо ограничены paper; PRF/rename/commit/free-list и их владельцы сохраняются [paper §10.2][paper-10] [ADR-002][adr-002];
- разрешает удаление compatibility/legacy surface по имени или по valid-input parity без closed-world caller proof [ArchitectureAuthorityRefactorSummary governance][ArchitectureAuthorityRefactorSummary-gov] [RF-13 ledger][rf13-ledger];
- предписывает сопутствующий core refactoring.

### Как пользоваться manual

1. Сначала классифицировать инструкцию по [decision tree](#3-decision-tree-по-семейству).
2. Заполнить change/evidence record из [раздела 10](#10-минимальный-шаблон-changeevidence-record).
3. Выполнить workflow строго по шагам 0–10. Неприменимый шаг пометить `N/A` с причиной, а не молча пропускать.
4. Пройти family-specific и общие gates из [test matrix](#8-test-matrix).
5. Не merge-ить изменение с неразрешённым `STOP/ADR`.

## 2. Модель authority

### 2.1 Иерархия

| Слой | Что ему разрешено утверждать | Текущий repository seam | Чем он **не** является |
|---|---|---|---|
| Architecture authority | Семантика, typed lanes, legality/ownership, replay, retirement, timed memory и допустимые claims | Markdown в [`ResearchPaper/section/md base/`][paper-dir] | Не evidence и не roadmap |
| Migration/governance | План, ledger, retention/removal decisions, risks, DoD и подтверждённые validation gates | [ArchitectureAuthorityRefactorSummary overview][ArchitectureAuthorityRefactorSummary-overview], [target architecture][target-authority], [governance][ArchitectureAuthorityRefactorSummary-gov], [RF-13 ledger][rf13-ledger] | Не самостоятельная ISA specification |
| Declaration authority | Единственная редактируемая декларация статических opcode-фактов | typed rows в [`OpcodeInfo.Registry.Data*.cs`][opcode-data] | Не runtime provider и не generated output |
| Versioned generated manifest/binding | Детерминированная проекция декларации для review и потребителей | [`hybridcpu-isa.lock.json`][isa-lock], [`GeneratedIsaShadowCatalog.g.cs`][generated-catalog], generator [`Program.cs`][isa-gen] | Не редактируется вручную |
| Decoder legality | Допустимость bytes/operands/encoding/extension/sidebands и canonical snapshot | [`VliwDecoderV4.cs`][decoder-facade], [`DeclarativeDecoderStages.cs`][decoder-stages] | Не execution permission и не live scheduler state |
| Compiler metadata | Parsing/lowering, hazards, annotations и versioned structural evidence | [`HybridCpuIrBuilder.cs`][ir-builder], [`HybridCpuBundleLowerer.cs`][bundle-lowerer], [`HybridCpuHazardModel.cs`][hazard-model] | Не runtime semantic authority |
| Runtime provider | Создание carrier/MicroOp, binding, execute/capture outcome | [`InstructionRegistry.Runtime.cs`][runtime-registry] и family-specific initializers/providers | Не источник статической ISA-классификации |
| Evidence | Проверяет claim и отрицательные границы | [`HybridCPU_ISE.Tests/Architecture/`][architecture-tests], `eng/validate.ps1`, ArchitectureAuthorityRefactorSummary ledger | Не может узаконить отсутствующую семантику |

Целевой контракт — одна versioned декларация, детерминированный manifest и generated-source-first static binding. Редактируется декларация; lock/generated files обновляет generator; runtime consumers не заводят параллельные handwritten static tables [target architecture][target-authority]. Контейнер `CoreRuntimeState` не становится универсальным `Execute/Commit/Publish/Rollback` owner: authority остаётся у scheduler, memory, replay, retire и family-specific владельцев [ADR-010][adr-010].

### 2.2 Важное ограничение текущей реализации

На текущем состоянии нельзя честно заявить завершённый generated-source-only static ISA. Generator читает `OpcodeRegistry.Opcodes`, но rich descriptor fields частично выводит как `legacy-*` defaults, а static policies собирает через `IsaV4Surface`; RF-13 ledger сохраняет handwritten readbacks/fallback classifiers и не даёт deletion proof [generator source][isa-gen] [RF-13 ledger][rf13-ledger]. Поэтому:

- прямыми полями текущего `OpcodeInfo` являются opcode, mnemonic, category, operand count, flags, latency, bandwidth, instruction class и serialization class [OpcodeInfo type][opcode-types];
- aliases, encoding form, operand schema, extension, slot/resource facts, provider/materializer/effect/latency-model должны быть сначала сформулированы в change record, но текущая schema может не позволять выразить их без отдельного архитектурного/schema решения [generator source][isa-gen];
- если новая инструкция не укладывается в существующую детерминированную проекцию, ставится `STOP/ADR`; запрещено «дополнить» generated file, compiler switch или runtime registry и объявить это authority.

### 2.3 Запрещённые конструкции

- handwritten static ISA duplicate в classifier, decoder, compiler или runtime;
- ручная правка lock/generated output;
- compiler-as-runtime-authority или reconstruction semantic facts из mnemonic/opcode после canonical binding;
- raw compatibility API, напрямую минующий canonical decoder, checked-family validation, scheduler, timed-memory или retire owner;
- unknown/invalid opcode → NOP, zero/default ID, permissive scalar class или «успех»;
- fallback без названного owner, условия входа, fail-closed поведения и теста;
- удаление retained contour без caller inventory, включая reflection, TestSupport, generated/parser/compiler/runtime seams и fallback paths [checked-ID rules in paper §3.7][paper-3] [ArchitectureAuthorityRefactorSummary governance][ArchitectureAuthorityRefactorSummary-gov] [RF-13 ledger][rf13-ledger].

## 3. Decision tree по семейству

```text
Новая сущность меняет публично декодируемую ISA?
├─ Нет → compatibility-only/test-only contour
│        Не добавлять opcode в public catalog; доказать отсутствие production ingress/effect.
└─ Да
   ├─ Читает/пишет память?
   │  ├─ atomic → load/store + atomic commit/fault/ordering contour
   │  └─ load/store → memory capability + timed memory + retire publication
   ├─ Имеет vector/VT state или vector sideband? → vector/VT
   ├─ MatrixTile protocol / lane 6? → MatrixTile/lane 6
   ├─ system/accelerator singleton / lane 7? → accelerator/system/lane 7
   └─ Иначе → scalar ALU/control
            control-flow дополнительно требует PcWrite/link/fault/retire review.
```

Typed topology — ALU `0..3`, LSU `4..5`, DMA/stream `6`, control/system `7`; lane 7 содержит aliased singleton classes, а typed path остаётся неуниверсальным рядом с retained exact-slot contour [paper §3.1.1][paper-3] [paper §10.2][paper-10]. Не выводить lane только из имени класса: подтвердить generated binding, runtime placement и family owner.

| Семейство | Обязательные дополнительные главы/gates | Обычно `N/A` только при явном обосновании | `STOP/ADR`, если… |
|---|---|---|---|
| Scalar ALU | semantic identity; provider; Stage A/B; RegisterWrite/flags; x0; retire/replay | memory, lane 6/7 protocols | новый effect, новый execution owner, нет exact-attempt handoff |
| Scalar control | всё для scalar + hard pin/lane 7 conflict + `PcWrite`/link + fault winner | memory, если нет access | меняется redirect/publication/fault precedence |
| Load/store | static address/access contract; dynamic `MemoryCapability`; memory admission/differential; timed memory; store publication | MatrixTile/accelerator | новый queue/tick/completion/publication owner или unsupported memory shape |
| Atomic | всё load/store + acquire/release, returned result, `AtomicCommit`, single-fault/publication analysis | простая ALU retirement | существующий atomic residual contour недостаточен |
| Vector/VT | vector config/sidebands; VT owner and VT0→VT3 order; vector resource conflicts; vector memory gates при access | MatrixTile/accelerator | новый vector architectural state/effect или mixed-SMT ordering theorem |
| MatrixTile/lane 6 | MatrixTile package/resource contour, execute-capture, replay/rollback ABI, independent retire publication protocol | общий scalar retire как замена | меняется tile state, numeric/layout policy, lane-6 protocol или commit boundary [MatrixTile resource][matrix-resource] [MatrixTile retire ABI][matrix-retire] |
| Accelerator/system/lane 7 | singleton conflict; privilege/domain/device checked IDs; system/accelerator capture and independent commit protocol | scalar register path, кроме явно существующего ABI result | новый device/domain/commit/invalidation/fault owner [accelerator commit][accelerator-commit] |
| Compatibility-only/test-only | caller inventory; public-ingress negative; reflection/TestSupport closure; explicit retention owner | generator/runtime execution, если contour действительно не public и не architectural | contour фактически декодируется, исполняется или публикует state — тогда это ISA change |

## 4. Пошаговый workflow

Для каждого шага таблица фиксирует owner, I/O, valid и invalid behavior, запрещённые обходы, evidence и **change rollback boundary**. Последнее означает безопасную границу отката данного изменения, а не архитектурный replay rollback.

### Шаг 0. Зафиксировать semantic decision и stop conditions

До кода заполнить record из раздела 10 и сослаться на paper-параграф, который уже определяет семантику. Если инструкция вводит новый effect kind, state owner, lane topology, fault precedence, retire timing, replay envelope, timed-memory contour или family zero/absence rule, шаблон неприменим: требуется paper change и отдельное ADR до реализации [ADR-009][adr-009] [paper §7][paper-7].

| Поле | Требование |
|---|---|
| Owner | ISA/CPU architecture reviewer |
| Input → output | Требование + paper → однозначный semantic record или `STOP/ADR` |
| Valid | Opcode/family/effects/owners/invalid behavior определены без реконструкции |
| Invalid/fail-closed | Неясность owner/effect/version/lane → работа остановлена |
| Запрещённый bypass | «Сначала добавим switch, потом опишем»; evidence как замена решения |
| Tests/evidence | Ссылки на paper, ArchitectureAuthorityRefactorSummary retention decision и список затрагиваемых seams |
| Rollback boundary | Удалить только draft record; runtime не менялся |

### Шаг 1. Добавить declaration row

Выбрать ровно один family-файл `OpcodeInfo.Registry.Data.Scalar.cs`, `.Vector.cs`, `.MemoryControl.cs` или `.System.cs` и добавить уникальный row; агрегатор остаётся в `OpcodeInfo.Registry.Data.cs` [catalog sources][opcode-data].

Обязательная декларационная карточка:

| Fact | Что зафиксировать |
|---|---|
| Identity | числовой opcode; canonical mnemonic; aliases и collision policy |
| Encoding | raw form, reserved bits, allowed lanes/forms, valid/invalid/absent sidebands |
| Operands | arity, role каждого operand, x0/no-register/absence, immediate width/sign/alignment |
| Version | ISA/catalog version и extension/privilege requirements |
| Static capability | instruction class, slot/pinning/serialization, memory direction/static shape, resource facts |
| Semantics/support | architectural effect(s), runtime provider/materializer/latency model; unsupported/quarantined status отдельно от static class |

Текущая row schema выражает только подмножество этой карточки; невыразимый fact не переносится в handwritten duplicate — это schema/ADR gate [OpcodeInfo type][opcode-types] [generator source][isa-gen].

| Поле | Требование |
|---|---|
| Owner | Typed ISA catalog owner |
| Input → output | Одобренный record → один typed declaration row |
| Valid | Уникальные opcode/mnemonic; existing schema точно выражает semantics |
| Invalid/fail-closed | Collision, unknown category/flags, невыразимая encoding/provider policy → generation/ADR stop |
| Запрещённый bypass | Ручные rows в generated catalog, classifier, decoder или compiler |
| Tests/evidence | generator negatives, `GeneratedIsaCatalogAuthorityTests`, `IsaAuthorityInventoryTests` [catalog tests][catalog-tests] |
| Rollback boundary | Откат row до regeneration; предыдущий catalog остаётся authority |

### Шаг 2. Regeneration и generated parity

Из repository root:

```powershell
dotnet run --project ./tools/HybridCPU.IsaGen
dotnet run --project ./tools/HybridCPU.IsaGen -- --check
```

Первая команда — regeneration path, вторая — подтверждённый ArchitectureAuthorityRefactorSummary generator gate [generator source][isa-gen] [ArchitectureAuthorityRefactorSummary governance][ArchitectureAuthorityRefactorSummary-gov]. Review-ить вместе declaration row, lock diff и generated C# diff; generated output не править.

| Поле | Требование |
|---|---|
| Owner | ISA generator |
| Input → output | Typed catalog → deterministic lock manifest + generated C# binding |
| Valid | Повторный `--check` не видит drift; ровно ожидаемый descriptor изменён |
| Invalid/fail-closed | Duplicate/collision/schema drift/неполная parity → generator non-zero |
| Запрещённый bypass | Ручная синхронизация outputs; commit с failing `--check` |
| Tests/evidence | Generator gate; `IsaParity`; review трех артефактов |
| Rollback boundary | Откат row и обоих generated outputs одним change set |

### Шаг 3. Canonical decoder и validator legality

Публичный вход остаётся `VliwDecoderV4`, а canonical stages выполняют descriptor lookup, operand decode, named encoding constraints, extension payload и sideband validation [decoder facade][decoder-facade] [decoder stages][decoder-stages]. Для новой формы:

1. Доказать **valid**: минимальная и граничная корректная encoding, разрешённые aliases/sidebands.
2. Доказать **invalid**: unknown opcode, reserved bits, bad register/immediate/alignment, wrong privilege/extension, conflicting или malformed sideband.
3. Доказать **absence**: пустой slot и отсутствующий optional payload не отождествляются с нулевым valid ID/value.
4. Если нужна новая legality rule, зарегистрировать именованный constraint в существующем validator contour; не создавать manual decoder switch как статическую authority.

Lookup сегодня содержит bounded unsupported/prohibited compatibility checks до generated descriptor; это retained seam, а не образец для второго opcode table [decoder stages][decoder-stages] [RF-13 ledger][rf13-ledger].

| Поле | Требование |
|---|---|
| Owner | Canonical decoder + named validator |
| Input → output | Raw slot/bundle + sidebands/context → canonical instruction/bundle или typed decode failure |
| Valid | Один descriptor; operands и sidebands заморожены без потери значения |
| Invalid/fail-closed | Reject до materialization/admission; invalid не становится empty/NOP/default |
| Запрещённый bypass | Direct runtime construction из raw opcode; permissive unknown fallback |
| Tests/evidence | positive, invalid, absence; differential; mutation; facade compatibility [decoder tests][decoder-tests] |
| Rollback boundary | Удалить constraint/tests и row/output; старый decode corpus неизменен |

### Шаг 4. Frozen canonical contracts и semantic identity

Canonical slot должен сохранить raw slot, operand/payload/sideband snapshots и точный `GeneratedStaticBinding`. `SemanticInstructionKey` включает bytes, annotation hash, manifest version/hash, extension configuration, decoder epoch/version, privilege/domain/address-space, vector configuration, executable invalidation epoch и code generation epoch; equality дополнительно сравнивает raw bytes, а не только digest [canonical contracts][canonical-contracts].

Execution-relevant sideband должен находиться либо в canonical annotations/payload, либо в явном context field. Если его изменение не меняет key/frozen semantic content, возможен false replay hit — изменение не готово.

| Поле | Требование |
|---|---|
| Owner | Canonical contract/replay semantic identity owner |
| Input → output | Valid decode + explicit context → immutable canonical bundle + semantic key |
| Valid | Любое semantic изменение меняет key или frozen content |
| Invalid/fail-closed | Missing context → non-replay-eligible/unbound; binding mismatch → reject |
| Запрещённый bypass | Hash-only equality; mutable sideband; issue ID внутри semantic key |
| Tests/evidence | Key mutation по каждому релевантному полю; raw-byte collision defense; frozen content mutation |
| Rollback boundary | Откат identity projection вместе с instruction row; не оставлять serving entry старой формы |

### Шаг 5. Compiler parsing/lowering и annotation transport

Текущий inspected compiler строит IR из encoded instructions, вычисляет metadata/hazards и lower-ит bundle/annotations; он содержит compatibility opcode logic, которая должна быть parity-guarded, а не стать runtime authority [IR builder][ir-builder] [bundle lowerer][bundle-lowerer] [hazard model][hazard-model].

Правила:

- parser/assembler, если он находится в scope изменения, разрешает canonical mnemonic/aliases по declaration, но не определяет runtime semantics;
- lowering переносит operand roles, sidebands, versioned facts и placement annotations losslessly;
- metadata может предсказывать structural plausibility, но runtime заново декодирует и сохраняет право отказа; compiler facts staged и compatibility-tolerant внутри успешного version handshake [paper §3.4][paper-3];
- positive и negative compiler corpus должен дойти до **public canonical decoder**, не останавливаться на compiler-local IR [compiler parity harness][compiler-harness].

В просмотренном compiler seam не установлена единая repository-wide textual mnemonic parser authority. Поэтому manual не называет выдуманный parser path: inventory конкретного producer является обязательной частью change record.

| Поле | Требование |
|---|---|
| Owner | Compiler frontend/lowering owner; runtime decoder остаётся legality owner |
| Input → output | Source/encoded input + annotations → emitted bytes + canonical sidebands |
| Valid | Encode→public decode сохраняет opcode, operands, binding, capability и sidebands |
| Invalid/fail-closed | Unsupported alias/form/version отклоняется; compiler/runtime drift обнаруживается до execution |
| Запрещённый bypass | Runtime provider choice из compiler-only metadata; semantic reconstruction по opcode switch |
| Tests/evidence | Positive/negative `CompilerParity`, alias/version/sideband round trip |
| Rollback boundary | Откат compiler emission вместе с declaration; старый producer остаётся совместим |

### Шаг 6. Runtime provider, materialization и execution

`GeneratedStaticBinding`, `ExecutionContract`, `AdmissionRecord`, `ScheduledOperation` и `VliwOperationId` разделяют static binding, provider, admission, post-Stage-B attempt и execute outcome [RF-06 contracts][rf06-contracts]. Текущий `InstructionRegistry.Runtime.cs` остаётся live materialization seam и бросает ошибку при отсутствии factory; его manual registrations — compatibility implementation, не ISA declaration [runtime registry][runtime-registry].

Порядок:

1. Связать generated materializer/provider ID с существующим provider contract; если текущий generator выдаёт только generic `legacy.*`, нестандартный provider требует schema/architecture решения.
2. Создать immutable `ExecutionContract` из canonical binding и operands/sidebands; dynamic readiness/result/token туда не включать.
3. Materializer создаёт family carrier/MicroOp, но не исполняет и не публикует state.
4. Execution provider возвращает typed terminal/retry/fault outcome по RF-07; unknown exception не превращать в retry/not-ready [RF-07 migration][core-migration].

| Поле | Требование |
|---|---|
| Owner | Runtime materializer; family execution provider |
| Input → output | Canonical slot/binding → contract/carrier → typed execution outcome |
| Valid | Provider ID совпадает с frozen binding; один provider выполняет заявленную semantics |
| Invalid/fail-closed | Missing/mismatched provider, malformed carrier, impossible outcome → invariant/fault, не fallback success |
| Запрещённый bypass | `CreateMicroOp` из raw opcode до canonical decode; compiler executes semantics; unknown→NOP |
| Tests/evidence | `Materialization`, provider mismatch negatives, outcome/fault differential |
| Rollback boundary | Удалить provider registration/materializer вместе с declaration; не менять shared owner semantics |

### Шаг 7. Scheduler, Stage A/B, SMT/FSP и resource conflicts — если применимо

Stage A выполняет A1 class capacity → A2 explicit runtime legality → A3 outer-cap dynamic gates; Stage B после допуска выбирает concrete lane и не расширяет legality [scheduler admission][scheduler-admission] [paper §5][paper-5]. FSP SCHED1 фиксирует ready non-owner donors, SCHED2 повторно читает live candidate и выполняет Stage A → Stage B → commit [FSP pipeline][fsp-pipeline].

Проверить:

- exact-slot retained path отдельно от typed path; не менять его молча и не считать dead из-за названия legacy [paper §3.1][paper-3] [RF-13 ledger][rf13-ledger];
- owner bundle и donor consent, same-VT dependency rules, domain/boundary guards [ADR-001][adr-001] [paper §6][paper-6];
- VT enumeration и tie-break остаются `VT0, VT1, VT2, VT3`; zero — valid VT0, absence отдельна [paper §3.7][paper-3];
- aliased lane 7 conflict, hard pin conflict, late binding conflict, class exhaustion, memory bank/outer cap rejection;
- replay hint только сужает допустимые lanes; не выдаёт legality.

| Поле | Требование |
|---|---|
| Owner | Existing `MicroOpScheduler`; runtime legality service; owner/donor guard |
| Input → output | Candidate + bundle/certificate/live state → admission, concrete lane или typed reject |
| Valid | Class допускается до lane; fresh attempt создаётся только после Stage B |
| Invalid/fail-closed | Conflict/denial/stale guard → no mutation, no ID, no publication |
| Запрещённый bypass | Direct lane write; donor без owner consent; replay lane как authority; VT reorder |
| Tests/evidence | `Routing`; class/pin/lane7 conflicts; owner/donor/domain; exact-slot parity; fresh replay ID |
| Rollback boundary | Откат family placement projection; scheduler owner и exact-slot contour не переписывать |

### Шаг 8. Memory path — только для memory/atomic/vector-transfer/tile/accelerator memory effects

Разделить два договора:

- **static access contract:** opcode class, load/store/atomic direction, access width/form, base/index/immediate roles; scalar-load canonical plan намеренно не содержит resolved address/bank/runtime state [canonical contracts][canonical-contracts];
- **dynamic `MemoryCapability`:** runtime-resolved bank, direction и frozen footprint для Stage A; это не timing, readiness, MSHR, completion token или publication [RF-06 contracts][rf06-contracts] [memory admission][memory-admission].

Timed progress принадлежит `MemoryCycleController`: один controller edge публикует queued completions и один раз двигает retained legacy agent и bound DMA agent; caller-local completion loop запрещён [memory controller][memory-controller] [paper §7.7][paper-7]. Stores и canonical vector transfer не получают видимость до их существующего selected-retire protocol; RF-10 contours ограничены и не доказывают общую memory model [paper §10.2][paper-10].

| Поле | Требование |
|---|---|
| Owner | Decoder static-plan owner; memory admission owner; `MemoryCycleController`; existing retire publication owner |
| Input → output | Static shape + runtime address/topology → capability/request/completion → captured retire effect |
| Valid | Exact direction/width/footprint; one-tick progress; completion/fault привязан к запросу/attempt |
| Invalid/fail-closed | Invalid geometry/bank/size/address, denial или failed completion → no eager publication |
| Запрещённый bypass | Static plan как resolved request; bank=0 fallback; inline tick loop; execute-time store visibility |
| Tests/evidence | `MemoryAdmission`, `MemoryDifferential`, `MemoryCycle`, invalid size/geometry, denial, completion failure, store invisibility before retire |
| Rollback boundary | Удалить новый request adapter/capability projection; single tick и publication owners не менять |

### Шаг 9. Capture, replay/rollback, fault winner, retirement/publication

Semantic replay entry хранит immutable canonical bundle и semantic key; lookup не создаёт attempt ID, а каждый успешный Stage B получает новый `VliwOperationId` [replay entry][replay-entry] [paper §7][paper-7]. Replay-token rollback ограничен явно captured register/memory surfaces и не является backend-global rewind [paper §8.6][paper-8] [paper §10.2][paper-10].

Для любого architectural effect:

1. Capture effect только из exact terminal completed attempt; сохранить `ScheduledOperation`, `ExecutionRecord`, `VliwOperationId`, generated binding, source slot, working slot, lane, VT и effect ordinal [effect identity][effect-identity].
2. Определить replay invalidation при изменении bytes/annotations/context/sideband/code epochs.
3. В fault window выбрать authoritative stage/lane winner; в retirement попадает только older prefix [fault winner][fault-winner] [ADR-009][adr-009].
4. Сначала собрать bounded union effects и полностью prevalidate, затем публиковать в сохранённом порядке; отказ не должен менять register/memory/state, counters или освобождать ресурсы [WB stage flow][wb-stage] [retire coordinator][retire-coordinator].
5. Использовать существующего architectural-state owner. `RetireCoordinator` применяет `RegisterWrite`/`PcWrite`, отбрасывает запись в x0 и не является универсальным owner для CSR/system/tile/accelerator effects [retire coordinator][retire-coordinator] [ADR-009][adr-009].

| Поле | Требование |
|---|---|
| Owner | Replay semantic owner; fault arbitration; family capture owner; bounded retire/publication owner |
| Input → output | Terminal exact attempt → immutable effect → prevalidated selected prefix → publication/fault |
| Valid | Одна попытка, уникальный effect ordinal, owner VT совпадает; replay success создаёт fresh attempt |
| Invalid/fail-closed | Mismatch/duplicate/denial/fault → zero publication; winner suppresses faulting/younger work |
| Запрещённый bypass | Eager execute publication; reconstructed attempt; rollback whole backend; telemetry as commit proof |
| Tests/evidence | `Retire`, `ReplayIdentity`, `FaultInjection`; denial/rollback/no-publication; fault winner; bounded prevalidation |
| Rollback boundary | Удалить новый capture/effect mapping; восстановить прежний family protocol, не обходить его eager write |

### Шаг 10. Compatibility и legacy perimeter assessment

Построить closed-world caller inventory по opcode/alias/provider/classifier/adapter: production, compiler, generated output, parser/assembler, tests, TestSupport, reflection, serialization/checkpoint/trace и fallback calls. Valid parity, invalid behavior, retention и deletion proof — четыре разные строки evidence [ArchitectureAuthorityRefactorSummary governance][ArchitectureAuthorityRefactorSummary-gov] [RF-13 ledger][rf13-ledger].

| Поле | Требование |
|---|---|
| Owner | Compatibility owner, названный в ledger/change record |
| Input → output | Caller inventory + parity/negative results → retain/migrate/remove decision |
| Valid | Retained adapter валидирует и делегирует единственному owner с exact wire round trip |
| Invalid/fail-closed | Unknown/raw invalid value не clamp/modulo/default; test-only contour не становится production ingress |
| Запрещённый bypass | Удаление по имени; source-only grep без reflection/TestSupport; valid parity как invalid/deletion proof |
| Tests/evidence | Authority inventory, source scans, reflection/TestSupport tests, wire/zero/absence negatives, public-ingress absence |
| Rollback boundary | Восстановить adapter под прежним owner; не откатывать scheduler/replay/retire architecture |

## 5. Сводка owners и gates по шагам

Эта таблица — навигация, не замена подробных таблиц выше.

| Шаг | Решающий owner | Gate перед следующим шагом |
|---:|---|---|
| 0 | Paper/architecture reviewer | Semantics уже авторизована либо получен ADR |
| 1 | Typed catalog owner | Один row, все невыразимые facts остановлены |
| 2 | Generator | Lock/generated parity без drift |
| 3 | Canonical decoder/validator | Valid + invalid + absence закрыты |
| 4 | Semantic identity owner | Все execution-relevant mutations дают miss |
| 5 | Compiler producer | Positive/negative corpus проходит public decode |
| 6 | Runtime provider | Exact binding, typed outcome, no fallback success |
| 7 | Scheduler/legality | Stage A/B и exact-slot boundaries сохранены |
| 8 | Memory/timed-memory owner | Capability, tick, fault и publication owner доказаны |
| 9 | Fault/retire/family publication owner | Exact attempt + prevalidated older prefix |
| 10 | Compatibility owner | Retention отдельно от deletion proof |

## 6. Требования к semantic identity

### 6.1 Две identity, две фазы

| Identity | Содержит | Не содержит | Момент появления |
|---|---|---|---|
| `SemanticInstructionKey` | raw instruction/bundle bytes; canonical annotation hash; manifest version/hash; extension, decoder, privilege, domain, address-space, vector and code epochs | admission, lane, issue, attempt ID | После canonical decode |
| `VliwOperationId` | VT, working bundle/slot, fresh operation attempt; exact issued operation | semantic cache equality | Только после successful Stage B |

Это разделение зафиксировано canonical/replay contracts: semantic hit может переиспользовать immutable decode, но не прошлую issued attempt identity [canonical contracts][canonical-contracts] [replay entry][replay-entry] [RF-06 contracts][rf06-contracts].

### 6.2 Обязательные правила

- Instruction bytes сравниваются фактически, не только по hash.
- Canonical annotations включают каждый execution-relevant sideband. Raw sideband не может быть потерян и позднее «восстановлен» compiler/runtime switch.
- ISA manifest version/hash и extension configuration участвуют в identity.
- Privilege, domain, address-space, vector configuration и executable/code epochs участвуют там, где они способны изменить decode/semantics.
- Compiler lowering provider и runtime execution provider различны. Первый выпускает bytes/metadata; второй выполняет canonical contract.
- Replay hit разрешает reuse только внутри validation/invalidation envelope. Mutation → miss/refresh, не partial repair [paper §4.5][paper-4] [paper §7][paper-7].
- Любой architectural effect сохраняет **exact attempt identity** через capture и retirement. Нельзя заново вычислить `VliwOperationId` из opcode/VT/lane.
- x0 — valid register identity, но `RegisterWrite` в x0 не создаёт архитектурную mutation; отсутствие register — отдельное состояние [paper §3.7][paper-3] [retire coordinator][retire-coordinator].

### 6.3 Минимальные anti-false-hit mutations

Для одной valid encoding создать варианты с изменением по одному полю: raw byte, operand, immediate, alias-produced canonical annotation, instruction sideband, bundle sideband, manifest/hash, extension fingerprint, decoder version/epoch, privilege, domain, address space, vector config, executable invalidation epoch и code generation epoch. Каждый semantic-changing вариант обязан дать miss; byte-identical и полностью context-identical вариант может hit, после чего новый success получает fresh attempt [replay tests][replay-tests].

## 7. Effects по типам инструкции

`RetireVisibleEffectKind` перечисляет закрытый RF-08 vocabulary, но наличие enum member не означает универсальный migrated publication path; многие family effects сохранены как approved residual/independent protocol [effect identity][effect-identity] [ADR-009][adr-009].

| Effect | Publication owner | x0 / denial / fault | Rollback и replay | Когда нужен отдельный architecture decision |
|---|---|---|---|---|
| `RegisterWrite` | Для авторизованного scalar subset — WB capture + `RetireCoordinator`; прочие источники сверять с ADR-009 | rd=x0: discard/no mutation; denial/fault: no record/publication | Exact attempt; replay success fresh ID; rollback только captured surface | Новый producer вне существующего exact-handoff subset, multi-write shape или другой state owner |
| `PcWrite` | Existing control-flow capture и committed PC owner | x0 N/A; denial/faulting/younger redirect не публикуется | Redirect identity/ordering не реконструировать | Новая redirect timing, link coupling, winner precedence |
| `CsrWrite`/CSR readback | Existing CSR retire effect owner; не generic `RetireCoordinator` | x0 относится только к optional scalar readback; fault/privilege denial → no CSR mutation | CSR/domain/version контекст в semantic/replay analysis | Новый CSR namespace, privilege rule, side effect или publication order |
| Load result | Existing load completion + scalar/vector retire path | rd=x0 discards scalar result, но memory fault всё ещё реальный | Request/completion и attempt должны совпадать | Новый memory shape/timing/fault owner |
| Store memory effect | Selected-retire store publication owner | x0 N/A; denial/fault/rollback → память не меняется | Store bytes captured; replay не повторяет eager mutation | Любая execute-time visibility, новый queue/tick/commit protocol |
| `AtomicCommit` + optional returned result | Existing atomic independent/retire contour | rd=x0 может discard result, но atomic memory semantics остаётся; fault → no partial commit | Одно attempt, согласованные memory+register effects | Новая atomicity/order/fault combination или попытка объединить residual paths |
| Vector config/predicate/stream dirty/vector transfer | Existing vector capture/publication owners | x0 только для optional scalar result; denial/fault suppresses coupled effects | Vector config/sidebands входят в replay identity | Новый vector state, coupled effect, segment publication или mixed-SMT ordering claim |
| `MatrixTileCommit` | MatrixTile independent protocol/state owner [MatrixTile retire ABI][matrix-retire] | x0 N/A; protocol denial/fault → no tile publish | Использовать MatrixTile replay/rollback ABI, не generic register replay | Любая новая tile shape/layout/numeric/memory/commit semantics |
| `AcceleratorCommit` | Accelerator commit coordinator/protocol [accelerator commit][accelerator-commit] | x0 только если существующий ABI возвращает scalar result; rejection/fault → no commit | Device/domain/invalidation identity сохраняется | Новый device class, invalidation target, memory publication или commit owner |
| `SystemCommit`/trap/pipeline event | Existing system/exception/pipeline-event owner | x0 N/A или только optional result; denial/fault следует privilege/fault owner | Replay/rollback разрешён лишь явно существующим contour | Новый system-visible state, ordering guarantee, trap/fence behavior |

Общее правило: новый effect kind, новый architectural state, новый владелец publication, новая атомарность между effects, изменение fault winner или перемещение publication относительно selected retirement всегда оформляются paper decision + ADR, а не строкой по шаблону [ADR-009][adr-009].

## 8. Test matrix

Тесты должны различать: (a) valid parity, (b) invalid/fail-closed behavior, (c) compatibility retention, (d) deletion proof. Ни один столбец не закрывает другой [ArchitectureAuthorityRefactorSummary governance][ArchitectureAuthorityRefactorSummary-gov].

| Область | Обязательные positive checks | Обязательные negative/mutation checks | Existing evidence seam / gate |
|---|---|---|---|
| Manifest/generator | Row→lock→generated exact projection; deterministic repeat | duplicate opcode/mnemonic, bad schema/policy, stale output | `HybridCPU.IsaGen -- --check`; [catalog tests][catalog-tests] |
| ISA parity | descriptor/class/serialization/compiler profile совпадают | missing descriptor, extra handwritten static row, field drift | `-Stage IsaParity` |
| Decoder | Valid min/max encoding, alias, sideband, empty slots | unknown/prohibited, reserved bits, bad operands/immediate/alignment, wrong extension/privilege, malformed/absent sideband | `-Stage DecoderDifferential`; [decoder tests][decoder-tests] |
| Canonical/materialization | Exact `GeneratedStaticBinding`, immutable contract, expected provider | binding/provider mismatch, mutated canonical payload, missing provider | `-Stage Materialization` |
| Compiler parity | Source/encoded producer→bytes+annotations→public decode | unsupported alias/form/version; annotation loss; compiler-only accept | `-Stage CompilerParity`; [compiler parity tests][compiler-tests] |
| Scheduler/admission | Legal class, hard/flexible placement, owner/donor consent, VT order | overcommit, lane6/7 conflict, invalid pin, late bind, domain/boundary denial, no-ID reject | `-Stage Routing`; `-Stage FamilyDifferential` |
| Memory admission | Correct direction/bank/footprint | invalid geometry/size/bank, direction mismatch, capability/binding drift | `-Stage MemoryAdmission`; `-Stage MemoryDifferential` |
| Runtime outcome/fault | Completed/retry/fault по family contract | unknown exception, failed token, fallback denial, speculative suppression | `-Stage FaultInjection` |
| Replay/rollback | Exact identical entry hit; explicit rollback surface restored | mutation каждого key/sideband field misses; stale content; no reused attempt ID; no backend-global rewind | `-Stage ReplayIdentity`; [replay tests][replay-tests] |
| Retirement/publication | Exact attempt/effect ordinal; stable older prefix; complete prevalidation; x0 discard | duplicate/mismatch, faulting/younger publication, prevalidation failure, eager store/state mutation | `-Stage Retire`; [retire tests][retire-tests] |
| Timed memory | One controller edge; request/completion binding; store visibility at owner boundary | caller-local loop, double tick, failed completion publication, eager write | `-Stage MemoryCycle` |
| Compatibility/TestSupport/reflection | Retained adapter exact valid/wire parity and named owner | invalid raw value, production reachability of test-only contour, hidden reflection caller, missing fallback inventory | `-Stage AuthorityInventory`; RF-13 inventory tests [RF-13 ledger][rf13-ledger] |
| Documentation/reference | Paper link, ArchitectureAuthorityRefactorSummary limitation, source/test links, change record complete | broken relative link, manual claim without authority/seam, stale metrics as baseline | Local link-resolution check; final `-Stage Full` includes repository docs/architecture gates [ArchitectureAuthorityRefactorSummary governance][ArchitectureAuthorityRefactorSummary-gov] |

### 8.1 Подтверждённые ArchitectureAuthorityRefactorSummary validation commands

Запускать из repository root. Выбрать family-relevant stages, затем final gate; зелёный gate — только exit code `0`, complete non-aborted TRX и provenance, где они требуются [ArchitectureAuthorityRefactorSummary governance][ArchitectureAuthorityRefactorSummary-gov].

```powershell
pwsh ./eng/validate.ps1 -Stage Baseline
pwsh ./eng/validate.ps1 -Stage AuthorityInventory
dotnet run --project ./tools/HybridCPU.IsaGen -- --check
pwsh ./eng/validate.ps1 -Stage IsaParity
pwsh ./eng/validate.ps1 -Stage DecoderDifferential
pwsh ./eng/validate.ps1 -Stage Materialization
pwsh ./eng/validate.ps1 -Stage Routing
pwsh ./eng/validate.ps1 -Stage MemoryAdmission
pwsh ./eng/validate.ps1 -Stage MemoryDifferential
pwsh ./eng/validate.ps1 -Stage FamilyDifferential
pwsh ./eng/validate.ps1 -Stage CompilerParity
pwsh ./eng/validate.ps1 -Stage FaultInjection
pwsh ./eng/validate.ps1 -Stage Retire
pwsh ./eng/validate.ps1 -Stage ReplayIdentity
pwsh ./eng/validate.ps1 -Stage MemoryCycle
pwsh ./eng/validate.ps1 -Stage Full
```

External emulator gate добавляется только для затронутого RF-06 routing/admission slice или behavior-fixing RF-07 contour, как требует ArchitectureAuthorityRefactorSummary; не использовать telemetry output как retirement/semantic proof [ArchitectureAuthorityRefactorSummary governance][ArchitectureAuthorityRefactorSummary-gov] [paper §8][paper-8].

```powershell
dotnet run --project ./TestAssemblerConsoleApps/TestAssemblerConsoleApps.csproj --configuration Debug --no-restore -- --iterations 100 --telemetry-logs minimal
```

## 9. Review checklist перед merge

### Authority и declaration

- [ ] Change record содержит ссылку на paper semantics; manual/evidence не объявлены authority.
- [ ] Редактировался один typed catalog row; lock/generated outputs созданы generator-ом.
- [ ] Opcode, mnemonic/aliases, encoding, operands, version/extension, class/slot/resource, effect/provider и support status определены.
- [ ] Невыразимый current schema fact привёл к `STOP/ADR`, а не handwritten duplicate.
- [ ] RF-12 не переоткрыта; RF-13 completion не заявлена сверх ledger.

### Decode, compiler, runtime

- [ ] Public canonical decoder имеет valid, invalid и absence cases.
- [ ] Reserved/unknown/malformed input fail-closed; invalid не становится NOP/zero/default.
- [ ] Canonical payload/sidebands и generated binding immutable.
- [ ] Compiler переносит semantics/annotations без reconstruction; negative corpus достигает public decoder.
- [ ] Runtime materializer/provider совпадает с binding; unknown exception не становится retry/success.

### Scheduling, memory, identity

- [ ] Lane/class взяты из authority/binding, а не имени; lane 7 alias conflict и lane 6 protocol проверены при применимости.
- [ ] Stage A order, Stage B late binding, exact-slot path, owner/donor и VT0→VT3 сохранены.
- [ ] Static memory shape отделена от dynamic `MemoryCapability` и timed-memory state.
- [ ] Нет caller-local tick/completion loop или eager memory publication.
- [ ] Все execution-relevant bytes/annotations/version/context/sidebands входят в semantic identity; mutation tests дают miss.
- [ ] Replay hit получает fresh issued attempt после Stage B.

### Effects, retirement, compatibility

- [ ] Каждый architectural effect имеет названных capture/publication/state owners и exact attempt identity.
- [ ] Fault winner/older prefix/complete prevalidation доказаны; denial/rollback не публикуют state.
- [ ] x0 и absence проверены раздельно.
- [ ] MatrixTile/accelerator/system independent protocols не заменены generic retirement.
- [ ] Compatibility retention, invalid behavior и deletion proof оформлены раздельно.
- [ ] Caller inventory включает parser/compiler/runtime/generated/reflection/TestSupport/fallback/wire seams.

### Evidence

- [ ] Family-relevant ArchitectureAuthorityRefactorSummary stages зелёные с полными артефактами; `Full` пройден на merge candidate.
- [ ] Все relative Markdown links разрешаются.
- [ ] Нет historical metrics или telemetry counters, объявленных current baseline/semantic proof.
- [ ] Residual limitation записано явно.

## 10. Минимальный шаблон change/evidence record

Скопировать в change description или локальный evidence Markdown. Заполненное поле ссылается на paper, ArchitectureAuthorityRefactorSummary decision или конкретный source/test seam; `N/A` всегда содержит причину.

```markdown
# ISA change record: <canonical mnemonic> / <opcode>

## Semantic role
- Family: <scalar | control | load/store | atomic | vector/VT | MatrixTile | accelerator/system | compatibility/test>
- Architectural semantics: <concise statement + paper relative link>
- Existing template applies: <yes/no>
- STOP/ADR triggers reviewed: <none | list + decision link>

## Declaration authority
- Typed row: <relative path>
- Opcode / canonical mnemonic / aliases: <...>
- Encoding constraints / reserved bits: <...>
- Operand roles and zero/absence rules: <...>
- ISA/catalog version, extension, privilege: <...>
- Static class/slot/pinning/serialization/resources: <...>
- Effect / provider / materializer / latency model: <...>
- Current-schema limitation: <none | explicit limitation/ADR>

## Callers and consumers
- Generator/lock/generated binding: <paths>
- Decoder/validator: <paths>
- Compiler/parser/assembler producers: <paths or proven N/A>
- Runtime materializer/provider: <paths>
- Scheduler/memory/replay/retire consumers: <paths or N/A reasons>
- Compatibility/reflection/TestSupport/wire/fallback inventory: <paths/results>

## Wire, zero and absence behavior
- Raw encoding and sideband round trip: <...>
- Valid zero identities/effects: <...>
- Absence representation: <...>
- x0 behavior: <...>

## Invalid behavior
- Unknown/reserved/malformed: <failure>
- Wrong version/extension/privilege/domain: <failure>
- Resource/admission denial: <failure, no mutation/no ID>
- Runtime fault/completion failure: <winner/publication behavior>

## Owners
- Declaration / decode legality / compiler transport: <...>
- Runtime provider / scheduler / memory tick: <...>
- Capture / fault / retirement / architectural state publication: <...>
- Compatibility retention owner: <...>

## Semantic identity and effects
- Key fields and sidebands: <...>
- Fresh post-Stage-B attempt proof: <...>
- Effect kinds and ordinals: <...>
- Replay invalidation and bounded rollback: <...>

## Tests and evidence
- Manifest/generator negatives: <...>
- ISA/decoder/compiler parity: <...>
- Scheduler/memory/fault/replay/retire: <...>
- Compatibility/reflection/TestSupport/docs: <...>
- Commands, exit codes, TRX/provenance: <...>

## Rollback point
- Change rollback boundary: <files/registrations removable together>
- Architectural rollback surface: <existing bounded owner or N/A>

## Residual limitation
- <explicit retained contour, unproven claim, or none with justification>
```

## 11. Типовые ошибки после ArchitectureAuthorityRefactorSummary

| Ошибка | Почему неверно | Правильное действие |
|---|---|---|
| Stale static tables | Создают вторую ISA truth и drift от generated binding | Добавить typed row; generator; parity/source-scan tests |
| Manual decoder switch | Смешивает static declaration и legality; часто оставляет permissive unknown path | Named validator constraint + invalid/mutation corpus |
| Raw bypass | Теряет checked-family zero/absence, canonical sidebands и owner guards | Validate raw wire и делегировать canonical family owner |
| Compiler reconstruction | Compiler metadata начинает решать runtime semantics/provider | Lossless annotation transport; public decode parity |
| Неверная resource/lane attribution | Имя opcode не доказывает typed class/pin; lane 7 aliased, lane 6 protocol-specific | Сверить paper, generated binding, placement и conflict tests |
| Premature publication | Execute success не равен retirement; faulting/younger effect может стать видимым | Capture exact effect; bounded full prevalidation; selected owner publishes |
| Replay identity loss | Semantic cache key или reconstructed ID ошибочно переиспользует старую попытку | Full key mutations; fresh `VliwOperationId` только post-Stage-B |
| Telemetry как authority | Counter показывает наблюдение, а не legality/commit/correctness | Использовать telemetry только как evidence surface вместе с owner-state/retire proof [paper §8][paper-8] |
| Valid parity как deletion proof | Ничего не говорит об invalid calls, reflection, TestSupport и fallback reachability | Раздельные invalid, retention и closed-world deletion inventories |
| Legacy удаляется по названию | Exact-slot, Stage A/B, SMT/FSP, replay fallback, bounded retire и timed-memory contours могут быть активны | Сначала owner/caller proof и ledger decision [RF-13 ledger][rf13-ledger] |

## 12. Repository reference map

Ни одна ссылка ниже не наделяет этот manual authority; это только resolvable navigation map.

[paper-dir]: ../../../../ResearchPaper/section/md%20base/
[paper-3]: ../../../../ResearchPaper/section/md%20base/3_Architectural_Overview_and_Frontend_Contract.md
[paper-4]: ../../../../ResearchPaper/section/md%20base/4_Execution_Bundles_Legality_Analysis_and_Resource_Certification.md
[paper-5]: ../../../../ResearchPaper/section/md%20base/5_Two_Stage_Admission_and_Bundle_Compositional_SMT_Packing.md
[paper-6]: ../../../../ResearchPaper/section/md%20base/6_Assist_Coupled_Data_Movement_and_Donor_Semantics.md
[paper-7]: ../../../../ResearchPaper/section/md%20base/7_Replay_Stable_Placement_Replay_Tokens_and_Execution_Boundaries.md
[paper-8]: ../../../../ResearchPaper/section/md%20base/8_Telemetry_Validation_and_Evaluation_Methodology.md
[paper-10]: ../../../../ResearchPaper/section/md%20base/10_Related_Work_Limitations_and_Conclusion.md

[ArchitectureAuthorityRefactorSummary-overview]: ../../../ArchitectureAuthorityRefactor/00_Overview/00_README.md
[target-authority]: ../../../ArchitectureAuthorityRefactor/02_Authority/02_Target_Architecture_and_Authority.md
[adr-001]: ../../../ArchitectureAuthorityRefactor/02_Authority/ADR-001_CrossVT_Composition.md
[adr-002]: ../../../ArchitectureAuthorityRefactor/02_Authority/ADR-002_Backend_Target.md
[adr-009]: ../../../ArchitectureAuthorityRefactor/02_Authority/ADR-009_VLIW_Retirement.md
[adr-010]: ../../../ArchitectureAuthorityRefactor/02_Authority/ADR-010_CPU_Core_State_Ownership.md
[ArchitectureAuthorityRefactorSummary-gov]: ../../../ArchitectureAuthorityRefactor/05_Governance/05_Invariants_Dependency_Risks_DoD.md
[core-migration]: ../../../ArchitectureAuthorityRefactor/04_CoreMigration/04_RF07_RF13_Core_Migration.md
[rf13-ledger]: ../../../ArchitectureAuthorityRefactor/12_RF13/00_CURRENT_STATUS_AND_LEDGER.md

[opcode-data]: ../../../../HybridCPU_ISE/NonRTL/Arch/OpcodeInfo.Registry.Data.cs
[opcode-types]: ../../../../HybridCPU_ISE/NonRTL/Arch/OpcodeInfo.Types.cs
[isa-gen]: ../../../../tools/HybridCPU.IsaGen/Program.cs
[isa-lock]: ../../../../isa/hybridcpu-isa.lock.json
[generated-catalog]: ../../../../HybridCPU_ISE/NonRTL/Arch/Generated/GeneratedIsaShadowCatalog.g.cs
[decoder-facade]: ../../../../HybridCPU_ISE/CloseToHSL/Core/Frontend/Decode/VliwDecoderV4Bridge/VliwDecoderV4.cs
[decoder-stages]: ../../../../HybridCPU_ISE/CloseToHSL/Core/Decoder/DeclarativeDecoderStages.cs
[canonical-contracts]: ../../../../HybridCPU_ISE/CloseToHSL/Core/Decoder/CanonicalDecodedContracts.cs
[rf06-contracts]: ../../../../HybridCPU_ISE/CloseToHSL/Core/Decoder/Rf06ExecutionContracts.cs
[memory-admission]: ../../../../HybridCPU_ISE/CloseToHSL/Core/Decoder/Rf06MemoryCapabilityAdmission.cs
[replay-entry]: ../../../../HybridCPU_ISE/CloseToHSL/Core/Decoder/Rf09ReplayEntry.cs
[runtime-registry]: ../../../../HybridCPU_ISE/CloseToHSL/Core/Diagnostics/InstructionRegistry.Runtime.cs
[scheduler-admission]: ../../../../HybridCPU_ISE/CloseToHSL/Core/Pipeline/Scheduling/Admission/MicroOpScheduler.Admission.cs
[fsp-pipeline]: ../../../../HybridCPU_ISE/CloseToHSL/Core/Pipeline/Scheduling/Fsp/MicroOpScheduler.FSPPipeline.cs
[memory-controller]: ../../../../HybridCPU_ISE/CloseToHSL/Memory/Timing/MemoryCycleController.cs
[wb-stage]: ../../../../HybridCPU_ISE/CloseToHSL/Core/Pipeline/ExecutionFlow/StageFlow/CPU_Core.PipelineExecution.cs
[fault-winner]: ../../../../HybridCPU_ISE/CloseToHSL/Core/Pipeline/ExecutionFlow/Faults/CPU_Core.PipelineExecution.Faults.cs
[retire-coordinator]: ../../../../HybridCPU_ISE/CloseToHSL/Core/Architecture/Registers/Retire/RetireCoordinator.cs
[effect-identity]: ../../../../HybridCPU_ISE/CloseToHSL/Core/Architecture/Registers/Retire/Rf08RetireEffectIdentityContracts.cs
[matrix-resource]: ../../../../HybridCPU_ISE/CloseToHSL/Core/ISA/Instructions/NonVmx/Lanes00_03Vector/MatrixTile/MatrixTileResourceContour.cs
[matrix-retire]: ../../../../HybridCPU_ISE/CloseToHSL/Core/ISA/Instructions/NonVmx/Lanes00_03Vector/MatrixTile/MatrixTileRetirePublicationAbi.cs
[accelerator-commit]: ../../../../HybridCPU_ISE/CloseToHSL/Core/Execution/ExternalAccelerators/Commit/AcceleratorCommitModel.cs

[ir-builder]: ../../../../HybridCPU_Compiler/Core/IR/Construction/HybridCpuIrBuilder.cs
[bundle-lowerer]: ../../../../HybridCPU_Compiler/Core/IR/Bundling/HybridCpuBundleLowerer.cs
[hazard-model]: ../../../../HybridCPU_Compiler/Core/IR/Hazards/HybridCpuHazardModel.cs
[compiler-harness]: ../../../../HybridCPU_ISE.Tests/TestHelpers/CompilerToIseParityHarness.cs

[architecture-tests]: ../../../../HybridCPU_ISE.Tests/Architecture/
[catalog-tests]: ../../../../HybridCPU_ISE.Tests/Architecture/GeneratedIsaCatalogAuthorityTests.cs
[decoder-tests]: ../../../../HybridCPU_ISE.Tests/Architecture/DeclarativeDecoderStagesTests.cs
[compiler-tests]: ../../../../HybridCPU_ISE.Tests/Architecture/Rf066CompilerParityTests.cs
[replay-tests]: ../../../../HybridCPU_ISE.Tests/Architecture/Rf090Rf091ReplayEntryFreezeTests.cs
[retire-tests]: ../../../../HybridCPU_ISE.Tests/Architecture/Rf084SelectedOlderPrefixEffectUnionPrevalidationTests.cs
