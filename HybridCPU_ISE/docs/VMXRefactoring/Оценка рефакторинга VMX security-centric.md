 Выбранная модель делает HybridCPU не только **legality-centric**, но и **security-centric** — если реализовать её последовательно.

Но точнее формулировка такая:

```text
HybridCPU virtualization model is:
  legality-centric at execution boundary,
  security-centric at authority boundary,
  evidence-centric at runtime boundary.
```

## 1. Почему это больше, чем legality-centric

`Legality-centric` отвечает на вопрос:

```text
Можно ли эту операцию легально декодировать, разместить, выполнить и retire-нуть?
```

Для HybridCPU это:

```text
opcode
decoder ABI
InstructionIR
typed slot
lane admission
descriptor validation
MicroOp materialization
execute/capture
retire/writeback
replay/rollback
```

Это необходимо, но недостаточно. Такая модель доказывает, что операция **корректна как исполнение**, но ещё не доказывает, что она **безопасна как authority transition**.

Например, операция может быть typed-slot legal, но всё равно небезопасна:

```text
Lane7 submit legal по opcode/slot,
но domain не имеет capability grant.

DMA descriptor legal по формату,
но IoDomain не владеет memory range.

VMREAD legal по VMX ABI,
но field раскрывает host-owned evidence.

Completion route legal по типу,
но ведёт в чужой domain.
```

Поэтому одной legality-модели мало.

---

## 2. Что добавляет secure-centric слой

Security-centric модель отвечает на другой вопрос:

```text
Кто имеет authority выполнить эту операцию,
увидеть этот state,
получить этот completion,
использовать этот token,
мигрировать этот descriptor,
или делегировать capability дальше?
```

То есть безопасность становится не внешним policy-layer, а частью substrate.

В корректной HybridCPU-модели security сидит в:

```text
CapabilityDescriptorSet
ExecutionDomainDescriptor
MemoryDomainDescriptor
IoDomainDescriptor
LaneDescriptor
TokenNamespace
FenceDomain
CompletionRouteDescriptor
EvidencePolicy
MigrationDescriptor
NestedDomainDescriptor
```

Это значит: **безопасность не прикручена после выполнения, а участвует до decode/materialization/execute/retire publication.**

---

## 3. Главный сдвиг: от “can execute?” к “is authorized to exist?”

Legality-centric вопрос:

```text
Можно ли выполнить эту инструкцию?
```

Secure-centric вопрос:

```text
Имеет ли этот domain право даже сформировать такое намерение?
```

Пример:

```text
DmaStreamCompute descriptor
```

Legality проверяет:

```text
descriptor well-formed
types valid
alignment valid
queue format valid
lane6 slot available
replay semantics defined
```

Security проверяет:

```text
domain owns source range
domain owns destination range
IoDomain permits DMA
IOMMU mapping exists
token namespace belongs to caller
completion route targets allowed VT/domain
dirty tracking policy allows write
fence domain covers required visibility
```

И только потом операция может стать executable MicroOp.

---

## 4. Почему VMX-as-frontend усиливает secure-centric модель

Когда VMX перестаёт быть архитектурной осью, исчезает опасный shortcut:

```text
VMX says allowed → execute
```

Вместо этого:

```text
VMX request
  → generated projection
  → substrate descriptor lookup
  → capability check
  → evidence policy check
  → legality check
  → retire publication
```

То есть VMX больше не может быть “магической привилегированной дверью”.

`VMFUNC`, `VMWRITE`, `INVEPT`, `VMSAVEX`, `Lane7 VMFUNC leaf` — всё это не bypass, а frontend-запросы к substrate.

Это делает модель security-centric.

---

## 5. Security-centric не означает “всё запрещать”

Это не paranoid-модель, где всё блокируется. Это модель, где каждое право выражено явно:

```text
who owns
who may delegate
who may observe
who may mutate
who may migrate
who may route completion
who may bind backend
who may invalidate evidence
```

Security становится typed и composable.

Например capability grant должен быть не битом:

```text
Lane7Enabled = 1
```

а объектом:

```text
CapabilityGrant {
  capability_id: Lane7Submit
  owner_domain: D42
  scope: AcceleratorClass.MatrixFp16
  delegation: NoFurtherDelegation
  token_namespace: NS7
  completion_routes: [RouteA, RouteB]
  migration_class: RebindRequired
  evidence_visibility: NoBackendHandles
}
```

Это уже secure-centric substrate.

---

## 6. Secure-centric слой обязан быть раньше retire

Самое важное: security decision не должен происходить после факта.

Неправильно:

```text
execute
  → detect violation
  → trap
```

Правильно:

```text
validate authority
  → materialize executable operation
  → execute
  → retire visible effects
```

Для HybridCPU это особенно важно из-за:

```text
DMA
external accelerators
stream memory
posted completions
non-coherent fences
nested domains
migration
debug trace
host-owned evidence
```

Если security проверяется поздно, ущерб уже мог произойти: DMA записал память, accelerator получил native token, completion ушёл в чужой domain, trace раскрыл evidence.

---

## 7. Три слоя проверки должны быть разделены

Корректная модель должна разделять:

### 7.1. Structural legality

```text
Формат корректен?
Operand form допустим?
Descriptor well-formed?
Typed-slot подходит?
Lane binding возможен?
```

### 7.2. Authority/security

```text
Domain имеет право?
Capability grant существует?
Memory/I/O ownership подтверждён?
Completion route разрешён?
Token namespace принадлежит caller?
Evidence visibility безопасна?
```

### 7.3. Runtime feasibility

```text
Есть ресурс?
Queue не заполнена?
Backend доступен?
Epoch не overflow?
Fence domain может быть опубликован?
Migration не заблокирована?
```

Ошибки тоже должны быть разными:

```text
IllegalEncoding
DescriptorMalformed
CapabilityDenied
MemoryAuthorityDenied
IoAuthorityDenied
EvidenceAccessDenied
RouteDenied
QuotaExceeded
BackendUnavailable
EpochOverflow
MigrationBlocked
```

Если всё свалить в “VMFail” или “InvalidGuestState”, модель теряет security precision.

---

## 8. EvidencePolicy — центральный security объект

Главное отличие secure-centric HybridCPU — понятие evidence.

Не всё, что существует в runtime, можно показывать domain:

```text
можно:
  architectural guest/domain state
  virtual token
  virtual handle
  projected capability
  safe counter
  completion status

нельзя:
  physical VT placement
  native DMA token
  native accelerator token
  backend binding pointer
  decoded bundle proof
  MicroOp cache entry
  typed-slot proof
  scheduler heuristic
  host TLB/IOTLB internals
  raw trace handles
```

Поэтому `EvidencePolicy` — это security primitive, а не debug feature.

---

## 9. Nested domains делают security-centric модель обязательной

Nested virtualization без security-centric substrate невозможна корректно.

При nested composition нужно не просто проверить, что child descriptor legal. Нужно вычислить:

```text
effective_caps = parent_caps ∩ child_requested_caps
effective_memory = parent_memory ∩ child_memory_projection
effective_io = parent_io ∩ child_io_projection
effective_routes = parent_allowed_routes ∩ child_routes
effective_evidence = min(parent_evidence_policy, child_policy)
```

То есть nested mode — это security composition problem.

Если оставить VMCS12/VMCS02 как центр, легко начать думать “составим VMCS”. Но правильно: **составить authority graph**.

---

## 10. Migration тоже становится security-centric

Migration — это не просто сериализация state. Это security event.

Нужно доказать:

```text
target supports required capabilities
target security policy compatible
native tokens are not serialized
backend handles are not serialized
evidence is discarded
dirty log generation consistent
completion queues safe
Lane7 backend can be rebound or fail closed
IOMMU/domain mappings rebuilt
debug trace ACL preserved
```

Поэтому `MigrationDescriptor` и `EvidencePolicy` — часть security boundary.

---

## 11. Важная формула

Legality отвечает:

```text
может ли это быть корректной операцией?
```

Security отвечает:

```text
имеет ли этот domain право на эту операцию и её последствия?
```

Evidence отвечает:

```text
что можно сделать видимым после этой операции?
```

Retire отвечает:

```text
когда это становится архитектурным фактом?
```

В HybridCPU все четыре обязательны.

---

## 12. Значит ли это, что HybridCPU становится “secure-centric CPU”?

Да, но в правильном смысле.

Не так:

```text
security = отдельный режим / монитор / VMX root
```

А так:

```text
security = свойство каждого descriptor transition
```

То есть security не централизована в одном привилегированном режиме. Она распределена по substrate:

```text
Domain ownership
Capability grants
Memory authority
I/O authority
Lane authority
Token namespace
Fence domain
Completion route
Evidence policy
Migration policy
Retire publication
```

Это и есть secure-centric HybridCPU.

---

## 13. Финальная формула

```text
HybridCPU virtualization model =
  legality-centric execution
+ capability-centric authority
+ evidence-centric visibility
+ retire-centric publication
+ migration-aware security
+ nested-domain composability
```

Или короче:

```text
Correct HybridCPU virtualization is not only:
  “is this operation legal?”

It must also prove:
  “who is allowed to cause it,
   what it may touch,
   what it may reveal,
   where it may complete,
   what survives migration,
   and what evidence must remain host-owned.”
```

Да — выбранная модель делает HybridCPU **secure-centric**, и это не побочный эффект, а обязательное следствие отказа от VMX как архитектурной оси.