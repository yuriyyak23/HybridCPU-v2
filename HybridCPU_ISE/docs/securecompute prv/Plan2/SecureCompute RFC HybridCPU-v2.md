
## Рекомендации по RFC: Positive Secure Backend Runtime Execution

Главная позиция: **этот RFC не должен сразу “включать backend execution”**. Он должен сначала определить нейтрального владельца, цепочку допуска, публикацию результата и отрицательные тесты. Сейчас Plan2 прямо фиксирует: positive secure backend runtime execution не открыт и требует отдельной implementation phase, RFC/ADR, neutral runtime owner, proof chain, denied tests и доказательства, что VMX/VMCS/`VmxCaps` не могут авторизовать исполнение. 

# 1. Название RFC

```text
RFC-14A: Positive Secure Backend Runtime Execution Owner
```

Связанный ADR:

```text
ADR-14B: Secure Backend Completion / Retire Publication Contract
```

Можно держать как один RFC с двумя крупными разделами, но архитектурно лучше разделить:

- RFC-14A — **кто имеет право исполнять**;
- ADR-14B — **когда и как результат становится видимым**.

---

# 2. Главный принцип RFC

RFC должен закрепить:

```text
Secure backend execution is not authorized by:
  VMX decode
  VMCALL compatibility admission
  VMREAD / VMWRITE
  VMCS field
  VmxCaps projection
  compatibility projection
  scalar handle
  completion record alone
```

Разрешение возможно только через:

```text
neutral runtime backend owner
+ enabled secure domain descriptor
+ typed capability grant
+ secure operation policy
+ argument/shared-buffer classification
+ evidence approval
+ completion fence
+ retire publication rule
+ current epoch/provenance validation
+ negative conformance coverage
```

Это продолжает уже закрытый Post-Phase10 gate: он допускает только policy-evidence proof, но backend execution request остаётся denied даже при полной proof chain. 

---

# 3. Обязательные сущности RFC

## 3.1. `SecureBackendOwnerDescriptor`

Нейтральный владелец backend execution.

Минимальные поля:

```text
BackendOwnerId
DomainTag
OwnerKind
SupportedOperationClasses
RequiredSecurityLevel
RequiredGrantClass
RequiredEvidenceClass
RequiredCompletionFence
RequiredRetireRule
BackendEpoch
Provenance
MaterializationState
```

Ключевое правило:

```text
BackendOwnerId is not VMCS pointer.
BackendOwnerId is not VMX state.
BackendOwnerId is not guest-visible scalar authority.
```

## 3.2. `SecureBackendExecutionRequest`

Запрос на реальное исполнение.

Поля:

```text
DomainTag
OperationClass
OperationId
ArgumentClassification
SharedBufferHandles
RequiredGrant
EvidenceRequestClass
CompletionPublicationIntent
RetirePublicationIntent
CurrentEpoch
CallerProvenance
```

## 3.3. `SecureBackendExecutionDecision`

Результат допуска.

Статусы должны быть максимально явными:

```text
DeniedMissingOwner
DeniedMissingGrant
DeniedMissingPolicy
DeniedInvalidArguments
DeniedPrivatePointer
DeniedEvidencePolicy
DeniedMissingCompletionFence
DeniedMissingRetireRule
DeniedStaleEpoch
DeniedCompatibilityAuthority
AllowedProofOnlyNoExecution
AllowedInternalExecutionNoPublication
AllowedCompletionOnly
AllowedRetirePublication
```

Важно: **не начинать сразу с `AllowedRetirePublication`**. Первый positive этап должен, максимум, разрешить:

```text
AllowedInternalExecutionNoPublication
```

или даже оставить только:

```text
AllowedProofOnlyNoExecution
```

до появления полноценного publication contract.

---

# 4. Минимальная цепочка допуска

RFC должен зафиксировать строгую последовательность:

```text
1. Secure descriptor present and enabled
2. DomainTag / AddressSpaceTag match
3. Operation class is secure-backend eligible
4. Neutral backend owner exists and is materialized
5. Backend owner epoch is current
6. Typed grant exists
7. Grant scope matches operation class
8. Grant provenance is valid
9. Secure operation policy allows the request
10. Arguments are classified
11. Raw private pointers are denied
12. Shared buffers are explicit and bounded
13. Evidence policy approves visibility
14. Completion fence exists
15. Retire publication rule exists
16. VMX/VMCS/VmxCaps/compatibility authority absent
17. Negative tests exist for every denied branch
18. Only then backend execution may proceed
```

---

# 5. Что считать первым допустимым positive path

Рекомендация: **не начинать с VMCALL**.

Первый positive backend path должен быть минимальным, нейтральным и не VMX-зависимым:

```text
SecureBackendOperationKind.TestNoSideEffect
```

или:

```text
SecureBackendOperationKind.MeasurementRefresh
```

Свойства первого positive path:

```text
no private memory read
no DMA
no device side effect
no VMX projection
no guest-visible publication by default
no migration payload mutation
no host evidence leakage
```

Идеальный первый positive path:

```text
neutral secure backend owner
+ typed grant
+ no arguments
+ evidence class = internal/recomputed
+ completion fence required
+ retire publication denied
```

То есть он доказывает, что backend owner может быть найден и проверен, но ещё не открывает опасные внешние эффекты.

---

# 6. Договор публикации результата

## 6.1. Разделить три результата

RFC/ADR должен запретить смешение:

```text
backend execution result
completion publication
retire publication
```

Рекомендуемая модель:

```text
BackendExecutionResult
  внутренний результат backend owner

SecureCompletionRecord
  результат прошёл completion fence

RetirePublicationRecord
  результат опубликован архитектурно
```

Нельзя:

```text
backend success -> guest visible
completion record -> retire effect
VMX exit/projection -> publication
```

## 6.2. Publication ladder

```text
Denied
  ↓
AllowedProofOnlyNoExecution
  ↓
AllowedInternalExecutionNoPublication
  ↓
AllowedCompletionPublication
  ↓
AllowedRetirePublication
```

Каждый уровень требует отдельного доказательства.

## 6.3. Completion fence

Completion fence должен проверять:

```text
operation completed internally
evidence class approved
no host-owned evidence in guest-visible result
arguments sanitized
private buffers not exposed
shared buffers bounded
backend epoch current
publication target allowed
```

## 6.4. Retire publication rule

Retire publication требует дополнительно:

```text
retire-owned publication rule exists
operation is architecturally publishable
side effects are classified
rollback/replay policy is valid
migration class is defined
guest-visible state update is allowed
compatibility projection is not authority
```

---

# 7. Evidence policy для backend execution

RFC должен требовать классификацию каждого evidence item:

```text
HostOwned
GuestVisible
MigrationSerializable
RecomputedAfterRestore
DebugOnly
CompatibilityAlias
Denied
```

Правило:

```text
HostOwned evidence never enters:
  guest architectural state
  VMCS projection as authority
  checkpoint payload as authority
  retire publication payload
```

Если результат нужно показать гостю, он должен быть пересобран как guest-visible evidence, а не скопирован из host-owned evidence.

---

# 8. Аргументы и память

Для secure backend execution нельзя принимать “сырой гостевой адрес” как полномочие.

Нужны классы аргументов:

```text
ImmediateValue
GuestVisibleSharedBuffer
MeasuredBuffer
OpaqueRuntimeHandle
PrivatePointerDenied
HostPointerDenied
CompatibilityScalarDenied
```

Для shared buffer:

```text
BufferId
DomainTag
AddressSpaceTag
Bounds
Direction
Lifetime
Owner
EvidenceClass
Grant
Epoch
```

Правило:

```text
Raw private guest pointer -> denied.
Scalar handle from guest-visible state -> lookup only, never authority.
```

---

# 9. VMX / VMCALL позиция

Если backend execution запускается через VMCALL-compatible вход, путь должен быть таким:

```text
VMCALL decode succeeds
→ compatibility admission succeeds as vocabulary
→ runtime secure backend admission starts
→ VMX contributes no authority
→ backend owner lookup is neutral
→ result publication is neutral
→ optional VMX projection only after publication policy
```

И явно:

```text
VMCALL admitted != backend success
VMExit-like event != publication
VMREAD field != evidence owner
VmxCaps bit != grant
VMCS field != backend state
```

---

# 10. Минимальные negative tests RFC

Перед первым positive backend path нужны тесты:

```text
missing backend owner -> denied
backend owner from VMCS -> denied
backend owner from VMX helper -> denied
VmxCaps grant -> denied
VMREAD/VMWRITE backend mutation -> denied
compatibility projection as authority -> denied
missing typed grant -> denied
wrong grant scope -> denied
revoked grant -> denied
stale backend epoch -> denied
missing provenance -> denied
raw private pointer -> denied
shared buffer without bounds -> denied
shared buffer without grant -> denied
host-owned evidence publication -> denied
completion without fence -> denied
retire without retire rule -> denied
backend success cannot publish guest-visible state directly
completion fence alone does not imply retire
```

---

# 11. Минимальные positive tests

Только после negative tests:

```text
neutral backend owner + typed grant + valid epoch + no args
→ AllowedInternalExecutionNoPublication

neutral backend owner + typed grant + evidence approval + completion fence
→ AllowedCompletionPublication

neutral backend owner + typed grant + completion fence + retire rule
→ AllowedRetirePublication
```

Но последний тест лучше оставить на отдельный поздний PR.

---

# 12. Запрещённые сокращения

RFC должен прямо запретить:

```text
BackendExecutionAuthorized: true без retire rule
backend success через VMCALL decode
backend owner через VMCS field
backend owner через active VMCS pointer
secure grant через VmxCaps
secure result через VMREAD
completion record как retire permission
host evidence как guest-visible result
raw private pointer как shared buffer
positive secure hypercall backend без neutral owner
```

---

# 13. Рекомендуемая очередность PR

```text
PR-14A.1 — RFC/ADR documents only
PR-14A.2 — SecureBackendOwnerDescriptor + denied tests
PR-14A.3 — SecureBackendExecutionRequest/Decision + proof-only admission
PR-14A.4 — internal no-side-effect backend execution, no publication
PR-14B.1 — completion fence contract
PR-14B.2 — completion publication for one safe operation
PR-14B.3 — retire publication rule, still one safe operation only
PR-14B.4 — production-claim audit update
```

---

# 14. Рекомендуемый итоговый статус после RFC

Не писать:

```text
SecureCompute complete
```

Писать:

```text
SecureCompute backend execution opened for explicitly listed neutral operation classes,
with proof-chain admission, completion fence and retire publication contract.
VMX/VMCS/VmxCaps remain non-authoritative.
All other backend operation classes remain denied.
```

---

## Краткая итоговая рекомендация

Для завершения этого аспекта нужен RFC, который сначала **доказывает право на backend execution**, а не просто включает его. Центральная конструкция — `SecureBackendOwnerDescriptor` + `SecureBackendExecutionDecision`. Связанный ADR должен отделить **internal execution**, **completion publication** и **retire publication**. Первый positive path должен быть нейтральным, без побочных эффектов, без VMX, без private memory и без guest-visible публикации по умолчанию.

> Phase 14 closure note (2026-05-31): this RFC guidance is advisory input only. It does not authorize product code, backend execution, completion publication, retire effects, VMX/VMCS/`VmxCaps` authority, decoder/encoder changes or capability-aware ISA work. Any implementation requires a separate approved RFC/ADR, phase plan, proof chain and negative conformance tests.
