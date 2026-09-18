# you asked

прикрепил virtualizationactiovationplan в zip - дай оценку плана и объясни блокер:  E2 остаётся заблокирован до внешнего, атрибутируемого решения нейтрального владельца с точным leaf; backend-работы открывать нельзя.

  Предложи предпочтительные решения по блокерам учитывая всю специфику и философию архитектуры HybridCPU-v2 и её системы виртуализации @GitHub

---

# chatgpt response

Сначала разберу ZIP как первичный текст плана, затем зафиксирую текущий `master`/SHA через GitHub и проверю спорные E2/neutral-owner/leaf утверждения по коду и тестам. После этого дам не пересказ, а архитектурную рецензию: что в плане верно, где он чрезмерно блокирует работу, и какие решения предпочтительны.

Уже видно ключевую проблему: план правильно запрещает VMX frontend самоназначаться backend-владельцем, но затем превращает это в организационное ожидание **внешнего** субъекта. Это не одно и то же. Сейчас проверяю, есть ли в самом плане механизм назначения нейтрального owner’а и leaf allocator — похоже, процесс создал deadlock и потом документирует ожидание в двенадцати последовательных фазах.

## Краткий вердикт

**План архитектурно сильный как safety-specification и реестр запретов, но в текущем виде слабый как исполнимый activation plan.** Он правильно защищает HybridCPU-v2 от скрытого VMX-backend, VMCS-as-state-store, обхода SafetyVerifier, смешения completion и retire, утечки host evidence и присвоения authority compatibility-слоем.

Однако E2 заблокирован не только отсутствием реализации. План создал **governance deadlock**:

1. запрещает репозиторию назначить нейтрального владельца;
2. требует решения от не определённого в архитектуре «внешнего владельца»;
3. запрещает выделить leaf до принятия решения;
4. одновременно требует уже существующий точный leaf как условие принятия;
5. после этого многократно документирует отсутствие ответа.

Поэтому формулировка:

> «E2 остаётся заблокирован до внешнего, атрибутируемого решения нейтрального владельца с точным leaf; backend-работы открывать нельзя»

**верна как production/release gate, но неверна как абсолютный запрет инженерных работ.** Запрещать нужно положительный executor и production composition. Fail-closed инфраструктуру E2 — manifest, leaf registry schema, opaque certificate, negative tests, disabled composition — можно и нужно делать до активации.

Полная активация Virtualization сейчас запрещена. Максимальный честный режим:

> **VMX Compatibility Projection + SafetyVerifier E1 fault-only transport.**

Production `VmxMicroOp` и dispatcher по-прежнему создают только `SecurityPolicyViolation`, а retire применяет fail-closed fault.   

---

## База оценки и расхождение Git

ZIP и публичный GitHub описывают разные Git subjects.

В ZIP:

- `local_only: true`;
- origin: `yaksysdev/HybridCPU-v2`;
- branch: `refactor/compiler-core-authority-boundaries`;
- clean subject: `b6d4871e0f06ebde07015e393c0d36af0362f506`;
- E1 реализация помечена как `local-uncommitted-worktree`, без containing commit.

Это зафиксировано в:

- `evidence/2026-08-06-clean-head-evidence.json:3-13`;
- `:22-29`;
- `:58-78`.

В запрошенном `yuriyyak23/HybridCPU-v2` текущий `master` заканчивается SHA `d3814d1f332f083034d3b245f807a45f97792070`, commit `upd refactor 086082026`; он является потомком `f794ea3`. ([GitHub](https://github.com/yuriyyak23/HybridCPU-v2/commit/d3814d1f332f083034d3b245f807a45f97792070))

Поэтому моя оценка использует:

- **ZIP** как первичный текст плана;
- **GitHub SHA `d3814d1...`** как проверяемое production-состояние;
- без утверждения, что `b6d4871...` эквивалентен или является предком публичного `master`.

Это первый release-blocker самого плана: его evidence baseline необходимо пересобрать на одном доступном SHA.

---

# Что на самом деле означает блокер E2

В Phase 17 цепочка задана так:

`E0 evidence → E1 typed admission → E2 accepted owner + exact leaf → E3 backend → E4 composition → E5 completion → E6 retire...`

См. `17_phase_rollout_and_pr_order.md:55-61`.

### Что уже есть в E1

E1 корректно привязывает carrier к:

- issuer generation;
- attempt;
- opcode и operation;
- VT и owner context;
- domain;
- source/working slot;
- bundle и replay epoch.

Но сертификат намеренно содержит:

- `HasAcceptedNumericLeaf => false`;
- отсутствие descriptor/capability/evidence/restore identity;
- `BackendExecutionAuthorized => false`;
- `CompletionPublicationAuthorized => false`;
- `RetirePublicationAuthorized => false`.

То есть E1 доказывает только: **«именно этот VMX carrier законно находится здесь и может безопасно дойти до fault»**. Он не доказывает: **«эту операцию разрешено выполнить»**. 

### Что отсутствует в E2

Для VMCALL E2 должен доказать:

1. фактическое значение leaf, прочитанное из канонического source operand, а не номер регистра `Rs1`;
2. точный backend owner;
3. версию owner policy;
4. capability grant identity и revocation epoch;
5. evidence policy identity;
6. domain и restore generation;
7. допустимую result/effect class;
8. связь с конкретным E1 attempt.

Текущий VMCALL compatibility path принимает только номера регистров `HypercallLeafRegister` и `DescriptorRegister`. Он не получает runtime leaf value. Затем backend admission вызывается через `MissingNeutralOwner`, а route всегда строится как `ProjectionOnlyDenied`. `VmxCompatibilityAdmissionService.Traps.cs:58-75, 241-274`. ([GitHub](https://raw.githubusercontent.com/yuriyyak23/HybridCPU-v2/d3814d1f332f083034d3b245f807a45f97792070/HybridCPU_ISE/CloseToHSL/Core/Virtualization/Compatibility/Frontend/Handlers/VmxCompatibilityAdmissionService.Traps.cs))

В backend policy:

- нет enum-состояния `Allowed`;
- `BackendExecutionAuthorized` существует только как `bool`;
- после всех проверок метод всё равно возвращает `DeniedNeutralBackendOwnerRfcAdr`.

`HypercallBackendAdmissionPolicy.cs:3-18, 119-164, 180-300`.   

В owner descriptor также существуют только:

- `Missing` и `DraftOnly`;
- `Missing` и `CandidateOnlyNoNumericLeaf`.

Принятого owner-state и точного leaf-state в типовой системе нет. `NeutralHypercallBackendOwnerDescriptor.cs:16-32, 54-88`. 

Следовательно, E2 — реальный блокер. Но он **архитектурно-управленческий**, а не внешний технологический dependency.

---

# Главная ошибка: «нейтральный» не означает «организационно внешний»

Правильное значение терминов:

- **neutral owner** — владелец вне VMX/VMCS compatibility plane;
- **attributable** — решение подписано или утверждено конкретной архитектурной ролью;
- **external to frontend** — не decoder, не projection service, не VMCS schema;
- это **не обязано** означать внешний репозиторий, другую компанию или неизвестного участника.

В плане это постепенно превратилось в буквальное ожидание внешнего артефакта:

- `06_neutral_hypercall_backend_owner_rfc.md:120-159`;
- `17_phase_rollout_and_pr_order.md:87-97`;
- `32_e1_nonforgeable_safetyverifier_admission_contract.md:22,74`.

Проблема особенно очевидна в `17:89`:

> Owner: `UNASSIGNED`; this plan cannot appoint it.

Если никто внутри architecture governance не может назначить owner, а внешняя организация не определена, E2 по построению неразрешим.

## Предпочтительное исправление

Нужно заменить требование:

> external neutral owner artifact

на:

> **owner-approved architecture decision external to the compatibility authority plane**.

Атрибуция обеспечивается не ожиданием письма извне, а:

- ADR с именованным `DecisionOwner`;
- `CODEOWNERS`/required reviewers;
- owner ID и policy schema в machine-readable manifest;
- commit SHA принятого решения;
- запретом self-approval со стороны VMX frontend owner.

Оптимальный owner:

```text
DomainHypercallRuntimeOwner
  namespace: Core/Runtime/Events/Hypercalls
  owns:
    leaf semantics
    operand/result contract
    deterministic execution effect
    capability requirement
    cancellation/rollback contract
  does not own:
    decode
    SafetyVerifier admission
    completion publication
    retire
    migration
```

SafetyVerifier остаётся единственным admission authority. Backend owner предоставляет policy snapshot, но не выдаёт себе право исполнения.

---

# Оценка плана

## Что сделано правильно

### 1. Authority model

Очень хорошо проведено разделение:

- compatibility vocabulary ≠ authority;
- schema presence ≠ field availability;
- admission ≠ execution;
- backend success ≠ completion;
- completion ≠ retire;
- checkpoint ≠ state owner.

Это соответствует философии HybridCPU-v2.

### 2. E1

E1 — сильнейшая часть плана. Attempt-bound, issuer-live, slot/bundle/replay-bound сертификат — правильный способ не позволить FSP, SMT или позднему stage реконструировать authority.

### 3. VMREAD/VMWRITE

Верно, что VMREAD должен быть field-by-field и read-only projection, а VMWRITE не должен приводить к появлению универсального `Dictionary<VmcsField, ulong>`.

### 4. Completion/retire

Правильно запрещена публикация архитектурного эффекта непосредственно из trap mapper или compatibility handler.

### 5. SecureCompute, nested и lanes

Правильно исключены:

- SecureCompute как «VMX mode»;
- Shadow VMCS как owner nested state;
- Lane6/Lane7 token как virtualization authority;
- VMCS EPT/NPT-like field как реальный address-space root.

## Что необходимо исправить

### 1. Смешанная provenance

План одновременно говорит о clean `b6d4871...`, uncommitted E1 и реализованном E1. Публичный проверяемый source находится на другом SHA. До следующего архитектурного решения нужен новый E0 manifest.

### 2. Governance deadlock E2

Нейтральный owner не назначен, leaf allocator отсутствует, но оба требуются до начала процесса их назначения.

### 3. Смешение decision gate и runtime certificate

Сейчас E2 означает «принятый ADR и leaf». Это не execution admission.

Лучше разделить:

```text
D2 — accepted owner/ABI decision manifest
E2 — live operation-specific SafetyVerifier certificate
E3 — backend execution receipt
```

ADR — governance evidence. E2 — runtime authority.

### 4. Циклический leaf gate

План требует точный production symbol/table до implementation review, но запрещает создавать leaf в коде до принятия решения.

Решение: leaf выделяется **в принятом machine-readable ADR manifest**, а generated production registry появляется в следующем PR. Наличие production enum до ADR не требуется.

### 5. Слишком широкий запрет backend-work

До D2 действительно нельзя добавлять:

- executor с положительным исходом;
- `Allowed`;
- production composition;
- completion/retire publication.

Но допустимы:

- manifest schema;
- allocator rules;
- opaque E2 type;
- owner interface;
- disabled/null implementation;
- negative conformance;
- source scans;
- feature-state machine.

Сам код уже содержит draft owner descriptor и fail-closed backend admission scaffolding, то есть принцип «никакого кода до owner decision» фактически не соблюдается и не нужен.

### 6. Process bloat

Файлы 20–31 — 12 документов и примерно 12,6 тысячи слов, почти целиком повторяющих отсутствие owner response. Сам `19_open_decision_backlog.md:7` уже признаёт их историческими snapshot, а не фазами.

Предпочтительно переместить их в `history/` и оставить один append-only decision journal.

### 7. Bool authority scaffolding

Текущие route descriptors имеют публичные конструкторы и флаги:

- `allowsCompletionPublication`;
- `allowsRetirePublication`;
- `BackendExecutionAuthorized`.

Это допустимо в полностью denied состоянии, но нельзя использовать в positive path. 

Для E3–E6 нужны неприсваиваемые opaque receipts, а не комбинации `true`.

---

# Предпочтительное решение для первого leaf

Для первого active slice я рекомендую **не version query, не yield и не SecureCompute call**, а отдельный no-state canary:

```text
HCPU_HV_PROBE_V1
```

Предпочтительная 64-битная схема идентификатора:

```text
0x48594350_00000001
```

Где верхняя часть — отдельное HybridCPU hypercall namespace, нижняя — версия/номер leaf. Конкретное число должен зафиксировать ADR; смысл структуры — исключить путаницу с:

- opcode `VMCALL = 259`;
- `VmExitReason.VmCall = 18`;
- VMFUNC leaves;
- owner ID;
- test fixture;
- номером source-регистра.

### ABI первого leaf

| Свойство | Решение |
|---|---|
| `Rs1` | содержит фактический 64-битный leaf value |
| `Rs2` | обязан быть нулём; descriptor отсутствует |
| `Rd` | отсутствует, поскольку текущий VMCALL path не доказывает result-register ABI |
| Effect | no-state, no-payload |
| Success | completion + precise retire без register/PC redirect effect |
| Capability | leaf-specific `InvokeHypercall(HCPU_HV_PROBE_V1)` |
| Domain | только обычный L1 domain |
| SecureCompute | denied в первой версии |
| Nested | denied |
| Memory/I/O/lane | никаких пересечений |
| Migration | `NoPayload`, checkpoint только после drain |
| FSP | после E1 не stealable; FSP on/off архитектурно эквивалентны |

Почему не `YIELD`: он изменяет scheduling и создаёт тяжёлое доказательство FSP/SMT observability.

Почему не `ABI_QUERY`: потребует сначала определить register result ABI и VMX retire writeback.

Почему probe не бесполезен: он доказывает всю положительную цепь E2→backend→completion→retire, оставаясь без state authority.

---

# Предпочтительные решения по остальным блокерам

| Блокер | Предпочтительное решение |
|---|---|
| **E0 provenance** | Новый manifest на одном публично доступном SHA: remote, branch, commit/tree, source hashes, generators, test commands. Не смешивать local-only и remote evidence. |
| **E2 owner** | Назначить `DomainHypercallRuntimeOwner` решением architecture governance. «External» трактовать как external-to-VMX, не external-to-repository. |
| **Leaf allocator** | Versioned `VirtualizationOperationManifest`; leaf allocation совершается ADR, registry генерируется из него. Manifest не является runtime authority. |
| **E2 runtime admission** | `VirtualizationOperationAdmissionCertificate`, выдаваемый только SafetyVerifier после E1 и чтения canonical operands. Привязать leaf, owner policy version, grant/revocation, evidence, restore и attempt. |
| **Backend** | Executor принимает только E2 certificate и возвращает opaque `BackendExecutionReceipt`. Никаких public bool authorization. |
| **Composition** | Backend вызывается из canonical execute path. `AdmitVmCallTrapProjection` не должен становиться ISA backend. Compatibility service остаётся out-of-band projection API. |
| **Completion** | Completion owner преобразует backend receipt в exact-once completion token. Handler не создаёт `CompletionRecord`. |
| **Retire** | Только canonical retire owner выдаёт `VirtualizationRetireGrant`; register/PC effects применяются один раз через RetireCoordinator. |
| **VMREAD** | Отдельный параллельный slice: один уже имеющий canonical owner field. E2 связывает field ID, source epoch и destination register; execute создаёт immutable read effect; retire делает writeback. Не начинать с CR0/CR4, если цель — минимальный риск. |
| **VMWRITE** | Оставить permanently denied для первого release. В будущем — command-oriented операции к конкретным owners, а не VMCS field store. |
| **Nested** | Recursive neutral domain graph с authority-issued child IDs и независимыми grants. Shadow VMCS — только projection. |
| **Memory/IOMMU** | Typed transaction envelope: domain, address-space owner, translation epoch, IOMMU grant, lane class, sequence. VMCS не хранит translation root. |
| **Lane6/Lane7/FSP** | До E1 разрешено только placement; после E1 identity immutable. Sideband completion не публикует architectural state. |
| **SecureCompute** | Независимый owner graph. Первый VMCALL leaf в secure domain должен fail closed; никакого transitive grant от VMX capability. |
| **Compiler** | Default no-emission. Controlled emission только из того же manifest hash; runtime всё равно заново выполняет SafetyVerifier admission. |
| **Migration** | Для первого probe — drain-only и `NoPayload`. Все E1/E2/E3 tokens после restore недействительны. |
| **Release** | Claim содержит exact leaf, ABI version, SHA и exclusions. Не использовать «VMX supported» или «virtualization enabled». |

---

# Исправленный порядок маленьких PR

### PR-0 — Rebaseline

- Зафиксировать один remote/branch/SHA.
- Перегенерировать evidence.
- Переместить 20–31 в historical archive.
- Никаких runtime changes.

### PR-1 — Governance и ABI manifest

- Назначить `DomainHypercallRuntimeOwner`.
- Ввести leaf namespace/allocator policy.
- Принять `HCPU_HV_PROBE_V1`.
- Machine-readable owner/ABI/migration/denial map.
- Без production enum `Allowed`.

### PR-2 — E2 certificate substrate

- Private opaque certificate.
- Issuer — только SafetyVerifier.
- Leaf value читается из canonical operand snapshot.
- Все outcomes пока fault-only.
- Negative tests: forged, stale, wrong leaf, cross-VT/domain/replay/restore.

### PR-3 — Exact-leaf registry

- Generated registry из accepted manifest.
- Unknown и adjacent leaves denied.
- Manifest/source hash parity.
- Executor ещё отсутствует.

### PR-4 — Backend canary

- Один `HCPU_HV_PROBE_V1` executor.
- Возвращает opaque backend receipt.
- Default-off kill switch.
- Нет completion и retire.

### PR-5 — Canonical composition

- Decode → E1 → operand capture → E2 → executor.
- Exactly once.
- Direct service route невозможен.
- FSP donor/source alias tests.

### PR-6 — Completion-only

- Backend receipt → completion token.
- Duplicate/out-of-order/stale completion denied.
- Architectural state не изменяется.

### PR-7 — Precise retire

- Retire grant от canonical retire owner.
- Успешный no-writeback retire.
- Squash/exception/x0/duplicate tests.
- FSP on/off и SMT interleaving equivalence.

### PR-8 — Migration, rollback, release

- Drain-only checkpoint.
- Restore invalidates all certificates/receipts.
- Kill switch возвращает byte/trace-equivalent fault.
- Exact-scope release claim.

---

## Итог по формулировке блокера

Я бы заменил её на следующую нормативную формулировку:

> **Положительная E3 backend-реализация и production composition остаются заблокированы до принятого, атрибутируемого owner-specific ADR, назначающего нейтрального runtime owner и один точный numeric VMCALL leaf. Нейтральность означает независимость от VMX/VMCS compatibility authority, а не обязательную внешность по отношению к репозиторию. До принятия ADR разрешены только fail-closed substrate-работы: manifest schema, leaf allocator policy, opaque E2 certificate, disabled composition, static gates и negative tests.**

Это сохраняет все необходимые инварианты HybridCPU-v2, но убирает искусственное вечное ожидание внешнего субъекта.

**Уровень уверенности:** высокий по E1/E2/VMCALL/route/retire; средне-высокий по оптимальному первому leaf, поскольку его numeric namespace является новым архитектурным решением, а не уже зафиксированным фактом.

[Исходный VirtualizationActivationPlan.zip](sandbox:/mnt/data/VirtualizationActivationPlan.zip)

---
Powered by [AI Exporter](https://saveai.net)