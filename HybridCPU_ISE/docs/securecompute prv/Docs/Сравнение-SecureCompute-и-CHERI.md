
# 2. Что такое Secure Compute в HybridCPU в вашей модели

Рассматриваемый Secure Compute в HybridCPU — это не новая VMX-функция и не “защищённый VMCS”. Это **усиленный доменный режим исполнения**, задаваемый примерно такими нейтральными объектами:

```text
SecureComputeDomainDescriptor
SecureMemoryDomainDescriptor
DomainMeasurementDescriptor
SecureEvidencePolicy
SecureMigrationDescriptor
SecureIoDomainDescriptor
SecureHypercallDescriptor
```

Смысл этой модели: домен может быть обычным или защищённым. Если защищённый домен не активирован, текущая ISA, декодер и non-VMX инструкции должны работать как раньше. Если защищённый домен активирован, Stage B/runtime-допуск начинает требовать дополнительные условия: измерение, private/shared memory policy, запрет host inspection, secure evidence policy, secure migration policy, secure I/O и secure hypercall policy.

Это хорошо согласуется с текущей философией HybridCPU: VMX не является архитектурной осью, VMCS не является владельцем состояния, VmxCaps не является источником полномочий, а новые non-VMX инструкции должны интегрироваться с общей системой легальности, исполнения, публикации и свидетельств, а не с VMX frontend. 

# 3. Главное различие: CHERI защищает ссылку, HybridCPU защищает домен

Самая короткая формула:

```text
CHERI:
  защита идёт от capability-ссылки к памяти

HybridCPU Secure Compute:
  защита идёт от домена к памяти, свидетельствам, вводу-выводу и публикации
```

CHERI спрашивает:

> “Есть ли у этой конкретной ссылки аппаратное право обратиться к этому диапазону памяти с такими правами?”

HybridCPU Secure Compute спрашивает:

> “Имеет ли этот домен, в этом runtime-контексте, с этой политикой памяти, свидетельств, ввода-вывода и миграции право выполнить это действие и опубликовать результат?”

Это не конкурирующие, а разные уровни. CHERI ближе к **микрооснованию безопасной адресации**. HybridCPU Secure Compute ближе к **макрооснованию защищённого исполняемого домена**.

# 4. Сравнительная таблица

| Ось | CHERI | HybridCPU Secure Compute |
|---|---|---|
| Первичная единица защиты | Capability как аппаратно проверяемая ссылка/полномочие | Домен исполнения с набором нейтральных дескрипторов |
| Главный уровень архитектуры | ISA, регистры, память, компилятор, ОС | Runtime-домены, виртуализация, память, свидетельства, миграция, завершение |
| Память | Все load/store/fetch авторизуются capability; есть bounds, permissions, tag, sealing | Private/shared/measured memory через `SecureMemoryDomainDescriptor`; пока скорее доменно-региональная модель |
| Полномочия | Невыковываемые capabilities, монотонное сужение прав | Typed grants и policy descriptors; не обязательно являются указателями или аппаратными токенами |
| Совместимость | Гибридная интеграция с обычными ISA, MMU, C/C++ и существующими ОС; для полной пользы нужен CHERI-aware стек ([University of Cambridge](https://www.cl.cam.ac.uk/research/security/ctsrd/cheri/)) | VMX/VMCS/VmxCaps остаются совместимым фасадом; secure compute не должен зависеть от VMX |
| Компартментализация | Тонкая: внутри процесса, между библиотеками, ОС-компонентами, приложениями | Более крупная: домен, вложенный домен, гостевая среда, secure-domain boundary |
| Миграция/checkpoint | Не является центральной частью базовой CHERI ISA | Одна из центральных осей: guest-visible state отдельно от host-owned evidence |
| Свидетельства/evidence | Не базовая ось CHERI; больше фокус на доказуемой memory authority | Центральная ось: evidence visibility, host-owned evidence quarantine, projection policy |
| Аттестация | Возможна поверх CHERI-систем, но не является ядром базовой модели capability ISA | Должна быть частью Secure Compute через `DomainMeasurementDescriptor` и secure evidence |
| Виртуализация | Не первичный механизм; CHERI может сосуществовать с MMU, ОС и гипервизором | Центральный механизм сборки secure compute как доменной функции |
| Сильная сторона | Аппаратная защита указателей, памяти, provenance, bounds, permissions | Политика защищённого домена, миграция, evidence, compatibility projection, host/guest разделение |
| Слабая сторона относительно другой модели | Не решает автоматически confidential VM, migration image, host evidence, VMX projection | Не даёт автоматически CHERI-уровня per-pointer spatial memory safety |

# 5. Где CHERI сильнее рассматриваемой HybridCPU-модели

## 5.1. CHERI сильнее в аппаратной нековкости полномочий

В CHERI capability защищается validity tag: если capability повреждена или сформирована как обычные данные, тег сбрасывается, и её нельзя использовать как действительное полномочие. ([University of Cambridge](https://www.cl.cam.ac.uk/techreports/UCAM-CL-TR-941.pdf))

В предложенной HybridCPU-модели typed grants и descriptors задают полномочия архитектурно и runtime-логически, но сами по себе они ещё не равны CHERI capability. Они не обязательно сопровождают каждый указатель, каждую ссылку, каждый load/store/fetch. Поэтому если HybridCPU хочет приблизиться к CHERI по строгости памяти, ему нужен дополнительный слой:

```text
SecureMemoryCapability
  base
  bounds
  permissions
  domain tag
  address-space tag
  epoch
  validity/integrity tag
  derivation policy
```

Иначе Secure Compute в HybridCPU остаётся сильной доменной политикой, но не становится CHERI-подобной capability-памятью.

## 5.2. CHERI сильнее в защите каждого обращения к памяти

CHERI требует, чтобы память, включая загрузки, сохранения и выборку инструкций, авторизовалась capability. ([University of Cambridge](https://www.cl.cam.ac.uk/techreports/UCAM-CL-TR-941.pdf))

HybridCPU Secure Compute, в предложенном виде, защищает память через `SecureMemoryDomainDescriptor`, private/shared/measured classifications, second-stage translation и domain policy. Это хорошо для confidential domain, но более грубо, чем CHERI. Оно может запретить хозяину читать private memory, но без per-reference capability оно не обязательно ловит все ошибки вида:

```text
указатель внутри домена вышел за границы объекта;
указатель был подделан арифметикой;
модуль внутри того же домена получил больше прав, чем должен;
компонент домена использовал чужой буфер как свой.
```

CHERI как раз нацелен на такие ошибки: bounds, permissions и provenance привязаны к capability, а не только к странице или домену. ([University of Cambridge](https://www.cl.cam.ac.uk/techreports/UCAM-CL-TR-941.pdf))

## 5.3. CHERI сильнее как формальная ISA-модель

CHERI — это hardware/software/semantics co-design: Cambridge подчёркивает, что у них есть формальные модели ISA, которые используются как архитектурное определение, документация, основа симуляторов, генерации тестов и механизированной проверки свойств безопасности. ([University of Cambridge](https://www.cl.cam.ac.uk/research/security/ctsrd/cheri/))

HybridCPU уже имеет conformance-ориентированную философию, но Secure Compute пока должен быть доведён до аналогичного уровня: формальная семантика `SecureComputeDomainDescriptor`, строгие invariants, отрицательные тесты, proof-like conformance и генератор схем.

# 6. Где HybridCPU Secure Compute потенциально сильнее CHERI

## 6.1. HybridCPU лучше расположен к confidential-domain compute

CHERI прежде всего отвечает на вопрос: “какая ссылка имеет право обращаться к какой памяти?” Он не является сам по себе полным аналогом SEV/TDX/TrustZone/confidential VM. Он не обязательно скрывает память от более привилегированного системного слоя, если тот обладает соответствующими capabilities и политикой ОС.

HybridCPU Secure Compute, наоборот, проектируется вокруг домена, в котором:

```text
private memory не должна читаться хозяином;
host-owned evidence не должно попадать в guest-visible state;
checkpoint не должен содержать host evidence;
restore должен пересоздавать host evidence;
migration должна сохранять только guest-visible state/policy;
VMX projection не должна становиться authority.
```

Это ближе к confidential computing / secure VM / protected domain, чем базовая CHERI ISA.

## 6.2. HybridCPU сильнее в evidence/migration дисциплине

Текущая HybridCPU-модель уже запрещает считать compatibility projection авторитетным checkpoint-состоянием: `DomainCheckpointImage` различает `DomainDescriptor` и `CompatibilityProjection`, а restore через compatibility projection не должен восстанавливать authoritative domain state. 

Текущая migration validation также запрещает compatibility projection metadata как authoritative restore state, требует recompute для host-owned evidence и требует preserve-policy для guest architectural state. 

У CHERI базовая ISA не занимается VMCS-проекциями, host-owned evidence и миграционными образами доменов. Это не недостаток CHERI как capability ISA, а просто другая область ответственности.

## 6.3. HybridCPU лучше контролирует совместимые фасады

В HybridCPU уже есть строгое правило: VMX — compatibility frontend, а не источник истины; VMCS — generated compatibility projection, а не substrate object; VmxCaps — projection, а не authority. 

Это важно для secure compute, потому что защищённый домен не должен случайно раскрыть состояние через VMREAD/VMWRITE или совместимые VMCS-поля. В CHERI такой проблемы в форме VMX/VMCS нет; зато у CHERI есть другая проблема совместимости — старый C/C++ код и старые представления указателей должны быть адаптированы или перекомпилированы для получения преимуществ capability-модели. Cambridge подчёркивает, что CHERI совместим с современными RISC, MMU и C/C++ стеком, но требует адаптации compiler/OS/application stack для полноценной защиты. ([University of Cambridge](https://www.cl.cam.ac.uk/techreports/UCAM-CL-TR-941.pdf))

# 7. Главное расхождение по ISA

CHERI **не прозрачен для ISA в том же смысле**, в каком вы хотите сделать Secure Compute в HybridCPU прозрачным для текущего non-VMX функционала.

CHERI расширяет ISA: появляются capability registers или расширенные регистры, capability-aware instructions, tagged memory, правила загрузки/сохранения capabilities, capability bounds, permissions, sealing. ([University of Cambridge](https://www.cl.cam.ac.uk/techreports/UCAM-CL-TR-941.pdf))

HybridCPU Secure Compute, если его вводить как мы обсуждали, может быть прозрачным для существующей ISA:

```text
SecureComputeDomainDescriptor absent
  → обычный домен
  → Stage A/B как раньше

SecurityLevel = Disabled
  → обычный домен
  → Stage A/B как раньше

SecurityLevel > Disabled
  → дополнительные Stage B/runtime restrictions
  → Stage A почти не меняется
```

То есть CHERI — это **расширение архитектуры указателей и памяти**. HybridCPU Secure Compute — это **расширение доменной политики исполнения**.

Если HybridCPU захочет получить CHERI-подобную строгость, тогда придётся добавить capability-aware memory model. Это уже будет либо новая ISA-подмодель, либо новый режим операндов/адресации, либо VLIW/EPIC metadata с capability-bearing operands. Полностью “невидимым” для ISA это уже не будет.

# 8. Сопоставление с вашими дескрипторами

## `SecureComputeDomainDescriptor`

Ближайший аналог CHERI — не один объект, а совокупность root capabilities, initial capabilities, compartment roots и sealing authority. В CHERI полномочия передаются и сужаются через capability-деривацию; в HybridCPU это доменный дескриптор, который задаёт политику защищённого режима.

Рекомендация: добавить CHERI-подобный принцип **монотонности доменной политики**:

```text
дочерний secure domain не может получить больше прав, чем родитель;
производная secure policy не может расширить host inspection;
производная migration policy не может разрешить больше payload/evidence;
производная compatibility projection policy не может открыть больше полей.
```

Это прямое архитектурное соответствие CHERI monotonicity.

## `SecureMemoryDomainDescriptor`

Это место, где стоит максимально учиться у CHERI.

Сейчас естественная HybridCPU-модель:

```text
private
shared
measured
runtime mutable
migration serializable
host visible / host invisible
```

CHERI добавляет недостающий уровень:

```text
base
bounds
permissions
validity tag
sealed/unsealed state
object type
derivation rules
```

Рекомендация: `SecureMemoryDomainDescriptor` должен иметь не только page/domain policy, но и возможность будущей per-object или per-region capability policy.

## `DomainMeasurementDescriptor`

У CHERI measurement не является центральной частью базовой capability ISA. Это скорее область secure boot, attestation, platform root of trust и ОС/прошивки. Поэтому здесь HybridCPU не должен пытаться “копировать CHERI”. Напротив, HybridCPU может быть сильнее: measurement должен быть родным элементом secure-domain creation.

## `SecureEvidencePolicy`

Прямого аналога в базовой CHERI ISA нет. CHERI доказывает допустимость доступа через capability, но не строит системную evidence visibility lattice для гостя/хозяина/миграции. Здесь HybridCPU должен сохранять собственную линию: evidence-centric visibility, host-owned evidence quarantine, retire-owned publication.

## `SecureMigrationDescriptor`

У CHERI нет базового VM migration/checkpoint authority слоя. Для HybridCPU это обязательная часть. Но можно заимствовать CHERI-подход к provenance:

```text
миграционный образ должен сохранять происхождение состояния;
нельзя восстановить capability/state без доказательства происхождения;
epoch/revocation должны предотвращать откат;
все восстановленные права должны быть не шире исходных.
```

## `SecureIoDomainDescriptor`

CHERI хорошо защищает память, но устройства, DMA, очереди и внешние агенты требуют отдельной системной модели. В HybridCPU это правильно выделять отдельно. Если добавить CHERI-подобную строгость, DMA descriptors должны нести memory capabilities или ссылаться на secure shared buffers с bounds/permissions/epoch.

## `SecureHypercallDescriptor`

CHERI даёт хороший урок против confused deputy: capability как аргумент системного вызова ограничивает, что более привилегированный код может сделать от имени менее привилегированного кода. В отчёте CHERI прямо подчёркивается intentionality: ядро, даже обладая широкими правами, должно использовать именно capability, переданную пользователем, чтобы не обратиться к памяти, которая не была намеренно авторизована. ([University of Cambridge](https://www.cl.cam.ac.uk/techreports/UCAM-CL-TR-941.pdf))

Для HybridCPU это означает: secure hypercall не должен получать “сырой guest pointer”. Он должен получать:

```text
SecureArgumentCapability
  bounds
  permissions
  memory class
  domain tag
  address-space tag
  evidence class
```

Иначе secure hypercall backend станет confused deputy.

# 9. Можно ли считать HybridCPU Secure Compute “аналогом CHERI”?

Нет, в строгом смысле нельзя.

**HybridCPU Secure Compute без capability-памяти — не CHERI.** Это доменная secure virtualization модель.

**CHERI без secure-domain evidence/migration/VMX-projection дисциплины — не HybridCPU Secure Compute.** Это capability ISA и программно-аппаратная модель безопасной памяти и компартментализации.

Более точная классификация:

```text
CHERI:
  memory-safety and compartmentalization architecture

HybridCPU Secure Compute:
  secure-domain and virtualization-oriented confidential execution architecture

Возможный синтез:
  CHERI-like capabilities inside HybridCPU secure domains
```

# 10. Лучший путь заимствования CHERI для HybridCPU

Я бы не предлагал “переписать HybridCPU под CHERI”. Это сломает философию VMX-free domain runtime и может нарушить прозрачность текущей ISA.

Лучший путь — **трёхслойный**.

## Слой 1. Текущий HybridCPU Secure Domain

Сначала ввести:

```text
SecureComputeDomainDescriptor
SecureMemoryDomainDescriptor
DomainMeasurementDescriptor
SecureEvidencePolicy
SecureMigrationDescriptor
SecureIoDomainDescriptor
SecureHypercallDescriptor
```

При отсутствии secure-домена всё должно быть прозрачно для текущей ISA.

## Слой 2. CHERI-подобная дисциплина полномочий

Добавить к secure descriptors принципы:

```text
unforgeability
monotonic derivation
sealed handles
explicit provenance
bounded memory authority
permission narrowing
epoch/revocation
```

Это ещё можно сделать без полного изменения ISA, если capabilities сначала будут runtime/descriptors-level.

Практическая рекомендация:

```text
Не объявлять HybridCPU Secure Compute эквивалентом CHERI.

Объявить его secure-domain моделью.

Добавить CHERI-inspired invariants:
  - unforgeable secure grants;
  - monotonic derivation;
  - sealed domain handles;
  - bounded memory authority;
  - explicit provenance;
  - epoch/revocation;
  - confused-deputy-safe hypercalls.
```
