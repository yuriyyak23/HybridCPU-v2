# HybridCPU-v2 Virtualization: доказательный аудит плана, блокеров и пути к безопасной активации

## Вердикт и зафиксированная база доказательств

**Вердикт: полноценную Virtualization в текущем публичном `yuriyyak23/HybridCPU-v2` активировать нельзя.** Максимальный безопасный production-claim на исследованном публичном SHA — **frozen VMX/VMCS compatibility vocabulary + guarded read-only compatibility projections + SafetyVerifier E1 fault-only admission transport**. Канонический VMX execution по-прежнему завершается `SecurityPolicyViolation`; успешного VMCALL backend, архитектурного VMREAD writeback, VMWRITE, nested execution, completion publication или successful VMX retire в публичном runtime нет. `SafetyVerifier` прямо кодирует E1 как `IssuedForFaultOnlyTransport` / `ValidForFaultOnlyTransport` и фиксирует отсутствие numeric leaf, address-space identity, capability/evidence/restore identity и всех трёх поздних полномочий. `VmxMicroOp.Execute` затем безусловно формирует `VmxRetireEffect.Fault(... SecurityPolicyViolation)`. fileciteturn71file0L2-L2 fileciteturn70file0L2-L2

Публичный `master`, который удалось зафиксировать на момент исследования, заканчивается commit **`d3814d1f332f083034d3b245f807a45f97792070`**, сообщение `upd refactor 086082026`, parent `f794ea3`; GitHub показывает 221 изменённый файл и +5,063/−2,587 строк. citeturn6view0

При этом приложенный `VirtualizationActivationPlan(2).zip` **не является планом, привязанным к этому публичному Git subject**. Его собственные evidence-файлы сообщают другую ветку и другой origin:

- `evidence/2026-08-06-clean-head-evidence.json`: `local_only: true`, origin `yaksysdev/HybridCPU-v2`, branch `refactor/compiler-core-authority-boundaries`, clean subject `b6d4871e...`;
- `evidence/2026-08-07-e1-containing-sha-evidence.json`: clean local `55807df7...`;
- `evidence/2026-08-08-a594-clean-sha-evidence.json`: clean local `a594d10...`;
- Phase 19: текущий observation subject — local `ddfffa2...` **плюс dirty worktree**;
- Phase 37 прямо говорит, что P2 closure evidence относится к `ddfffa2... + dirty working tree`, а не к clean containing SHA.  
  См. attachment `VirtualizationActivationPlan/evidence/*.json` и `19_open_decision_backlog.md:174-177`, `37_testing_only_canonical_issue_materialization_composition.md:166-178`.

Это существенно: на публичном `d3814d1...` я не нашёл ни `VirtualizationOperationDecisionManifest`, ни `ResearchVirtualizationRuntimeProbe`; сам `docs/ref2/VirtualizationActivationPlan/19_open_decision_backlog.md` по ожидаемым путям публичного commit отсутствует. Поэтому Phase 34–37 следует считать **доказательствами локальной линии разработки/плана**, но не состоянием публичного remote до тех пор, пока соответствующие исходники не окажутся на проверяемом SHA.

На публичном SHA E1, напротив, действительно существует и покрыт тестами. Тест проверяет opaque attempt-bound certificate, отсутствие public constructor, отказ foreign issuer, mutation, cross-domain, cross-VT, wrong lane, replay mismatch и подтверждает, что после канонической materialization VMX всё равно fault-only и не имеет register writeback. fileciteturn55file0L1-L2

**Уверенность по production verdict: высокая.**  
**Уверенность по фактической реализации локальных Phase 34–37: средняя**, потому что ZIP содержит документацию/evidence, но не локальный исходный Git tree этих SHA; публичный remote их не содержит.

## Фактическая архитектура исполнения и точки разрыва authority

Ниже — не пересказ WhiteBook, а восстановленный путь по production source и tests публичного SHA `d3814d1...`.

| Переход | Фактический owner | Носитель | Что реально разрешено | Где обрывается | Доказательство |
|---|---|---|---|---|---|
| ISA → decode metadata | `OpcodeRegistry` / generated VMX architecture metadata | `InstructionIR`, `VmxInstructionPayload` | VMX privileged, `VmxSerial`, системный singleton; VMREAD/VMWRITE/VMCALL имеют compatibility operand forms | metadata не выдаёт runtime authority | `NonRTL/Arch/VmxSpecTable.cs:1-~180`; generated test сверяет opcode/serialization/lane model. fileciteturn39file0L2-L2 fileciteturn40file0L2-L2 |
| Decode → `VmxMicroOp` | canonical pipeline materializer | `VmxMicroOp` | переносит **индексы** `Rd/Rs1/Rs2`; hard-pinned SystemSingleton lane 7 | runtime values leaf/descriptor не материализованы | `MicroOp.IO.cs:100-190`. fileciteturn70file0L2-L2 |
| Stage-B → E1 | **SafetyVerifier** | opaque `VirtualizationAdmissionCertificate` | VT, context, domain, source/working slot, bundle, replay, carrier digest; только fault transport | leaf, address space, capability, evidence, restore и late authority отсутствуют | `SafetyVerifier.VirtualizationAdmission.cs:1-170,170-380`. fileciteturn71file0L2-L2 fileciteturn72file0L2-L2 |
| Scheduler/materialization → E1 carrier | `MicroOpScheduler` как transport, **не authority** | certificate attached к `VmxMicroOp` | E1 выдаётся после successful Stage-B; lane 7/source slot проверяются повторно | scheduler не может повысить E1 до execution authority | `MicroOpScheduler.SMT.cs:300-500`. fileciteturn73file0L2-L2 |
| Runtime operand capture | **owner отсутствует** | должен появиться immutable runtime operand snapshot | сейчас нет | это один из реальных D2/E2 blockers | `VmxMicroOp` хранит byte register indices; compatibility decoder строит VMCALL qualification непосредственно из `rs1/rs2` register selectors. fileciteturn70file0L2-L2 fileciteturn61file0L2-L2 |
| VMCALL compatibility projection | compatibility frontend + runtime projection policies | `VmxCallTrapProjectionRequest`, `NeutralTrapResult`, route DTOs | admitted-denied trap projection | backend запрашивается как `MissingNeutralOwner`; completion route — `ProjectionOnlyDenied` | `VmxCompatibilityAdmissionService.Traps.cs:1-~300`. fileciteturn69file0L2-L5 |
| Backend admission | runtime hypercall policy | `HypercallBackendAdmissionResult` | только denied states | enum не имеет production `Allowed`; конечный candidate всё равно `DeniedNeutralBackendOwnerRfcAdr` | `HypercallBackendAdmissionPolicy.cs:1-~300`. fileciteturn27file0L2-L2 |
| Owner descriptor | runtime hypercall compatibility fence | `NeutralHypercallBackendOwnerDescriptor` | `Missing` / draft candidate | нет accepted owner и exact numeric leaf | `NeutralHypercallBackendOwnerDescriptor.cs:1-~200`. fileciteturn29file0L2-L2 |
| Execute | `VmxMicroOp` / `ExecutionDispatcherV4` surfaces | `VmxRetireEffect` / `ExecutionResult` | fault only | `VmxMicroOp.Execute` выдаёт `SecurityPolicyViolation`; dispatcher VMX contour тоже fail-closed | `MicroOp.IO.cs:100-190`; `ExecutionDispatcherV4.VmxCompatibility.cs`; dispatcher routes `InstructionClass.Vmx` only through its explicit VMX surface contract. fileciteturn70file0L2-L2 fileciteturn67file0L2-L2 |
| Completion | neutral route/fence scaffolding | route/fence DTO → potential `CompletionRecord` | положительная vocabulary существует, но current VMCALL не достигает её | нет E3-bound nonforgeable publication token | `TrapCompletionRoutePolicy.cs`, `TrapCompletionPublicationFence.cs`. fileciteturn30file0L2-L2 fileciteturn31file0L2-L2 |
| Compatibility completion projection | compatibility projection | `CompletionRecord` | `FromCompatibilityExit` может создать projection record при разрешённом fence result | production caller для VMX пути не найден; это не owner receipt | `CompletionRecordCompatibilityProjection.cs`; transitive search даёт production definition + tests, но не canonical pipeline consumer. fileciteturn34file0L2-L2 fileciteturn35file0L1-L5 |
| Retire | canonical CPU retire owner | `VmxRetireEffect` → `VmxRetireOutcome` | current conversion — fault only, без writeback | нет E5 completion → E6 grant chain | `CPU_Core.PipelineExecution.VmxRetire.cs`. fileciteturn15file0L2-L2 |
| VMCS projection | state-specific runtime owners | read-only projections | Completion/Execution/Memory owners могут дать отдельные guarded values; GuestCr0/Cr4 идут через privileged execution owner; Host execution aliases denied | это direct compatibility projection, не архитектурный VMREAD | `VmcsReadOnlyValueProjectionService.cs:1-430`. fileciteturn42file0L2-L2 fileciteturn54file0L2-L2 |
| Migration | domain/migration runtime | `DomainCheckpointImage`, migration/evidence policies | guest architectural state может мигрировать; compatibility fields recomputed; host evidence rejected/recomputed | нет payload/order contract для активного virtualization operation | migration conformance tests. fileciteturn50file0L2-L2 |

Здесь выявляется очень важный архитектурный факт: **VMCALL сейчас имеет два разных понятия “leaf”**. Canonical micro-op несёт `Rs1` как номер исходного регистра, а `VmxInstructionPayload.FromDecodedRegisters` строит `VmxExitQualification` из самого `rs1`, то есть из selector-а. `VmxExitQualification.Leaf` при этом имеет тип `ushort`, а `VmxRetireEffect.VmCall` также принимает `ushort leaf`. Это допустимо для frozen compatibility projection, но абсолютно недостаточно как runtime hypercall ABI. fileciteturn61file0L2-L2 fileciteturn60file0L2-L2 fileciteturn62file0L2-L2

Отсюда я бы сформулировал E2 proof obligation жёстче плана:

> **E2 должен быть выдан только после одноразовой canonical materialization фактического значения source operand; номер `Rs1`, `ExitQualification.Leaf`, decode DTO или compatibility projection никогда не являются доказательством runtime leaf.**

Это один из ключевых blocker-resolution decisions.

Есть и второй скрытый риск, который план видит, но недооценивает: положительные DTO/factory уже существуют. `TrapCompletionRouteDescriptor` имеет positive-looking runtime publication descriptors, `TrapCompletionPublicationFence` умеет получить разрешение публикации, `CompletionRecord.FromCompatibilityExit` создаёт запись при разрешённом fence, а `VmxRetireEffect` имеет public positive-looking factories `VmcsRead`, `VmcsWrite`, `VmCall` и другие. Пока canonical retire всё сводит к fault, это безопасное dormant vocabulary; **после открытия E3 доверять таким bool/DTO/factory как authority нельзя**. fileciteturn30file0L2-L2 fileciteturn31file0L2-L2 fileciteturn34file0L2-L2 fileciteturn62file0L2-L2

Иными словами, будущий positive path должен быть построен не как:

```text
bool BackendExecutionAuthorized = true
-> bool CompletionPublicationAllowed = true
-> VmxRetireEffect.VmCall(...)
```

а как:

```text
live E1
  -> canonical immutable operand snapshot
  -> SafetyVerifier E2
  -> owner E3 opaque receipt
  -> E4 canonical pipeline handoff
  -> completion-owner E5 one-shot token
  -> retire-owner E6 one-shot grant
  -> architectural publication
```

Ни один поздний этап не должен реконструировать предыдущую authority из полей DTO.

## Сверка Phase 19 с исходным кодом

Последний приложенный Phase 19 уже значительно лучше ранних версий. Особенно правильно, что `19_open_decision_backlog.md:176` прямо исправляет старую трактовку: neutral owner **может быть repository-local**, но обязан быть независим от VMX/VMCS compatibility authority plane и атрибутирован принятой governance-процедурой. Это соответствует архитектуре HybridCPU значительно лучше, чем старое “ждать внешнего субъекта”.

| Фаза | Оценка после проверки | Что подтверждено или требует коррекции |
|---|---|---|
| 00 | **подтверждён blocker** | Production chain действительно не завершена. На `d3814d1` максимум — E1 fault transport + projections. |
| 01 | **подтверждено** | Успешного canonical VMX execution/writeback нет; direct VMREAD projection имеет отдельные callers, но ISA path отсутствует. Поиск `AdmitVmReadProjection` находит compatibility service, tests и showcase, не canonical VMX retire path. fileciteturn68file0L1-L10 |
| 02 | **правильно сформулировано** | Static tests доказывают absence/denial, не authority. Это принципиально важно. |
| 03 | **реальный главный governance blocker** | Accepted D2 отсутствует на публичном SHA; Phase 34 schema в публичном repo вообще не найдена. Решение ниже — repository-local neutral owner + independent review. |
| 04 | **подтверждено** | VMREAD metadata существует, field projection существует, но canonical runtime field-value capture → immutable effect → retire writeback отсутствует. fileciteturn39file0L2-L2 fileciteturn54file0L2-L2 |
| 05 | **подтверждено, scope нельзя расширять** | GuestCr0/Cr4 имеют guarded read-only projection через `PrivilegedExecutionStateProjectionService`; host execution aliases явно denied. Это не архитектурный VMREAD. fileciteturn54file0L2-L2 |
| 06 | **подтверждено** | Current VMCALL carries selectors, не runtime leaf; owner descriptor не имеет accepted state/exact leaf. fileciteturn61file0L2-L2 fileciteturn29file0L2-L2 |
| 07 | **подтверждено** | E2/E3 отсутствуют на public SHA; backend policy заканчивается denial. fileciteturn27file0L2-L2 |
| 08 | **blocker верен, но его нужно усилить** | Проблема не только в отсутствии E3 receipt: существующий completion API основан на constructible DTO/bools. Перед positive E5 это должно быть заменено неприсваиваемым one-shot token. fileciteturn30file0L2-L2 fileciteturn31file0L2-L2 |
| 09 | **подтверждено** | Положительная `VmxRetireEffect` vocabulary существует, но canonical current retire превращает effect в fault. E6 должен быть отдельным authority object. fileciteturn62file0L2-L2 fileciteturn15file0L2-L2 |
| 10 | **подтверждено** | VMWRITE нельзя “починить” созданием VMCS dictionary. Memory-owned fields read-only, write aliases denied. fileciteturn46file0L2-L2 |
| 11 | **подтверждено** | Tests запрещают VMCS12/VMCS02 authority и mutable Shadow VMCS; nested runtime требует runtime authority и сохраняет projection-only fence. fileciteturn45file0L2-L2 |
| 12 | **подтверждено** | IOMMU VMX aliases fail closed; compatibility-owned Lane6/Lane7 descriptors не получают runtime authority; passthrough к VMX frontend отсутствует. fileciteturn46file0L2-L2 |
| 13 | **подтверждено** | SecureCompute capability/evidence фильтруются из `VmxCaps`; compatibility/VMX/VMCS owner sources denied; даже complete neutral secure proof остаётся `AllowedProofOnlyNoExecution`. fileciteturn47file0L2-L2 |
| 14 | **подтверждено** | Compiler diagnostics знают VMX opcodes как runtime/raw transport, но `CompilerHelperEmittable=false`; production emission scans запрещают activation opcodes. fileciteturn48file0L2-L2 |
| 15 | **частично реализованный substrate, blocker остаётся** | Generic migration separation хороша: compatibility completion fields recomputed, host evidence не сериализуется. Но operation-specific pending receipt/order/restore contract отсутствует. fileciteturn50file0L2-L2 |
| 16 | **подтверждено** | Тесты хорошо защищают отрицательные границы, но positive fixture не может назначить owner или leaf. |
| 17 | **архитектурный порядок правильный** | Цепь `E0→E1→D2→E2→E3→E4→E5→E6→E7→E8→E9` разумна. Attachment `17_phase_rollout_and_pr_order.md:55-69`. |
| 18 | **NO-GO подтверждён** | Production D2–E7 отсутствуют; более того, latest local P2 не имеет clean containing SHA. |
| 19 | **правильный активный backlog, но есть stale wording** | Строка 176 корректно говорит repository-local neutral owner; старая строка 85 всё ещё говорит “external neutral-runtime-owner artifact”. Её надо удалить/переформулировать. |
| 20–31 | **правильно архивированы** | Они не должны участвовать в dependency graph; Phase 19:7 и 176 уже это фиксируют. |
| 32 | **подтверждено кодом и тестами** | E1 действительно closed/fault-only. fileciteturn71file0L2-L2 fileciteturn55file0L1-L2 |
| 33 | **рекомендации в основном правильные** | Особенно D2/E2/E3 separation и correction “neutral ≠ external organization”. Но это local-plan evidence. |
| 34 | **архитектурно хороший design, remote не подтверждает implementation** | ZIP говорит о schema/validator/opaque-unissued E2 на `a594...`; публичный `d3814...` такого symbol не содержит. Следовательно, статус для public repo — “not merged/not proven”. |
| 35 | **evidence-only, не activation** | Даже согласно ZIP clean SHA `a594...` содержит fail-closed substrate и всё ещё `neutral_owner_appointed=false`, `exact_numeric_leaf=false`, `e2_issuer_or_carrier=false`. |
| 36 | **полезный research P1, но не production evidence** | По плану direct TESTING-only no-state/no-payload probe; public remote symbol отсутствует. Это допустимый experiment, но не E2/E3. |
| 37 | **архитектурно полезный P2, но здесь следует остановиться** | План утверждает default-off TESTING-only canonical seam, exact-once research receipt и отсутствие production caller; однако evidence — dirty worktree. Сам Phase 37 правильно запрещает P3. `37:24-39,80-98,166-182`. |

У плана есть одна внутренняя stale inconsistency: `19_open_decision_backlog.md:197` всё ещё говорит, что P2 “not yet composed”, тогда как `:217` и Phase 37 уже объявляют P2 закрытым `CLOSED/TESTING-ONLY`. Это не архитектурный blocker, но evidence corpus должен быть однозначен: строку 197 нужно обновить, иначе machine audit получает два состояния одного gate.

Вторая важная коррекция — **Phase 19:85**. После Phase 33 её правильная нормативная версия должна быть:

> Production D2 остаётся заблокирован до **атрибутируемого решения owner-а, независимого от compatibility authority plane**, с точным leaf и полной owner map. Owner может находиться в этом же repository; VMX/VMCS compatibility code не может назначать, принимать или самопроверять его.

Так снимается искусственный организационный deadlock без ослабления authority isolation.

## Реестр реальных блокеров и предпочтительные решения

Ниже блокеры разделены именно по независимым proof obligations. Я **не рекомендую** превращать D2, leaf, E2, E3 и publication в один “VMCALL activation PR”: это сразу разрушит возможность доказать, кто именно владеет каждым effect.

| Приоритет / blocker | Механизм и нарушаемый инвариант | Предпочтительное решение | Запрещённый shortcut | Проверяемый DoD |
|---|---|---|---|---|
| **P0 — provenance / public SHA** | Local plan Phase 34–37 живёт на другой branch/origin/SHA и в конце dirty worktree. Нельзя утверждать runtime state публичного repo по такому evidence. | Начать новую production activation series с **одного проверяемого remote SHA**. Сравнить local branch с `d3814d1...` через merge-base/diff, перенести нужные fail-closed commits, затем зафиксировать clean SHA и tree hash. | “Тесты были зелёными локально, значит public master соответствует плану”. | Remote branch/PR содержит source + tests + plan; clean checkout воспроизводит hashes/tests; evidence указывает ровно этот SHA. |
| **P0 — D2 governance owner** | Нет субъекта, которому принадлежит семантика VMCALL operation; compatibility plane не может сам себя назначить. | Назначить repository-local **`DomainHypercallRuntimeOwner`** или эквивалентную runtime role под `Core/Runtime/Events/Hypercalls`, отдельно от VMX/VMCS. D2 принимается owner-review workflow. SafetyVerifier owner проверяет admission contract, compatibility owner не является sole approver. | VMX frontend, VMCS field, `VmExitReason`, test или schema как owner. | Accepted owner-specific ADR; CODEOWNERS/reviewer mapping; owner ID; owner source = runtime; explicit adjacent denials. |
| **P0 — D2 acceptance provenance** | `accepted_commit_sha` внутри того же manifest может создать self-reference/circular evidence. CODEOWNERS сам по себе также не доказывает completed review. | Разделить **Decision Spec** и **Acceptance Attestation**. Commit A фиксирует immutable owner/ABI spec + digest. Commit/attestation B ссылается на SHA/digest A и review evidence. Generated registry строится только из accepted attestation. | Manifest, который сам пытается содержать SHA собственного commit; `state=Accepted` как достаточное доказательство. | Attestation references immutable spec SHA/digest; required reviewers проверены; compatibility self-approval denied. |
| **P0 — numeric leaf / ABI** | `Rs1` сейчас register selector; decode projection ошибочно было бы принять за runtime leaf. `VmxExitQualification.Leaf` и current positive retire vocabulary используют `ushort`. fileciteturn61file0L2-L2 fileciteturn60file0L2-L2 | D2 должен отдельно заморозить `HypercallLeafId` width, source, descriptor ABI и invalid encodings. Для **первого slice рекомендую 16-bit canonical leaf**, фактическое значение читается из source register; high bits runtime value должны быть zero, иначе denial. `Rs2` для первого probe — обязательно `0`. | Использовать opcode `259`, exit reason `18`, `Rs1` index, owner ID, VMFUNC leaf или test number как VMCALL leaf. | Один exact non-zero leaf зарезервирован allocator-ом; adjacent/zero/high-bit/unknown leaves denied; decode selector ≠ operand value test. |
| **P0 — первая семантика leaf** | Stateful leaf немедленно тащит writeback/scheduling/memory/migration dependencies. | Первый leaf — **no-state / no-payload / no-register-result / no-PC-redirect probe**. Он доказывает authority chain, не добавляя нового architectural state. Numeric value назначает D2 allocator; не надо заранее “зашивать красивую константу” в plan/test. | `yield`, capability query с register result, secure call, memory/I/O operation как первый leaf. | Success означает только owner execution receipt → completion → precise no-effect retire; architectural state до/после идентичен кроме retired instruction count/PC progression, определённых обычным retire. |
| **P0 — canonical operand snapshot** | `VmxMicroOp` хранит только `Rd/Rs1/Rs2`. Late backend re-read создаст TOCTOU/state reconstruction и может разойтись с VT/FSP attempt. fileciteturn70file0L2-L2 | После E1, на canonical operand-read/materialization boundary создать immutable `VirtualizationOperandSnapshot`: attempt, VT/context/domain, source register IDs, **captured values**, replay/bundle epoch, operand digest. | Backend сам читает register file; compatibility handler передаёт “leaf”; leaf вычисляется из exit qualification. | Изменение source register/VT/replay после capture не изменяет E2 input; stale/foreign snapshot denied. |
| **P0 — production E2** | E1 намеренно не содержит leaf/address-space/capability/evidence/restore. fileciteturn71file0L2-L2 | SafetyVerifier один выдаёт opaque `VirtualizationOperationAdmissionCertificate` после проверки D2 registry + operand snapshot + capability grant identity/revocation epoch + evidence policy digest/epoch + address-space identity + restore generation + E1 attempt. | Расширить E1 bool-ами; создать public constructor; считать runtime-boundary booleans admission authority. | Forged/default/stale/cross-VT/domain/address-space/replay/restore/revocation/leaf mismatch denied; valid exact leaf получает ровно один E2. |
| **P0 — production E3 backend** | Current `HypercallBackendAdmissionResult` — DTO с bool; service не имеет Allowed, но простое добавление `Allowed` сделает authority присваиваемой. fileciteturn27file0L2-L2 | Executor API принимает **только live E2**, а accepted owner выдаёт opaque **`VirtualizationExecutionReceipt`**. Receipt связывает E2 attempt, owner/policy version, leaf, execution sequence, effect class/digest. Для probe effect class = `NoStateNoPayload`. | `BackendExecutionAuthorized=true`; `NeutralHypercallBackendOwnerDescriptor.Accepted` как caller-constructible token; static `Default` service как скрытый owner. | Без E2 executor невызваем; E2 exact-once consumed; duplicate/foreign owner/stale policy rejected; receipt private/nonforgeable. |
| **P0 — E4 canonical composition** | Compatibility direct API не является pipeline. Backend, подключённый туда, обойдёт E1/SafetyVerifier/retire order. | Подключать E2/E3 к **canonical VMX pipeline execute contour после operand materialization**, с explicit injected owner registry. Compatibility frontend получает только projection результата, но не вызывает executor. | `AdmitVmCallTrapProjection → backend`; `ExecutionDispatcherV4` eager direct API как отдельный hidden backend; TESTING P2 как production hook. | Transitive caller scan: единственный production E3 caller — canonical execution path; frontend/VMCS/compiler direct calls = 0. |
| **P0 — E5 completion** | Current route/fence/`CompletionRecord` positive DTOs потенциально конструктивны и не связывают publication с one-shot E3 receipt. fileciteturn30file0L2-L2 fileciteturn31file0L2-L2 fileciteturn34file0L2-L2 | Completion owner consume-once E3 → opaque **`VirtualizationCompletionToken`**, привязанный к attempt, completion sequence, owner, evidence class, migration class. Только он позволяет canonical completion queue создать record. | Trust `CompletionPublicationAllowed=true`; public fence result как authority; frontend создаёт CompletionRecord. | Forged/duplicate/out-of-order/squashed/stale E3 не публикует completion; exactly one valid token → exactly one completion. |
| **P0 — E6 retire** | `VmxRetireEffect` уже имеет positive factories, поэтому effect не может быть authority. fileciteturn62file0L2-L2 | Считать `VmxRetireEffect` **данными**, а не grant. Canonical retire owner выдаёт отдельный `VirtualizationRetireGrant`, связанный с E5, ROB/retire slot, VT, attempt и ordering epoch. `Apply...` требует grant. | Любой `VmxRetireEffect.VmCall()` как доказательство успеха; backend напрямую пишет register/PC. | Squash/exception/duplicate/wrong VT/wrong retire slot deny; no architectural effect происходит до head-of-retire. |
| **P0 — E7 restore/migration** | Generic migration правильно отделена от projection, но in-flight virtualization receipts не классифицированы. fileciteturn50file0L2-L2 | Для первой версии выбрать **drain-only + NoPayload**. Checkpoint запрещён до drain E2–E6; restore увеличивает `RestoreGeneration`, поэтому все pre-restore E1/E2/E3/E5/E6 invalid. | Сериализовать opaque certificate/receipt; восстановить completion из VMCS projection; replay E3 после restore. | Checkpoint waits/drains; restore invalidates old tokens; stale post-restore token tests; byte/architectural trace deterministic. |
| **P0 — FSP/SMT determinism** | E1 уже связывает VT/context/domain/source+working slot/bundle/replay, но positive E2–E6 ещё не доказаны относительно FSP. fileciteturn72file0L2-L2 | После E1 VMX carrier identity immutable. FSP может влиять только на scheduling opportunity **до** admitted operation, но не на owner, operands, exception, completion или retire ordering. Сравнивать architectural traces FSP on/off и SMT permutations. | Re-reading donor state; FSP-created capability; перенос certificate между slots/VT. | Identical retired instruction/effect/fault traces при FSP on/off, different SMT interleavings и replay patterns. |
| **P1 — architectural VMREAD** | Direct read-only projection существует, canonical writeback нет. GuestCr0/Cr4 идут от privileged owner и имеют epoch/migration guards. fileciteturn54file0L2-L2 | После VMCALL slice сделать отдельный owner package: runtime selector value capture → field-specific SafetyVerifier E2 → canonical owner snapshot → immutable `VmReadEffect(value, rd, epoch)` → E5/E6 → register writeback. Первый кандидат — уже guarded GuestCr0/Cr4, а не host/control/EPT fields. | Использовать `VmcsReadOnlyValueProjectionService` как ISA backend; VMCS dictionary; late value read at retire. | Selector is runtime value, owner epoch bound, x0 semantics, squash/replay, exact one Rd write, no host evidence. |
| **P1 — VMWRITE** | Generic field write смешает владельцев execution, memory, controls, nested и completion. Tests уже держат memory aliases read-only. fileciteturn46file0L2-L2 | **Оставить denied в первом release.** Позже каждый field class превращать в owner-specific command, например `UpdateGuestPrivilegedState`, а не `WriteVmcsField(id,value)`. | `Dictionary<VmcsField,ulong>`, `TryWriteField`, shadow VMCS as store, compatibility owner as write owner. | Нет generic mutable VMCS state; каждый разрешённый field имеет конкретного canonical owner, E2, effect и rollback. |
| **P1 — nested virtualization** | Runtime descriptors существуют, но tests специально deny VMCS12/02 and mutable shadow authority; compatibility bridge fail-closed. fileciteturn45file0L2-L2 | Ввести authority-issued **parent→child domain edge**: parent/child IDs, independent capability grants, address-space relation, policy/revocation epochs, restore generation. Child grant = explicit delegated intersection, никогда transitive host grant. | VMCS12/VMCS02 storage as owner; Shadow VMCS execution state; copying parent capability mask. | Cross-parent/cross-child/cross-domain grant denial, host evidence exclusion, migration round-trip of neutral descriptor only. |
| **P1 — memory/I/O/IOMMU/lane** | Current code правильно отказывает compatibility-owned Lane6/Lane7 и VMX IOMMU mutation. fileciteturn46file0L2-L2 | Создать `VirtualizedTransactionEnvelope`: attempt, VT/domain, addressSpace, translation policy epoch, IOMMU grant/revocation, device, lane class, sequence/fence domain, completion owner. MMU/IOMMU revalidate grant at use. | `EptPointer`/`Vpid` VMCS field как translation authority; Lane token как capability; backend sideband как retire authority. | Cross-domain/device/address-space replay denied; stale IOMMU epoch denied; sideband completion не изменяет architectural state без E5/E6. |
| **P1 — SecureCompute** | `VmxCaps` фильтрует hypothetical secure/evidence bits; compatibility owner sources для secure backend denied. fileciteturn47file0L2-L2 | **Не связывать с первым virtualization release.** Отдельный secure owner package; secure hypercall capability никогда не наследуется из ordinary VMCALL capability. Secure evidence остаётся host-invisible/recompute-only согласно своему owner. | `VmxCaps.Secure`; VMCS secure fields; “VMX enabled ⇒ SecureCompute enabled”. | Ordinary VMX grant не проходит secure E2; secure/host evidence не появляется в compatibility projections/checkpoints. |
| **P1 — compiler emission** | Compiler helper emission уже отключена, sidebands validation-only. fileciteturn48file0L2-L2 | Сохранять no-emission до E7/release. Затем compiler может читать **released target manifest** для emission eligibility, но generated manifest никогда не заменяет runtime SafetyVerifier. | Opcode presence ⇒ emit; compiler-created admission token; fallback to raw VMCALL при unknown target. | Runtime/compiler manifest digest parity; unsupported target emits 0 VMX; emitted code всё равно fails closed без runtime E2. |

### Почему D2 — не “внешний блокер”, а governance blocker

Это центральный вывод исследования.

Фраза старых фаз:

> “до external neutral-runtime-owner artifact backend work blocked”

была слишком сильной и фактически создавала вечное ожидание неизвестной внешней организации. Последний Phase 19 уже исправляет это в `:176`: owner может быть repository-local. Phase 03 также формулирует neutral как независимость от compatibility authority plane, а не обязательную внешность по отношению к repository/organization (`03_owner_specific_rfc_adr_process.md:28-39`).

**Предпочтительная модель authority для D2:**

```text
Architecture / repository governance
        |
        | accepts immutable D2 specification
        v
DomainHypercallRuntimeOwner
        |
        | semantics, exact leaf, ABI, effect class,
        | capability/evidence/migration policy
        |
        +---- NOT decoder
        +---- NOT VMX frontend
        +---- NOT VMCS
        +---- NOT compiler
        +---- NOT SafetyVerifier itself

SafetyVerifier
        |
        | independently checks live legality
        | and D2-derived policy identities
        v
E2 certificate
```

То есть **runtime owner владеет смыслом операции, SafetyVerifier владеет admission, execute owner владеет выполнением, completion owner — публикацией completion, retire owner — архитектурным commit**. Ни один из них не должен присваивать себе полномочия следующего этапа.

Это лучше всего соответствует философии HybridCPU-v2: один canonical owner каждого state/effect, а compatibility layers — только read-only projection/fail-closed vocabulary.

## Пересобранный dependency-ordered план PR

Существующий rollout direction в Phase 17 правильный, но production lane я бы сделал ещё более мелким. Особенно важно **не переносить P1/P2 prototype classes непосредственно в production**: они полезны для доказательства формы, но production E2/E3 должны иметь D2-bound identity и другой trust provenance.

| PR | Scope и authority boundary | Изменения | Обязательные tests / release gate | Rollback |
|---|---|---|---|---|
| **Rebaseline** | evidence only | Ветка от проверяемого public SHA; compare local `558.../a594.../ddff...` с remote; перенести нужный fail-closed substrate; удалить stale Phase 19 status; зафиксировать clean containing SHA | clean checkout, source hashes, generated spec tests, full VMX negative suite, Release without TESTING | revert evidence/substrate; runtime fault-only |
| **Governance substrate** | governance, не runtime | `D2DecisionSpec` schema + separate `D2AcceptanceAttestation`; CODEOWNERS/review policy; no owner instance/leaf | malformed SHA/reviewer/self-approval/duplicate decision/accepted-without-attestation all deny | docs/tooling only |
| **D2 decision** | hypercall semantic owner | Назначить runtime owner; определить first no-state probe; exact `HypercallLeafId`; 16-bit ABI policy; Rs1 runtime value source; Rs2=0; capability/evidence/migration/adjacent denial map | manifest consistency, leaf collision scan, incompatible widths/high bits denied | withdraw attestation; no runtime behavior changed |
| **Canonical operand materialization** | architectural operand owner | Immutable operand snapshot after E1; no backend | mutation/replay/VT/domain/source register stale tests; FSP variation | fallback to old fault path |
| **Production E2** | SafetyVerifier sole issuer | D2-bound opaque operation certificate; bind leaf, grant/revocation, evidence, address space, restore gen, attempt | forgery/stale/cross-* matrix; no public constructor; no compatibility caller | disable issuance → E1 fault |
| **E3 backend** | accepted runtime owner | One exact-leaf no-state executor + opaque execution receipt; default-off feature gate allowed, but gate cannot grant authority | unknown/adjacent leaf, duplicate E2, stale owner policy, foreign executor; no completion/retire | feature gate off → byte/trace-equivalent fault |
| **E4 composition** | canonical pipeline only | Explicit dependency injection into canonical VMX execute contour; compatibility direct services remain projection-only | transitive caller scan; direct frontend/dispatcher/tooling bypass denied; FSP/SMT equivalence | disconnect composition; E2 can remain diagnostic/fault-only |
| **E5 completion** | completion owner | consume-once E3 receipt → opaque E5; generic positive bool DTO no longer trusted | duplicate, delayed, wrong attempt/VT/domain, squash before completion | disable token issuance; no retire |
| **E6 retire** | canonical retire owner | E5 → one-shot E6; apply no-state successful effect exactly at retire head | precise exception/squash/replay/duplicate/out-of-order; PC/retire sequence; zero unintended register writes | success mapping off → existing fault |
| **E7 determinism/migration** | migration owner + pipeline determinism | drain-only checkpoint; restore generation invalidation; rollback drill; FSP/SMT/replay matrices | checkpoint while in-flight denied/drained; pre-restore token unusable; trace equality FSP on/off, SMT schedules | disable virtualization feature and restore fault baseline |
| **Limited release** | release governance, no new authority | exact-scope capability manifest + public claim | clean SHA, remote CI evidence, zero TESTING symbols, compiler still no-emission unless separately accepted | kill switch returns exact fail-closed behavior |
| **VMREAD follow-up** | privileged/execution field owner | один field slice, canonical read effect/writeback | owner epoch/selector/x0/squash/restore | field returns to projection-only |
| **VMWRITE/nested/memory/Secure/compiler** | каждый отдельный owner | независимые RFC/ADR/PR chains | independent negative matrices | independent feature rollback |

Здесь я **поддерживаю решение Phase 37 “no P3”**. P1/P2 уже доказали две полезные вещи по плану: можно построить no-state/no-payload contract и можно экспериментально связать его с canonical issue/materialization seam без production publication. Дальнейший P3 без D2 начал бы формировать параллельный shadow runtime — именно тот тип hidden authority, который план пытается предотвратить. Поэтому следующая положительная инженерная работа должна быть **D2**, а не ещё один TESTING-only execution layer.

При этом формулировку “backend-работы открывать нельзя” нужно понимать точно. **До D2 действительно нельзя:**

- добавлять production executor;
- добавлять positive backend state;
- подключать compatibility path к executor;
- добавлять positive completion/retire;
- резервировать неутверждённый production leaf.

Но разрешены и полезны fail-closed работы, уже предусмотренные Phase 03/34: schema/validator, denial-only interfaces, opaque unissued types, source scans, negative conformance, evidence rebaseline. Attachment `03_owner_specific_rfc_adr_process.md:74-80`, `34_d2_schema_attribution_and_e2_negative_substrate.md:26-58`.

## Открытые архитектурные решения и итоговая оценка плана

Сам план после Phase 33–37 стал **существенно сильнее** предыдущей версии. Я бы оценил его так:

**как safety/governance specification — высокий уровень; как production activation plan — пока NO-GO из-за D2 и provenance; как набор локальных research results — полезен, но их нельзя смешивать с production evidence.**

Наиболее важные ещё не принятые решения следующие.

| Решение | Варианты | Рекомендация | Что должно быть доказано до кода |
|---|---|---|---|
| **Кто D2 owner** | external organization / compatibility owner / repository-local runtime owner | **repository-local runtime owner, independent from compatibility plane** | owner scope, review/attribution, отсутствие VMX/VMCS self-approval |
| **Как принимать D2** | enum `Accepted`; manifest with self SHA; separate spec+attestation | **separate immutable Decision Spec + Acceptance Attestation** | deterministic digest, review evidence, withdrawal/replacement semantics |
| **VMCALL leaf width** | byte selector; 16-bit; unrestricted register width | **16-bit first ABI**, потому что existing compatibility qualification/retire vocabulary уже losslessly представляет `ushort`; high source bits denied | whole-repo collision scan, exact operand specification, no truncation |
| **Первый leaf** | capability query; yield; secure op; no-state probe | **no-state/no-payload/no-result probe** | no side effects, Rs2 zero, deterministic completion and retire |
| **Где читать leaf** | decode; compatibility handler; backend; canonical operand stage | **canonical operand materialization after E1, before E2** | source value corresponds exactly to admitted attempt and cannot be reconstructed later |
| **Что является backend authority** | result bool / accepted descriptor / E2 / E3 receipt | E2 authorizes attempt; **E3 opaque receipt proves actual execution** | unforgeability, exact-once, owner/policy/attempt binding |
| **Что является completion authority** | route bool / fence DTO / E3 receipt directly / E5 token | **separate one-shot E5 token** | backend cannot publish architectural completion itself |
| **Что является retire authority** | `VmxRetireEffect` / completion record / E6 grant | **E6 one-shot canonical retire grant** | precise retirement, squash, exception and ordering proof |
| **Первая migration policy** | serialize in-flight operation / reconstruct / drain | **drain-only NoPayload** | no pre-restore receipt survives; restore generation invalidates all ephemeral authority |
| **VMREAD first field** | generic all fields / GuestCr0/4 / host/control/EPT | после VMCALL — **один уже owner-backed guest field, предпочтительно GuestCr0 или GuestCr4** | architectural selector semantics, immutable value snapshot, precise Rd writeback |
| **VMWRITE** | generic VMCS store / owner-specific commands / permanently denied | **denied first release; later owner-specific commands** | one owner and effect model for every admitted field |
| **Nested** | Shadow VMCS / VMCS12/02 / neutral parent-child descriptor graph | **neutral parent-child authority graph** | independent grants, address-space relation, host evidence isolation, restore semantics |
| **Compiler** | emit from opcode existence / emit from accepted D2 / wait for released runtime E7 | **wait until released E7, then manifest-controlled emission** | runtime/compiler manifest parity and mandatory runtime re-admission |

Ключевой выбор по leaf я сознательно уточняю относительно прежней идеи “придумать красивый 64-bit номер”. В текущем коде нет доказанного 64-bit VMCALL leaf ABI; напротив, frozen compatibility representation содержит `ushort Leaf`, а `VmxRetireEffect.VmCall` также принимает `ushort`. Поэтому **выделять произвольный 64-bit leaf сейчас архитектурно преждевременно**. fileciteturn60file0L2-L2 fileciteturn62file0L2-L2

Предпочтительный D2 должен сначала утвердить namespace и width, а уже затем allocator должен выделить **ровно один non-zero numeric value**. Сам номер не должен появляться ни в test fixture, ни в prototype P1/P2, ни в compatibility code раньше accepted decision. Это полностью соответствует исправленному Phase 19 и предотвращает превращение случайной константы в скрытую ISA.

Самый важный итог по блокеру E2/D2 можно сформулировать нормативно так:

> **Production E2 и любой production backend остаются заблокированы до принятого D2, который атрибутируемо назначает runtime owner, независимого от VMX/VMCS compatibility plane, и замораживает один exact numeric VMCALL leaf вместе с runtime operand ABI, capability/evidence/migration и adjacent-denial map. Owner не обязан быть внешним к repository. До D2 допустимы только fail-closed governance/evidence/substrate работы; positive executor, production composition, completion и retire запрещены. После D2 authority всё равно не переносится из manifest в runtime: live execution требует нового SafetyVerifier-issued E2.**

Именно эта модель одновременно устраняет governance deadlock и сохраняет главный принцип HybridCPU-v2: **решение о том, что операция существует, решение о том, что текущая попытка законна, факт её выполнения, факт completion и право retire — пять разных видов доказательства, принадлежащих разным authority boundaries.**

Поэтому безопасный путь к первой активации — не “дописать VMX backend”, а провести очень узкий **no-state VMCALL slice** через D2 → E2 → E3 → E4 → E5 → E6 → E7, сохранив VMREAD projection-only, VMWRITE denied, nested denied, SecureCompute independent, memory/IOMMU/lane passthrough denied и compiler no-emission. Только после clean remote SHA, determinism/restore/rollback proof и exact-scope release gate этот slice можно честно назвать **limited runtime virtualization**; более широкий claim вроде “VMX enabled” или “Virtualization supported” на текущем состоянии был бы технически ложным.