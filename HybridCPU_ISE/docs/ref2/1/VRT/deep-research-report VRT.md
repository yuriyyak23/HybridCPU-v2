# Аудит состояния Virtualization в HybridCPU-v2

## Краткий вердикт и зафиксированная база исследования

**Полноценная активация Virtualization на исследованном состоянии репозитория запрещена.** Фактически реализован не исполняющий виртуализационный backend, а **замороженный VMX compatibility frontend**, набор read-only/denied projections, typed fault-only admission после materialization lane 7 и fail-closed retire. Любая производственная VMX micro-op в текущем pipeline завершается `SecurityPolicyViolation`; успешный VM entry/exit, mutable VMCS, активный VMCS pointer, VMWRITE, VMCALL backend и архитектурная VMREAD writeback-семантика отсутствуют. citeturn24view0turn24view1turn24view2turn23view0

**Максимальный безопасный объявляемый режим:**

> **VMX Compatibility Projection — frozen, read-only, fail-closed, no-emission by default.**

В этом режиме допустимы:

- распознавание замороженного VMX opcode vocabulary;
- типизированное размещение VMX carrier в serializing system lane 7;
- выдача SafetyVerifier-сертификата, который разрешает только fault-only transport;
- ограниченные read-only compatibility projections из явно предоставленных neutral runtime descriptors;
- admitted-denied projection VMCALL trap без backend execution, completion publication и retire publication;
- negative conformance, provenance, nested-composition и migration-readiness проверки, не являющиеся доказательством работающего guest execution. citeturn30view0turn23view0turn30view1turn24view3

Нельзя объявлять:

- «virtualization enabled»;
- «VMX execution supported»;
- «VMREAD/VMWRITE supported» без уточнения projection-only;
- «nested virtualization supported» как исполняемый L2 runtime;
- «hypercalls supported»;
- «VMCSv2 is the virtualization state store»;
- «migration-ready virtualization»;
- «SecureCompute under VMX». Репозиторий сам формулирует текущий VMX как compatibility frontend/projection, а не authority owner, и отдельно исключает успешный VMX backend, VMCS state store и VMX-owned secure execution. citeturn3view0turn31view1

### Фиксация состояния

| Параметр | Зафиксированный факт | Уверенность |
|---|---|---:|
| Remote | `github.com/yuriyyak23/HybridCPU-v2` | Высокая |
| Ветка | `master` | Высокая |
| SHA | `d3814d1f332f083034d3b245f807a45f97792070` | Высокая |
| Commit | `upd refactor 086082026`, 6 августа 2026 года | Высокая |
| Масштаб последнего commit | 221 изменённый файл, 5063 additions, 2587 deletions | Высокая |
| История перед SHA | Последовательные изменения pipeline/FSP/SMT/authority refactor в период 2–3 августа 2026 года | Высокая |
| Локальная рабочая копия | Не была предоставлена; попытка clone в исследовательской среде завершилась DNS-ошибкой, поэтому сравнение uncommitted/local state с remote невозможно | Высокая |
| Статус CI | Не установлен: содержимое workflow в данной среде не было успешно извлечено, а тестовый suite локально не запускался | Высокая |

SHA, дата и состав последнего изменения подтверждаются страницами commit и истории. Последний commit затрагивает Virtualization WhiteBook, audit tests и `SafetyVerifier.VirtualizationAdmission.cs`, но не вводит успешный backend execution. citeturn4view0turn5view0turn13view0

Запрошенный пользователем путь `docs/ref2/VirtualizationActivationPlan/` на фиксированном SHA отсутствует; вариант `Documentation/ref2/VirtualizationActivationPlan/` также отсутствует. Поэтому буквальная построчная сверка исходного плана невозможна. В репозитории остались `VirtualizationActivationPlanAuditGuardTests.cs` и новый `Documentation/Virtualization WhiteBook/`, который был использован только как перечень гипотез для проверки по production source и tests. citeturn11view0turn11view1turn10view0turn21view2

## Фактическая архитектура и end-to-end путь

### Карта production path

Уровни доказательности в таблице:

- **A** — непосредственно исполняемый production source;
- **B** — тест, проверяющий конкретное runtime/static свойство;
- **C** — документационный claim, использованный только как вспомогательный;
- **D** — отрицательный вывод по отсутствию найденного production пути.

| Переход | Owner и носитель | Допустимые исходы | Точка отказа | Доказательство |
|---|---|---|---|---|
| Frozen ISA vocabulary | `VmxCompatDecodeBoundary`; `VmxCompatDecodeRequest` с opcode/registers и четырьмя boolean gates | `Allowed`, unknown opcode, descriptor/capability/scheduling/no-emission denied | Любой false gate или opcode вне frozen set | `CloseToHSL/Core/Virtualization/Compatibility/Frontend/Decode/VmxCompatDecodeBoundary.cs:4-77,79-94` — **A**. citeturn30view0 |
| Compatibility decode → payload | Compatibility frontend создаёт `VmxInstructionPayload`; архитектурного authority token здесь нет | Payload либо empty denied result | Проверка состоит из переданных caller booleans; сам decoder не устанавливает provenance | `VmxCompatDecodeBoundary.cs:13-35,39-77` — **A**. Тест подтверждает, что эти booleans не создают E1 certificate: `VmxTypedSafetyVerifierAdmissionTests.cs:1643-1672` — **B**. citeturn30view0turn22view3 |
| Canonical carrier | `VmxMicroOp`; носитель — `InstructionIR`, `Rd/Rs1/Rs2`, placement metadata | VMX carrier, hard-pinned lane 7, `VmxSerial` | Opcode/IR mismatch вызывает exception; неизвестный frozen operation не исполняется | `CloseToHSL/Core/Pipeline/MicroOps/Types/MicroOp.IO.cs:86-128,134-165,187-287` — **A**. citeturn24view0turn25view1 |
| Scheduler/materialization | Stage-B materialization, source slot 7 → physical lane 7; SafetyVerifier выдаёт E1 | Fault-only certificate либо typed deny | VT owner/context/domain/source lane/working lane/replay mismatch, duplicate issuance | `SafetyVerifier.VirtualizationAdmission.cs:134-227` — **A**; canonical lane test `VmxTypedSafetyVerifierAdmissionTests.cs:1531-1597` — **B**. citeturn23view0turn22view3 |
| E1 validation | Единственный issuer — конкретный live `SafetyVerifier`; carrier хранит opaque certificate | `ValidForFaultOnlyTransport` либо typed denial | Foreign issuer, copied certificate, mutation, generation, VT, context, domain, slot, bundle или replay mismatch | `SafetyVerifier.VirtualizationAdmission.cs:228-299` — **A**; negative tests `VmxTypedSafetyVerifierAdmissionTests.cs:1170-1441` — **B**. citeturn23view0turn22view3 |
| Execute через `VmxMicroOp` | Execution owner — сама micro-op, но backend owner отсутствует | Только valid fault effect | Любая frozen VMX operation преобразуется в `SecurityPolicyViolation` | `MicroOp.IO.cs:134-145` — **A**. citeturn24view0 |
| Execute через dispatcher | `ExecutionDispatcherV4` | `ExecutionResult.VmxFault()` и captured fault retire effect | Успешной ветви нет | `CloseToHSL/Core/Execution/Dispatch/ExecutionDispatcherV4.VmxCompatibility.cs:9-34` — **A**. citeturn24view1 |
| VMCALL compatibility trap projection | `VmxCompatibilityAdmissionService`; neutral `TrapRequest`, `NeutralTrapResult`, runtime admission, hypercall admission | Admitted-denied trap projection либо decode/projection/runtime/trap-policy deny | Missing neutral backend owner; projection-only route | `VmxCompatibilityAdmissionService.Traps.cs:58-149,150-195,241-274` — **A**. citeturn24view3turn25view0 |
| Trap completion route | Neutral `TrapCompletionRouteService`; route descriptor и publication fence | Структурно возможны completion-only и retire-capable descriptors, но VMX frontend всегда строит `ProjectionOnlyDenied` | Compatibility projection не может быть route authority; production VMCALL не публикует completion или retire | `VmxCompatibilityAdmissionService.Traps.cs:241-262` — **A**; `VmxTrapCompletionRouteOwnerTests.cs:820-946` — **B**. citeturn25view0turn22view7 |
| Retire effect | `CPU_Core.ApplyRetiredVmxEffect`; носитель — `VmxRetireEffect` → `VmxRetireOutcome` | Invalid effect → no-op; valid effect → fault | Все valid VMX effects идут через `ApplyRemovedFrontendFailClosedEffect` | `CloseToHSL/Core/Pipeline/Retire/Evidence/CPU_Core.PipelineExecution.VmxRetire.cs:19-60` — **A**. citeturn24view2turn25view2 |
| Architectural publication | `RetireCoordinator` публикует register/PC records только при соответствующих flags в outcome | Ноль либо register/SP/PC records | Текущий fault outcome не содержит register writeback/redirect | `CPU_Core.PipelineExecution.VmxRetire.cs:75-116` — **A**; VMX canonical test ожидает `HasRegisterWriteback == false` — **B**. citeturn25view2turn22view3 |
| Replay/FSP boundary | `CPU_Core` инвалидирует replay generation и assist runtime при flushing VM transition | Deterministic invalidation | В текущем fault-only path нет успешного VM transition owner | `CPU_Core.PipelineExecution.VmxRetire.cs:28-44,61-73` — **A**. citeturn25view2 |
| VMREAD projection service | Runtime admission → `VmcsReadOnlyValueProjectionService`; sources передаются явно: execution, memory, completion, privileged state | Read-only projected value либо denied | Нет descriptor/evidence/domain/source/conformance; unknown/control/host fields denied | `VmxCompatibilityAdmissionService.cs:84-178` — **A**. citeturn30view1 |
| VMREAD как архитектурная инструкция | Production `VmxMicroOp`/dispatcher/retire | Только fault | Projection service не подключён к production VMX execute/writeback chain | Сопоставление `VmxCompatibilityAdmissionService.cs:152-178` с `MicroOp.IO.cs:134-145` и dispatcher `:22-33` — **A/D**. citeturn30view1turn24view0turn24view1 |
| VMCS representation | Generated compatibility blocks | В основном read-only constants/defaults; отдельный `ExitInfoBlock` принимает internal projection updates | Не является canonical mutable virtualization state store | `Generated/VmcsProjection/VmcsV2Blocks.cs:14-29,73-217,219-285` — **A**. citeturn30view2turn31view4 |
| Nested compatibility bridge | `NestedDomainController` + `ShadowVmcsNestedProjectionService`; bridge создаёт descriptor с runtime authority | Projection enable/disable validation | Hardcoded parent/child IDs; доказательства gates приходят из request; production L2 execution path не установлен | `NestedDomainControllerCompatibilityProjection.cs:6-58` — **A**. citeturn31view3 |
| Capability projection | Generated `CapabilityDescriptorSetSchema`; grant-first mask projection | Только известные compatibility bits; unknown bits фильтруются | Schema/artifact mismatch или неизвестные bits | `CapabilityDescriptorSetSchema.cs:32-107` — **A**. citeturn31view2 |
| Compiler boundary | `VirtualizationNoEmissionRegressionGate.cs` присутствует как отдельная compiler boundary surface | No-emission gate | Controlled production emission не доказана | Directory inventory — **B/D**. citeturn31view0turn11view2 |
| Observability/migration | Compatibility projection может читать neutral descriptors; VMCS projection blocks не владеют checkpoint state | Recomputed/read-only evidence либо deny | Нет canonical virtualization checkpoint owner и успешного restore generation для VM execution | `VmcsV2Blocks.cs:159-203,219-285`; closure tests inventory — **A/B**. citeturn31view4turn11view2 |

### Важное разделение двух путей

В репозитории существуют два внешне похожих, но архитектурно различных пути:

1. **Production instruction path:** decoder/IR → `VmxMicroOp` → lane 7 → E1 fault-only admission → execute fault → retire fault.
2. **Compatibility service path:** caller вручную формирует request с descriptors/evidence booleans → read-only VMREAD projection или admitted-denied VMCALL trap projection.

Второй путь не является backend первой цепочки. Он не доказывает, что guest instruction получает projected value или что VMCALL достигает hypervisor handler. Статический hardening test перечисляет production VMX chain через `MicroOp.IO.cs`, `ExecutionDispatcherV4.VmxCompatibility.cs` и `CPU_Core.PipelineExecution.VmxRetire.cs`; compatibility trap handler проверяется как отдельная boundary surface. citeturn22view8turn24view0turn24view1turn24view2

### VMX/VMCS boundary по операциям

| Операция | Реально работающая семантика | Intentional denial / placeholder | Мёртвый или дублирующий контур |
|---|---|---|---|
| `VMREAD` | Out-of-band read-only projection после compatibility decode, projection validation и runtime admission. Источники передаются явно через execution/memory/completion/privileged descriptors. citeturn30view1 | Production instruction всегда faults; control, host, unknown и source-less поля должны быть denied. Архитектурная register writeback отсутствует. citeturn24view0turn24view1turn31view1 | `VirtualCpuBlock.GuestPc/GuestSp` и другие VMCS-shaped classes выглядят stateful, но их setters отсутствуют либо значения constant/default; их нельзя считать backend. citeturn30view2turn31view4 |
| `VMWRITE` | Нет работающей production write semantics | Любая production VMWRITE становится security fault; mutable VMCS owner отсутствует | Наличие VMCS classes и mutable internal helpers в отдельных projection blocks не означает admitted VMWRITE |
| `VMCALL` | Compatibility service способен построить neutral intercept result при валидных runtime/trap descriptors | Hypercall backend request создаётся как `MissingNeutralOwner`; completion и retire запрещены | Сервисный admitted-denied path не вызывается production `VmxMicroOp.Execute` |
| `VMXON/OFF`, `VMLAUNCH/RESUME` | Frozen opcode recognition, typed lane placement и fail-closed transport | Backend execution отсутствует | Любая прежняя x86-подобная VMX state machine должна считаться удалённой или неавторитетной |
| `VMPTRLD/ST`, `VMCLEAR` | Только vocabulary и register metadata | Active VMCS pointer state и VMCS manager отсутствуют | VMCS descriptor objects не заменяют active pointer owner |
| `INVEPT/INVVPID` | Vocabulary/metadata/capability projection | Нет доказанного translation invalidation effect, привязанного к authoritative guest address-space owner | Нельзя напрямую вызывать host TLB/IOMMU invalidation |
| `VMFUNC`, `VMSAVEX`, `VMRESTX` | Vocabulary, capability bits и fail-closed metadata | Нет production transition/save/restore semantics | Read-only compatibility snapshot не является architectural save state |

## Сверка с базовым планом и матрица разрывов

Поскольку каталог `VirtualizationActivationPlan` удалён или переименован до фиксированного SHA, ниже приведена **реконструкция его surviving obligations** из `VirtualizationActivationPlanAuditGuardTests`, набора `VmxRefactoring` tests и текущего closure matrix. Это не подмена утраченного плана: отсутствие исходного versioned plan само является разрывом traceability. Репозиторий содержит более сорока специализированных VMX refactoring tests, включая tests для authority boundaries, generated projections, no-emission, nested, lanes, migration, trap completion и SafetyVerifier admission. citeturn11view2turn21view2turn31view1

| Пункт/обязательство плана | Проверенный факт | Статус | Риск ложного claim | Требуемое исправление |
|---|---|---|---|---|
| Зафиксировать compatibility ABI | Frozen opcode set явно перечислен; decode unknown opcode fail-closed | **Подтверждён** | Низкий, если claim ограничен vocabulary | Сохранить generated parity и ABI hash gate |
| Сделать SafetyVerifier единственным admission authority | E1 выпускается конкретным SafetyVerifier после Stage-B и связан с VT/context/domain/slot/replay | **Частично реализован** | Высокий: decode всё ещё принимает plain booleans, а E1 разрешает только fault transport | Заменить caller booleans typed proof references; отдельно определить E2 backend authorization |
| Подключить VMX к production pipeline | Carrier, lane 7, execute и retire подключены | **Подтверждён только fail-closed contour** | Критический: наличие end-to-end path легко принять за working execution | Не расширять claim, пока execute не имеет neutral backend owner |
| Реализовать VMREAD | Есть read-only service projection; production instruction faults | **Частично реализован / неверно называть ISA support** | Критический | После определения owners реализовать минимальный architectural read effect и register writeback через retire |
| Реализовать VMWRITE | Успешной ветви нет | **Отсутствует, intentional deny** | Критический | Сначала решить, нужен ли вообще mutable compatibility field contract; не создавать generic dictionary VMCS |
| Реализовать VMCALL trap | Neutral trap projection есть | **Частично реализован** | Высокий: admitted trap можно ошибочно назвать hypercall | Добавить neutral hypercall owner, completion semantics и explicit retire effect |
| Разделить trap, completion и retire | Структурное разделение реализовано | **Подтверждён как scaffolding** | Высокий: positive descriptors существуют, но production VMX их не использует | Подключать только после backend-effect ownership proof |
| Capability provenance | Generated grant-first projection schema существует | **Частично реализован** | Средний/высокий: projection bit может быть принят за execution entitlement | Capability grant и backend entitlement должны быть разными types |
| Host evidence non-leak | Schema и tests предполагают guest-visible projection; VMCS dirty/debug blocks возвращают `ContainsHostEvidence == false` | **Частично подтверждён** | Высокий без runnable negative integration | Добавить end-to-end host-evidence poisoning tests |
| Nested virtualization | Есть neutral descriptor bridge и shadow projection service | **Projection-only / не runtime-ready** | Критический | Удалить hardcoded domain identities, определить nested owner graph и L2 execution contract |
| Compiler no-emission | Отдельный regression gate и tests присутствуют | **Подтверждён как safety policy; controlled emission не доказан** | Высокий при включении feature flag | Оставить no-emission default; controlled emission только после activation manifest |
| Memory/I/O/IOMMU lanes | Отдельные boundary tests и projection surfaces существуют; VMCS lane/stream blocks read-only/default | **Частично реализован вне VMX backend** | Критический | Нужен neutral domain/lane transaction owner, а не VMCS-side effect |
| Lane 6/7 passthrough isolation | Nested descriptor имеет gate `Lane6Lane7PassthroughBlocked` | **Policy representation есть; runtime enforcement не доказан в этом аудите** | Высокий | Proof должен связывать descriptor, scheduler lane assignment и completion transport |
| Checkpoint/restore | Есть migration/evidence tests и readiness vocabulary; canonical VM execution state отсутствует | **Не готово для active virtualization** | Критический | Определить canonical payload, epochs, pending effects и restore invalidation |
| SecureCompute separation | VMX не является SecureCompute owner по declared model/tests | **Подтверждён как запрет** | Высокий при добавлении compatibility fallback | Сохранить отдельные capability/admission/effect owners |
| Generated artifacts | Capability and VMCS schemas присутствуют | **Частично подтверждён** | Средний: hardcoded hash без проверенного generator/CI run не доказывает freshness | Reproducible generator + dirty-tree check + artifact lineage |
| Production composition root | Production instruction path найден и fail-closed; positive projection services существуют отдельно | **Activation composition отсутствует** | Критический | Явный composition manifest с concrete owners и feature-state machine |
| CI/release evidence | README содержит validation commands, но workflow и actual green run не подтверждены этим исследованием | **Не доказан** | Высокий | Required CI jobs с pinned SDK, generators, integration и determinism seeds |
| Versioned activation plan | Запрошенный path отсутствует | **Устарел/удалён** | Высокий traceability risk | Восстановить plan/decision log с SHA-bound requirement IDs |

Текущий WhiteBook достаточно точно описывает fail-closed состояние — partial VMREAD projection, denied VMWRITE, admitted-denied VMCALL, отсутствие backend/VMCS manager и production publication. Но это совпадение не превращает документацию в proof: решающими являются unconditional fault в production execute и fault-only E1 certificate. citeturn31view1turn24view0turn24view1turn23view0

## Реестр реальных блокеров

### Критический блокер: отсутствует canonical virtualization runtime owner

**Механизм.** В production path нет объекта, владеющего guest execution mode, VM level, active domain, guest address space, intercept policy, VM transition epoch и pending virtualization effects. VMCS-shaped classes преимущественно возвращают read-only defaults; `VmxRootControlBlock`, NPT, bundle, timer, interrupt, stream, security и capability blocks не являются изменяемым runtime state. citeturn30view2turn31view4

**Нарушаемые инварианты.** Единственный canonical owner architectural state; host/guest/nested authority isolation; deterministic replay and retire; отсутствие state reconstruction из compatibility projection.

**Минимально корректное решение.** Ввести neutral `VirtualizationRuntimeContext` на virtual thread/domain, владеющий только доказанными HybridCPU concepts:

`VtId`, `OwnerContextId`, `DomainId`, `AddressSpaceId`, `VirtualizationLevel`, transition generation, execution state reference, memory/IOMMU authority reference, trap policy reference и pending effect sequence.

VMCS compatibility layer получает только read-only projection view. Она не может хранить или восстанавливать runtime context.

**Запрещённые shortcuts.** Generic `Dictionary<VmcsField, ulong>`; перенос x86 VMCS semantics; выбор текущего VM по `VMPTRLD` без HybridCPU ISA contract; получение guest state из host CPU state; создание owner внутри compatibility frontend.

**DoD.** Для каждого field/effect задокументирован ровно один owner; duplicate-owner static test; context snapshot deterministic; no compatibility type exposes mutation; context identity входит в replay/admission certificates.

### Критический блокер: SafetyVerifier admission разрешает только fault transport

**Механизм.** E1 certificate прямо объявлен неспособным авторизовать backend, completion или retire; свойства `BackendExecutionAuthorized`, `CompletionPublicationAuthorized` и `RetirePublicationAuthorized` всегда false. Certificate также явно не содержит accepted numeric leaf, address-space identity, descriptor identity, capability grant, evidence policy или restore generation. `SafetyVerifier.VirtualizationAdmission.cs:51-120`. citeturn23view0

**Инварианты.** SafetyVerifier — единственный legality/admission authority; capability/evidence provenance; no hidden backend; replay-safe VT/FSP execution.

**Минимально корректное решение.** Не расширять E1 неструктурированными flags. Ввести последовательные typed authorities:

- E0 — decode/descriptor provenance;
- E1 — carrier placement and replay binding;
- E2 — operation-specific backend admission, привязанный к canonical owner, exact operation, capability grant identity, evidence policy identity и restore generation;
- E3 — effect publication certificate, привязанный к completion/retire sequence.

Каждый следующий сертификат создаётся только SafetyVerifier на основе предыдущего и current canonical snapshots.

**Запрещённые shortcuts.** Установить текущие false properties в true; использовать decode booleans; позволить backend проверять capability самостоятельно; выдавать reusable per-domain token; разрешить FSP перенос без повторной validation.

**DoD.** Mutation/copy/cross-VT/cross-domain/cross-replay/cross-restore tests для каждого certificate; zero public constructors; deterministic identity digest; invalidation при replay, migration restore, domain switch и policy revocation.

### Критический блокер: production execute безусловно faults

**Механизм.** `VmxMicroOp.Execute` для любого frozen opcode создаёт `VmxRetireEffect.Fault(...SecurityPolicyViolation)`. Dispatcher независимо возвращает `ExecutionResult.VmxFault()`. Это не placeholder branch после failed lookup, а единственная реализованная ветвь. `MicroOp.IO.cs:134-145`; `ExecutionDispatcherV4.VmxCompatibility.cs:22-33`. citeturn24view0turn24view1

**Инварианты.** Единственный effect owner; precise exception/retire; decoder не может обходить verifier.

**Минимально корректное решение.** После появления runtime owner и E2 выделить neutral `VirtualizationExecutionEffect` с закрытым набором outcomes: read projection, trap request, domain transition, translation invalidation или explicit deny. До этого unconditional fault должен остаться.

**Запрещённые shortcuts.** Вызов compatibility service напрямую из `Execute` с booleans `true`; switch по VMX mnemonic с direct state mutation; host hypervisor API; silent no-op для unsupported opcode.

**DoD.** Каждая admitted operation создаёт один immutable effect; execute не мутирует architectural state; retire применяет effect строго один раз; rejected operation создаёт precise trap/fault без writeback.

### Критический блокер: VMREAD projection не является архитектурной VMREAD

**Механизм.** `AdmitVmReadProjection` способен получить value из явно предоставленных runtime sources, но production `VmxMicroOp` этот сервис не вызывает, всегда создаёт fault, а retire не публикует register record. citeturn30view1turn24view0turn25view2

**Инварианты.** Architectural effects публикуются только на retire; projection read-only; compatibility не backend; precise register ownership.

**Минимально корректное решение.** Первая positive instruction slice должна быть очень узкой: один или несколько уже имеющих neutral owner полей, например execution-owned guest PC/SP/flags либо explicitly approved privileged fields. SafetyVerifier E2 фиксирует field ID, source owner identity, source epoch, destination register, VT/domain и replay generation. Execute материализует immutable read effect; retire делает единственный register write.

**Запрещённые shortcuts.** Чтение из `VirtualCpuBlock`; arbitrary field dictionary; computed fallback zero; direct register write в frontend/execute; расширение на host/control fields; называние service projection архитектурным VMREAD.

**DoD.** Positive writeback test через полный decode→issue→execute→retire; x0 deny; stale source epoch deny; cross-VT/domain deny; replay determinism; FSP-on/off equivalence; no writeback on trap.

### Критический блокер: VMWRITE не имеет допустимого owner или effect model

**Механизм.** Frozen opcode и register dependency metadata существуют, но production execute всегда faults. Mutable VMCS store и active pointer отсутствуют. citeturn24view0turn31view1

**Инварианты.** Единственный state owner; compatibility read-only; no hidden runtime backend; fail-closed control authority.

**Минимально корректное решение.** До реализации определить небольшой HybridCPU-native set writable compatibility controls, если они вообще нужны. Каждый control должен проецироваться в command к конкретному neutral owner, а не в VMCS field. Команда валидируется SafetyVerifier и применяется at retire с new generation.

**Запрещённые shortcuts.** Writable VMCS mirror; mutation projection object; x86 VMCS access policy по умолчанию; generic «unknown field stored for forward compatibility».

**DoD.** Unknown/read-only/host field writes denied; atomic retire; rollback exact; checkpoint captures owner state, но не compatibility alias; concurrent VT/FSP tests.

### Критический блокер: VMCALL не имеет neutral backend owner

**Механизм.** После successful projection/trap admission код вызывает `HypercallBackendAdmissionRequest.MissingNeutralOwner`; результат обязан deny backend. Затем route строится через `ProjectionOnlyDenied`, поэтому запрещены completion и retire publication. `VmxCompatibilityAdmissionService.Traps.cs:241-274`. citeturn25view0

**Инварианты.** Host/guest authority isolation; capability provenance; hypercall effect ownership; no host callback fallback.

**Минимально корректное решение.** Определить HybridCPU hypercall ABI независимо от x86 VMCALL: typed leaf ID, exact operand/result registers, capability grant, domain policy, deterministic completion class и error model. Backend должен быть neutral runtime service, возвращающий immutable effect, а не вызывающий host delegate из compatibility layer.

**Запрещённые shortcuts.** Direct host function pointer; map leaf to arbitrary C# service; считать VMX capability bit правом исполнения; разрешить compatibility projection публиковать completion.

**DoD.** Unknown leaf denied; revocation; cross-domain isolation; bounded execution; deterministic result; asynchronous completion ordering, если оно допускается; completion и retire certificates; replay-safe duplicate suppression.

### Критический блокер: completion и retire scaffolding не подключены к VMX authority

**Механизм.** Neutral route service умеет различать completion-only и retire-capable routes, но VMX frontend использует denied route. Compatibility projection как route authority специально отвергается тестом. citeturn22view7turn25view0

**Инварианты.** Precise retire; single publication owner; FSP не меняет observable order; sideband transport не становится architectural owner.

**Минимально корректное решение.** Runtime backend создаёт completion record и publication intent; SafetyVerifier проверяет backend owner, domain, sequence, evidence visibility и replay identity; retire coordinator остаётся единственным architectural publisher.

**Запрещённые shortcuts.** Публиковать trap completion из mapper; трактовать completion record как доказательство retire; повторно конструировать effect из VMCS exit fields; разрешить lane completion transport изменять PC/registers.

**DoD.** Completion-only не пишет architectural state; retire-capable требует E3; duplicate/out-of-order completion denied; FSP/VT interleaving deterministic; rollback не оставляет published sideband residue.

### Высокий блокер: plain booleans на compatibility decode boundary

**Механизм.** `DescriptorValidated`, `CapabilityValidated`, `SchedulingValidated` и `NoEmissionValidated` передаются как `bool`. Decoder не проверяет issuer, generation или subject identity. Это безопасно только потому, что production backend остаётся закрытым. citeturn30view0turn22view3

**Инварианты.** SafetyVerifier as sole authority; provenance; no caller self-attestation.

**Минимально корректное решение.** Compatibility decoder принимает opaque typed proof references или prevalidated immutable envelope. Boolean convenience API оставить только internal test adapter либо удалить.

**Запрещённые shortcuts.** Добавить ещё booleans; проверять их только в frontend; считать internal method достаточной security boundary.

**DoD.** Невозможно сконструировать allowed decode из public caller без valid issuer-bound envelope; stale/revoked proofs denied.

### Высокий блокер: nested bridge реконструирует identity

**Механизм.** Compatibility bridge создаёт `NestedDomainDescriptor` с `parentDomainId: 1`, `childDomainId: 2`; host evidence exclusion и lane passthrough gates выводятся из request bitmask. Это годится как projection/conformance adapter, но не как production nested identity. `NestedDomainControllerCompatibilityProjection.cs:28-48`. citeturn31view3

**Инварианты.** Host/guest/nested authority isolation; no state reconstruction; domain identity provenance; lane isolation.

**Минимально корректное решение.** Nested domain IDs выдаёт canonical domain authority; parent-child edge содержит issuer, generation, capability grant, address-space relation и evidence policy. Compatibility bridge только читает этот edge.

**Запрещённые shortcuts.** Hardcoded IDs; derive L2 authority from VMCS12; host feature leakage; lane 6/7 passthrough; reuse parent grant as child grant.

**DoD.** Sibling isolation; nested revocation; cross-parent denial; host evidence poisoning; L1/L2 checkpoint ordering; deterministic nested intercept chain.

### Высокий блокер: memory, I/O, IOMMU и stream path не привязаны к virtualization effects

**Механизм.** VMCS projection blocks для NPT, lane completion, stream, dirty log и security являются read-only/default; `VectorStreamStateBlock.VirtualizationEnabled` всегда false, snapshots отсутствуют, lane counters равны нулю. Это подтверждает отсутствие VMX-owned lane/stream backend, но не доказывает готовность neutral memory subsystem к guest execution. citeturn30view2turn31view4

**Инварианты.** Явные data/domain/sideband boundaries; lane 6 DMA и lane 7 system ownership; IOMMU authority; no aliasing; deterministic VT/FSP.

**Минимально корректное решение.** Определить typed transaction envelope: issuing VT/domain, address-space owner, translated range, IOMMU grant, lane class, effect sequence и completion destination. VMX compatibility получает только projection статуса.

**Запрещённые shortcuts.** Передавать host pointer; использовать VMCS EPT/NPT field как translation root; lane passthrough; получать domain из текущего active thread после issue; публиковать completion напрямую из DMA callback.

**DoD.** TOCTOU tests между admission и translation; IOMMU revocation; cross-domain DMA deny; lane 6/7 contention determinism; replay invalidation; exact fault address provenance.

### Высокий блокер: checkpoint/restore не имеет полного payload authority

**Механизм.** Compatibility fields частично recomputed из runtime descriptors, а VMCS blocks не содержат активное execution/memory/trap/pending-effect state. Stream snapshots отсутствуют по умолчанию. Readiness projection не равна migratable state. citeturn31view1turn31view4

**Инварианты.** Single owner; exact restore; no reconstruction; evidence epochs; deterministic pending completion order.

**Минимально корректное решение.** Checkpoint состоит из canonical runtime context, owner-specific state snapshots, pending effects/completions, replay generation, capability/evidence revocation epochs и lane transaction state. VMCS projection после restore пересчитывается, но не является payload.

**Запрещённые shortcuts.** Сериализовать VMCSv2 object graph; восстанавливать capability из projected bits; терять pending VMCALL/DMA completion; принимать старый E1/E2 certificate после restore.

**DoD.** Byte-stable snapshot; restore generation invalidates old certificates; deterministic resume with FSP on/off; no host evidence; negative corrupted/incomplete snapshot tests.

### Высокий блокер: compiler controlled-emission отсутствует как доказанный production gate

**Механизм.** No-emission regression surface существует, decoder требует no-emission validation, tests перечисляют compiler no-emission gates. Но composition, issuer и release-controlled positive emission path не доказаны. citeturn31view0turn30view0turn11view2

**Инварианты.** Compiler cannot bypass SafetyVerifier; frozen opcode vocabulary; release claim integrity.

**Минимально корректное решение.** Сохранить default no-emission. Controlled emission требует machine-readable activation manifest, exact supported operation slice и runtime feature contract. Compiler может emit только operation, для которой runtime advertises signed schema/version, но runtime всё равно повторно проверяет legality.

**Запрещённые shortcuts.** Feature flag `EnableVmx`; emit based on opcode existence; compiler capability implies runtime capability; fallback to generic system op.

**DoD.** Default builds emit zero VMX; controlled build emits only whitelisted slice; stale schema denied; emitted bundle проходит normal SafetyVerifier; compiler/runtime mismatch integration tests.

### Высокий release-блокер: отсутствует воспроизводимое доказательство CI и generator freshness

**Механизм.** Generated capability artifact имеет source/artifact hashes и canonical flag, но в рамках аудита не подтверждён запуск generator, dirty-tree check или required CI workflow. citeturn31view2

**Инварианты.** Generated artifact provenance; release reproducibility; no stale compatibility map.

**Минимально корректное решение.** Pinned toolchain, single generator command, regenerate-and-diff CI, ABI/schema hash validation, full negative conformance, pipeline determinism matrix и published test artifact tied to SHA.

**Запрещённые shortcuts.** Ручное обновление hash constants; «tests exist» как release evidence; activation на основании README claims.

**DoD.** Clean checkout на SHA воспроизводит generated files byte-for-byte; required jobs green; test report и artifact hashes подписаны SHA.

## Пересобранный dependency-ordered план малых PR

| PR | Scope и authority boundary | Основные изменения | Обязательные tests | Release gate и rollback |
|---|---|---|---|---|
| **Baseline manifest** | Только evidence/reproducibility, без изменения semantics | Восстановить versioned activation requirements; зафиксировать remote/SHA/toolchain; inventory production callers, generated artifacts и supported claims | Clean build; full test discovery; generator diff; source-path guards | Gate: reproducible clean checkout. Rollback: documentation/build-only |
| **Authority ledger** | Определить owners до кода исполнения | Machine-readable таблица state/effect owners; запрет compatibility mutation; static duplicate-owner analyzers | Reflection/static tests на public setters, duplicate storage и forbidden dependencies | Gate: каждый planned field/effect имеет owner. Rollback: no runtime change |
| **Canonical runtime context** | Neutral virtualization owner per VT/domain | Immutable identities, generations, execution/memory/trap references; read-only projection interfaces | Cross-VT/domain tests; snapshot determinism; FSP-independent identity | Feature остаётся disabled; rollback удаляет unused composition |
| **Typed admission envelope** | SafetyVerifier authority | Удалить production reliance на booleans; E0/E1 typed proofs; revocation and restore generations | Foreign issuer, copy, mutation, replay, restore, domain tests | Existing fault-only execution unchanged |
| **VMREAD projection hardening** | Read-only source owners | Полностью описать field→owner/source policy; deny unknown/control/host; generator parity | Positive service projections; source epoch; host leakage; migration recomputation | No instruction emission; rollback to all-denied |
| **Architectural VMREAD slice** | Execution effect + retire owner | Одна минимальная field slice; E2 field-bound certificate; immutable read effect; retire register publication | Full pipeline positive/negative; x0; stale epoch; FSP on/off; replay | Disabled feature flag scoped to exact field IDs; rollback returns unconditional fault |
| **Neutral trap route integration** | Trap policy owner, без hypercall backend | Production carrier может создать neutral denied/intercept effect, но без completion publication | Intercept/no-intercept; route authority; duplicate trap; VT order | Claim остаётся «intercept projection», не hypercall |
| **Hypercall owner slice** | Neutral backend service | Typed leaf ABI, capability grant identity, deterministic effect; no host callbacks | Unknown/revoked leaf, cross-domain, replay duplicate, deterministic result | One leaf only; kill switch returns admitted-denied |
| **Completion and retire certificates** | Runtime completion owner → RetireCoordinator | E3 publication proof; completion-only vs retire-capable effects | Out-of-order, duplicate, stale generation, rollback residue, FSP ordering | Rollback blocks publication but preserves precise fault |
| **Memory/IOMMU transaction envelope** | Memory/IOMMU authority | Domain/address-space/lane-bound transactions; explicit sideband completion identity | Cross-domain DMA, revocation, TOCTOU, lane contention, fault provenance | No passthrough; rollback disables guest memory operations |
| **Checkpoint and restore** | Owner-specific snapshot services | Canonical context, capabilities, replay, pending effects, lane state; new restore generation | Round-trip, corruption, incomplete payload, stale certificate, FSP determinism | Migration claim blocked until all owner payloads green |
| **Nested domain identities** | Domain authority, not VMCS bridge | Remove hardcoded IDs; issuer-bound parent-child edge; independent grants/evidence | Sibling/parent isolation, revocation, host evidence, nested checkpoint | Projection-only until L2 execution integration passes |
| **Controlled compiler emission** | Compiler boundary remains subordinate | Activation manifest/schema negotiation; emit exact approved slice | Default zero-emission; stale manifest; unsupported opcode; full generated bundle runtime test | Release requires runtime and compiler artifact hash match |
| **Activation composition** | Production composition root | Explicit state machine: `Disabled`, `ProjectionOnly`, `ReadOnlyInstructionSlice`, later controlled modes | Cold boot, feature transition, rollback, mixed VT, long determinism run | Default remains `ProjectionOnly`; one-step rollback to unconditional fault |
| **Claim release** | Documentation and packaging | Publish exact supported operations, fields, owners, nonclaims and SHA-bound evidence | Independent clean-run report | Запрещено использовать generic label “VMX supported” |

Порядок принципиален. В частности, architectural VMREAD нельзя делать раньше canonical owner и E2 admission; hypercall completion нельзя делать раньше neutral backend owner; nested нельзя строить на VMCS shadow representation; compiler emission нельзя открывать до появления production composition и rollback gate.

## Открытые архитектурные решения

| Решение | Варианты | Trade-off | Рекомендация | Что доказать до кода |
|---|---|---|---|---|
| Форма canonical virtualization state | Один монолитный VM state; набор owner descriptors; event-sourced state | Монолит удобен, но создаёт hidden authority; полностью event-sourced сложен для restore | Per-VT `VirtualizationRuntimeContext` с ссылками на отдельные execution/memory/trap/capability owners и monotonic generations | Unique ownership, snapshot boundaries, VT/FSP determinism |
| Статус VMCSv2 | State store; cached mirror; stateless projection | State store проще для VMX API, но нарушает architecture philosophy | Stateless/read-only compatibility projection; допускается cache только с source epoch и без restore authority | Cache invalidation, no setters, no checkpoint authority |
| Первая positive VMREAD slice | Execution fields; completion fields; privileged controls | Completion fields проще projection-wise, но не доказывают live guest state; controls опаснее | Начать с одного execution-owned field с простым epoch contract; privileged fields позже | Exact owner, stable field schema, register retire semantics |
| Нужно ли вообще VMWRITE | Полный VMCS write; небольшой control command set; permanent deny | Полный write резко расширяет state authority | Permanent deny до появления реальной HybridCPU use case; затем command-oriented controls, не field store | ISA need, owner for every control, rollback semantics |
| Hypercall ABI | x86-like VMCALL leaves; generic service RPC; HybridCPU typed leaves | x86-like semantics не соответствуют архитектуре; generic RPC слишком широк | Frozen HybridCPU typed leaf ABI с capability and deterministic effect class | Operand/result ownership, latency, fault and replay model |
| Admission certificate topology | Один расширенный token; chained E0–E3; central mutable registry | Один token проще, но смешивает authority | Chained issuer-bound certificates с operation-specific payload | Non-forgeability, revocation, bounded lifetime, replay/restore behavior |
| Nested state model | Shadow VMCS12; nested descriptor graph; recursive runtime contexts | VMCS12 удобен для compatibility, но становится hidden backend | Recursive neutral domain/context graph; shadow VMCS только projection | Parent-child authority, L1 intercept ownership, L2 address-space relation |
| Domain IDs | Caller-supplied integers; derived hash; authority-issued handles | Integers легко aliasing; hashes сложны для revocation | Authority-issued opaque identity + generation | Uniqueness, serialization, migration remapping |
| Capability publication | Project all grants; operation-specific projection; static mask | Broad projection leaks authority shape | Operation-specific guest-visible projection; execution entitlement отдельного типа | Host non-leak, unknown-bit behavior, revocation |
| Memory virtualization root | VMCS EPT-like field; runtime address-space descriptor; host IOMMU root | VMCS/host root создают hidden authority | Neutral runtime address-space descriptor, validated IOMMU binding | Translation ownership, invalidation ordering, DMA/lane isolation |
| Completion transport | Lane-local direct publish; central completion queue; retire-owned pull | Direct publish быстрее, но нарушает retire ownership | Sideband queue с immutable record; retire publication отдельно сертифицируется | Ordering, backpressure, duplicate suppression, rollback |
| FSP interaction | Запрет stealing для всех VM ops; stealing carrier before materialization; effect migration | Полный запрет проще, но снижает throughput | VMX/system effects non-stealable; только pre-admission carrier placement может изменяться, после E1 lane/VT binding immutable | Equivalence FSP on/off, exception order, ownership |
| Migration representation | Serialize projections; serialize owners; hybrid | Projection serialization компактна, но реконструирует state | Serialize canonical owners и pending effects; projections recomputed | Exact round-trip, compatibility schema evolution, stale proof invalidation |
| Activation naming | Boolean enabled; capability levels; operation manifest | Boolean провоцирует overclaim | Versioned operation/field manifest плюс режимы `Disabled`, `ProjectionOnly`, `ExecutionSlice` | Runtime-enforced claim parity |
| SecureCompute relationship | VMX activates secure mode; shared capability; independent authority | Shared path создаёт privilege escalation | Полностью независимые owners; VMX может только читать разрешённую projection или получать deny | No transitive grant, no shared mutable state, host evidence isolation |

## Итоговая оценка готовности и release gate

| Область | Оценка |
|---|---|
| Frozen VMX ABI | Готово для compatibility preservation |
| Decode and carrier formation | Готово только как pre-admission frontend |
| SafetyVerifier placement admission | Сильно реализовано, но намеренно fault-only |
| Production execution | Не реализовано |
| VMREAD compatibility projection | Частично реализовано вне architectural instruction path |
| VMREAD architectural writeback | Не реализовано |
| VMWRITE | Не реализовано, fail-closed |
| VMCALL trap projection | Реализован admitted-denied contour |
| VMCALL backend | Не реализован |
| Completion/retire positive publication | Scaffolding есть, production VMX connection отсутствует |
| Mutable VMCS / active pointer | Отсутствуют и не должны добавляться как скрытый owner |
| Nested execution | Не реализовано; projection bridge частичный |
| Memory/I/O/IOMMU virtualization | Neutral boundary work присутствует по inventory, но VM execution integration не доказана |
| Checkpoint/restore active VM | Не реализовано |
| Compiler no-emission | Подходящий безопасный default |
| Controlled emission | Не доказано |
| Generated artifact lineage | Структура есть, reproducible CI proof не установлен |
| Полная активация | **Запрещена** |

Формальный activation gate может быть открыт лишь тогда, когда одновременно доказаны: canonical runtime owner; E2/E3 SafetyVerifier authority; хотя бы одна full-pipeline positive operation; точный retire effect; negative host/nested/domain tests; FSP-on/off determinism; checkpoint/restore generation; compiler/runtime manifest parity; reproducible green CI на pinned SHA. До этого любое изменение feature flag должно оставлять production `VmxMicroOp` в текущем unconditional fault режиме. citeturn23view0turn24view0turn24view1turn24view2

**Итоговый release claim для SHA `d3814d1f332f083034d3b245f807a45f97792070`:**

> HybridCPU-v2 содержит замороженный VMX compatibility vocabulary, fail-closed typed transport, ограниченные neutral read-only projections и admitted-denied trap scaffolding. Исполняемая host/guest/nested virtualization, VMCS-backed state, успешные hypercalls и architectural VMX retire effects не активированы и не должны считаться поддерживаемыми.