Status update 2026-05-30 closure `253`: descriptor readiness policy audit is closed fail-closed. `ValidateMigrationReadiness`, `ValidateNestedEnablementReadiness`, restore validation, and nested checkpoint readiness do not consume VMREAD projection values, VMCS scalar stores, or compatibility projection metadata; they require neutral materialized state, checkpoint, migration policy, and evidence policy.

Status update 2026-05-30 closure `254`: migration/evidence proof for recomputed compatibility fields is closed. `ExitReason`, `ExitQualification`, `GuestPhysicalAddress`, and `EptViolationQualification` remain recomputed completion projections, not checkpoint payload classes or serialized VMCS authority.

Status update 2026-05-30 closure `255`: execution-owned VMREAD audit follow-up is closed. The safe snapshot source now exposes explicit materialization metadata (`IsMaterialized`, `HasCompleteGuestPcSpFlags`, `StateEpoch`), while privileged execution fields `GuestCr0` and `GuestCr4` remain denied until a separate neutral privileged-state owner exists.

Status update 2026-05-29 closure `250`: execution-owned VMREAD is now partially closed by a neutral read-only value source. `GuestPc`, `GuestSp`, and `GuestFlags` project only from `ExecutionDomainReadOnlyStateView` through `ExecutionDomainDescriptor.TryCreateReadOnlyStateView()` after runtime admission, generated owner lookup, and guest-architectural-state evidence. `GuestCr0` and `GuestCr4` remain denied until neutral privileged execution-state semantics exist.

Status update 2026-05-29 closure `251`: host execution VMREAD aliases are explicitly denied. `HostPc`, `HostSp`, `HostFlags`, and `HostCr0` return `HostExecutionStateOwnerMissing`; there is no neutral host-execution owner, and guest read-only state is not allowed to stand in as host state.

Status update 2026-05-29 closure `252`: remaining control-like VMREAD fields now have a single fail-closed conformance fence. No new value projection opens; `GuestCr0`, `GuestCr4`, `HostCr0`, `HostCr3`, and compatibility-control fields stay denied unless future neutral semantics and value sources are introduced.

Ниже — **оставшиеся незакрытые задачи** по текущему состоянию. Я разделяю их на **реальные open work items** и **постоянные guardrail-задачи**, чтобы не смешивать “ещё не реализовано” с “закрыто fail-closed”.

## Краткий статус

VMX compatibility frontend freeze уже объявлен, legacy VMX backend отсутствует, `VmxExecutionUnit`, `VmcsManager`, `IVmcsManager` отсутствуют, capability/memory/Lane evidence в основном переведены на neutral runtime ownership. Но это **не feature-complete VMX execution**: текущие рабочие VMX-пути — это узкие admitted projection/value slices и admitted-denied VMCALL trap projection. 

---

# Оставшиеся задачи

## 1. Расширить VMREAD value projection за пределы completion-owned полей

Задача уже частично продвинута closure `240`: VMREAD теперь может пройти decode → alias projection → `RuntimeBoundaryAdmissionService` → generated schema owner lookup → read-only value projection для completion-owned полей `ExitReason`, `ExitQualification`, `GuestPhysicalAddress`, `EptViolationQualification`.

Closure `241` закрывает первый memory-owned slice: `GuestCr3` берётся только из neutral `MemoryDomainTranslationControl.AddressSpaceRoot`, а `EptPointer` — только из owned neutral `MemoryDomainTranslationControl.SecondStageRoot` через `MemoryDomainDescriptor.TryCreateReadOnlyTranslationView()`. Остальные поля остаются denied/fail-closed, пока их neutral owner не даст явный read-only value source. 

Открытая часть:

```text
control-like aliases without neutral value sources
compatibility-control aliases
remaining memory-owned fields
control-owned fields
```

Правило: **не создавать VMCS field store**. Для каждого нового VMREAD-поля нужен neutral owner: `ExecutionDomainDescriptor`, `MemoryDomainDescriptor`, `CompletionRecord` или neutral compatibility-control owner. 

---

Closure `242` closes VPID semantics: `Vpid` VMREAD projects only neutral `MemoryDomainReadOnlyTranslationView.AddressSpaceTag` when address-space tagging is enabled and the tag is non-zero. If tagging is not materialized, VPID remains denied.

Closure `243` materializes `CompatibilityControlDescriptor` as a neutral, fail-closed control-semantics owner. Closure `245` chooses the clean current-code option: control VMREAD values stay explicitly denied with `CompatibilityControlValueProjectionDenied`. A compatibility-control projection mapper is not admitted unless a future separate neutral control-bit value contract exists.

Closure `248` closes the next small memory-owned VMREAD value slice: `Cr3TargetCount` now projects only from neutral `MemoryDomainReadOnlyTranslationView.AddressSpaceTargetCount` after runtime admission, generated owner lookup, compatibility-alias evidence, and valid memory translation control. After closure `250`, the residual execution-owned VMREAD fields are `GuestCr0` and `GuestCr4`; `HostCr3`, other host aliases, and control-owned fields remain denied unless a neutral owner exposes a real value source.

Closure `249` resolves the `HostCr3` branch as explicit denial. No neutral host-address-space owner exists, so `HostCr3` returns `HostAddressSpaceOwnerMissing` after runtime admission and alias/evidence validation, before guest/domain translation view materialization.

Closure `250` closes the safe execution-owned VMREAD value slice: `GuestPc`, `GuestSp`, and `GuestFlags` now project only from neutral `ExecutionDomainReadOnlyStateView` through `ExecutionDomainDescriptor.TryCreateReadOnlyStateView()` after runtime admission, generated schema owner lookup, and `GuestArchitecturalState` evidence. `GuestCr0` and `GuestCr4` remain denied with `PrivilegedExecutionStateProjectionDenied` because neutral privileged execution-state semantics are not materialized.

Closure `251` closes the host execution branch as explicit denial: `HostPc`, `HostSp`, `HostFlags`, and `HostCr0` now return `HostExecutionStateOwnerMissing` after runtime admission and compatibility-alias evidence. No neutral host-execution owner exists, so these aliases must not project from `ExecutionDomainReadOnlyStateView`.

Closure `252` closes the remaining control-like branch as a fail-closed conformance fence. `GuestCr0`, `GuestCr4`, `HostCr0`, `HostCr3`, `PinBasedControls`, `ProcBasedControls`, `ExitControls`, `EntryControls`, and `SecondaryProcControls` remain denied with explicit decisions; the schema remains read-only/write-denied; no opened neutral value source can be reused for these aliases.

## 2. Спроектировать runtime-owned VMCALL / hypercall owner

Текущий `VMCALL` путь закрыт только как **admitted-denied projection**: он проходит decode, alias projection, runtime admission, neutral trap policy, `NeutralTrapResult`, `VmxTrapProjectionMapper`, но backend execution остаётся denied. 

Открытая задача — сделать настоящий successful VMCALL path:

```text
VMCALL
  -> decode / alias projection
  -> RuntimeBoundaryAdmissionService
  -> neutral hypercall/trap owner
  -> capability grant
  -> evidence policy
  -> neutral completion record
  -> TrapCompletionPublicationFence allowed
  -> retire publication allowed
  -> VMX-compatible projection
```

При этом `VmExitReason.VmCall` не должен становиться authority. Это только проекция результата, а не доказательство разрешённости hypercall. 

---

## 3. Реализовать runtime-owned trap completion route для успешной публикации

Closure `247` chooses the clean fail-closed option for the VMCALL/hypercall backend item. `HypercallBackendAdmissionService` now lives under neutral `Core/Runtime/Events/Hypercalls` and checks runtime admission, neutral trap result, runtime-owned backend authority, domain validation, typed capability requirement, and evidence requirement. Production VMCALL still passes `MissingNeutralOwner`, so it returns `MissingBackendDescriptor` and remains admitted-denied before route/fence publication.

Closure `246` closes the route-design part of this item: `TrapCompletionRouteDescriptor` and `TrapCompletionRouteService` now live under neutral `Core/Runtime/Completion/Routing`. The route owner requires runtime admission, a neutral trap result, runtime-owned route authority, domain validation, backend execution authorization, completion publication permission, and retire publication permission before the publication fence can allow a completion.

The admitted-denied VMCALL path uses only `TrapCompletionRouteDescriptor.ProjectionOnlyDenied`, so completion publication and retire publication remain forbidden.

Открытая часть для real successful publication:

```text
neutral trap/hypercall backend
  -> neutral completion route owner already exists
  -> publication policy
  -> retire publication policy
  -> compatibility completion projection
```

Важно: completion expansion должен сохранять `CompletionRecord` нейтральным, не пропускать произвольные числовые `VmExitReason` как authority и отделять completion publication от retire publication. 

---

## 4. Довести nested virtualization через neutral child-intent owner

Nested сейчас безопасен, но не feature-complete. Neutral `NestedDomainProjectionCheckpointService` существует, Shadow VMCS compatibility admission остаётся denied/fail-closed. 

Открытая задача:

```text
neutral child-domain intent descriptor
  -> capability filter
  -> nested memory composition owner
  -> nested evidence policy
  -> runtime admission
  -> neutral completion/retire publication
  -> VMCS12/VMCS02 compatibility projection
```

Shadow VMCS не должен стать mutable runtime state. VMCS12/VMCS02 должны оставаться compatibility projection boundary. 

---

## 5. Провести descriptor readiness policy audit

Сейчас migration/nested readiness остаются fail-closed, пока neutral generated projection не даёт materialized state и conformance evidence. 

Открытая задача — определить, когда descriptor готов к:

```text
VMREAD projection eligibility
nested projection eligibility
migration readiness
restore readiness
completion-owned compatibility projection
```

И при этом не допустить, чтобы readiness вычислялся из VMCS/VMX projection state.

---

## 6. Доказать migration/evidence policy для recomputed compatibility fields

Roadmap прямо оставляет задачу: доказать, что recomputed compatibility fields не сериализуются как independent VMCS authority. 

Closure `254` closes this item for the current completion-owned VMREAD projection slice. The schema marks `ExitReason`, `ExitQualification`, `GuestPhysicalAddress`, and `EptViolationQualification` as `RecomputedCompletion`; migration/checkpoint payload classes do not include those values, `CompletionRecord`, or `VmxCompletionProjection`; compatibility projection metadata and compatibility-projection checkpoint authority remain rejected; host-owned evidence remains recompute-only.

Открытая задача:

```text
checkpoint image
  содержит neutral guest-visible state / descriptors / policy

checkpoint image
  не содержит VMCS projection as authority
  не содержит host evidence
  не содержит recomputed completion fields как state owner
```

Особенно важно для VMREAD fields, completion projection, nested projection и future hypercall state.

---

## 7. Реализовать feature-complete VMX backend execution через neutral owners

Это самая крупная открытая задача. Audit прямо фиксирует: successful VMX backend execution, successful VMX backend publication и feature-complete nested compatibility execution остаются открытыми за пределами текущих admitted-denied slices. 

Сюда входят будущие пути:

```text
VMLAUNCH / VMRESUME
VMXON / VMXOFF
VMCALL successful backend
VMFUNC
INVEPT / INVVPID
VMREAD broad projection
VMWRITE neutral-owner-backed writes, если вообще нужны
nested VMX paths
```

Каждый путь должен иметь:

```text
neutral owner
runtime admission
capability/evidence requirements
publication fence
retire rule
VMX projection only after neutral success
```

---

## 8. Не открывать VMWRITE без отдельного neutral write owner

Сейчас VMWRITE находится в состоянии denied/fail-closed, потому что нет admitted neutral owner. 

Открытая задача только условная: если понадобится writable VMCS-compatible surface, для каждого поля нужен отдельный neutral write owner и отдельная admission policy. Нельзя делать:

```text
VMWRITE -> VMCS field store
```

Можно только:

```text
VMWRITE
  -> generated field schema
  -> neutral owner write request
  -> runtime admission
  -> capability/evidence policy
  -> retire publication
```

---

## 9. Продолжать strengthening admitted-denied naming/tests

Это отдельная открытая guardrail-задача из audit backlog: admitted-denied projection нельзя принять за success. Имена результатов, тесты и документация должны оставаться однозначными. 

Критичные места:

```text
TrapProjectionDeniedBackend
ReadOnlyProjectionDenied
CompletionPublicationDenied
RetirePublicationDenied
ProjectionOnly
DeniedAlias
```

Любой новый result type должен ясно различать:

```text
admitted projection
backend execution
completion publication
retire publication
guest-visible success
```

---

## 10. Поддерживать generated/projection inventory для новых файлов

Projection inventory сейчас закрыт: 0 forbidden-authority targets, generated/frontend projection scope классифицирован. 

Но это постоянная задача: каждый новый файл в `Compatibility/Generated/*` или `Compatibility/Frontend/Projection/*` должен быть классифицирован как:

```text
generated-lineage
contract-only
denied-only
```

И не должен стать:

```text
runtime owner
VMCS field store
VMX manager
backend authority
host evidence store
```

---

## 11. Синхронизировать WhiteBook/current-state с closure `239` / `240` / `241` / `242` / `243`

WhiteBook/current-state documentation must not describe the post-239 state as if broad VMREAD value projection were still entirely absent. It is still not feature-complete backend execution, but several generated read-only VMREAD slices are now closed through neutral owners.

`audit3.md` and the current model audit already fix closure `240`: completion-owned VMREAD value projection для четырёх полей закрыт; closure `241`: memory-owned VMREAD value projection для `GuestCr3` / `EptPointer` закрыт; closure `242`: `Vpid` is projected from neutral memory-domain tagging semantics only; closure `243`: compatibility-control semantics are materialized but control VMREAD values remain denied; closure `248`: `Cr3TargetCount` is projected from neutral `AddressSpaceTargetCount` only; closure `249`: `HostCr3` is explicitly denied until neutral host-address-space ownership exists.

Документационная задача для этих closures закрыта в current-state docs; если отдельный `WhiteBook/16_Current_State_And_Closure_Matrix.md` снова появится как отдельный artifact, его нужно синхронизировать той же формулировкой:

```text
обновить WhiteBook:
  - Current State
  - Roadmap
  - Closure Matrix
  - VMCS Projection / Field Access
  - Admission Boundaries
```

Нужно явно написать:

```text
VMREAD value projection:
  closed for completion-owned slice
  closed for memory-owned GuestCr3/EptPointer/Vpid/Cr3TargetCount slice
  HostCr3 explicit HostAddressSpaceOwnerMissing denial
  control owner materialized as fail-closed neutral semantics
  control VMREAD values explicitly denied with CompatibilityControlValueProjectionDenied
  open for execution/nested fields and remaining memory-owned fields
```

---

## 12. Поддерживать запрет на возврат VMX authority

Это не новая функциональная задача, но постоянный архитектурный запрет. Future review red flags уже перечислены:

```text
VMX runtime manager
mutable VMCS field store
active VMCS pointer state
VmExitReason inside neutral runtime policy
compatibility completion without neutral fence
TrapDecision as runtime policy
VMX no-emission tests becoming production backend emission
host evidence in guest-visible VMCS projection
```



---

# Приоритетный порядок работ

Я бы упорядочил оставшиеся задачи так:

1. **Поддерживать WhiteBook/current-state sync под closure 239-248**, чтобы документация не отставала от кода.
2. **Расширить VMREAD value projection field-by-field**, только через neutral owners.
3. **Материализовать real neutral hypercall/trap owner для successful VMCALL**, replacing `MissingNeutralOwner` only after runtime semantics, typed capability, and evidence policy exist.
4. **Использовать уже добавленный runtime-owned completion route только после neutral hypercall/trap owner**, затем открывать allowed publication fence path отдельным closure.
5. **Сделать nested child-intent owner**, без Shadow VMCS state store.
6. **Провести descriptor readiness policy audit**.
7. **Закрыть migration/evidence proof для recomputed compatibility fields**.
8. **Только после этого двигаться к feature-complete VMX backend execution**.

---

## Самая короткая формула остатка

```text
Закрыто:
  VMX как compatibility frontend
  legacy VMX authority
  VMCS manager / field store
  first VMREAD admission
  completion-owned VMREAD value slice
  memory-owned GuestCr3/EptPointer/Vpid/Cr3TargetCount VMREAD value slice
  HostCr3 explicit denied contract
  CompatibilityControlDescriptor fail-closed neutral semantics
  explicit control VMREAD value denial
  VMCALL admitted-denied projection
  neutral trap split
  publication fence

Открыто:
  successful VMX backend execution
  broader VMREAD values beyond completion/GuestCr3/EptPointer/Vpid/Cr3TargetCount
  optional future control-field mapper only after a separate neutral control-bit value contract
  successful VMCALL/hypercall
  allowed completion/retire publication path
  nested child-intent owner
  descriptor readiness
  migration/evidence proof
  feature-complete nested compatibility execution
```

Итог: сейчас модель уже сильна как **secure compatibility frontend over neutral runtime**, но ещё не завершена как **полноценная исполняющая виртуализация VMX-гостя**.
