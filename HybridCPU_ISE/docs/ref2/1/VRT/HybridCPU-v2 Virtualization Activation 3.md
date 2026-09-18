
## Актуализированное решение по оставшимся production-блокерам

### 0. Базовая точка и обязательные архитектурные инварианты

Решения ниже сохраняют действующие архитектурные правила HybridCPU-v2:

- canonical decode/ingress не является execution authority;
- финальная legality остаётся runtime-owned;
- compatibility projection не владеет production state;
- VMX/VMCS являются compatibility vocabulary, а не runtime authority;
- compiler metadata является structural/emission evidence, а не admission capability;
- generic runtime legality, Stage A admission и Stage B lane materialization сохраняются;
- Stage B не расширяет legality, принятую Stage A;
- execution ownership определяется live owner/context/domain state;
- memory, I/O/IOMMU, SecureCompute и lane-local state имеют собственных neutral runtime owners;
- backend execution не означает completion publication;
- completion publication не означает architectural retire;
- replay/determinism ограничены явным invalidation-bounded envelope;
- неизвестные, неподтверждённые или неполные пути остаются fail-closed.

Virtualization WhiteBook прямо определяет virtualization как часть общей runtime legality/admission system, а execution, memory, I/O, capability/evidence и completion — как neutral-owned surfaces; VMX проецирует эти факты только после neutral authorization. 

---

# 1. Целевая authority chain

Production-цепочка принимается в следующем виде:

```text
                 GOVERNANCE / POLICY PLANE

VirtualizationDecisionSpecV2
            +
VirtualizationDecisionAcceptanceRecordV2
            ↓
       D2 Accepted Decision
            ↓
VirtualizationOperationOwnerSnapshot (O1)

D2/O1 define accepted semantics and policy.
D2/O1 are NOT runtime execution authority.


                    RUNTIME PLANE

Canonical VMX/VMCALL decode
            ↓
generic runtime legality / owner-domain guards
            ↓
Stage A class admission
            ↓
Stage B lane materialization
            ↓
E1 VirtualizationAdmissionCertificate
            ↓
canonical operand materialization
            ↓
VirtualizationOperandSnapshot
            ↓
RuntimeBoundaryAdmissionService
            ↓
domain / capability / evidence admission
            ↓
neutral trap / operation policy
            ↓
owner-specific D2/O1 validation
            ↓
SafetyVerifier E2
            ↓
DomainHypercallRuntimeOwner / Executor
            ↓
E3 VirtualizationExecutionReceipt
            ↓
TrapCompletionRouteService
            ↓
TrapCompletionPublicationFence
            ↓
neutral completion owner
            ↓
E5 VirtualizationCompletionToken
            ↓
canonical retire eligibility / ordering
            ↓
E6 VirtualizationRetireGrant
            ↓
RetireCoordinator
            ↓
architectural publication
            ↓
E7 migration / restore / determinism contract
```

Назначение уровней:

```text
D2 = accepted architectural/governance decision
O1 = immutable runtime-loaded policy snapshot
E1 = admitted VMX attempt, fault-only
E2 = authority выполнить одну конкретную virtualization operation
E3 = evidence фактически выполненного backend execution
E4 = доказанная canonical production composition
E5 = exact-once completion authority
E6 = exact-once architectural retire authority
E7 = migration/restore/determinism contract
```

---

# 2. Блокер 1 — нет v2 Decision Spec / Acceptance validator

## Решение

Ввести два независимых machine-readable артефакта.

### `VirtualizationDecisionSpecV2`

Обязательные поля:

```text
SchemaVersion
DecisionId

OperationNamespace
OperationId

OwnerId
OwnerClass

LeafWidth
NumericLeaf

OperandAbiVersion
OperandRules
ResultAbi

OperationClass
ArchitecturalEffectClass

CapabilityRequirement
EvidenceRequirement
DomainRequirement

CancellationPolicy
ReplayPolicy
MigrationPolicy

CompletionPolicy
RetirePolicy

AdjacentLeafPolicy

SpecDigest
```

Spec определяет **семантику принятой операции**.

### `VirtualizationDecisionAcceptanceRecordV2`

Использовать `AcceptanceRecord`, а не security-loaded термин `Attestation`.

Обязательные поля:

```text
DecisionId
SpecDigest
SpecCommitSha

AcceptanceState
AcceptedBy
AcceptancePolicyVersion

OwnerReviewEvidence
ArchitectureReviewEvidence

SupersedesDecisionId

AcceptanceDigest
```

Acceptance Record определяет **какой exact spec был принят и по какой governance-процедуре**.

Сам `DecisionSpec` не может объявлять себя принятым.

## Validator

Ввести:

```text
VirtualizationDecisionValidatorV2
```

Validator fail-closed проверяет:

```text
supported schema
canonical deterministic serialization
SpecDigest
AcceptanceRecord -> exact SpecDigest
DecisionId consistency
OwnerId != 0
valid OwnerClass
owner not compatibility/compiler/VMCS authority
NumericLeaf != 0
leaf width
namespace/leaf collision
operand ABI completeness
result ABI completeness
effect class
capability policy
evidence policy
domain policy
cancellation policy
replay policy
migration policy
completion policy
retire policy
adjacent/unknown leaf = deny
revoked/superseded state
required owner review
required architecture review
CODEOWNERS/reviewer mapping
```

Выход validator:

```text
AcceptedVirtualizationDecision
```

`AcceptedVirtualizationDecision` не является runtime capability.

## Acceptance criteria

D2 закрыт, когда невозможно загрузить как accepted:

```text
malformed spec
zero leaf
duplicate leaf
wrong namespace
incomplete ABI
missing owner
self-accepted spec
digest mismatch
missing required review
revoked decision
superseded decision
```

---

# 3. Блокер 2 — не назначен neutral owner

## Решение

Назначить neutral runtime role:

```text
DomainHypercallRuntimeOwner
```

Рекомендуемая структура:

```text
Core/Runtime/Events/Hypercalls/

HypercallRuntimeOwnerDescriptor
DomainHypercallRuntimeOwnerService
DomainHypercallRuntimeExecutor
```

Neutral означает независимость от **VMX/VMCS compatibility authority plane**, а не нахождение за пределами репозитория.

## Owner scope

`DomainHypercallRuntimeOwner` владеет:

```text
hypercall operation semantics
hypercall namespace
leaf allocation policy
operand ABI
result ABI
operation effect class
backend semantics
capability requirements
evidence requirements
domain requirements
cancellation semantics
migration classification
```

Не владеет:

```text
generic runtime legality
Stage A / Stage B
SafetyVerifier
VMCS projection
memory/address-space state
IOMMU state
device state
lane-local runtime state
SecureCompute
completion publication
architectural retire
compiler emission authority
```

Обязательные различия:

```text
DomainHypercallRuntimeOwner != SafetyVerifier
DomainHypercallRuntimeOwner != VmxCompatibilityAdmissionService
DomainHypercallRuntimeOwner != MemoryDomain owner
DomainHypercallRuntimeOwner != IoDomain owner
DomainHypercallRuntimeOwner != RetireCoordinator
DomainHypercallRuntimeOwner != Compiler
```

Текущая архитектура уже запрещает compatibility frontend владеть backend execution и требует neutral runtime owner. 

## Governance

Зафиксировать:

```text
stable non-zero OwnerId
CODEOWNERS mapping
required owner reviewer
required architecture reviewer
OwnerPolicyVersion
revocation rules
supersession rules
```

---

# 4. Блокер 3 — нет namespace, width, exact leaf и operand ABI

## Решение для первого production slice

Принять через D2:

```text
Namespace:
    HybridCPU.VMCALL.Runtime.v1

Leaf width:
    16 bit

Reserved invalid leaf:
    0x0000

First production leaf:
    0x0001

Operation:
    PROBE_NO_STATE_V1
```

`0x0001` становится production ABI только после accepted D2.

## Operand ABI

Для `PROBE_NO_STATE_V1`:

```text
Rs1 = architectural register containing actual numeric leaf
Rs2 = x0
Rd  = x0 / no result
```

Leaf — **значение Rs1**, а не register selector `Rs1`.

Canonical capture:

```text
E1
 ↓
read architectural Rs1 exactly once
 ↓
VirtualizationOperandSnapshot
 ↓
validate upper bits == 0
 ↓
ushort leaf
 ↓
exact D2/O1 lookup
```

Silent truncation запрещён.

Условие:

```text
fullRs1Value & ~0xFFFF == 0
```

## Первый leaf

`PROBE_NO_STATE_V1`:

```text
architectural register writes = none
memory effects                = none
IO effects                    = none
IOMMU effects                 = none
device effects                = none
SecureCompute effects         = none
nested effects                = none
scheduler-visible semantics   = none
explicit PC redirect          = none
payload                       = none
```

Физическое выполнение остаётся serializing VMX/SystemSingleton-compatible, но отсутствие state/effect существенно уменьшает первый proof surface.

---

# 5. Блокер 4 — нет production O1 snapshot и D2-bound E2

## O1

Ввести immutable:

```text
VirtualizationOperationOwnerSnapshot
```

Поля:

```text
DecisionId
SpecDigest

OwnerId
OwnerPolicyVersion

OperationNamespace
OperationId

LeafWidth
ExactLeaf
OperandAbiVersion
ResultAbiVersion

OperationClass
ArchitecturalEffectClass

CapabilityRequirement
EvidenceRequirement
DomainRequirement

CancellationPolicy
ReplayPolicy
MigrationPolicy
```

O1 материализуется только из:

```text
AcceptedVirtualizationDecision
```

O1 — policy snapshot, а не permission.

## E2

Ввести SafetyVerifier-owned opaque certificate:

```text
SafetyVerifier.VirtualizationOperationAdmissionCertificate
```

E2 выпускается **только после существующей общей legality/admission цепочки**.

Нормативный порядок:

```text
canonical decode
 ↓
owner/domain guards
 ↓
generic runtime legality
 ↓
Stage A
 ↓
Stage B
 ↓
E1
 ↓
VirtualizationOperandSnapshot
 ↓
D2/O1 resolution
 ↓
RuntimeBoundaryAdmissionService
 ↓
capability/evidence/domain validation
 ↓
neutral operation/trap policy
 ↓
SafetyVerifier E2
```

E2 не является вторым legality engine.

E2:

```text
does not reopen Stage A legality
does not change Stage B placement
does not replace RuntimeBoundaryAdmissionService
does not replace capability/evidence owners
```

## E2 binding

E2 связывает:

```text
E1 AttemptId
E1 IssuerGeneration

Opcode
Operation

VirtualThreadId
OwnerContextId
DomainId

SourceSlot
WorkingSlot
BundleIdentity
ReplayEpoch

DecisionId
SpecDigest

OwnerId
OwnerPolicyVersion

captured numeric leaf
OperandSnapshotDigest

CapabilityGrantIdentity
CapabilityRevocationEpoch

EvidencePolicyDigest
EvidenceEpoch

RestoreGeneration
```

Address-space identity включается только для operations, которым memory реально требуется.

Для `PROBE_NO_STATE_V1`:

```text
AddressSpaceIdentity = absent by contract
```

## Authority rule

```text
D2 != runtime capability
O1 != runtime capability
E1 != backend capability

E2 =
  one-attempt,
  owner-bound,
  leaf-bound,
  domain-bound,
  live execution authority
```

## E2 lifecycle

```text
opaque
issuer-bound
attempt-bound
VT-bound
domain-bound
owner-bound
leaf-bound
replay-bound
restore-bound
consume-once
non-serializable
```

## Mandatory negative matrix

```text
null/default E2
forged E2
foreign issuer
stale E1
duplicate E2
wrong DecisionId
wrong SpecDigest
wrong OwnerId
wrong OwnerPolicyVersion
wrong leaf
zero leaf
out-of-width leaf
changed operand
cross-VT
cross-domain
bundle mismatch
replay mismatch
revoked capability
capability epoch mismatch
evidence epoch mismatch
restore-generation mismatch
```

---

# 6. Блокер 5 — нет E3 backend/receipt и production E4

## E3 executor

Ввести:

```text
DomainHypercallRuntimeExecutor
```

Концептуальный API:

```text
Execute(E2) -> VirtualizationExecutionReceipt
```

Executor:

```text
accepts only live E2
consumes E2 exactly once
checks exact OwnerId
checks OwnerPolicyVersion
checks exact OperationId/leaf
does not re-read operands
does not widen capability/domain
does not publish completion
does not retire
```

## E3

Ввести opaque:

```text
VirtualizationExecutionReceipt
```

Поля:

```text
AttemptId
E2IdentityDigest

DecisionId
OwnerId
OwnerPolicyVersion

OperationId
NumericLeaf

ExecutionSequence

EffectClass
EffectDigest

ResultClass
ResultDigest

RestoreGeneration
```

Для первого operation:

```text
EffectClass = NoStateNoPayload
ResultClass = NoPayload
```

E3 означает:

```text
backend operation actually executed
```

и не означает:

```text
completion published
retire authorized
```

## Production E4

E4 определяется как доказанная canonical composition:

```text
Canonical VMCALL decode
 ↓
generic legality / Stage A / Stage B
 ↓
E1
 ↓
canonical operand capture
 ↓
D2/O1 exact operation lookup
 ↓
RuntimeBoundaryAdmissionService
 ↓
domain/capability/evidence admission
 ↓
neutral TrapRequest / operation request
 ↓
TrapPolicyDescriptor / neutral policy
 ↓
NeutralTrapResult
 ↓
SafetyVerifier E2
 ↓
DomainHypercallRuntimeExecutor
 ↓
E3
```

Для canonical backend invocation должен использоваться neutral runtime operation kind типа:

```text
InvokeHypercall
```

или owner-specific equivalent.

`ProjectCompatibilityTrap` остаётся compatibility projection operation и не становится backend API.

## Forbidden paths

```text
VmxCompatibilityAdmissionService -> executor
VMCS projection -> executor
TrapDecision -> executor
VmExitReason -> executor authorization
compiler -> executor
lane scheduler -> executor authority
testing service -> production executor
```

Существующий `HypercallBackendAdmissionService` должен эволюционировать в neutral policy/admission seam для E2, а не быть обойдён новой параллельной системой.

---

# 7. Блокер 6 — нет non-forgeable E5/E6

## E5 — completion authority

Ввести:

```text
VirtualizationCompletionToken
```

Но E5 должен интегрироваться в существующую completion architecture.

Production contour:

```text
E3
 ↓
TrapCompletionRouteService
 ↓
runtime-owned completion route
 ↓
TrapCompletionPublicationFence
 ↓
neutral completion owner
 ↓
E5
 ↓
CompletionRecord
```

Существующие route/fence boundaries сохраняются.

E5 не создаёт параллельную completion систему.

## E5 binding

```text
AttemptId
E3IdentityDigest

DecisionId
OwnerId

VirtualThreadId
DomainId

ExecutionSequence
CompletionSequence

EffectDigest

EvidenceClass
MigrationClass

RestoreGeneration
```

E5:

```text
opaque
non-forgeable
E3-bound
attempt-bound
consume-once
restore-bound
non-serializable
```

Один E3 может породить не более одного architectural E5.

Backend/device/lane completion не может самостоятельно выпустить E5.

## E6 — retire authority

Ввести:

```text
VirtualizationRetireGrant
```

Production contour:

```text
E5
 ↓
canonical retire-head / ordering checks
 ↓
retire publication policy
 ↓
E6
 ↓
RetireCoordinator
```

E6 связывает:

```text
AttemptId
E5IdentityDigest

VirtualThreadId
RetireWindowIdentity
RetireSequence
OrderingEpoch

DecisionId
OperationId
EffectDigest

RestoreGeneration
```

Только live E6 позволяет architectural materialization.

Для `PROBE_NO_STATE_V1`:

```text
register writes       = 0
memory writes         = 0
VM state writes       = 0
explicit PC redirects = 0
```

Retire завершает instruction precisely без дополнительного architectural effect.

## Mandatory distinction

```text
E3 != E5
E5 != E6

backend success
    != completion publication

completion publication
    != retire authorization

route authorization
    != completion publication
```

Это соответствует существующему HybridCPU completion/retire separation. 

---

# 8. Блокер 7 — нет E7 drain/restore/determinism proofs

## Migration model v1

Принять:

```text
MigrationPolicy = DrainOnly
```

Checkpoint разрешён только если отсутствуют:

```text
live E2
live E3
unpublished E5
pending E6
pending virtualization transaction
pending owner-bound device operation
```

## Non-serialized authority

Не сериализуются:

```text
E1
E2
E3
E5
E6
MemoryAccessAuthorizationToken
DmaIommuAuthorizationToken
DeviceOperationAuthorizationToken
VirtualizedTransactionPermit
lane-local transient dispatch tokens
```

Migration сериализует neutral architectural/domain model, а не live host/runtime authority.

## Restore

Каждый restore:

```text
RestoreGeneration++
```

Все pre-restore runtime certificates/tokens/receipts становятся stale.

После restore запрещено продолжать старую:

```text
E1 -> E2 -> E3 -> E5 -> E6
```

цепочку.

## Determinism evidence

Обязательные сравнения:

```text
FSP off / on
SMT schedule variants
replay / no replay
checkpoint before operation
checkpoint after operation
squash
cancellation
```

Сравнивается architectural trace:

```text
retired instruction sequence
architectural register writes
architectural memory writes
fault/exception sequence
domain/VM transition sequence
architectural completion multiplicity/order
architectural retire multiplicity/order
```

Не требуется идентичность:

```text
cycle timing
physical lane sequence
FSP internal decisions
scheduler timing
internal queue timing
```

Replay/determinism остаётся bounded explicit envelope, а mismatch приводит к invalidation/revalidation, а не к скрытой попытке сохранить глобальную exactness. 

---

# 9. Блокер 8 — P1/P2 отсутствуют в clean containing SHA

## Решение

P1/P2 оформить отдельным clean research-evidence slice.

Рекомендуемая линия:

```text
research/virtualization-p1-p2-evidence
```

Containing commit включает:

```text
P1 source
P2 source
P1/P2 tests

base commit SHA
containing commit SHA
exact source hashes

clean git status evidence

build configuration
runtime/toolchain version
test result summary
```

P1/P2 остаются:

```text
TESTING / RESEARCH ONLY
```

Обязательные ограничения:

```text
production callers = 0
backend authority = 0
completion authority = 0
retire authority = 0
```

Clean-SHA evidence доказывает provenance/reproducibility, но не превращает P1/P2 в D2/E2/E3.

После landing production E2/E3 P1/P2 могут быть удалены из active runtime tree и сохранены как historical proof surface.

## Production evidence

Отдельный clean containing SHA должен быть получен для production chain:

```text
D2
O1
E2
E3
E4
E5
E6
E7
```

Test-only behavior не является authority; это закреплено действующими virtualization security invariants. 

---

# 10. Блокер 9 — future-gated направления

Эти направления не блокируют первый `PROBE_NO_STATE_V1`.

Каждое открывается отдельным owner-specific D2.

---

## 10.1 VMREAD ISA

Architectural VMREAD реализуется field-by-field.

Production contour:

```text
canonical VMREAD decode
 ↓
generic legality
 ↓
E1
 ↓
runtime field-selector capture
 ↓
field-specific D2/O1
 ↓
RuntimeBoundaryAdmissionService
 ↓
field canonical owner
 ↓
SafetyVerifier E2
 ↓
immutable VMREAD execution effect / E3
 ↓
E5
 ↓
E6
 ↓
exact one Rd writeback
```

Первым выбирается field с уже существующим однозначным neutral owner/value source.

VMREAD запрещает:

```text
generic VMCS backing store
host-state aliases
scalar fallback
compatibility-authored values
hidden mutation
```

`GuestCr0`/`GuestCr4` guarded direct projection не расширяется автоматически до architectural VMREAD.

---

## 10.2 VMWRITE

VMWRITE остаётся denied в первом release.

Запрещён generic API:

```text
WriteVmcsField(field, value)
```

Будущий production path:

```text
VMWRITE selector/value
 ↓
canonical field resolution
 ↓
field-specific neutral owner
 ↓
owner-specific D2/O1
 ↓
SafetyVerifier E2
 ↓
typed owner command/effect
 ↓
E3
 ↓
E5
 ↓
E6
```

Каждый writable state class имеет собственного canonical owner.

VMCS остаётся projection vocabulary и не становится mutable architectural state store.

---

## 10.3 Nested virtualization

Ввести neutral nested-domain model:

```text
ParentVirtualizationDomain
        ↓
NestedDelegationGrant
        ↓
ChildVirtualizationDomain
```

Минимальные сущности:

```text
VirtualizationDomainIdentity
NestedDelegationGrant
NestedCapabilityFilter
NestedAddressSpaceBinding
NestedEvidencePolicy
NestedRestoreGeneration
```

Effective authority:

```text
ParentGrant
 ∩ NestedCapabilityFilter
 ∩ ChildPolicy
 ∩ RuntimeBoundaryAdmission
 ∩ SafetyVerifier E2
```

Child domain identity выдаётся neutral runtime owner.

Shadow VMCS / VMCS12 / VMCS02:

```text
projection/bridge only
```

и не являются nested runtime state owner.

---

## 10.4 SecureCompute

SecureCompute сохраняет полностью отдельный authority plane.

```text
Virtualization capability
        !⇒
SecureCompute capability
```

Первый VMCALL release в secure domain остаётся denied, пока отдельный secure owner-specific D2 не принят.

Virtualization не может через:

```text
VMX
VMCS
VmxCaps
VMCALL leaf
compiler metadata
```

grant/activate/materialize/checkpoint/migrate SecureCompute authority.

Положительная интеграция требует собственной цепочки:

```text
SecureCompute owner
 ↓
secure descriptor/policy
 ↓
secure capability
 ↓
secure admission
 ↓
secure execution evidence
 ↓
completion/retire
```

Текущий SecureCompute positive production chain остаётся отдельно future-gated. 

---

# 11. Memory / I/O / IOMMU / device transactions

Memory/IOMMU integration не строится через один `VirtualizedTransactionEnvelope`.

Используется staged model:

```text
Transaction Intent
        +
existing owner policy
        +
existing capability grant
        +
attempt-bound authorization token
        +
lane/domain execution admission
        +
execution receipt
```

Новые transaction tokens не создают параллельных Memory/IOMMU owners.

Они являются attempt-bound конкретизацией authority уже существующих neutral runtime owners.

---

## 11.1 Transaction intent

Ввести immutable:

```text
VirtualizedTransactionIntent
```

Поля:

```text
TransactionId
AttemptId

DecisionId
OperationId

VirtualThreadId
DomainId

AddressSpaceId
VirtualAddress
Length
AccessKind

DeviceEndpointId
TransactionClass

RestoreGeneration
```

Intent определяет:

```text
what this admitted attempt requests
```

Intent не разрешает операцию.

Запрещены transferable bool authority fields:

```text
MemoryAllowed
IoAllowed
IommuAllowed
DeviceAllowed
```

---

## 11.2 Memory authorization

Не создавать нового `MemoryOwner`.

Использовать существующий canonical MemoryDomain/address-space owner.

Он материализует attempt-bound:

```text
MemoryAccessAuthorizationToken
```

на основе:

```text
E2
existing CapabilityGrant
MemoryDomain policy
AddressSpace identity
live translation state
protection state
```

Token связывает:

```text
TransactionId
AttemptId

DomainId
AddressSpaceId

VirtualAddressRange
AccessKind

TranslationEpoch
ProtectionEpoch

CapabilityGrantIdentity
CapabilityRevocationEpoch

RestoreGeneration
```

При:

```text
current TranslationEpoch != token.TranslationEpoch
```

операция должна быть revalidated или denied.

Запрещён TOCTOU:

```text
admit mapping A
mapping changes
execute mapping B
```

Memory domain уже является canonical owner translation/invalidation state; VMX projection metadata им не является. 

---

## 11.3 DMA / IOMMU authorization

Не создавать второго IOMMU authority subsystem.

Использовать существующие:

```text
IoDomainDescriptor
DmaWindowDescriptor
IommuDomainDescriptor
IommuDomainBinding
DmaAuthorityService
```

Результат их live проверки материализуется как attempt-bound:

```text
DmaIommuAuthorizationToken
```

Token связывает:

```text
TransactionId
AttemptId

IoDomainId
DeviceEndpointId
DmaAddressSpaceId

DMA range
direction

IommuPolicyEpoch
IommuRevocationEpoch

CapabilityGrantIdentity
RestoreGeneration
```

Memory authorization и DMA/IOMMU authorization независимы.

```text
MemoryAccessAuthorizationToken
        !⇒
DmaIommuAuthorizationToken
```

Текущий DMA authority уже требует neutral I/O-domain ownership, DMA window, IOMMU binding, permissions, range и fence validation. 

---

## 11.4 Device authorization

Для device/MMIO operation canonical device/IO owner создаёт:

```text
DeviceOperationAuthorizationToken
```

на основе существующей device policy/capability system.

Token связывает:

```text
TransactionId
AttemptId

DeviceEndpointId
DeviceDomainId

OperationClass
QueueOrChannelIdentity

DevicePolicyEpoch
DeviceRevocationEpoch

CapabilityGrantIdentity
RestoreGeneration
```

---

## 11.5 Aggregate permit

При необходимости canonical transaction layer может создать opaque:

```text
VirtualizedTransactionPermit
```

Permit является sealed aggregation:

```text
E2
+
MemoryAccessAuthorizationToken
+
DmaIommuAuthorizationToken
+
DeviceOperationAuthorizationToken
```

в соответствии с требованиями конкретной operation.

Permit:

```text
attempt-bound
transaction-bound
grant-bound
restore-bound
consume-once
non-forgeable
```

Permit не является новым policy engine и не принимает решения самостоятельно.

---

# 12. Lane semantics

Нормативное правило:

```text
lane placement != authority

lane-local runtime
    may own lane-local state
```

То есть сам факт:

```text
selectedLane == N
```

не выдаёт:

```text
memory authority
DMA authority
device authority
VMX authority
architectural completion authority
```

При этом существующие Lane 6 / Lane 7 runtime owners сохраняют authority над собственной lane-local state.

### Lane 6 runtime может владеть

```text
lane-local queue state
token namespace
fences
lane-owned evidence
```

### Lane 7 runtime может владеть

```text
accelerator token namespace
handles
backend binding policy
lane-local completion routing
lane-local checkpoint state
lane-owned evidence
```

Эта ownership не расширяет authority исходной virtualization transaction.

WhiteBook прямо разделяет memory/I/O authority и Lane6/Lane7 runtime ownership; VMX может только проецировать lane facts после neutral authorization. 

## Placement carrier

Если generic carrier необходим, использовать:

```text
LaneDispatchTicket
```

только как placement provenance:

```text
TransactionId
AttemptId
LaneClass
DispatchSequence
ReplayEpoch
```

Он означает:

```text
authorized transaction was scheduled/materialized here
```

и не означает:

```text
transaction is authorized because it occupies this lane
```

### Нормативное правило

> Lane selection may transport an already-authorized transaction; lane placement shall neither establish nor widen memory, I/O, device, virtualization, evidence, completion or retire authority. Existing lane-local runtime owners remain authoritative only for their own lane-local state and policy.

---

# 13. Transaction execution receipt

После фактической memory/IO/device operation canonical executor выпускает:

```text
VirtualizedTransactionReceipt
```

Поля:

```text
TransactionId
AttemptId

OwnerId
DeviceEndpointId

ExecutionSequence

ResultClass
BytesTransferred

EffectDigest
CompletionSequence

RestoreGeneration
```

Receipt означает backend evidence.

Он не является:

```text
E5
E6
RetireRecord
```

Композиция:

```text
VirtualizedTransactionReceipt
        ↓
DomainHypercallRuntimeExecutor aggregation
        ↓
E3 VirtualizationExecutionReceipt
        ↓
completion route/fence
        ↓
E5
        ↓
E6
```

Device interrupt, DMA completion, lane completion или accelerator completion не могут напрямую публиковать architectural state.

---

# 14. Transaction cancellation / squash / restore

Вся transaction chain связывается одним `AttemptId`:

```text
E1
E2

VirtualizedTransactionIntent

MemoryAccessAuthorizationToken
DmaIommuAuthorizationToken
DeviceOperationAuthorizationToken
VirtualizedTransactionPermit

lane-local dispatch carrier

VirtualizedTransactionReceipt

E3
E5
E6
```

При squash/cancellation:

```text
AttemptId -> invalid
```

поздний backend/device receipt может использоваться только для:

```text
drain
cleanup
neutral telemetry
```

но не для:

```text
E5
E6
architectural mutation
```

При restore все pre-restore transaction tokens/receipts инвалидируются через `RestoreGeneration`.

---

# 15. Compiler emission

До production E7:

```text
controlled VMX/VMCALL emission = disabled
```

После release compiler может читать released D2 manifest для:

```text
target capability
supported namespace
supported leaf
supported operand ABI
supported release profile
```

Manifest parity означает только:

```text
compiler emitted instruction
matches a released runtime ABI
```

и не означает:

```text
compiler metadata is runtime authority
```

Runtime path остаётся:

```text
instruction bits
 ↓
canonical decode
 ↓
runtime legality
 ↓
E1
 ↓
operand capture
 ↓
RuntimeBoundaryAdmissionService
 ↓
E2
```

Отсутствие compiler-side manifest annotation не должно становиться новым mandatory correctness carrier для уже существующей корректно закодированной ISA instruction.

Compiler никогда не выпускает:

```text
E1
E2
E3
E5
E6
MemoryAccessAuthorizationToken
DmaIommuAuthorizationToken
```

Это соответствует действующему compiler/runtime contract: compiler может классифицировать и ограничивать emission, но не утверждать backend success. 

---

# 16. Итоговый owner map

| Surface | Canonical authority |
|---|---|
| ISA/decode structure | Canonical ISA/decode layer |
| Generic execution legality | Runtime legality service / SafetyVerifier contour |
| Stage A admission | Typed-slot runtime admission |
| Stage B placement | Typed lane materialization |
| D2 hypercall semantics | `DomainHypercallRuntimeOwner` |
| O1 policy snapshot | Accepted D2 → runtime materialization |
| Attempt-specific virtualization authority | `SafetyVerifier` E2 issuer |
| Hypercall backend | `DomainHypercallRuntimeExecutor` |
| Trap policy | Neutral trap owner |
| Completion route | `TrapCompletionRouteService` |
| Completion publication | Neutral completion owner + publication fence |
| Architectural retire | Canonical retire owner / `RetireCoordinator` |
| Memory/address spaces | MemoryDomain / translation owners |
| DMA/IOMMU | IoDomain/IOMMU/DMA owners |
| Device operation | Canonical device/IO owner |
| Lane-local state | Existing Lane6/Lane7/other lane runtime owner |
| Lane placement | Scheduler/materialization policy, not access authority |
| Nested virtualization | Neutral nested-domain owner |
| SecureCompute | SecureCompute runtime owner |
| Migration/checkpoint | Neutral checkpoint/restore owner |
| Compiler emission | Compiler target/emission policy only |

---

# 17. Hard blockers первого production VMCALL slice

Первый limited production release блокируют:

```text
1. DecisionSpecV2 + AcceptanceRecordV2 + validator
2. DomainHypercallRuntimeOwner
3. exact namespace / width / leaf / ABI
4. production O1 + SafetyVerifier E2
5. E3 + canonical E4
6. non-forgeable E5/E6
7. E7 drain / restore / determinism evidence
8. clean containing SHA / production evidence
```

Не являются blockers первого `PROBE_NO_STATE_V1`:

```text
architectural VMREAD
VMWRITE
nested virtualization
SecureCompute integration
memory/DMA/IOMMU operations
device transactions
controlled compiler VMX emission
```

Они являются explicit future-gated exclusions.

---

# 18. Рекомендуемый PR-порядок

## PR-A — D2 v2 governance substrate

```text
VirtualizationDecisionSpecV2
VirtualizationDecisionAcceptanceRecordV2
VirtualizationDecisionValidatorV2
canonical serialization
digest validation
collision validation
negative tests
CODEOWNERS/reviewer policy
```

Backend остаётся denied.

## PR-B — first accepted operation

Принять:

```text
DomainHypercallRuntimeOwner
HybridCPU.VMCALL.Runtime.v1
LeafWidth = 16
0x0001 = PROBE_NO_STATE_V1
Rs1 leaf value
Rs2 = x0
Rd = x0
NoState/NoPayload semantics
DrainOnly migration class
```

Backend остаётся denied.

## PR-C — O1 + operand materialization

```text
VirtualizationOperationOwnerSnapshot
VirtualizationOperandSnapshot
exact leaf resolution
```

Positive backend отсутствует.

## PR-D — production E2

```text
SafetyVerifier.VirtualizationOperationAdmissionCertificate
RuntimeBoundaryAdmission binding
capability/evidence binding
live registry
restore/revocation binding
negative matrix
```

## PR-E — E3 backend

```text
DomainHypercallRuntimeExecutor
VirtualizationExecutionReceipt
exact leaf 0x0001 only
```

Feature default-off.

## PR-F — production E4

Подключить:

```text
canonical VMX
→ legality
→ Stage A/B
→ E1
→ operands
→ RuntimeBoundaryAdmission
→ neutral trap/owner policy
→ E2
→ executor
→ E3
```

Добавить static/conformance guard против direct compatibility backend invocation.

## PR-G — E5

Интегрировать E3 с:

```text
TrapCompletionRouteService
TrapCompletionPublicationFence
VirtualizationCompletionToken
```

Retire остаётся закрыт.

## PR-H — E6

Добавить:

```text
VirtualizationRetireGrant
canonical retire-head checks
RetireCoordinator integration
```

Для `PROBE_NO_STATE_V1` — no-effect precise retire.

## PR-I — E7

```text
drain-only checkpoint
RestoreGeneration
token invalidation
FSP equivalence
SMT equivalence
replay tests
squash/cancellation tests
```

## PR-J — release evidence

```text
clean containing SHA
DecisionId
SpecDigest
OwnerPolicyVersion
namespace/leaf/ABI
full test matrix
toolchain/runtime version
explicit exclusions
rollback/kill-switch policy
```

P1/P2 clean-evidence PR может выполняться параллельно PR-A/PR-B.

---

# 19. Production acceptance gates

## Research substrate

`GO`, если P1/P2 находятся в clean containing SHA и production callers отсутствуют.

## E1 fault-only

`GO`, если E1 по-прежнему не может разрешить backend/completion/retire.

## D2/O1

`GO`, если accepted operation machine-validated и D2/O1 не используются как runtime capability.

## E2

`GO`, если только live `SafetyVerifier` может выпустить attempt-bound E2 после общей legality/admission цепочки.

## Canary backend

`GO`, если executor принимает только exact live E2 для `0x0001` и возвращает opaque E3.

## Completion-only

`GO`, если E3 проходит существующий route/fence contour и только neutral completion owner выпускает exact-once E5.

## Precise retire

`GO`, если только canonical retire contour выпускает E6 и только E6 достигает `RetireCoordinator`.

## Migration-ready

`GO`, если:

```text
drain-only
restore invalidates live authority
FSP/SMT/replay architectural trace equivalent
```

## Production limited release

`GO` только для exact scope:

```text
HybridCPU.VMCALL.Runtime.v1
LeafWidth = 16
leaf = 0x0001
PROBE_NO_STATE_V1

no register payload
no memory
no IO
no DMA/IOMMU
no device operation
no nested virtualization
no SecureCompute
no compiler positive emission
```

Неизвестный leaf:

```text
fail closed
```

---

# 20. Нормативный release scope

> HybridCPU Virtualization Runtime v1 поддерживает ровно один D2-approved VMCALL operation `PROBE_NO_STATE_V1` с 16-bit numeric leaf `0x0001`. Операция выполняется исключительно через canonical HybridCPU execution contour после generic runtime legality, Stage A admission, Stage B materialization, E1 attempt binding, canonical operand capture, runtime domain/capability/evidence admission и SafetyVerifier-issued E2.
>
> `VirtualizationDecisionSpecV2`, `VirtualizationDecisionAcceptanceRecordV2` и O1 определяют accepted semantics и policy, но не являются runtime execution capability.
>
> Только SafetyVerifier может выпустить E2 для конкретной live attempt. Только neutral hypercall runtime owner/executor может потребить E2 и выпустить E3. Backend execution не означает completion.
>
> Completion проходит существующие `TrapCompletionRouteService` и `TrapCompletionPublicationFence`; только neutral completion owner может выпустить E5. Completion publication не означает retire.
>
> Только canonical retire contour может выпустить E6, и только E6 разрешает architectural publication через `RetireCoordinator`.
>
> E1/E2/E3/E5/E6 и transaction authorization tokens не сериализуются. Первый release использует drain-only migration; restore увеличивает generation и инвалидирует всю pre-restore authority.
>
> VMX/VMCS compatibility state, compatibility projections, `VmExitReason`, validation booleans, compiler metadata, lane placement и test-only services не могут предоставлять или расширять production backend/completion/retire authority.
>
> Architectural VMREAD, VMWRITE, nested virtualization, SecureCompute integration, memory/DMA/IOMMU/device operations и controlled compiler emission не входят в первый release и остаются отдельными owner-specific future-gated направлениями.
>
> Future memory/MMIO/DMA/device operations используют immutable transaction intent и attempt-bound authorization tokens, производные от существующих canonical MemoryDomain, IoDomain/IOMMU, device и capability owners. Они не создают параллельную authority hierarchy.
>
> Lane placement не является authority. Существующие lane-local runtime owners сохраняют authority над собственной queue/token/fence/backend-binding state, но физический lane assignment никогда не расширяет memory, I/O, virtualization, completion или retire permission.
>
> Compiler может использовать released D2 manifest только для emission eligibility и target/ABI parity. Финальная legality и execution authority всегда повторно устанавливаются runtime.