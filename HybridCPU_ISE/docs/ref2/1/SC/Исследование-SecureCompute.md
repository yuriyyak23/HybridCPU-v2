# you asked

прикрепил securecomputeactiovationplan в zip - дай оценку плана и объясни блокеры:

C0: canonical legality authority/taxonomy, production composition root, descriptor registry/lifecycle owner, SafetyVerifier certificate, grant ledger, exhaustive admission, backend/result/completion/retire owners.
C1: canonical region/shared-buffer maps, private-memory enforcement, device/IOMMU authority, hypercall executor, checkpoint/key/anti-replay protocol, единый evidence publisher.
C2: end-to-end disabled equivalence, generated-artifact no-emission proof, clean immutable CI reproduction, independent release evidence.


Предложи предпочтительные решения по блокерам учитывая всю специфику и философию архитектуры HybridCPU-v2 @GitHub

---

# chatgpt response

Сопоставлю ZIP-план с текущим состоянием репозитория, зафиксирую SHA, а затем разложу C0/C1/C2 по реальным архитектурным блокерам и предпочтительным решениям.

ZIP-план уже значительно лучше прежней версии: он сам отделяет policy/contract surface от исполнимого runtime path. Сейчас проверяю, где он точен, где переусложнён, и какие пункты нужно изменить до начала кода.

Уже вижу два принципиальных места, где план надо поправить: test-only controlled emission поставлен слишком поздно, а будущий Phase 22 нельзя превращать в «разрешающий набор boolean-фактов». Это должны быть отдельные offline/build gates, не runtime-authority.

# Вердикт

**План в ZIP архитектурно зрелый и в основном правильно диагностирует текущее состояние, но в существующем виде ещё не годится как исполнимый activation roadmap.** Он отлично работает как deny-by-default audit ledger, однако содержит три существенных ошибки порядка и authority-модели:

1. **Test-only controlled emission поставлен после limited release.** Для доказательства реального `decode → SafetyVerifier → issue` он должен появиться раньше первого backend probe, иначе end-to-end тесты продолжат строить carriers вручную.
2. **Будущий Phase 22 нельзя активировать, просто заменив финальный deny на allow.** Его входы — caller-supplied booleans. Такой классификатор может оставаться deny-only, но не может стать release authority.
3. **SafetyVerifier certificate в плане местами перегружен capability/effect authority.** SafetyVerifier должен единолично удостоверять legality/admission и точные допустимые bounds, но grant mint/consume/revoke остаются собственностью отдельного ledger, а результат исполнения — backend owner.

**Текущий разрешённый режим:** SecureCompute disabled/no-emission, policy/admission-only testing и VMX read-only zero-authority projection. Положительное runtime execution по remote `master` запрещено.

Кодовая база проверки — remote `yuriyyak23/HybridCPU-v2`, `master`, SHA `d3814d1f332f083034d3b245f807a45f97792070`. 

При этом приложенный план фиксирует другой локальный baseline: `8bcdb637...` на `refactor/compiler-core-authority-boundaries`, dirty worktree и SDK `10.0.204` вместо запрошенного `10.0.201` (`24_audit_revalidation_and_dependency_order.md:5-38`). Commit `8bcdb637...` и эта ветка не существуют в подключённом remote, а каталог плана отсутствует в дереве remote SHA. Поэтому план сейчас является **out-of-tree audit artifact**, а не воспроизводимым repository evidence.

[Приложенный SecureComputeActivationPlan](sandbox:/mnt/data/SecureComputeActivationPlan(1).zip)

---

# Общая оценка плана

| Аспект | Оценка | Вывод |
|---|---:|---|
| Диагностика текущих разрывов | **9/10** | C0/C1/C2 выделены корректно, policy surface не перепутан с runtime execution |
| Authority/domain modelling | **8/10** | Верно заданы owners, но certificate следует отделить от capability consumption |
| Fail-closed дисциплина | **9/10** | Phase 18/20/21/22 правильно остаются future/deny-gated |
| Dependency ordering | **6/10** | Controlled emission и equivalence harness расположены неудачно |
| Release mechanics | **5/10** | Boolean-based Phase 22 нельзя эволюционировать в approving authority |
| Реализуемость маленькими PR | **8/10** | После корректировки порядка получится хороший рабочий roadmap |

Главная сильная сторона плана — он честно отмечает, что generic admission service не является CPU Stage-B authority. Это совпадает с тестом репозитория: тесты прямо утверждают, что CPU Stage-B enforcement остаётся открытым и что `decode/SafetyVerifier/certificate/issue` call graph не доказан. 

---

# C0 — блокеры любого положительного runtime path

## C0.1 Canonical legality authority и operation taxonomy

### Факт

Сейчас `SecureDomainOperationClass` находится в публичном request и выбирается вызывающим кодом. Значение `Ordinary = 0`, а все неизвестные либо недостаточно специализированные non-ordinary операции в `SecureDomainAdmissionPolicy` заканчиваются общим `AllowedSecureOperation`. `SecureIo` и `SecureHypercall` имеют дополнительные проверки, остальные классы — нет.  
`RuntimeBoundaryAdmissionService.cs:19-31, 122-177`; `SecureDomainAdmissionPolicy.cs:3-16, 55-139`.  

### Почему это blocker

Caller может классифицировать тот же carrier как `Ordinary`, либо новый enum/value может попасть в generic positive tail. Это нарушает:

- SafetyVerifier как единственного legality/admission authority;
- closed-world operation taxonomy;
- невозможность compatibility/compiler layers создавать authority.

### Предпочтительное решение

Не «decoder owns taxonomy», как сейчас сформулировано в `24...:116`, а трёхступенчатая модель:

```text
ISA Registry owns vocabulary/version
Decoder materializes canonical decoded identity
SafetyVerifier derives SecureOperationKind and decides legality
```

`SecureOperationKind` не должен поступать в production request извне. SafetyVerifier вычисляет его из canonical opcode, decoded leaf, operands, carrier digest и ISA registry generation.

Рекомендуемая форма:

```csharp
internal readonly record struct CanonicalSecureOperationIdentity(
    ushort Opcode,
    ushort DecodedLeaf,
    ulong CarrierDigest,
    uint IsaSchemaVersion);
```

Exhaustive switch обязан иметь явный `UnknownDenied`; значение `0` — `Unknown`, не `Ordinary`.

### Запрещённые shortcuts

- trust compiler hint;
- передавать `SecureDomainOperationClass` через DTO;
- трактовать неизвестный opcode как ordinary;
- разрешать generic `AllowedSecureOperation`.

### DoD

Negative: unknown enum/opcode, mismatch decoder/registry, caller substitution.  
Positive: один canonical named operation.  
Determinism: одинаковая identity при replay.  
Replay: смена ISA generation или carrier digest инвалидирует admission.

---

## C0.2 Production composition root

### Факт

`RuntimeBoundaryAdmissionService` — публично создаваемый generic service с несколькими публичными конструкторами и общим `Allowed` result. Его результат не связан с Stage-B placement, конкретным SafetyVerifier instance, VT, bundle, FSP working slot или replay epoch. 

Репозиторий уже содержит хороший противоположный пример для VMX: certificate выдаётся одним live SafetyVerifier после Stage-B, связан с issuer generation, VT, owner context, domain, source/working slot, bundle и replay epoch, а backend/completion/retire authority жёстко false. 

### Предпочтительное решение

Production composition root:

```text
decode
  → canonical operation identity
  → Stage-A structural legality
  → Stage-B lane/FSP placement
  → SafetyVerifier secure admission
  → SecureAdmissionCertificate attached to accepted MicroOp attempt
  → issue
  → exact backend owner
  → execution receipt
  → completion owner
  → in-order retire owner
```

`RuntimeBoundaryAdmissionService` следует:

- сделать `internal`;
- использовать только как policy helper внутри SafetyVerifier;
- запретить его результату пересекать issue boundary;
- удалить его из любого compatibility-facing composition root.

Issue carrier несёт certificate как opaque internal reference; scheduler/FSP не реконструирует его.

---

## C0.3 Descriptor registry/lifecycle owner

### Факт

`SecureComputeDomainDescriptor` имеет публичный конструктор; `IsMaterialized` фактически означает только `DomainTag != 0`. 

Generic request и runtime context одновременно могут нести полные descriptor objects:

```csharp
context.SecureCompute ?? request.SecureDescriptor
```

Это два источника authority. 

### Предпочтительное решение

Один `SecureDomainRegistry`, принадлежащий neutral runtime domain manager:

```text
Configuration DTO
    ↓ validate/canonicalize
Registry materialization
    ↓
SecureDomainBinding {
    RegistryId,
    DomainId,
    Generation,
    PolicyDigest
}
```

Полные descriptors остаются immutable registry-owned state. Pipeline, context и requests несут только opaque binding.

Lifecycle:

```text
Created → Active → Quiescing → Revoked/Destroyed
```

Любая policy mutation создаёт новую generation. Старые certificates, grants, memory maps, DMA intents и checkpoint handles становятся stale.

### Уточнение к плану

Не использовать `DomainTag` или `RegistrySeal` как самостоятельную capability. `DomainTag` — identity component; authority появляется только при live registry lookup и matching generation.

---

## C0.4 SafetyVerifier certificate

### Факт

SecureCompute certificate отсутствует. Текущий `SecureDomainAdmissionResult` содержит только decision/reason. VMX certificate показывает пригодный issuer/replay pattern, но намеренно не несёт SecureCompute authority.  

### Предпочтительное решение

Certificate должен удостоверять:

- exact operation identity;
- descriptor binding/generation;
- domain и address-space identity;
- VT, owner context;
- source, working и physical lane;
- bundle/FSP decision identity;
- replay epoch;
- referenced grant IDs/epochs;
- **максимально допустимый effect envelope**.

Но certificate **не должен сам быть capability grant** и не должен напрямую разрешать retire. Правильное разделение:

```text
SecureAdmissionCertificate
  = legality/admission proof

SecureGrantLedger entry
  = resource authority

SecureExecutionReceipt
  = proof that exact backend effect occurred

SecureCompletionRecord
  = exactly-once completion materialization

Retire publication
  = architectural visibility
```

Certificate single-use до backend acceptance; execution receipt single-use до completion/retire.

---

## C0.5 Grant ledger

### Факт

`SecureGrantAuthorityPolicy.Validate` принимает caller-supplied handle, epoch set, bounds, `runtimeOwnerMaterialized` и `grantRevoked`. Это валидатор переданных фактов, не mint/revoke owner. 

Сам handle — четыре scalar-поля: kind, local ID, provenance hash, epoch. 

В hypercall admission `runtimeOwnerMaterialized: true` подставляется прямо в коде. 

### Предпочтительное решение

`SecureGrantLedger` как единственный owner:

```text
GrantEntry {
    GrantId,
    Subject,
    ResourceId,
    Rights,
    DomainGeneration,
    GrantEpoch,
    Nonce,
    RemainingUses,
    State
}
```

Операции ledger:

```text
Mint → Lookup/Reserve → Consume → Revoke
```

Для read-like многократных прав может использоваться bounded lease; для backend/DMA/hypercall intents — one-shot grant consumption.

`ProvenanceHash` не должен быть доказательством существования grant без ledger lookup.

Restore никогда не восстанавливает старый live grant: он создаёт новую domain generation и reissues grants.

---

## C0.6 Exhaustive admission

### Факт

Текущий policy разрешает generic tail. Migration/checkpoint используют `_ => Allowed`, то есть новая payload class автоматически становится разрешённой.   

### Предпочтительное решение

- ప్రతి authoritative enum имеет `Unknown = 0`;
- exhaustive switch;
- default — deny;
- source generator создаёт operation/payload coverage table;
- CI падает при добавлении enum member без policy handler и tests.

Особенно важно разделить:

```text
PolicyAdmitted
BackendAuthorized
EffectProduced
CompletionMaterialized
Retired
EvidencePublished
```

`AllowedIo` и `AllowedAdmittedDenied` сейчас безопасны только потому, что authority bits false. Но названия слишком легко прочитать как success. Лучше переименовать в:

```text
PolicyValidatedNoExecution
RecognizedButExecutionDenied
```

---

## C0.7 Backend/result/completion/retire owners

### Факт

`SecureCompletionPublicationFence` — DTO с двумя enum и вычисляемыми booleans. Сам по себе он не связывает publication с конкретным admitted attempt или backend result. 

### Предпочтительное решение

Для первого пути нужны четыре независимых типа:

```text
SecureExecutionRequest
SecureExecutionReceipt
SecureCompletionRecord
SecureRetireEffect
```

Каждый содержит exact attempt identity и предыдущий one-shot token.

Completion owner не может принимать «fence присутствует». Он принимает только live execution receipt.

Retire owner не принимает backend result напрямую; только completion record, находящийся в правильной VT/ROB позиции.

---

# C1 — блокеры отдельных effect domains

C1 не обязан блокировать первый **register-local, no-memory, no-I/O** secure path. Но каждый C1 блокирует claim соответствующего домена: private memory, DMA, hypercall, checkpoint или attestation.

## C1.1 Canonical region/shared-buffer maps

### Факт

`SecureMemoryDomainDescriptor.TryFindRegion` и `SecureIoDomainDescriptor.TryFindSharedBuffer` используют first-match iteration. Constructors не отвергают overlap, duplicate или order-dependent policy.  

### Предпочтительное решение

Immutable interval-map builder:

- checked `start + length`;
- reject zero length, overflow, overlap и duplicate IDs;
- canonical sort;
- stable map digest/generation;
- separate private-region map и shared-buffer map;
- insertion-order invariance property test.

Не использовать priority/first-match для первой версии.

---

## C1.2 Private-memory enforcement

### Факт

Сейчас memory policy вызывается только если caller уже передал optional `SecureMemoryAccess`. Проверка выполняется над caller-provided address request; производственный load/store/cache path не является owner этой проверки.  

### Предпочтительное решение

Двухфазное enforcement:

1. **Pre-translation intent admission**  
   Accepted uop + certificate → expected region ID/access kind.
2. **Post-translation effect enforcement**  
   Translation result must remain bound to the same domain, address-space generation и region ID.

Проверять:

- instruction fetch;
- load/store/atomic;
- cache fill/writeback;
- prefetch и assists;
- VDSA/donor-prefetch;
- lane 6/7 memory movement;
- DMA.

Для первой активации secure uops лучше полностью исключить из FSP. После стабилизации разрешать только same-VT/same-domain pilfer с certificate, выданным после final placement.

---

## C1.3 Device/IOMMU/DMA authority

### Предпочтительное решение

Разделить owners:

```text
DeviceContextOwner
IommuMappingOwner
DmaSubmissionOwner
DmaCompletionOwner
```

One-shot intent должен связывать:

```text
device + PASID/context + VT + domain generation
+ buffer region ID + direction + exact range
+ grant epoch + map generation + sequence number
```

Reset, rebind, restore и device substitution инвалидируют outstanding sequences.

Для первого выпуска предпочтителен **copy-in/copy-out staging buffer**, а не direct shared mapping. Это проще доказать, легче zeroize и не создаёт implicit coherence contract.

---

## C1.4 Hypercall executor

### Факт

Текущий hypercall path способен только на policy validation и `AllowedAdmittedDenied`; backend execution намеренно закрыт. Его входы включают booleans `neutralBackendOwnerMaterialized` и `domainValidated`. 

### Предпочтительное решение

Не расширять generic VMCALL. Нужен:

- canonical secure service ID из decoded carrier;
- registry-owned ABI version;
- typed arguments;
- explicit grant references;
- exact backend owner;
- internal result only;
- no compatibility projection до retire.

Первая secure operation не должна быть hypercall: слишком много одновременно открываемых boundaries. Оптимальный первый эффект — register-local deterministic measured transform.

---

## C1.5 Checkpoint/key/anti-replay

### Факт

Migration policy и checkpoint classifier имеют deny-list плюс default allow. Restore принимает caller booleans `measurementRevalidated`, `reattestationCompleted`, `grantProvenanceValidated`. Реального serializer/key/counter owner нет.  

### Предпочтительное решение

**Для первой limited activation — checkpoint/restore активного secure domain полностью denied.**

Это лучше, чем сразу имитировать production security через process-local encryption key.

Позднее отдельный protocol:

```text
Quiesce
→ canonical closed-world serialization
→ monotonic snapshot sequence reservation
→ AEAD seal by explicit key owner
→ invalidate source generation
→ restore into Disabled state
→ verify/decrypt
→ rebuild descriptor generation
→ remeasure/reissue grants
→ atomic reopen
```

Unknown authoritative field — deny. Deserialize-then-validate — запрещено; сначала framing/version/authentication, затем materialization.

До выбора platform root нельзя делать hardware-grade attestation claim.

---

## C1.6 Единый evidence publisher

### Предпочтительное решение

Единый publisher — да, но он не должен становиться владельцем самих фактов.

```text
Measurement owner → measurement state
Backend owner → execution receipt
Retire owner → retired effect
Evidence publisher → audience-specific projection
Signer → signature only
```

Внешнее evidence публикуется **после retire**. После execute допускается лишь internal receipt, недоступный host/guest APIs.

Отдельные closed-world schemas:

- guest evidence;
- host/operator evidence;
- migration rebuild evidence;
- debug output.

Нельзя сериализовать internal descriptors, grants, certificates, pointers, backend IDs или host object identity.

---

# C2 — conformance и release blockers

## C2.1 End-to-end disabled equivalence

План правильно требует paired execution, но harness поставлен слишком поздно — Step 7 после registry/grants/maps (`24...:115-123`).

### Исправление

Harness должен появиться **до semantic refactoring** и затем выполняться после каждого PR:

```text
C2-A0 baseline capture
C0 changes
C2-A1 regression comparison
C1 changes
C2-A2 regression comparison
```

Сравнивать:

- architectural registers и memory;
- exception identity/order;
- issue и retire sequence;
- VT scheduling;
- FSP decisions;
- replay trace/digest;
- public evidence/telemetry;
- completion visibility.

Не включать wall-clock, GC, allocation count или иные непубличные implementation metrics.

Машина является фиксированной `W=8`, 4-way SMT с heterogeneous lanes, поэтому корпус должен включать SMT 1/2/4 и FSP on/off. 

---

## C2.2 Generated-artifact no-emission proof

План местами смешивает два разных требования:

1. secure encoding отсутствует из production emitter reachability;
2. secure opcode отсутствует из generated ISA catalog.

Второе не всегда требуется: opcode может быть **зарезервирован в schema/decoder**, но недоступен production compiler profile.

Правильное доказательство:

- production compiler/lowering/assembler не может emit secure carrier;
- raw/manual carrier fail-closed в default runtime profile;
- generated registry, compiler и decoder согласованы;
- test-only controlled profile создаётся отдельно;
- regeneration deterministic и leaves tree clean.

Текущий compiler gate уже исключает `SecureComputePolicyAdmissionOnly` и `NoEmission` из production lowering, но это ещё не complete artifact/reachability proof. 

---

## C2.3 Clean immutable CI reproduction

План здесь точен. Необходимы:

- detached clean SHA;
- exact SDK `10.0.201`, указанный в `global.json`; 
- locked dependencies;
- generator input/output hashes;
- exact test filters и counts;
- `git diff --exit-code` после generation;
- artifact manifest с SHA-256;
- provenance, не зависящая от local-only plan files.

До внесения ZIP-плана в Git его собственные document-based tests не воспроизводимы из remote SHA.

---

## C2.4 Independent release evidence

### Критическое исправление Phase 22

Сейчас Phase 22 принимает набор boolean prerequisites и всегда возвращает deny. В таком виде он безопасен. Но его нельзя превращать в approving gate: любой caller сможет сконструировать request со всеми `true`. 

### Предпочтительное решение

Оставить current `SecureComputePhase22LimitedReleaseGatePolicy` **постоянно deny-only** как runtime/documentation guard.

Создать отдельный offline/build-time:

```text
SecureComputeReleaseEvidenceVerifier
```

Он принимает не booleans, а immutable evidence bundle:

```text
commit SHA
build profile digest
generator manifest hash
test result artifact hashes
named call-graph proof
negative bypass inventory
rollback drill result
review signatures/approvals
```

Результат verifier — build profile artifact, например `SecureComputeLimitedProfile.json`, связанный с точным SHA. Но даже этот профиль не является runtime legality authority: каждую операцию всё равно допускает SafetyVerifier.

---

# Как изменить dependency order

Текущий порядок `24...:113-156` следует заменить следующим:

| PR | Изменение | Release state |
|---|---|---|
| 1 | Внести plan в Git, clean-SHA audit manifest, exact SDK CI | no-emission, denied |
| 2 | Disabled equivalence harness — до semantic changes | no-emission, denied |
| 3 | Canonical ISA operation identity; удалить caller taxonomy | no-emission, denied |
| 4 | Descriptor registry, opaque binding, lifecycle generations | no-emission, denied |
| 5 | Grant ledger и revoke/consume protocol | no-emission, denied |
| 6 | SafetyVerifier-issued SecureAdmissionCertificate | no-emission, denied |
| 7 | Production certificate carrier через Stage-B/FSP/SMT/issue | no-emission, denied |
| 8 | **Test-only controlled emission profile** | test images only; runtime still denies |
| 9 | End-to-end decode→certificate→issue→expected deny | controlled test emission; no backend |
| 10 | Named no-side-effect backend transport probe | internal result only |
| 11 | Register-local deterministic positive operation | exact receipt→completion→retire |
| 12 | Single evidence publisher, external output after retire | named path only |
| 13 | Independent release evidence verifier | limited named path |
| 14+ | Memory, shared buffers, DMA/IOMMU, hypercall — отдельными gates | per-domain enablement |
| Future | Production compiler emission RFC | only after named runtime release |
| Future | Checkpoint/attestation platform protocol | separate claim |
| Future | Nested child intent | hard deny until separate release |

Таким образом controlled emission делится на:

- **test-only controlled emission** до backend activation;
- **production emission** после независимого limited release.

Иначе canonical decoder path невозможно доказать без ручной сборки micro-op, что оставляет скрытый composition gap.

---

# Рекомендуемый первый положительный SecureCompute path

Не private memory, не DMA, не hypercall.

Оптимальный первый путь:

```text
register-local
single VT
no FSP
no memory
no I/O
no VMX
no nested
deterministic
retire-visible measured result
```

Например, named secure measurement/mix operation с фиксированными scalar inputs и одним scalar result.

Proof chain:

```text
controlled test encoding
→ canonical decode
→ final Stage-B placement
→ SafetyVerifier certificate
→ ledger grant consumption
→ exact backend owner
→ deterministic execution receipt
→ completion record
→ in-order retire
→ evidence publication
```

Это закрывает C0 и проверяет реальную архитектурную цепочку, не притаскивая сразу cache aliasing, IOMMU, DMA, key management и hypercall ABI.

Для первого release:

- secure uop не pilferится;
- только один secure VT на core;
- VMX projection отсутствует либо возвращает только `Unavailable/Denied`;
- checkpoint/restore denied;
- debug denied;
- compiler production emission остаётся закрытым.

---

# Итоговая классификация блокеров

**C0 — обязательны до любого положительного исполнения:** taxonomy, composition root, descriptor lifecycle, certificate, grant ledger, exhaustive admission, execution/result/completion/retire chain.

**C1 — обязательны только до включения соответствующего effect domain:** memory, shared buffers, DMA/IOMMU, hypercall, migration, attestation. Их допустимо оставить hard-denied при первом register-local release.

**C2 — обязательны для merge/release claims:** disabled equivalence, deterministic generation, clean immutable CI и независимое evidence. C2 нельзя откладывать «на конец»: baseline harness и reproducibility должны идти первыми.

**Решение по активации сейчас:** запрещена.  
**Решение по плану:** принять как основу после трёх обязательных правок — ранний equivalence harness, ранний test-only controlled emission и замена будущего approving Phase 22 на независимый artifact-backed release verifier.

---
Powered by [AI Exporter](https://saveai.net)