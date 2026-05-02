
Уже видно базовое противоречие будущего развития: модель сознательно разводит lane6 DSC, StreamEngine и lane7 L7-SDC, но общие основания — токены, retire, память, cache, compiler lowering — пока больше описаны как gates, чем как единая исполнимая архитектура. Дальше смотрю конкретные code anchors, чтобы отделить “архитектурный долг” от корректного fail-closed состояния.

На уровне кода границы действительно fail-closed, но это не снимает главную архитектурную проблему: несколько “почти исполнительных” model/helper контуров уже накопили семантику исполнения, публикации и памяти. Риск не в сегодняшнем Execute, а в том, что будущий переход к executable может собрать их в неправильном порядке.

## Вердикт

**Архитектура HybridCPU в этих зонах сейчас безопасна как fail-closed/model-only baseline, но не выглядит готовой как единая executable architecture.** Главный недостаток не в том, что `DmaCompute`, `StreamEngine`, `VectorALU` и `ExternalAccelerators` плохо разделены. Наоборот, разделение выполнено намеренно. Проблема в том, что будущая исполнимая архитектура уже распалась на несколько параллельных “почти-исполнительных” контуров: runtime/helper, StreamEngine direct execution, token commit, L7 commit coordinator, register ABI model, conflict manager, cache observer, compiler sideband. Они согласованы документально, но пока не сведены в один обязательный механизм исполнения, публикации, памяти, faults и compiler lowering.

Ниже — только существенные недочеты.

---

# 1. Ключевые архитектурные недочеты

## 1. Нет единого retire/publication authority для DSC, StreamEngine, VectorALU и L7

Сейчас есть несколько разных “публикующих” механизмов:

`DmaStreamComputeRuntime` делает staged writes и переводит token в `CommitPending`, но явно не является `MicroOp.Execute` и не подключен к StreamEngine/DMAController . Сам `DmaStreamComputeMicroOp.Execute` hard-fail-closed . `DmaStreamComputeToken.Commit` публикует staged writes в память, но это helper/model path, не pipeline retire. StreamEngine отдельно умеет публиковать scalar/predicate retire-visible результаты через `CaptureRetireWindowPublications(...)` . L7 имеет отдельный commit coordinator model, который тоже не является `SystemDeviceCommandMicroOp.Execute` и не публикует текущие retire exceptions .

**Почему это плохо:** при переходе к executable DSC/L7 появится риск трех разных истин: “memory committed”, “retire-visible”, “token completed”. Для CPU/ISA это должны быть разные стадии, но с одним нормативным порядком. Сейчас порядок описан как future gate, а не как общий механизм.

**Что нужно:** единая публикационная модель:

`issue/admit -> execute/backend -> device complete -> commit pending -> retire/commit arbitration -> memory/register publication -> exception visibility`.

Она должна покрывать DSC, L7, StreamEngine scalar/predicate публикации, VectorALU exceptions, DMA completion, cancellation, squash/trap/context switch.

---

## 2. DmaStreamCompute и StreamEngine/VectorALU дублируют вычислительную семантику, но не имеют общего semantic source of truth

`DmaStreamComputeRuntime` сам реализует Copy/Add/Mul/Fma/Reduce, сам загружает/сохраняет integer/float элементы, сам решает размер элемента и поведение overflow/FP через `BitConverter`/`BinaryPrimitives` . StreamEngine же использует отдельную `VectorALU` семантику, datatype-aware compute, predicate/tail/mask behavior и exception counters .

**Архитектурный риск:** одно и то же “vector compute” поведение может разойтись:

- integer overflow semantics;
- FP NaN/rounding/inexact/overflow/underflow;
- FMA: fused или `(a*b)+c`;
- reduction associativity;
- predicate/tail/mask policy;
- exception/trap/interrupt behavior.

Сейчас это безопасно, потому что DSC не executable. Но если lane6 DSC станет executable, модель получит два compute engines: StreamEngine/VectorALU и DmaStreamComputeRuntime. Это противоречит философии HybridCPU “один контракт — одна authority surface”.

**Что нужно:** до executable DSC выбрать один из вариантов:

1. DSC lowering использует VectorALU semantic kernel;
2. DSC имеет отдельный compute semantic spec, но с обязательной conformance матрицей против VectorALU;
3. DSC ограничивается memory-move/descriptor DMA, а compute остается в StreamEngine/VectorALU.

Без этого executable DSC будет архитектурно двусмысленным.

---

## 3. StreamEngine уже реально исполняет stream/vector, но DSC запрещен как fallback — мост между ними не определен

Документация правильно запрещает silently degrade DSC в StreamEngine/VectorALU . Но это оставляет архитектурный разрыв: StreamEngine — единственный живой raw stream/vector executor; DSC — descriptor-backed typed-slot carrier plus helper. Между ними нет формального lowering/translation контракта.

**Недостаток:** не описано, при каких условиях будущий DSC может быть:

- самостоятельным исполнительным блоком;
- фронтендом к StreamEngine;
- DMA+VectorALU fused path;
- compiler-only sideband, который runtime переводит в StreamExecutionRequest.

Сейчас “не fallback” защищает от unsafe claims, но не дает future architecture.

**Что нужно:** ADR уровня “DSC execution binding”:

- DSC-to-StreamExecutionRequest запрещен/разрешен;
- какие DSC shapes отображаются на StreamEngine;
- какие DSC operations должны оставаться отдельными;
- как сохраняется owner/domain/guard;
- как переносится fault/retire/publication;
- что происходит с SRF/prefetch/cache invalidation.

---

## 4. Memory/addressing модель разорвана между physical helper, StreamEngine backend, IOMMU infra и L7 model

DSC helper path использует physical main memory и явно не использует IOMMU/cache-coherent runtime memory . StreamEngine BurstIO использует `MemorySubsystem` или fallback backend, а для больших contiguous transfers может заходить в DMAController helper path . L7 model имеет guarded source reads, staging и commit через отдельный coordinator .

**Архитектурный долг:** нет единого address-space contract для всех четырех поверхностей:

- CPU virtual/physical;
- owner domain;
- mapping epoch;
- IOMMU translation;
- device id;
- backend selection;
- no-fallback rule;
- fault priority.

Phase06/Phase13 правильно говорят, что это future-gated, но сама архитектура пока не отвечает: “какой адрес видит DSC/L7/StreamEngine — virtual, physical, IO virtual, descriptor virtual?” 

**Что нужно:** общая `AddressSpaceExecutionContract` для CPU/DSC/StreamEngine/DMA/L7:

- input address kind;
- translation authority;
- fallback policy;
- mapping epoch check;
- permission/domain check;
- fault class;
- cache visibility obligation.

Без этого executable DMA/accelerator memory будет небезопасной.

---

## 5. GlobalMemoryConflictService пока концептуален; текущие conflict managers не являются global authority

Phase05 прямо говорит: `MemoryUnit` и `AtomicMemoryUnit` не устанавливают conflict manager как mandatory global CPU load/store truth; future executable overlap требует installable `GlobalMemoryConflictService` . L7 conflict manager тоже model-local и не hook’нут в глобальный CPU load/store pipeline .

**Недостаток:** архитектура уже содержит footprints, resource masks, `MemoryOrdered`, `FullSerial`, reservations, conflict observations. Но это пока “evidence”, не enforcing mechanism.

**Опасность:** при включении async overlap разработчик может ошибочно решить, что resource masks + conflict manager tests уже дают ordering. Не дают.

**Что нужно:** обязательная state machine:

- absent mode;
- passive observe mode;
- enforcing mode;
- CPU load/store/atomic hooks;
- DSC issue reservation;
- L7 submit reservation;
- DMA transfer reservation;
- StreamEngine/SRF/assist hooks;
- commit validation;
- fence/wait/poll rules;
- release on commit/fault/cancel/squash/trap/context switch.

---

## 6. Cache/coherency находится в состоянии “explicit non-coherent observer”, но dirty-line story не закрыта

Phase09 честно фиксирует: cache/prefetch surfaces существуют, но это не coherent DMA/cache hierarchy; explicit observer invalidation only; flush is currently a no-op proof while data cache remains read-materialized/non-dirty .

**Главный недочет:** архитектура не закрыла вопрос CPU dirty data. Если CPU stores могут быть buffered/dirty/cache-resident, DMA/DSC/L7 reads требуют writeback/flush. Если dirty lines невозможны, это должно быть hard invariant. Сейчас это future obligation.

**Что нужно до executable memory:**

- доказать “data cache read-materialized/non-dirty only” или реализовать dirty/writeback;
- определить flush-before-device-read;
- определить invalidate-after-device-write;
- определить domain-tag behavior;
- отделить VLIW fetch invalidation от data invalidation;
- запретить compiler lowering без явных cache barriers.

---

## 7. L7 ExternalAccelerators слишком богаты model APIs для fail-closed ISA

L7 carrier fail-closed: `Execute` throws, `WritesRegister=false`, no backend execution/writeback/commit/fallback . Но рядом уже есть token lifecycle, poll/wait/cancel/fence model, register ABI, backend staging, fake backend, commit coordinator  .

**Недостаток:** model API выглядит почти как полноценная accelerator ISA, но production protocol отсутствует. Это повышает риск evidence inversion: “раз есть token/fence/register ABI, значит ACCEL_* почти готов”. Нет.

**Что нужно:** разделить L7 на tiers:

1. **L7-T0 carrier-only**: current.
2. **L7-T1 read-only query/poll**: только CSR-like non-memory publication, если ADR разрешит `rd`.
3. **L7-T2 submit without memory commit**: token handle only.
4. **L7-T3 staged backend execution**.
5. **L7-T4 architectural commit/fence/wait/cancel**.
6. **L7-T5 production backend protocol**.

Сейчас всё это описано как future-gated, но не разложено в executable maturity levels.

---

## 8. Compiler/backend contract правильно запрещает lowering, но будущий capability model слишком coarse

`CompilerBackendLoweringContract` требует `ProductionExecutable` и набор gates для DSC/L7; reject’ит descriptor/parser/model evidence, hardware coherence assumptions и successful partial completion .

**Недостаток:** состояния `DescriptorOnly`, `ParserOnly`, `ModelOnly`, `ExecutableExperimental`, `ProductionExecutable` слишком крупные. Для реального backend lowering нужны finer-grained capabilities:

- supports virtual addressing;
- supports physical-only;
- supports all-or-none commit;
- supports precise retire faults;
- supports async completion;
- supports fence/wait/poll;
- supports cache invalidate;
- supports flush-before-read;
- supports cancellation;
- supports deterministic FP semantics;
- supports token handle register writeback;
- supports memory commit but not register result;
- supports read-only query/poll.

Иначе backend будет вынужден либо полностью запрещать всё, либо получать слишком широкое “ProductionExecutable”.

---

## 9. Partial completion закрыт как current feature, но progress diagnostics создают будущий semantic trap

DSC current contract утверждает: `AllOrNone` единственный successful completion policy; progress/poll/wait/fence diagnostics do not publish memory . Token diagnostics имеют `CanPublishMemory=false`, `CanSetCommitted=false`, `IsAuthoritative=false` .

**Недочет:** progress diagnostics уже содержат bytesRead/bytesStaged/elementOperations/modeledLatency. Это полезно, но в будущей async архитектуре может быть ошибочно превращено в partial-success observable state.

**Что нужно:** отдельный ADR “progress observability”:

- что может видеть user code;
- что может видеть debugger/telemetry;
- что может видеть poll;
- может ли progress survive squash/trap;
- запрещено ли использовать bytesStaged как partial completion.

---

## 10. StreamEngine DMA helper выглядит как DMA, но архитектурно это synchronous helper

BurstIO явно говорит, что large transfers use DMAController, но helper drives DMA cycles synchronously before returning . Для write path еще хуже с точки зрения архитектурной чистоты: current helper writes destination surface first, then drives DMA bookkeeping synchronously .

**Недостаток:** название “DMA route” может создавать false mental model. Это не architectural DMA overlap, не asynchronous completion, не DMA commit ordering.

**Что нужно:** переименовать или жестко документировать как:

- `SynchronousDmaBookkeepingHelper`;
- not overlap;
- not commit authority;
- not completion token;
- not ordering evidence.

И обязательно не использовать этот path как основание для future async DMA.

---

# 2. Зональный анализ

## DmaCompute / DmaStreamCompute

**Главная проблема:** DSC находится между ISA descriptor carrier и runtime compute helper. Carrier не executable, helper реально считает и коммитит staged writes. Это архитектурно безопасно только пока сохраняется fail-closed.

Недочеты:

- compute semantics duplicated с VectorALU;
- physical memory helper не совпадает с будущей IOMMU/cache model;
- token commit не является pipeline retire;
- progress diagnostics могут быть неверно поняты как partial execution;
- all-or-none есть, но cancellation/replay/trap/context-switch policy пока не полноценная pipeline policy;
- descriptor ABI strict, но DSC2 parser-only уже рядом и может создать давление на executable расширения.

**Рекомендация:** следующий безопасный шаг — не executable DSC, а “DSC execution binding ADR”: определить, будет ли DSC отдельным engine или frontend к StreamEngine/VectorALU.

---

## StreamEngine

**Главная проблема:** это реальный executor, но он живет в своем мире: raw stream/vector request, BurstIO, SRF, VectorALU, scalar/predicate retire helper.

Недочеты:

- no unified relation to DSC descriptor ABI;
- DMA helper synchronous, but naming/placement can mislead;
- BurstIO has fallback semantics for legacy entry points, which conflicts philosophically with no-fallback future IOMMU gates;
- direct retire publication is narrow and separate from token retire;
- memory-visible vector effects are not integrated with global conflict service;
- SRF/prefetch invalidation is explicit local protocol, not global coherency.

**Рекомендация:** сделать StreamEngine “architectural service” с clearly exported contracts: memory access, compute semantics, retire publication, exception model, cache invalidation. Сейчас это больше execution subsystem, чем shared authority.

---

## VectorALU

**Главная проблема:** VectorALU — semantic core для typed vector compute, но DSC runtime не обязан им пользоваться.

Недочеты:

- FP/integer exception semantics могут разойтись с DSC helper;
- predicate/tail/mask behavior не применим к DSC, но DSC operations похожи на vector operations;
- reductions/FMA/dot/widening semantics требуют conformance spec, иначе compiler lowering будет нестабильным;
- нет явного “VectorALU semantic profile” для DSC/L7 backends.

**Рекомендация:** выделить `VectorSemanticContract`: element codec, FP behavior, overflow, reduction order, predicate policy, exception counters. Потом решить, какие поверхности обязаны его использовать.

---

## ExternalAccelerators / L7-SDC

**Главная проблема:** L7 уже имеет слишком много model machinery для non-executable ISA. Это полезно как design lab, но опасно как evidence.

Недочеты:

- fake/test backend может психологически выглядеть как production backend;
- register ABI model может быть принят за `rd` writeback promise;
- token lifecycle похож на ISA lifecycle, но не pipeline lifecycle;
- fence/wait/cancel model не связан с global conflict/order service;
- commit coordinator существует отдельно от retire/publication authority;
- conflict manager model-local, не global CPU truth.

**Рекомендация:** формально ввести maturity tiers L7-T0..T5 и запретить compiler/backend capability “ProductionExecutable” без указания tier.

---

# 3. Сквозные архитектурные риски

| Риск | Где проявляется | Почему важно |
|---|---|---|
| Evidence inversion | DSC2 parser, L7 fake backend, token/progress, compiler sideband | Можно ошибочно принять модельные тесты за ISA execution |
| Semantic duplication | DSC runtime vs VectorALU | Разные результаты для одинаковых операций |
| Publication split | DSC commit, L7 commit, StreamEngine retire | Нет единого architectural visibility order |
| Address-space split | physical DSC helper, StreamEngine backend/IOMMU, L7 guarded memory | Future IOMMU execution будет опасным без единого контракта |
| Ordering gap | resource masks есть, global conflict authority нет | Async overlap нельзя включать безопасно |
| Cache dirty ambiguity | explicit invalidation есть, coherent/dirty writeback нет | DMA/accelerator reads могут видеть stale data |
| Compiler capability coarseness | ProductionExecutable слишком широк | Backend может получить слишком много прав одним флагом |
| Naming drift | “DMA via DMAController” in StreamEngine | Может быть принято за async DMA |

---

# 4. Что сделано правильно — коротко

- Fail-closed границы для lane6 DSC и L7 сохранены.
- Текущее/future разделение в документации в целом выдержано.
- Phase13 правильно запрещает использовать downstream evidence как upstream executable proof.
- `CompilerBackendLoweringContract` правильно запрещает production lowering без full gates.
- Cache/coherency честно описаны как non-coherent explicit protocol, не coherence.

---

# 5. Приоритетные исправления

## P0 — перед любым executable DSC/L7

1. **Unified publication/retire contract** для DSC, StreamEngine, L7, DMA.
2. **GlobalMemoryConflictService** с CPU load/store/atomic hooks.
3. **Address-space/backend contract**: physical/virtual/IOMMU/domain/epoch/no-fallback.
4. **Cache visibility contract**: flush-before-read, invalidate-after-write, dirty-line decision.
5. **DSC execution binding ADR**: DSC standalone vs StreamEngine/VectorALU frontend.

## P1 — перед compiler/backend production lowering

1. Fine-grained capability model вместо одного `ProductionExecutable`.
2. Vector semantic conformance profile.
3. L7 maturity tiers.
4. Negative tests against evidence inversion.
5. Documentation path/link linting для traceability.

## P2 — cleanup / claim-safety

1. Переименовать или жестче оговорить synchronous DMA helper paths.
2. Развести telemetry/progress/debug observability и architectural observability.
3. Уточнить, что SRF/prefetch/cache observers — invalidation fan-out, не memory ordering authority.

---

# 6. Итоговая оценка

**HybridCPU сейчас архитектурно осторожен, но не архитектурно завершен.** Его философия — typed slots, fail-closed, explicit authority, descriptor/execution separation, no hidden fallback — выдержана. Но именно эта философия требует следующего шага: не добавлять новые helper/model поверхности, а собрать уже существующие поверхности в единый обязательный контракт исполнения.

**Главный запрет:** не переходить к executable lane6 DSC, executable L7, async DMA overlap или compiler production lowering, пока не закрыты retire/publication, ordering/conflict, addressing/IOMMU и cache visibility. Otherwise модель будет выглядеть работоспособной по тестам, но архитектурно станет несогласованной.