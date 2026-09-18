О границе воздействия: какие элементы могут быть добавлены пассивно, а какие начнут менять Legality A|B или поведение обычных инструкций. Главный критерий: default/inactive secure policy не должна менять результат существующего исполнения.

Да, **такая интеграция может быть сделана прозрачно для текущей ISA, декодера, non-VMX инструкций и существующего функционала** — **если secure compute вводится как необязательный нейтральный доменный договор**, а не как новая глобальная обязанность всех инструкций.

Но есть важное уточнение:

> **secure compute не должен “активироваться только при VMX”. Он должен активироваться только при наличии secure-домена / secure-политики в runtime-контексте. VMX может быть одним из фасадов входа, но не владельцем включения.** 

# 1. Короткий ответ

**Не ломает текущий ISA и существующую Stage A|B легальность, если:**

```text
secure descriptor отсутствует
→ обычный домен работает как раньше

secure descriptor присутствует, но SecurityLevel = None / Disabled
→ обычный домен работает как раньше

secure descriptor присутствует и SecurityLevel > None
→ включаются дополнительные Stage B / runtime / memory / evidence / migration ограничения
```

**Ломает или рискует сломать, если:**

Forbidden examples, not implementation guidance:

```text
secure compute добавляется как обязательная проверка для всех инструкций;
Stage A начинает требовать secure-политику от обычных non-VMX инструкций;
декодер начинает решать secure-допуск;
VMX/VmxCaps начинает включать secure compute;
VMCS начинает хранить secure-состояние;
обычные инструкции начинают получать новые отказы без secure-домена.
```

То есть правильная интеграция — **пассивная по умолчанию, строгая при явном secure-домене**.

# 2. Влияет ли это на Stage A?

В нормальной архитектуре — **почти нет**.

Stage A должна отвечать за форму команды, синтаксис, базовую ISA-допустимость, декодирование, структурные ограничения. Secure compute не должен превращать Stage A в “проверку защищённого мира”.

Для существующих инструкций Stage A должна остаться прежней:

```text
ADD
LOAD
STORE
BRANCH
CALL
RET
VLIW bundle
EPIC slot legality
non-VMX instruction
```

не должны становиться незаконными только потому, что в архитектуре появился `SecureComputeDomainDescriptor`.

Документация текущей модели как раз говорит, что новые non-VMX инструкции должны интегрироваться через ISA metadata, Stage A legality, Stage B legality, execution lane binding, retire/publication model и evidence/migration policy, если это нужно, а не через VMX frontend или VMX-specific handler. 

Поэтому secure compute должен добавить к Stage A максимум **классификационные признаки**, например:

```text
InstructionMayAccessMemory
InstructionMayTouchIo
InstructionMayCreateEvidence
InstructionMayTrap
InstructionMayAffectRetirePublication
InstructionIsDebugSensitive
InstructionIsPrivilegedStateSensitive
```

Но Stage A не должна спрашивать:

```text
есть ли измерение домена?
можно ли хозяину читать память?
разрешён ли secure migration?
можно ли показать evidence?
можно ли открыть VMREAD?
```

Это не её уровень.

# 3. Влияет ли это на Stage B?

Да, **но только для secure-доменов**.

Stage B — правильное место для secure compute, потому что именно там решается не “правильная ли форма команды”, а “имеет ли данный домен право выполнить это действие с данным ресурсом и с данной видимостью”.

Для обычного домена:

```text
SecureComputeDomainDescriptor absent
или SecurityLevel = Disabled
```

Stage B должна вести себя как сейчас.

Для secure-домена:

```text
SecureComputeDomainDescriptor present
и SecurityLevel > Disabled
```

Stage B получает дополнительные условия:

```text
доступ к памяти должен пройти SecureMemoryDomainDescriptor;
ввод-вывод должен пройти SecureIoDomainDescriptor;
гипервызов должен пройти SecureHypercallDescriptor;
миграция должна пройти SecureMigrationDescriptor;
свидетельства должны пройти SecureEvidencePolicy;
отладка должна пройти DebugPolicy;
VMX-проекция должна пройти CompatibilityProjectionPolicy;
host inspection должен пройти HostInspectionPolicy.
```

То есть **существующие инструкции не становятся другими по форме**, но их выполнение внутри защищённого домена может быть запрещено, если они нарушают secure-договор.

Это нормально и не ломает ISA. Это означает: одна и та же команда имеет разный Stage B результат в разных доменах.

# 4. “Активируется только при виртуализации” — почти, но точнее иначе

Формулировку лучше немного поправить.

Не так:

```text
secure compute активируется только при виртуализации
```

А так:

```text
secure compute активируется только при secure domain runtime context
```

Почему это важно?

Потому что в HybridCPU виртуализация — это доменная модель, а не VMX. Secure compute должен быть свойством домена, а не VMX-режима.

Правильная иерархия:

```text
обычное исполнение
  → без secure descriptor
  → поведение прежнее

обычная виртуализация
  → domain descriptor есть
  → secure descriptor отсутствует
  → обычная virtualization policy

secure compute
  → domain descriptor есть
  → secure descriptor materialized
  → SecurityLevel > Disabled
  → включаются secure restrictions

VMX
  → только compatibility projection
  → не включает secure compute сам
```

VMX может показать часть secure compute наружу, но не должен быть механизмом включения.

# 5. Почему текущий код хорошо подходит для такой интеграции

Потому что уже есть правильная граница допуска.

Текущий VMREAD path сначала проходит decode, затем compatibility projection, затем `RuntimeBoundaryAdmissionService`, и только после этого read-only value projection из neutral owners. 

Это именно тот паттерн, в который secure compute можно встроить без поломки ISA:

```text
decode
→ обычная форма команды
→ runtime boundary admission
→ secure-domain admission, если secure domain есть
→ neutral owner lookup
→ evidence policy
→ completion / retire publication
```

Также тесты runtime admission уже показывают правильную философию: runtime-owned domain enter разрешается при typed grant и evidence policy, compatibility frontend authoritative mutation запрещается, missing typed grant запрещается, host-owned evidence exposure запрещается. 

Это означает, что secure compute можно встроить как дополнительный слой требований в уже существующую модель допуска, а не переписывать декодер или ISA.

# 6. Как должны вести себя предложенные дескрипторы

## `SecureComputeDomainDescriptor`

Это главный переключатель, но не глобальный.

Рекомендуемая семантика:

```text
null / absent
  → secure compute не активен
  → обычный домен

SecurityLevel = Disabled
  → secure compute не активен
  → обычный домен

SecurityLevel > Disabled
  → secure compute активен
  → все sub-policies обязательны по мере использования
```

Поля:

```text
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

не должны сами менять декодирование. Они должны менять **runtime-допуск и владельцев ресурсов**.

## `SecureMemoryDomainDescriptor`

Должен влиять на LOAD/STORE/FETCH/DMA только когда домен secure.

Для обычных доменов — нет эффекта.

Для secure-домена:

```text
private memory нельзя читать хозяину;
shared memory должна быть явно помечена;
measured memory входит в измерение;
DMA не может обходить secure memory policy;
откат эпохи памяти должен быть запрещён или обнаружен.
```

## `DomainMeasurementDescriptor`

Не должен влиять на обычное исполнение.

В secure-домене:

```text
MeasurementRequired = true
и measurement missing
→ domain enter denied
```

Но обычная инструкция `ADD` или `LOAD` не должна становиться Stage A illegal из-за отсутствия measurement. Отказ должен происходить при входе в secure-домен или при операции, требующей measured state.

## `SecureEvidencePolicy`

В обычном домене — обычная `EvidencePolicy`.

В secure-домене — более строгая политика:

```text
host-owned evidence не видна гостю;
compatibility alias может быть запрещён;
debug evidence может быть запрещён;
migration evidence должна быть классифицирована;
attestation evidence не должна раскрывать host runtime facts.
```

Текущая migration/evidence модель уже близка к этому: checkpoint обнаруживает host-owned evidence и запрещает compatibility projection как authoritative state. 

## `SecureMigrationDescriptor`

Не должен менять обычный checkpoint.

Для secure-домена должен требовать:

```text
запрет host-owned evidence;
запрет VMCS projection as authority;
anti-rollback / epoch;
preserve guest-visible secure state;
recompute host evidence after restore;
secure re-attestation after restore, если требуется.
```

Текущая `MigrationValidationPolicy` уже содержит нужную форму: она запрещает compatibility projection metadata как authoritative state, требует recompute для host-owned runtime evidence и требует preserve policy для guest architectural state. 

## `SecureIoDomainDescriptor`

Для обычных инструкций без I/O — нет эффекта.

Для secure-домена:

```text
портовый I/O
MMIO
DMA
device queue
interrupt injection
shared buffers
```

должны проходить secure I/O policy.

## `SecureHypercallDescriptor`

Не должен открывать `VMCALL`.

Сейчас hypercall backend admission уже fail-closed: нужен runtime admission, neutral trap, backend descriptor, runtime authority, validated domain, typed grant, evidence approval и materialized neutral backend owner. При отсутствии owner backend execution запрещается. 

Для secure compute это нужно сохранить:

```text
secure hypercall owner missing
→ denied

compatibility VMCALL recognized
→ admitted-denied
→ не backend success
```

Текущий VMCALL path как раз передаёт `MissingNeutralOwner`, то есть не создаёт backend success. 

# 7. Что будет с non-VMX инструкциями?

Они не должны ломаться.

Но внутри secure-домена некоторые из них могут получать новые Stage B отказы.

Пример:

```text
LOAD обычной памяти в обычном домене
→ как раньше

LOAD private secure memory внутри secure-домена
→ allowed, если домен владелец

LOAD private secure memory хозяином
→ denied

STORE shared secure buffer
→ allowed только если область явно shared

DEBUG_READ guest register
→ denied, если DebugPolicy запрещает

MMIO write
→ denied, если SecureIoDomainDescriptor не разрешает
```

Это не поломка ISA. Это нормальная доменная политика исполнения.

# 8. Что будет с декодером?

Декодер должен остаться почти неизменным.

Сейчас VMX decode boundary проверяет frozen opcode и предварительные условия descriptor/capability/scheduling/no-emission validation. 

Для secure compute не нужно превращать декодер в security authority.

Можно добавить только metadata:

```text
requiresSecureDomainCheck
mayTouchPrivateMemory
mayExposeEvidence
mayRequireSecureRetireFence
isDebugSensitive
isIoSensitive
```

Но декодер не должен выполнять:

```text
measurement validation;
private memory validation;
host inspection validation;
attestation validation;
migration validation;
secure hypercall validation.
```

# 9. Где возможна реальная поломка основ

Интеграция начнёт ломать основы HybridCPU, если сделать одно из следующих:

1. **Secure compute станет VMX capability.**  
   Например, `VmxCaps` начнёт выдавать право на secure domain. Это нельзя. Сейчас `VmxCapsProjection` является read-only compatibility alias поверх capability publication policy, а запись отвергается или не имеет эффекта. 

2. **Secure state будет храниться в VMCS.**  
   Это вернёт VMCS как state owner.

3. **Stage A начнёт требовать secure policy для обычных инструкций.**  
   Тогда обычная ISA действительно сломается.

4. **Отсутствие secure descriptor будет означать deny.**  
   Это сломает весь текущий runtime. Отсутствие secure descriptor должно означать “не secure-домен”, а не “некорректный домен”.

5. **Secure policy будет глобальной, а не доменной.**  
   Тогда все домены внезапно получат новые ограничения.

6. **VMREAD начнёт раскрывать secure fields без neutral owner + secure evidence policy.**  
   Сейчас VMREAD value projection аккуратно требует runtime admission, schema owner lookup и neutral owner source. 

7. **GuestCr0/GuestCr4 будут открыты как secure state.**  
   Сейчас они правильно denied до появления neutral privileged execution-state semantics. 

# 10. Правильная модель совместимости

Нужна двухрежимная семантика по умолчанию:

```text
SecureComputeDomainDescriptor отсутствует
  → legacy/current behavior unchanged

SecureComputeDomainDescriptor присутствует, SecurityLevel = Disabled
  → current behavior unchanged

SecureComputeDomainDescriptor присутствует, SecurityLevel > Disabled
  → secure-domain Stage B restrictions active

SecureComputeDomainDescriptor присутствует, но sub-policy missing
  → fail-closed only for secure-domain operation
```

Это важно: **fail-closed должен срабатывать только после явной активации secure-домена**.

Иначе secure compute действительно начнёт ломать текущую систему.

# 11. Итог

Правильно интегрированная модель:

```text
SecureComputeDomainDescriptor
SecureMemoryDomainDescriptor
DomainMeasurementDescriptor
SecureEvidencePolicy
SecureMigrationDescriptor
SecureIoDomainDescriptor
SecureHypercallDescriptor
```

**не ломает текущую ISA, не ломает декодер, не ломает обычные non-VMX инструкции и не меняет Stage A для существующего кода.**

Она добавляет новый слой Stage B / runtime-допуска, который активен только при наличии secure-домена.

Самая точная формула:

> **secure compute должен быть opt-in доменным усилением, а не глобальной ISA-модификацией.  
> Для обычного исполнения он невидим.  
> Для secure-домена он становится обязательной политикой допуска, памяти, свидетельств, ввода-вывода, гипервызовов, миграции и публикации.  
> VMX при этом остаётся только совместимой проекцией, а не переключателем secure compute.**
