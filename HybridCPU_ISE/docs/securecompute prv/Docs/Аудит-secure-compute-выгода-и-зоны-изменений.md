 **текущий код уже достаточно подготовлен, чтобы начинать проектировать secure compute**, но **ещё не стоит включать secure compute как полноценную исполнимую функцию**. Правильный ход сейчас — не “модифицировать VMX под secure compute”, а заложить **нейтральный secure compute substrate**: защищённый домен, защищённую память, измерение, политику свидетельств, перенос и ввод-вывод. Это должно быть в режиме **fail-closed / design fence / denied until owner exists**. Исходный контекст вашей задачи прямо требует не принимать слово “closed” за архитектурное закрытие без проверки кода и не путать freeze VMX frontend с feature-complete VMX execution. 

# 1. Выгодно ли сейчас модифицировать под secure compute?

**Да, выгодно — но только на уровне нейтральных владельцев и политик, а не на уровне открытия новых VMX-возможностей.**

Текущий код уже сделал несколько важных подготовительных шагов:

VMX больше не должен быть точкой интеграции новых инструкций: документация прямо говорит, что новая non-VMX инструкция должна интегрироваться с общей системой легальности, исполнения, публикации и свидетельств, а не с VMX frontend, VMCS manager, VmxCaps или VMX-specific handler. 

VMX формально описан как compatibility frontend, а источником истины названы generic domain / descriptor / capability runtime substrate, не VMCS, VMCSv2, VMX CSR или VMX instruction plane. В той же модели execution state принадлежит `ExecutionDomainDescriptor`, memory state — `MemoryDomainDescriptor`, capability state — `CapabilityDescriptorSet`, migration — checkpoint/domain model, evidence — `EvidencePolicy`. 

Код VMREAD уже идёт через явную цепочку: decode → compatibility projection → `RuntimeBoundaryAdmissionService` → read-only value projection. В `AdmitVmReadProjection` сначала проверяется VMREAD decode, затем compatibility projection, затем runtime admission как `ReadCompatibilityProjection`, и только после этого вызывается `VmcsReadOnlyValueProjectionService` с neutral execution/memory/completion owners из `DomainRuntimeContext`. 

Это хорошая основа для secure compute, потому что secure compute в HybridCPU должен быть не “защищённым VMCS”, а **усиленным доменом исполнения**. Уже есть правильная архитектурная тропа: runtime admission, typed capabilities, evidence policy, read-only owner views, migration policy, denied-by-default.

Но текущий код ещё не показывает готовой secure compute модели. Поиск по репозиторию не нашёл явных сущностей вроде `SecureComputeDomainDescriptor`, `SecureMemory`, `Attestation`, `Measurement` или `EvidenceVisibilityPolicy` в форме отдельного secure compute слоя. Это значит: **secure compute ещё не материализован как самостоятельный архитектурный договор**.

Поэтому мой вердикт:

> **сейчас выгодно начинать secure compute как архитектурную подготовку, но не как включение реального режима исполнения.**

Правильная форма ближайшей работы:

```text
SecureComputeDomainDescriptor
SecureMemoryDomainDescriptor
DomainMeasurementDescriptor
SecureEvidencePolicy / SecureVisibilityPolicy
SecureMigrationDescriptor
SecureIoDomainDescriptor
SecureHypercallPolicy
```

Неправильная форма:

Forbidden examples, not implementation guidance:

```text
VmxCaps.SecureCompute = true
VMCS.SecureState = ...
VMREAD exposes secure state
VMWRITE mutates secure state
VMCALL becomes secure backend success
```

# 2. Почему сейчас хороший момент именно для подготовки

Потому что текущая архитектура уже прошла важную очистку от VMX-владения.

Например, `VmxCapsProjection` сейчас читает через `CapabilityDescriptorSet` и `CapabilityPublicationPolicy`, а запись в VmxCaps оценивается как rejected или compatibility no-effect, то есть не создаёт authority. 

`CapabilityDescriptorSet` построен вокруг `CapabilityGrantCollection` и typed grants; hardware/runtime/domain capability masks являются проекциями grant-состояния, а не самостоятельной VMX-властью. 

Миграция тоже уже находится в правильной форме: `DomainCheckpointImage` различает `DomainDescriptor` и `CompatibilityProjection`, умеет обнаруживать host-owned evidence и запрещает compatibility projection checkpoint как authoritative restore state. 

`MigrationValidationPolicy` отдельно запрещает compatibility projection metadata как authoritative state, требует recompute для host-owned runtime evidence и требует preserve-policy для guest architectural state. 

Это прямо полезно для secure compute, потому что secure compute требует ровно таких гарантий:

не переносить хозяйские свидетельства;

не сериализовать совместимую VMCS-проекцию как истину;

не восстанавливать защищённое состояние из совместимого кэша;

не выдавать полномочия через VmxCaps;

не открывать VMREAD без нейтрального владельца.

# 3. Но почему нельзя сразу “включать secure compute”

Потому что несколько критических направлений ещё находятся в режиме denied/fail-closed.

`VmcsReadOnlyValueProjectionService` явно допускает value projection только для владельцев `CompletionRecord`, `ExecutionDomainDescriptor` и `MemoryDomainDescriptor`; compatibility-control owner сразу получает `CompatibilityControlValueProjectionDenied`, а остальные владельцы без neutral read-only source получают deny. 

Для execution-owned VMREAD код открыл только `GuestPc`, `GuestSp`, `GuestFlags`, и только если `ExecutionDomainDescriptor` материализует read-only state view. `GuestCr0` и `GuestCr4` прямо запрещены до появления нейтральной privileged execution-state semantics. 

Сам `ExecutionDomainDescriptor` пока содержит read-only state view только для guest PC/SP/flags, а `ExecutionDomainReadOnlyStateView` действительно имеет поля только `GuestPc`, `GuestSp`, `GuestFlags` и признаки их материализации.  

Для hypercall/VMCALL backend ситуация также fail-closed: `HypercallBackendAdmissionService` требует runtime admission, neutral trap result, backend descriptor, runtime authority, validated domain, typed capability grant, evidence approval и materialized neutral backend owner. При отсутствии backend descriptor или neutral backend owner он запрещает backend execution. 

А VMCALL compatibility path прямо вызывает `HypercallBackendAdmissionRequest.MissingNeutralOwner`, то есть производственный путь остаётся admitted-denied, а не backend success. 

Для secure compute это означает: **ещё нет безопасного нейтрального владельца гипервызовов, нет privileged-state semantics, нет secure I/O, нет measurement/attestation модели.**

Следовательно, полноценный secure compute сейчас открывать нельзя. Но проектировать его каркас сейчас — правильно.

# 4. Ответ на вопрос 2: где будет основная масса модернизации?

Здесь нужно разделить два смысла слова “виртуализация”.

Если под “слоем виртуализации” понимать **весь доменный runtime-контур HybridCPU** — execution domain, memory domain, capability domain, evidence, completion, migration, nested, I/O — тогда да: **большая часть модернизации действительно ляжет на виртуализационно-доменный слой**.

Если под “слоем виртуализации” понимать только:

```text
Core/Virtualization/Compatibility
VMX frontend
VMCS projection
VmxCaps
VMREAD/VMWRITE handlers
```

— тогда нет. В этот слой должна попасть меньшая часть. Он должен остаться тонким compatibility boundary.

Правильное распределение примерно такое:

| Зона | Доля изменений | Характер изменений |
|---|---:|---|
| `Core/Runtime/Domains` | высокая | secure domain descriptor, secure admission, domain policy |
| `Core/Runtime/Memory` | высокая | private/shared/measured memory, integrity epochs, secure translation policy |
| `Core/Runtime/Capabilities` | средняя/высокая | typed grants для create/measure/attest/migrate/secure I/O |
| `Core/Runtime/Evidence` | высокая | secure evidence visibility, host-owned evidence quarantine, attestation evidence |
| `Core/Runtime/Migration` | высокая | secure checkpoint, anti-rollback, no host evidence, no VMCS authority |
| `Core/Runtime/Completion` | средняя | secure completion fence, retire-only publication |
| `Core/Runtime/Events/Hypercalls` | средняя/высокая | secure hypercall backend owner, argument classification |
| `Core/Runtime/Nested` | средняя, позже | secure child-intent, nested secure domain policy |
| `Core/Virtualization/Compatibility` | средняя/низкая | deny/project secure visibility through VMX-compatible surface |
| Decoder / ISA metadata | низкая | classification, legality hooks, no-emission/secure-domain constraints |
| Non-virtualization execution lanes | низкая/средняя | только там, где есть память, retire, side effects, I/O, evidence, scheduling |

То есть коротко:

> **основная модернизация должна быть не в декодере и не в VMX-фасаде, а в нейтральном runtime-доменном слое. VMX/VMCS/VmxCaps должны только отразить или запретить совместимую видимость.**

# 5. Что в декодере менять минимально

Декодер сейчас для VMX уже выполняет роль “замороженной compatibility boundary”: он проверяет frozen opcode и предварительные флаги `DescriptorValidated`, `CapabilityValidated`, `SchedulingValidated`, `NoEmissionValidated`. 

Для secure compute декодер не должен становиться владельцем безопасности.

Ему достаточно добавить или расширить:

классификацию команды как secure-sensitive;

признак, что команда требует secure-domain admission;

признак, что команда может создавать host-owned evidence;

признак, что команда не должна испускать побочный эффект до retire;

связь с Legality A metadata.

Но декодер не должен решать:

доступна ли private secure memory;

разрешён ли secure hypercall;

можно ли раскрыть GuestPc;

можно ли мигрировать защищённый домен;

валидна ли аттестация;

разрешён ли host-visible evidence.

Эти решения должны оставаться ниже — в runtime admission, capabilities, memory/evidence/migration policies.

# 6. Что в non-Virt слое менять меньше, но не ноль

Non-Virt слой не должен переписываться ради VMX. Это уже закреплено архитектурно: новые non-VMX инструкции должны идти в ISA metadata, Stage A/B legality, execution lane binding, retire/publication model, evidence/migration policy, если нужно, а не в VMX frontend. 

Но secure compute всё же затронет часть non-Virt механизмов, потому что secure compute — это не только виртуальная машина. Это режим исполнения домена, где обычные инструкции тоже могут:

читать private memory;

писать private memory;

создавать исключения;

порождать evidence;

влиять на retire publication;

работать с вводом-выводом;

создавать побочные каналы;

участвовать в checkpoint/migration.

Поэтому non-Virt слой должен получить не VMX-интеграцию, а **нейтральную secure-domain awareness**.

Минимально нужны:

secure-aware memory access policy;

secure-aware exception/evidence classification;

secure-aware retire/publication fence;

secure-aware I/O denial или mediation;

secure-aware debug restrictions;

secure-aware scheduling/side-channel classification, хотя бы как declared risk level.

# 7. Что именно выгодно сделать сейчас

Я бы выбрал не “secure VMREAD”, не “secure VMCALL” и не “secure VmxCaps”, а следующий первый шаг:

## Ввести `SecureComputeDomainDescriptor` как нейтральный runtime-договор

Он должен находиться не в VMX compatibility frontend, а в Runtime, примерно концептуально:

```text
HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/
```

Минимальная модель:

```text
SecureComputeDomainDescriptor
  DomainTag
  SecurityLevel
  MeasurementRequired
  PrivateMemoryRequired
  HostInspectionPolicy
  EvidenceVisibilityPolicy
  MigrationPolicy
  IoPolicy
  HypercallPolicy
  DebugPolicy
  CompatibilityProjectionPolicy
```

Но в первом PR это должно быть **только договором и fail-closed admission**, без реального раскрытия новых VMREAD-полей.

Затем:

```text
SecureMemoryDomainDescriptor
DomainMeasurementDescriptor
SecureEvidencePolicy
SecureMigrationDescriptor
SecureIoDomainDescriptor
SecureHypercallDescriptor
```

# 8. Что не стоит делать сейчас

Не стоит сейчас:

открывать `GuestCr0` / `GuestCr4` для VMREAD;

добавлять secure compute bit в `VmxCaps` как authority;

разрешать VMCALL backend success;

делать VMCS secure-state store;

сериализовать secure VMCS image;

добавлять Shadow VMCS как protected nested state;

давать HostCr3/HostPc/HostSp через compatibility projection;

делать secure compute через декодерные флаги без runtime owner;

объявлять аттестацию без measurement descriptor и evidence policy.

Код прямо поддерживает такую осторожность: `GuestCr0`/`GuestCr4` denied, host execution aliases denied, HostCr3 denied, control-like VMREAD values denied, а VMREAD value projection не используется как readiness/migration authority. 

# 9. Практический ответ на два вопроса

## 1) При текущем прогрессе виртуализации выгоднее сейчас модифицировать под secure compute?

**Да, но только как foundational refactoring.**

Выгодно сейчас:

создать secure-domain descriptors;

добавить secure evidence/migration policies;

расширить memory domain классификацией private/shared/measured;

добавить typed grants для secure-domain действий;

закрыть VMX projection deny-by-default для secure-sensitive fields;

добавить conformance tests, что VMX не становится secure authority.

Не выгодно сейчас:

открывать полноценный secure compute execution;

открывать secure VMREAD/VMWRITE;

открывать secure hypercall backend;

добавлять attestation как готовую функцию;

делать VMX/VmxCaps владельцем secure compute.

## 2) Большая часть модернизации коснётся слоя виртуализации и меньше декодера и non-Virt слоя?

**Да, если под виртуализацией понимать доменную runtime-архитектуру. Нет, если иметь в виду только VMX compatibility frontend.**

Точная формула:

> **80% смысла secure compute должно лечь в Runtime/Domains/Memory/Capabilities/Evidence/Migration/Completion/I/O.  
> VMX compatibility должен получить только projection/denial hooks.  
> Decoder должен получить минимальную классификацию.  
> Non-Virt execution должен измениться только там, где обычные инструкции пересекают защищённую память, свидетельства, ввод-вывод, retire или миграцию.**

# 10. Итоговый вердикт

Текущий код находится в хорошем состоянии для **начала secure compute как нейтрального доменного направления**.

Но текущий код ещё не находится в состоянии для **объявления secure compute feature-complete**.

Лучший следующий шаг:

```text
SecureComputeDomainDescriptor + SecureAdmissionPolicy
```

с fail-closed поведением и тестами:

```text
secure owner missing -> denied
secure memory policy missing -> denied
measurement missing -> denied
evidence policy missing -> denied
VMX projection cannot grant secure authority
VmxCaps cannot enable secure compute
VMCS cannot store secure state
VMREAD secure-sensitive field denied unless neutral owner + secure visibility
migration rejects host-owned evidence
checkpoint rejects compatibility projection as authority
```

Это сохранит философию HybridCPU: **secure compute будет усилением доменной модели, а не возвращением к VMX/VMCS/VmxCaps как источнику истины.**
