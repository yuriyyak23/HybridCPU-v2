
## 1. Корректная архитектурная позиция VMX в HybridCPU


В модели HybridCPU виртуализация является частным случаем общего контура легальности:
она обеспечивает допустимое представление, ограниченное использование
и контролируемую публикацию ресурсов одного вычислительного домена
внутри другого домена.


Виртуализация в HybridCPU — это не отдельный привилегированный режим,
а особая форма легализации доступа к вычислительным, адресным,
вводно-выводным и побочным состояниям через общий механизм допуска.


В правильно построенной модели HybridCPU добавление обычных non-VMX инструкций должно быть прозрачно для VMX-части.

Разработчик не должен каждую новую инструкцию вручную интегрировать с VMX.

Правильная формула:

новая non-VMX инструкция
    интегрируется не с VMX,
    а с общей системой легальности и исполнения HybridCPU

А VMX уже видит её косвенно, через:

домен исполнения
домен памяти
систему возможностей
систему завершения
систему проекций
Главное правило
VMX не должен быть точкой интеграции новых инструкций.
VMX должен быть одним из клиентов общей runtime-модели.

То есть новая инструкция должна добавляться в:

ISA metadata
Stage A legality
Stage B legality
execution lane binding
retire/publication model
evidence/migration policy, если нужно

А не в:

VMX frontend
VMCS manager
VmxCaps special path
VMX-specific handler

Иначе VMX снова станет архитектурной осью.

Только если новая инструкция пересекает границу виртуализации.

Например, если инструкция:

должна вызывать перехват в госте;
имеет guest-visible privileged effect;
меняет состояние, которое VMX обязан проецировать через VMCS;
добавляет новое capability, видимое через VmxCaps;
создаёт новый тип VM-exit;
влияет на migration/checkpoint;
создаёт host-owned evidence;
использует DMA, Lane6, Lane7, external backend;
требует отдельной политики nested virtualization.

Только тогда нужна VMX-проекция или VMX-совместимое отображение.

1. **VMX не является архитектурной осью HybridCPU.**

2. **VMX является compatibility frontend-протоколом доступа** для VMX ABI, legacy VMX-v1/VMX8 surface и VMX-v2 compatibility сценариев.

3. **Источником истины является generic domain/descriptor/capability runtime substrate**, а не VMCS, VMCSv2, VMX CSR или VMX instruction plane.

4. **VMX не владеет execution state.** Execution state принадлежит `ExecutionDomainDescriptor`.

5. **VMX не владеет memory state.** Memory virtualization принадлежит `MemoryDomainDescriptor`.

6. **VMX не владеет I/O и DMA state.** I/O и DMA authority принадлежит `IoDomainDescriptor`.

7. **VMX не владеет Lane6/Lane7 state.** Lane execution, queues, tokens, fences и completions принадлежат lane descriptors и runtime namespaces.
   Progress 2026-05-25 task `205`: Lane7 native token maps, backend binding cache, and scheduler pressure cache are no longer physically held by the VMX substrate state block; they are delegated to neutral `Lane7HostOwnedEvidenceStore` under `Core/Runtime/Lanes/Lane7/HostOwnedEvidence`.
   Progress 2026-05-25 task `206`: generic domain-runtime services/descriptors are no longer physically under `Core/VMX/Substrate/Runtime`; `DomainRuntimeContext`, `DomainRuntimeOperation`, runtime authority/root, legality, validation, scheduling, and binding now live under `Core/Runtime/Domains/*`.
   Progress 2026-05-25 task `207`: generic non-capability descriptors, domain admission runtimes, and neutral nested projection primitives are no longer physically owned by `Core/VMX/Substrate`; they live under `Core/Runtime/Domains/*` or `Core/Runtime/Nested/*`.

8. **VMX не владеет capability state.** Capabilities принадлежат `CapabilityDescriptorSet`.

9. **VMX не владеет migration/checkpoint state.** Migration принадлежит `MigrationDescriptor` / domain checkpoint model.
   Progress 2026-05-25 task `192`: the VMCS-shaped checkpoint image and DTO are removed without replacement. `VmxCheckpointImage`, `VmcsV2Checkpoint`, and descriptor scalar restore helpers no longer serialize/restore VMCS projection state as authoritative migration state; neutral `DomainCheckpointImage` / `RestoreValidationService` remain the checkpoint validation boundary.
   Progress 2026-05-25 task `193`: `VmcsV2Blocks` no longer exposes vector checkpoint restore or dirty-log snapshot/restore helpers. Remaining VMCS block names are read-only compatibility projection vocabulary, not checkpoint authority.

10. **VMX не владеет security/evidence state.** Evidence visibility, recomputation, zeroization и host-owned runtime facts принадлежат `EvidencePolicy`.
   Progress 2026-05-25 task `205`: Lane7 host-owned native-token, backend-binding, and scheduler evidence is cleared on restore under fail-closed evidence policy and must be rebuilt explicitly from host runtime state.

11. **VMCS/VMCSv2 — не substrate object.** Это generated compatibility projection поверх domain substrate.

12. **VMX CSR — не authority.** Это ABI alias/projection поверх authoritative descriptors.

13. **VMExit/VMFail/VMAbort — не внутренняя модель runtime.** Это VMX-facing projection of generic domain trap/fail/abort outcomes.

14. **Nested VMX — не публичная модель nested virtualization.** Публичная модель — `NestedDomainDescriptor` и `NestedProjectionService`.

15. **Legacy остаётся только на ABI-границе.** Внутри архитектуры не должно быть legacy VMX naming как владельца состояния.

---

## 2. Что требуется от корректной VMX-модели HybridCPU

1. **Стабильный VMX compatibility ABI.**  
   Legacy VMX opcodes, VMCS field ids и VMX CSR addresses должны оставаться доступными для старого кода.

2. **VMX frontend должен быть тонким.**  
   Он принимает VMX instructions/CSR/VMCS access и переводит их в generic substrate operations.

3. **VMX frontend не должен хранить authoritative state.**  
   Любое VMX-visible поле должно мапиться на substrate descriptor, projection service или explicit unsupported/denied result.
   Progress 2026-05-24: `VmxExecutionUnit.ResolveActiveMemoryTranslationControl()` was removed without replacement. Intercept/event admission must not derive domain tags by reading `SecondaryProcControls`, `GuestCr3`, `EptPointer`, or `Vpid` VMCS fields; tagged admission belongs to generic domain-admission descriptors.
   Progress 2026-05-25: the entire legacy `VmxExecutionUnit` constructor/opcode shell has now been removed without replacement. Frozen VMX opcode identity remains only as typed fail-closed compatibility effects until generic runtime admission supplies policy-checked behavior.

4. **`VmcsV2Descriptor` должен стать generated projection.**  
   Ручное расширение VMCSv2 запрещено как источник architectural drift.
   Progress 2026-05-25 task `194`: public `VmcsV2Descriptor.TryWriteScalarField` was removed without replacement. `VmcsV2Descriptor` no longer exposes a compatibility scalar field-write API or the former scalar write validation helper path; remaining internal scalar projection cache mechanics are residual generated/read-only projection audit scope, not runtime authority.
   Progress 2026-05-25 task `195`: the residual internal scalar projection cache was removed/denied without replacement. `_scalarValues`, `_scalarWritten`, `WriteKnownScalar`, `HasScalarFieldValue`, and `TryGetScalarFieldValue` are absent; `TryReadScalarField` remains only as denied compatibility ABI until generated read-only projection over neutral owners exists.
   Progress 2026-05-25 task `225`: descriptor guest-state and host-evidence mutators were removed without replacement. `CaptureGuestStateEager`, `BeginLazyGuestStateSave`, `MaterializeLazyGuestRegisters`, `MaterializeVmExitGuestState`, `RecordHostEvidence`, `DiscardHostEvidenceAfterRestore`, and `ResetForClear` are absent; VMCSv2 cannot materialize guest GPR state or retain host evidence as descriptor-owned authority.
   Progress 2026-05-26 task `230`: VMCSv2 block root/NPT/bundle/event/debug mutators were removed without replacement. `BindRootDescriptor`, `AdvanceEpoch`, `BindControl`, `BindBundle`, VMCS-owned event queue/remap/delivery/snapshot helpers, and debug trace configure/record/reset helpers are absent; these blocks are read-only compatibility projection shells until neutral runtime owners provide admitted behavior.
   Progress 2026-05-26 task `231`: residual VMCSv2 vector/dirty/security/capability block-state is no longer held as private-set backing state. `VectorStreamStateBlock`, `DirtyLogBlock`, `SecurityIsolationBlock`, and `CapabilityNegotiationBlock` are read-only/default projection shells, and `ExitInfoBlock.Record*` is internal retire-publication plumbing behind descriptor `Record*Exit` helpers.
   Progress 2026-05-27 task `232`: every source under `Core/VMX/Compatibility/Generated/*` and `Core/VMX/Compatibility/Frontend/Projection/*` is now classified by test-local inventory as generated-lineage, contract-only, denied-only, or forbidden-authority.
   Progress 2026-05-27 task `233`: `SchedulingBudgetTimer` and `TrapPolicyBitmap` mutable authority was removed from `Core/VMX/Compatibility/Frontend/Projection/Events` and rehomed under `Core/Runtime/Events/Traps`. At closure `233` time, the remaining forbidden-authority targets were `VmcsV2Header` and `ChildDomainIntentDescriptor`.
   Progress 2026-05-27 task `234`: `VmcsV2Header` launch/invalidation epoch authority and `ChildDomainIntentDescriptor` child-intent field store/write/snapshot state are removed. The generated/frontend projection inventory now has `0` forbidden-authority targets; new projection sources must be generated-lineage, contract-only, denied-only, or explicitly fenced before authority can appear.
   Progress 2026-05-27 task `235`: first admitted VMX compatibility path is a narrow `VMREAD` projection admission through `RuntimeBoundaryAdmissionService`. It admits only `ReadCompatibilityProjection` under compatibility-alias evidence policy and still stops at denied `TryReadScalarField`; it does not create a VMCS backend, field store, manager, or success retire path.
   Progress 2026-05-28 task `236`: neutral trap result authority is split from VMX exit projection before any admitted VMCALL/trap/intercept path. Runtime trap policy now produces `NeutralTrapResult`; `VmExitReason`, `VmxExitQualification`, and `TrapDecision` are projected only by `VmxTrapProjectionMapper` in the VMX compatibility frontend.
   Progress 2026-05-28 task `237`: `VMCALL` has an admitted-denied trap projection path only. It passes decode, generated frozen alias projection, `RuntimeBoundaryAdmissionService`, neutral trap policy, `NeutralTrapResult`, and `VmxTrapProjectionMapper`, then returns backend-denied projection without successful VMCALL or intercept retire.
   Progress 2026-05-28 task `238`: admitted-denied VMCALL trap projection now carries a neutral `TrapCompletionPublicationFenceResult`. Completion and retire publication are denied with `DeniedBackendExecution`; VMX compatibility helpers require this neutral fence and cannot publish a VMX completion or intercept retire effect while the fence is closed.
   Progress 2026-05-29 task `246`: runtime-owned trap completion route design now exists before any real VMCALL/intercept publication. `TrapCompletionRouteDescriptor` and `TrapCompletionRouteService` live under neutral `Core/Runtime/Completion/Routing`; VMCALL uses only `ProjectionOnlyDenied`, so backend execution, completion publication, and retire publication remain denied.
   Progress 2026-05-29 task `247`: VMCALL now has explicit neutral hypercall backend admission evidence. `HypercallBackendAdmissionService` lives under neutral `Core/Runtime/Events/Hypercalls`; production VMCALL passes `MissingNeutralOwner`, so backend execution remains denied before route/fence publication.
   Progress 2026-05-29 task `248`: no real hypercall backend owner is opened because no concrete neutral runtime semantics exist. VMREAD value projection instead expands one field: `Cr3TargetCount` comes only from neutral `MemoryDomainReadOnlyTranslationView.AddressSpaceTargetCount` after runtime admission, generated owner lookup, compatibility-alias evidence, and neutral memory validation.
   Progress 2026-05-29 task `249`: `HostCr3` remains denied with explicit `HostAddressSpaceOwnerMissing`; no neutral host-address-space owner exists, and guest/domain `AddressSpaceRoot` must not be reused as host CR3 authority.
   Progress 2026-05-29 task `250`: execution-owned VMREAD value projection opens only `GuestPc`, `GuestSp`, and `GuestFlags`, sourced from neutral `ExecutionDomainReadOnlyStateView` through `ExecutionDomainDescriptor.TryCreateReadOnlyStateView()` after runtime admission, generated schema owner lookup, and `GuestArchitecturalState` evidence. `GuestCr0` and `GuestCr4` remain denied until neutral privileged execution-state semantics exist.
   Progress 2026-05-29 task `251`: host execution VMREAD aliases stay denied with explicit `HostExecutionStateOwnerMissing`. `HostPc`, `HostSp`, `HostFlags`, and `HostCr0` require a separate neutral host-execution owner and cannot read from guest execution state.
   Progress 2026-05-29 task `252`: remaining control-like VMREAD aliases are fenced as denied unless a real neutral owner/value source exists. `GuestCr0`, `GuestCr4`, `HostCr0`, `HostCr3`, and compatibility-control fields remain fail-closed; no control-bit mapper or VMCS store is admitted.
   Progress 2026-05-30 task `253`: descriptor readiness policy is audited fail-closed. Migration/nested readiness, restore validation, and nested checkpoint readiness do not consume VMREAD projection values, VMCS scalar stores, or compatibility projection metadata; they require neutral materialized state, checkpoint, migration policy, and evidence policy.
   Progress 2026-05-30 task `254`: migration/evidence proof for recomputed completion-owned compatibility fields is closed. `ExitReason`, `ExitQualification`, `GuestPhysicalAddress`, and `EptViolationQualification` are recomputed projection values only; checkpoint/migration payload classes do not serialize them, `CompletionRecord`, `VmxCompletionProjection`, VMCS fields, or host-owned evidence as authority.
   Progress 2026-05-30 task `255`: execution-owned snapshot source is hardened. `ExecutionDomainReadOnlyStateView` now exposes `IsMaterialized`, `HasCompleteGuestPcSpFlags`, and `StateEpoch`, but `StateEpoch` is metadata only and `GuestCr0`/`GuestCr4` remain denied until a separate neutral privileged execution-state owner exists.
   Progress 2026-05-28 task `240`: generated read-only VMREAD value projection is admitted only for completion-owned fields with a neutral `CompletionRecord` source. `ExitReason`, `ExitQualification`, `GuestPhysicalAddress`, and `EptViolationQualification` can be projected after `RuntimeBoundaryAdmissionService`; descriptor-owned and memory-owned fields remain denied until their neutral owners expose explicit value sources.
   Progress 2026-05-29 task `241`: memory-owned generated read-only VMREAD value projection is admitted only for `GuestCr3` and `EptPointer` with a neutral `MemoryDomainDescriptor` source. Values come from `MemoryDomainTranslationControl.AddressSpaceRoot` and owned `SecondStageRoot` through `MemoryDomainReadOnlyTranslationView` after runtime admission and generated schema owner lookup; `HostCr3`, `Vpid`, `Cr3TargetCount`, execution-owned fields, and compatibility-control fields remain denied at this closure.
   Progress 2026-05-29 task `242`: VPID semantics are now explicit and neutral. `Vpid` VMREAD can project only `MemoryDomainReadOnlyTranslationView.AddressSpaceTag` when neutral address-space tagging is enabled and the tag is non-zero; otherwise it remains denied. A neutral `CompatibilityControlDescriptor` exists as future control owner design, but control fields still have no admitted value source.
   Progress 2026-05-29 task `243`: `CompatibilityControlDescriptor` now materializes fail-closed neutral control semantics: runtime admission, read-projection-only execution, write/backend/mutation denial, neutral trap/result/fence requirements, neutral completion/publication requirements, entry validation, nested-intent requirements, memory-owner requirements, and denied control-value projection. Control VMREAD fields remain denied until a separate compatibility mapper is admitted.
   Progress 2026-05-29 task `245`: the control-field fork is resolved by keeping control VMREAD values explicitly denied. `PinBasedControls`, `ProcBasedControls`, `ExitControls`, `EntryControls`, and `SecondaryProcControls` return `CompatibilityControlValueProjectionDenied`; no control-bit mapper or fake VMX control value is admitted.
   Progress 2026-05-25 task `196`: `VmcsFieldProjectionSchema` is now checked by a build-time lineage verifier. The verifier regenerates expected C# from `docs/VMXRefactoring/schemas/vmcs-field-projection-schema.v1.json` and fails the build on drift before `CoreCompile`; this is projection lineage proof, not descriptor runtime authority.

5. **Каждый VMCS field должен иметь явный mapping.**  
   Возможные варианты:
   - maps to `ExecutionDomainDescriptor`;
   - maps to `MemoryDomainDescriptor`;
   - maps to `IoDomainDescriptor`;
   - maps to `CapabilityDescriptorSet`;
   - maps to `CompletionRouteDescriptor`;
   - maps to `EvidencePolicy`;
   - denied;
   - unsupported;
   - reserved.

6. **VMREAD/VMWRITE должны работать только через access policy.**
   Progress 2026-05-25: `VmxExecutionUnit` no longer routes `VMREAD`/`VMWRITE` through direct or nested VMCS helper calls. Those compatibility opcodes now fail closed until generated projection/access-policy admission owns field access.
   Progress 2026-05-25 task `194`: the descriptor-side public scalar write fallback is absent too. No `TryWriteScalarField` path may turn VMCS field access into descriptor-owned mutable scalar state; compatibility writes must remain denied/read-only until a generated projection over neutral owners exists.
   Progress 2026-05-25 task `195`: descriptor-side scalar read fallback is denied too. No scalar array/cache may answer VMREAD as VMCS-owned truth; scalar VMREAD needs a generated read-only projection over neutral descriptors or remains fail-closed.
   Progress 2026-05-25 task `196`: compat VMCS field aliases now have generated lineage proof for the field schema; the build verifies schema-to-C# parity and does not reintroduce VMREAD/VMWRITE execution authority.
   Progress 2026-05-27 task `235`: `VMREAD` now has an admitted compatibility projection path only up to runtime admission and access-policy evaluation. `VmxCompatibilityAdmissionService` requires decode, frozen alias projection, `RuntimeBoundaryAdmissionService`, and compatibility-alias evidence policy before calling the still-denied `TryReadScalarField` ABI.
   Progress 2026-05-28 task `240`: the admitted VMREAD path now uses `VmcsFieldProjectionSchema` owner metadata and `VmcsReadOnlyValueProjectionService` after runtime admission. It no longer falls back to `TryReadScalarField` for admitted value projection. Only completion-owned fields backed by `CompletionRecord` are projected; all writes and unsupported value sources remain denied.
   Progress 2026-05-29 task `241`: the same admitted VMREAD path now also projects `GuestCr3` and `EptPointer` from neutral memory-domain translation state only. Unsupported memory-owned fields remain denied, all writes remain denied, and no VMCS field store or successful VMX backend path was introduced.
   Progress 2026-05-29 task `242`: the same admitted VMREAD path now also projects `Vpid` only from neutral `AddressSpaceTag`; `HostCr3`, `Cr3TargetCount`, and all compatibility-control fields remain denied at this closure. Control fields must not open until `CompatibilityControlDescriptor` exposes a materialized neutral read-only control view and a separate conformance fence admits that source.
   Progress 2026-05-29 task `243`: the control owner now exposes that materialized neutral read-only view, but the VMREAD value path still does not map compatibility-control owners to control-bit values. `PinBasedControls`, `ProcBasedControls`, `ExitControls`, `EntryControls`, and `SecondaryProcControls` remain denied.
   Progress 2026-05-29 task `245`: VMREAD now denies `CompatibilityControlDescriptor`-owned fields with an explicit `CompatibilityControlValueProjectionDenied` state after runtime admission and schema owner lookup. A future mapper is not assumed; it requires a separate neutral control-bit value contract.
   Progress 2026-05-29 task `248`: the same admitted VMREAD path now also projects `Cr3TargetCount` only from neutral `AddressSpaceTargetCount`; `HostCr3`, execution-owned fields, and compatibility-control fields remain denied. Zero target count is a valid neutral value, while invalid counts are denied before projection.
   Progress 2026-05-29 task `249`: the same admitted VMREAD path now gives `HostCr3` its own denied decision before memory translation view fallback. Future `HostCr3` projection requires a separate neutral host-address-space owner.
   Progress 2026-05-29 task `250`: the same admitted VMREAD path now also projects `GuestPc`, `GuestSp`, and `GuestFlags` only from neutral execution-domain read-only state. Default/unmaterialized execution descriptors stay denied, `GuestCr0`/`GuestCr4` stay denied, all writes stay denied, and no VMCS state store or backend VMREAD execution is introduced.
   Progress 2026-05-29 task `251`: the same admitted VMREAD path now denies `HostPc`, `HostSp`, `HostFlags`, and `HostCr0` with explicit missing host-execution owner semantics before any guest read-only state view is consulted.
   Progress 2026-05-29 task `252`: the same admitted VMREAD path now has a conformance fence proving remaining control-like fields stay denied with explicit decisions and write access stays denied.
   Progress 2026-05-30 task `253`: VMREAD value projection remains separate from descriptor readiness. Even admitted neutral VMREAD values cannot make migration/nested readiness successful; readiness must come from neutral materialized state and migration/evidence policy.
   Progress 2026-05-30 task `254`: completion-owned VMREAD fields with `RecomputedCompletion` migration policy remain outside migration authority. They can be projected from neutral completion records but cannot become checkpoint payload state.
   Progress 2026-05-30 task `255`: execution-owned VMREAD remains limited to `GuestPc`, `GuestSp`, and `GuestFlags`. The new snapshot metadata is not a VMREAD field source, and privileged control-register values stay closed.

7. **VMREAD/VMWRITE не должны открывать host-owned evidence.**

8. **VMREAD/VMWRITE не должны читать scheduler evidence.**

9. **VMREAD/VMWRITE не должны читать native DMA tokens.**

10. **VMREAD/VMWRITE не должны читать native accelerator handles.**

11. **VMREAD/VMWRITE не должны читать backend binding evidence.**

12. **VMREAD/VMWRITE не должны читать decode cache, MicroOp cache, typed-slot proofs или replay evidence.**

13. **`VmxCaps` должен быть pure alias.**  
   Он должен публиковать projection of `CapabilityDescriptorSet`, а не хранить собственные capability bits.
   Progress 2026-05-25 task `198`: `VmxCapsProjection` now reads a grant-backed descriptor projection. `CapabilityDescriptorSet.CompatibilityCapsProjection` is computed from `CapabilityGrantCollection.EffectiveCompatibilityMask`; hardware/runtime/domain capability words are read-only projections over typed grants, not stored VMX-owned bitmap state.
   Progress 2026-05-25 task `199`: VmxCaps publication now checks publishable typed compatibility projection grants, not `HasEffectiveCapability(...)` as a mask-derived authority shortcut.

14. **Запись в `VmxCaps` должна быть fail-closed.**  
   В strict mode — illegal/fault. В compatibility mode — no architectural effect.

15. **Capability publication должна быть domain-specific.**  
   VMX frontend показывает только то, что разрешено текущему domain.
   Progress 2026-05-25 task `199`: active publication/admission paths now require typed `CapabilityBoundaryRequirement` grants. Non-typed nonzero requirements fail closed, and nested compatibility capability words are denied projection aliases rather than publication authority.

16. **VMX capability не равна hardware capability.**  
   Она равна:
   ```text
   hardware support
   ∩ runtime policy
   ∩ domain grant
   ∩ security policy
   ∩ migration compatibility
   ∩ evidence policy
   ```

17. **`VMXON` не должен быть архитектурным root switch.**  
   Он должен активировать VMX compatibility frontend для domain runtime.
   Progress 2026-05-25: `VmxExecutionUnit` no longer owns `VMXON`/`VMXOFF` root-switch authority. The legacy frontend no longer activates root descriptors, writes `CsrAddresses.VmxEnable`, emits VMXON/VMXOFF trace events, or drives `PipelineTransitionTrigger.VmxOff`; those opcodes fail closed until generic runtime-domain admission owns the path.

18. **`VMPTRLD` не должен делать VMCS владельцем состояния.**  
   Он должен bind-ить VMX projection handle к existing domain descriptor/projection.
   Progress 2026-05-25: `VmxExecutionUnit` no longer owns `VMCLEAR`/`VMPTRLD`/`VMPTRST` pointer lifecycle. The legacy frontend no longer calls VMCS pointer load/clear/store helpers or emits pointer trace events; those opcodes fail closed until generic execution-domain binding/projection-handle admission owns the path.

19. **`VMLAUNCH`/`VMRESUME` должны мапиться на `DomainEnter`.**
   Progress 2026-05-25: `VmxExecutionUnit` no longer owns `VMLAUNCH`/`VMRESUME` VM-entry. The legacy frontend no longer calls VMCS entry helpers, restores guest PC/SP, emits VM-entry trace events, or moves pipeline state through frontend-owned entry transitions; those opcodes fail closed until generic `DomainEnter` admission owns the path.

20. **`VMExit` должен мапиться на `DomainTrap` / `DomainFault` / `DomainAssist`.**
   Progress 2026-05-24: `VmxExecutionUnit.CompleteNestedTranslationFault(...)` was removed without replacement. Nested translation fault completion/publication must be owned by generic nested-domain/domain-fault routing, not by legacy VMX frontend VMCS publication.
   Progress 2026-05-24: VMCS-field-derived intercept/event admission domains were removed from `VmxExecutionUnit`; VMExit/admission routing must come from generic domain descriptors instead of frontend VMCS field reads.
   Progress 2026-05-25: standalone intercept-exit publication was removed from `VmxExecutionUnit`. Intercept observability/publication must be owned by generic domain-trap publication, not by frontend `RecordInterceptExit` / `VmxEventKind.InterceptExit` shortcuts.
   Progress 2026-05-25: pending virtual-event delivery was removed from `VmxExecutionUnit`. Event delivery must be owned by generic event-queue descriptors, remap policy, and runtime admission, not by frontend VMCS helper calls.
   Progress 2026-05-25: common qualified VM-exit completion was removed from `VmxExecutionUnit`. VM-exit completion/publication must be owned by generic domain-trap/domain-fault routing, not by frontend `CompleteQualifiedVmExit` / `RecordQualifiedVmExit` shortcuts.
   Progress 2026-05-25: guest-intercept request construction was removed from `VmxExecutionUnit`. Guest-intercept routing must be owned by generic domain-trap admission, not by frontend `TrapRequest.For*` construction or VMCS-backed guard checks.
   Progress 2026-05-25: removal of the legacy handler also removes its former safe-boundary hook surface; current dispatch, micro-op, and retire paths cannot publish VM-exit or event behavior through a returned frontend class.
   Progress 2026-05-25: `VmcsManager` no longer retains nested/intercept/qualified-exit publication or standalone virtual-event queue/delivery entry points as a fallback for the removed frontend. It remains quarantined solely because separate lane/vector/dirty/DMA state ownership is still unresolved.
   Progress 2026-05-25: `VmcsManager` and `IVmcsManager` are now deleted without replacement. Production Core has no VMCS-owned lane/completion/dirty/Lane7/DMA binding; compatibility guest Lane6/Lane7 admission fails closed before native runtime side effects, and `Legacy/VMX` is empty.
   Progress 2026-05-28 task `238`: VMCALL/trap compatibility projection cannot publish VM-exit completion through VMX-shaped vocabulary. `CompletionRecord.TryFromCompatibilityExit` requires the neutral trap publication fence, and neutral trap records with VMX-looking reason codes are not projected as VMX exits.
   Progress 2026-05-29 task `246`: `TrapCompletionRouteService` is the neutral route owner before the fence. A trap completion route can authorize publication only after runtime admission, neutral trap result, runtime-owned route authority, domain validation, backend execution authorization, completion publication permission, and retire publication permission. The VMX frontend does not use the allowed publication route.
   Progress 2026-05-29 task `247`: the backend-execution authorization input to the route now comes from `HypercallBackendAdmissionResult.IsAllowed`; production VMCALL gets `MissingBackendDescriptor`, so the input is false and the route remains `DeniedBackendExecution`.

21. **`VMFail` должен мапиться на descriptor/projection validation failure.**
   Progress 2026-05-25: common VMFail publication was removed from `VmxExecutionUnit`. Fault retire now returns typed `VmxRetireOutcome.Fault(...)` without frontend `HardwareWrite(...)` to VMX exit CSRs or `_vmcs.RecordVmxFailForObservability(...)`; publication must come from generic descriptor/projection validation and domain-fault routing.

22. **`VMAbort` должен мапиться на runtime invariant violation.**

23. **`INVEPT`/`INVVPID` не должны владеть MMU model.**  
   Они должны быть VMX aliases over generic translation/domain invalidation.
   Progress 2026-05-25 task `200`: canonical `AddressSpaceId` / `NestedTlbTag` and nested-TLB invalidation keys are now `DomainTag`, `AddressSpaceTag`, and `SecondStage*` identity. The unused VMX-shaped host IOMMU overload was removed without replacement; compatibility translation/status and IOTLB/DMA projection vocabulary remain separate cleanup scope.

24. **`VMFUNC` не должен становиться bypass.**  
   Он допустим только как restricted fast path через prevalidated capability grants.
   Progress 2026-05-25: `VmxExecutionUnit` no longer resolves Lane7 VMFUNC leaves through VMCS helper state and no longer owns the VMFUNC capability-query/readback gate. VMFUNC now fails closed until generic runtime capability and Lane7 admission own the path.

25. **`VMSAVEX`/`VMRESTX` не должны владеть extended state.**  
   Они должны вызывать save/restore projection over `ExecutionDomainDescriptor` / vector-stream descriptors.
   Progress 2026-05-25: `VmxExecutionUnit` no longer validates, saves, or restores vector-stream extended state through VMCS helper calls. `VMSAVEX` and `VMRESTX` now fail closed until generic vector-stream descriptors and runtime admission own that path.
   Progress 2026-05-25 task `191`: the standalone VMCSv2 vector-stream helper authority is removed too. `VmxVectorStreamStateManager` and `VmxStreamDescriptorValidator` were deleted without replacement; vector-stream runtime ownership remains with generic `VectorStreamDomainRuntime`, while remaining VMCSv2 vector/checkpoint DTO vocabulary is still audit scope before freeze.
   Progress 2026-05-25 task `193`: the remaining `VectorStreamStateBlock` helper methods for policy configure, stream descriptor binding, vector snapshot capture/restore, checkpoint restore, vector dirty marking, stream epoch advance, and vector fault helper publication were removed without replacement. The generic `VectorStreamDomainRuntime` remains the runtime owner.

26. **VMX retire effects должны быть typed.**  
   Требуются разные outcomes:
   - success;
   - VMFailValid;
   - VMFailInvalid;
   - VMExit;
   - VMAbort.

27. **Retire-owned publication обязательна.**  
   Никакой VMX effect не должен становиться видимым до корректного retire boundary.
   Progress 2026-05-25: the legacy frontend no longer owns common qualified VM-exit publication; former helper callers fail closed until retire/domain completion routing is supplied by generic runtime infrastructure.
   Progress 2026-05-25: the legacy frontend also no longer owns common trace or VMX CSR publication. `VmxExecutionUnit` no longer calls `RecordVmxEvent`, `RecordVmxFailForObservability`, or `HardwareWrite`; visible fault/trace publication must be supplied by the generic retire/domain-fault boundary.
   Progress 2026-05-25: the legacy frontend no longer publishes capability readback through VMCALL/VMFUNC. Those opcodes fail closed until generic runtime capability admission and typed publication policy own the retire-visible result.
   Progress 2026-05-25: `VmxExecutionUnit` no longer exists in `Legacy/VMX` or `Core/VMX`; current frozen opcode routing materializes only typed fail-closed effects and has no constructor ABI surface.
   Progress 2026-05-25: the remaining quarantined `VmcsManager` no longer exposes public VMX debug/fail/abort/invalidation observability entry points. No compatibility replacement may recreate this publication path.
   Progress 2026-05-25: task `190` removes that final carrier completely; any future lane/vector/dirty behavior must be owned by generic runtime services or denied, never by a renamed VMCS state manager.
   Progress 2026-05-25 task `191`: the removed VMCSv2 vector-stream helpers cannot re-enter as retire/publication shortcuts; descriptor-owned vector dirty publication and VMCS-shaped stream validation evidence are denied by static conformance.
   Progress 2026-05-25 task `193`: `VmcsV2Blocks` cannot re-enter as a dirty/vector/checkpoint publication shortcut; former vector epoch/dirty/fault helpers and dirty-log mutation helpers are absent and fenced by conformance.
   Progress 2026-05-25 task `194`: `VmcsV2Descriptor.TryWriteScalarField` cannot re-enter as a scalar retire/publication shortcut; public descriptor scalar mutation and scalar field-store replacement owners are fenced by conformance.
   Progress 2026-05-28 task `238`: `VmxRetireEffect.InterceptExit` now requires a neutral `TrapCompletionPublicationFenceResult` and fails closed to `SecurityPolicyViolation` when publication is denied. Production dispatch/retire still materializes only fail-closed VMX effects for frozen opcodes.
   Progress 2026-05-29 task `246`: admitted VMCALL projection now records a neutral `TrapCompletionRouteResult` before the publication fence. It is `DeniedBackendExecution`; no production dispatch/retire path calls a successful intercept or VMCALL publication factory.
   Progress 2026-05-29 task `247`: admitted VMCALL projection also records `HypercallBackendAdmissionResult`. It is `MissingBackendDescriptor`, not success, and no production dispatch/retire path materializes a hypercall backend owner.
   Progress 2026-05-25 task `195`: `WriteKnownScalar` and descriptor-owned scalar arrays cannot re-enter as scalar retire/publication cache; scalar projection stores and VMCS projection runtime managers are fenced by conformance.
   Progress 2026-05-25 task `225`: `VirtualCpuBlock` no longer exposes guest register capture/materialization/snapshot helpers, and `VmcsV2Descriptor` no longer exposes host-evidence record/discard/reset helper paths. At that point, event/debug/root/NPT helpers still had to be extracted to neutral runtime owners, denied, or deleted before they could be considered safe.
   Progress 2026-05-26 task `230`: the remaining root/NPT/bundle/event/debug VMCSv2 helper pool was deleted/denied rather than extracted into VMX. Real event queue/remap/delivery behavior remains under neutral `Core/Runtime/Events/Injection`, and future debug/observability publication must enter through neutral retire/domain evidence boundaries before any VMX compatibility projection can expose it.
   Progress 2026-05-26 task `231`: the remaining vector-stream, dirty-log, security-isolation, and capability-negotiation VMCSv2 block state was reduced to generated/read-only default projection vocabulary. Exit publication remains descriptor-mediated; direct `ExitInfoBlock.Record*` calls are internal and must not become a public VMX backend path.
   Progress 2026-05-27 task `232`: generated/frontend projection inventory is exact and executable. No unclassified file may be added under the generated/projection scope, and runtime manager/store/backend authority markers remain forbidden there even for currently named extraction targets.

28. **VMX frontend должен уважать VLIW/EPIC bundle model.**  
   VMX не может обходить typed-slot constraints и system-singleton scheduling.

29. **VMX operations должны иметь lane/slot legality.**  
   Обычно — Lane7/SystemSingleton, если не доказан иной safe path.

30. **VMX frontend должен быть capability-gated.**
   Progress 2026-05-25: `VmxExecutionUnit` no longer gates VMCALL/VMFUNC by reading VMX CSR state or frontend capability projections. Capability admission must come from generic runtime capability descriptors.

31. **VMX frontend должен быть descriptor-gated.**

32. **VMX frontend должен быть privilege-gated.**

33. **VMX frontend должен быть evidence-safe.**

34. **VMX frontend должен быть migration-safe.**

35. **VMX frontend должен быть replay/rollback-safe.**

---

## 3. Требования к domain substrate, который заменяет VMX-ось

1. **`ExecutionDomainDescriptor` должен владеть CPU execution context.**

2. **Он должен описывать:**
   - PC;
   - SP;
   - 32 GPR;
   - flags/status;
   - CSR shadow policy;
   - privilege/runlevel policy;
   - VT binding policy;
   - scheduling class;
   - trap policy;
   - run/halt state.

3. **`MemoryDomainDescriptor` должен владеть memory virtualization.**

4. **Он должен описывать:**
   - address-space root;
   - translation mode;
   - permission model;
   - memory type policy;
   - TLB epoch;
   - dirty tracking policy;
   - shared/private memory classification;
   - migration memory policy.

5. **`IoDomainDescriptor` должен владеть I/O и DMA authority.**

6. **Он должен описывать:**
   - IOMMU domain;
   - device ownership;
   - DMA windows;
   - IOTLB epoch;
   - interrupt/completion routing;
   - non-coherent DMA policy;
   - DMA fence requirements.

7. **Lane descriptors должны владеть lane-specific virtualization.**

8. **Lane6 descriptor должен владеть:**
   - DMA queues;
   - guest/runtime descriptors;
   - token namespace;
   - fence namespace;
   - descriptor validation;
   - replay/abort policy;
   - completion routing.

9. **Lane7 descriptor должен владеть:**
   - virtual accelerator handles;
   - token namespace;
   - backend binding policy;
   - quota policy;
   - pressure policy;
   - completion routing;
   - migration rebinding policy.

10. **`CapabilityDescriptorSet` должен владеть capabilities.**
    Progress 2026-05-25 task `198`: `CapabilityDescriptorSet` now uses `CapabilityGrantCollection` as its primary state. The legacy-compatible mask constructor remains only as seed ingress and immediately materializes typed grants.
    Progress 2026-05-25 task `199`: active callers no longer use the compatibility seed masks as security/admission authority. `FromCompatibilityMasks(...)` remains only a compatibility ingress into typed grant materialization.

11. **Capabilities должны быть typed grants, не просто bitmap.**
    Progress 2026-05-25 task `198`: `GlobalHardwareCaps`, `RuntimeEnabledCaps`, `DomainGrantedCaps`, `CompatibilityCapsProjection`, and `EffectiveCaps` are computed from typed grants. Guest-visible VmxCaps bits are projection grants with `GuestVisibleProjection` evidence; hardware/runtime/domain stage grants remain `HostOnly` and `NeverProject`.
    Progress 2026-05-25 task `199`: `CapabilityBoundaryRequirement`, runtime authority, legality, and admission now consume typed grants directly; a compatibility/effective mask alone cannot satisfy a nonzero capability boundary.

12. **Каждая capability должна иметь:**
   - id;
   - owner domain;
   - scope;
   - delegation policy;
   - revocation policy;
   - migration class;
   - evidence requirement;
   - frontend projection rule.

13. **`TokenNamespace` должен владеть virtual tokens.**

14. **Native tokens не должны быть guest-visible.**

15. **`FenceDomain` должен владеть ordering semantics.**

16. **Fence semantics должны покрывать CPU, DMA, IOMMU, Lane6, Lane7, memory visibility и completion publication.**

17. **`CompletionRouteDescriptor` должен владеть completion routing.**

18. **Completion routing должен быть generic, не VMX-specific.**

19. **VMX posted events должны быть projection of generic completions.**
    Progress 2026-05-25: the quarantined VMX frontend no longer publishes `VmxEventKind.EventDelivered` or calls VMCS-backed pending event delivery; future posted-event visibility must be projected from generic completion/event routing.
    Progress 2026-05-25: standalone VMCS-manager event queue/delivery APIs were also removed without replacement; its remaining lane-completion route is an unresolved quarantined dependency, not the approved generic completion owner.
    Progress 2026-05-25: final manager removal is closed: production no longer binds completion or dirty behavior through VMCS state, and compatibility guest Lane6/Lane7 paths fail closed before native effects.

20. **`EvidencePolicy` должен владеть visibility и lifetime runtime evidence.**

21. **Evidence должна классифицироваться как:**
   - guest/domain architectural state;
   - root-visible policy state;
   - host-owned runtime evidence;
   - recomputable evidence;
   - non-migratable evidence;
   - debug-only evidence.

22. **`MigrationDescriptor` должен владеть checkpoint compatibility.**

23. **Migration должна serialise domain state, не VMCS projection state.**
    Progress 2026-05-25 task `192`: `VmxCheckpointImage` and the VMCSv2 scalar checkpoint DTO path were deleted without replacement; VMCS-shaped scalar field snapshots and restore helpers cannot be used as migration source of truth.
    Progress 2026-05-25 task `193`: VMCS-shaped block checkpoint helpers were removed from `VmcsV2Blocks`; vector/checkpoint restore must come from generic domain/vector checkpoint owners or be denied.

24. **Host-owned evidence должна отбрасываться и пересчитываться после restore.**

25. **`NestedDomainDescriptor` должен владеть nested virtualization.**

26. **Nested mode должен быть domain composition, не VMCS12/VMCS02 as architecture.**

27. **`NestedProjectionService` должен генерировать frontend-specific nested views.**

28. **VMCS12/VMCS02 могут существовать только как legacy alias terms.**

---

## 4. Требования к generated projection

1. **Все VMX projections должны генерироваться из canonical schema.**
   Progress 2026-05-25 task `196`: `VerifyVmxProjectionLineage` is now an MSBuild `BeforeTargets="CoreCompile"` target for `VmcsFieldProjectionSchema` and `CompatAliasMap`. It reads the neutral JSON schemas, regenerates expected checked-in C#, writes comparison artifacts under `obj`, and fails the build if generated output drifts.
   Progress 2026-05-25 task `197`: the same build target now also covers `CapabilityDescriptorSetSchema` from `vmxcaps-capability-bit-schema.v1.json` and `CompatSpecArtifactSet` from `compat-spec-artifact-schema.v1.json`. Projection surfaces without executable generation are explicitly classified as `ProjectionContractOnly`.

2. **Canonical schema должна описывать:**
   - domain descriptors;
   - capabilities;
   - VMX field aliases;
   - CSR aliases;
   - access policies;
   - validation policies;
   - migration policies;
   - evidence visibility;
   - failure mappings;
   - frontend-specific encodings.

3. **Ручной VMCS field mapping запрещён.**

4. **Ручной VmxCaps mapping запрещён.**
   Progress 2026-05-25 task `197`: VmxCaps capability bit entries are now concrete schema rows and regenerate the checked-in capability bit table before compilation. This proves lineage for VmxCaps compatibility publication metadata, but does not make bitmap masks the authority source.
   Progress 2026-05-25 task `198`: VmxCaps bit entries now point to neutral typed grant sources (`CapabilityGrantCollection.TypedGrant` / `NestedDomainCapability.TypedGrant`) rather than `CapabilityDescriptorSet.*` bitmap authority.

5. **Ручной nested VMX projection запрещён.**

6. **Generated conformance должен проверять каждый alias.**
   Progress 2026-05-25 task `196`: `GeneratedProjectionLineageBuildContract` and `VmxProjectionSchemaAndQuarantineTests.GeneratedProjectionLineage_BuildTargetRegeneratesVmcsFieldSchemaAndCompatAliases` prove the build target, verifier functions, schema inputs, output source paths, and drift failure markers for VMCS field projection and compat aliases.
   Progress 2026-05-25 task `197`: the conformance proof now includes VmxCaps bit schema entries, compat spec artifact lineage kinds, generated output source paths, and explicit contract-only classification for `CompletionProjectionService`.

7. **Если VMX field не имеет substrate target — build/test должен падать.**

8. **Если VmxCaps bit не имеет typed grant source — build/test должен падать.**
   Progress 2026-05-25 task `198`: `VmxCapsBitSchemaConformanceContract` rejects `CapabilityDescriptorSet.*` as the typed grant source and requires real typed grant vocabulary for capability-bit projection.

9. **Если VMREAD открывает host evidence — conformance должен падать.**

10. **Если migration serializes projection-only state — conformance должен падать.**

11. **Если nested public docs используют VMCS12/VMCS02 как normative terms — documentation lint должен падать.**

---

## 5. Требования к VMX compatibility frontend

1. **VMX frontend должен жить в отдельном namespace/module.**

2. **VMX frontend не должен импортироваться substrate слоями.**

3. **Substrate не должен зависеть от VMX.**

4. **VMX зависит от substrate, но не наоборот.**

5. **VMX frontend должен поддерживать legacy ABI.**

6. **Legacy ABI должен быть frozen.**

7. **Legacy behavior должен быть preserved только на внешней границе.**

8. **Никакой new substrate design не должен называться VMX/VMCS.**

9. **New VMX behavior должен быть capability-gated.**

10. **New VMX behavior должен быть projection-gated.**

11. **New VMX behavior должен иметь strict failure taxonomy.**

12. **VMX frontend должен возвращать VMX-compatible results, но внутри использовать generic outcomes.**

13. **VMX frontend должен иметь clear unsupported behavior.**

14. **Unsupported VMX field/op должен fail-closed.**

15. **Deprecated VMX-v2 handwritten state должен быть удалён или превращён в projection.**

---

## 6. Требования к nested mode

1. **Nested virtualization должна быть domain composition.**

2. **Публичные имена:**
   - `NestedDomainDescriptor`;
   - `ChildDomainIntentDescriptor`;
   - `ComposedDomainProjection`;
   - `NestedProjectionService`;
   - `NestedTrapTranslationService`;
   - `NestedCapabilityFilter`;
   - `NestedMemoryDomainComposer`.

3. **Непубличные/legacy имена:**
   - `VMCS12`;
   - `VMCS02`;
   - `Shadow VMCS`.

4. **Legacy names допустимы только в compatibility glossary.**

5. **Nested child не должен видеть parent/root evidence.**

6. **Nested child не должен управлять physical VMID/VPID/IOMMU domain.**

7. **Nested child не должен получать native Lane6/Lane7 tokens.**

8. **Nested child не должен отключать parent-required intercepts.**

9. **Nested memory composition должна быть deterministic.**

10. **Nested capability composition должна быть intersection-based.**

11. **Nested completion routing должно проходить через parent-approved routes.**

12. **Nested migration должна serialise nested domain state, не nested VMCS cache.**
    Progress 2026-05-25 task `203`: `NestedDomainProjectionCheckpointService` is a neutral admission owner over `NestedProjectionService`, `DomainCheckpointImage`, and `RestoreValidationService`; host-owned evidence restore is denied. The generated Shadow VMCS compatibility service remains fail-closed and cannot bypass that owner.

---

## 7. Требования к MMU/TLB/IOMMU/IOTLB

1. **MMU/TLB не должны быть VMX-owned.**

2. **IOMMU/IOTLB не должны быть VMX-owned.**

3. **Translation state принадлежит `MemoryDomainDescriptor`.**
   Progress 2026-05-25 task `200`: `MemoryDomainDescriptor.TranslationControl` now stores `MemoryDomainTranslationControl`, and active nested composition/page-walk identity consumers use that neutral control rather than a VMX/NPT-shaped translation record.

4. **DMA translation state принадлежит `IoDomainDescriptor`.**

5. **TLB invalidation должна быть generic domain operation.**

6. **IOTLB invalidation должна быть generic I/O domain operation.**
   Progress 2026-05-25 task `201`: live IOTLB binding and invalidation paths now use `IoDomainTag` / `IoDomain`; frozen `InvalidateVmxIotlbByVmid(...)` is only an explicit compatibility alias delegating to neutral invalidation.

7. **VMX invalidation instructions должны быть aliases.**

8. **VPID/NPT/EPT vocabulary не должен быть substrate vocabulary.**
   Progress 2026-05-25 task `200`: `AddressSpaceId`, `NestedTlbTag`, nested TLB invalidation, and nested composition no longer expose VMID/VPID/NPT/EPT identity names. Remaining compatibility records, IOTLB/DMA/Lane names, and nested projection surfaces are not frozen and remain audit scope.
   Progress 2026-05-25 task `201`: active IOTLB/DMA/Lane identity and nested translation outcomes now use neutral `IoDomainTag`, `ExecutionDomainTag`, `AddressSpaceTag`, and `SecondStage*` vocabulary. `MemoryTranslationControl` and VMX invalidation aliases remain compatibility vocabulary only; event/trap projection identity remains open.
   Progress 2026-05-25 task `203`: executable event/trap descriptors, interrupt remap, event queues/fabric, and virtual timers now use `ExecutionDomainTag` / `AddressSpaceTag`; the compatibility event-delivery helper no longer exposes a `vmid` routing parameter.

9. **Address-space tags должны быть generic.**
   Progress 2026-05-25 task `200`: canonical address-space identity is expressed by `DomainTag`, `AddressSpaceTag`, `SecondStageRootIdentity`, `SecondStageEpoch`, `AddressSpaceTagEpoch`, and `AddressSpaceGeneration`, with executable conformance guarding against returned VMID/VPID/NPT/EPT keys.

10. **Epoch wraparound policy должна быть substrate security policy.**

11. **Dirty logging должно быть memory-domain service.**
    Progress 2026-05-25 task `193`: `DirtyLogBlock` no longer owns configure, dirty range mark, snapshot, clear, restore, page-index storage, accepted-write accounting, overflow, or generation-advance helpers.

12. **VMX dirty log view должен быть projection.**
    Progress 2026-05-25 task `193`: the remaining VMX dirty-log compatibility alias is fail-closed and the VMCSv2 dirty-log block is read-only status projection vocabulary.

---

## 8. Требования к Lane6/Lane7

1. **Lane6/Lane7 не должны быть VMX subdevices.**

2. **Lane6/Lane7 должны быть substrate-owned execution resources.**

3. **Lane6 должен использовать descriptor/token/fence/queue model.**
   Progress 2026-05-25 task `201`: live Lane6 queue/token and DMA validation identity uses `IoDomainTag` rather than VMID. Its remaining host-token evidence table and restore/rebuild proof are still required before freeze.
   Progress 2026-05-25 task `202`: live Lane6 queue/fence ownership is now `Lane6QueueRuntime`; native token bindings are held only by neutral `Lane6HostOwnedEvidenceStore`, restore clears them for explicit rebuild, and epoch exhaustion fails closed.

4. **Lane7 должен использовать capability/handle/token/completion model.**
   Progress 2026-05-25: the quarantined VMX frontend no longer owns Lane7 VMFUNC leaf resolution; any future Lane7 operation must enter through generic capability/handle/token/completion admission rather than VMCS helper state.
   Progress 2026-05-25 task `201`: Lane7 state/checkpoint and lane completion routing now key live state by `ExecutionDomainTag` / `AddressSpaceTag`, not VMID/VPID.
   Progress 2026-05-25 task `205`: host-owned Lane7 native-token lookup, backend binding state, and scheduler pressure cache are behind `Lane7HostOwnedEvidenceStore`; `Lane7StateBlock` keeps virtual handle/token ABI and delegates evidence.

5. **VMX может видеть virtual handles only.**

6. **VMX не может видеть native handles.**

7. **VMX не может видеть backend binding evidence.**

8. **VMX не может видеть scheduler pressure evidence unless explicitly projected as safe counter.**

9. **Lane completions должны маршрутизироваться через generic completion fabric.**

10. **VMX posted completion — projection, не primary route.**

11. **Lane6/Lane7 migration должна serialise virtual state only.**
    Progress 2026-05-25 task `202`: Lane6 native token handles are not a serializable image surface; conformance proves restore invalidates host-local mappings and requires fresh binding while `DomainCheckpointImage` rejects native-token evidence.
    Progress 2026-05-25 task `205`: Lane7 checkpoint restore clears host-owned token/backend/scheduler evidence and cannot project native accelerator handles from virtual-token or virtual-handle numeric values.

12. **Backend/native state должен rebuild/rebind after restore.**
    Progress 2026-05-25 task `202`: proven for the active Lane6 host-token mapping boundary; broader Lane7/backend/scheduler evidence remains distinct audit scope.
    Progress 2026-05-25 task `205`: proven for active Lane7 native token mappings, backend binding cache, and scheduler pressure cache through neutral rebuild-only APIs.

---

## 9. Требования к безопасности

1. **Fail-closed by default.**

2. **No host evidence exposure.**

3. **No native token exposure.**

4. **No backend handle exposure.**

5. **No physical placement exposure.**

6. **No raw scheduler evidence exposure.**

7. **No VMCS projection as security boundary.**

8. **Security boundary — domain descriptor + capability grants + evidence policy.**

9. **VMID/domain id reuse должен harden-иться substrate policy.**

10. **TLB/IOTLB epoch overflow должен иметь explicit fail-closed policy.**

11. **Debug/trace visibility должна быть capability-gated.**

12. **Migration image integrity должна быть substrate-level policy.**

13. **Side-channel policy должна быть domain-level, не VMX-level.**

14. **Quota policy должна быть domain/lane-level, не VMX-level.**

---

## 10. Требования к тестированию

1. **VMX ABI compatibility tests.**

2. **Generated projection tests.**

3. **VMCS field alias tests.**

4. **CSR alias tests.**

5. **VmxCaps projection tests.**

6. **CapabilityDescriptorSet authority tests.**

7. **VMREAD/VMWRITE access policy tests.**

8. **Host evidence exclusion tests.**

9. **DomainEnter/DomainTrap/DomainFault mapping tests.**

10. **VMExit projection tests.**

11. **VMFail/VMAbort projection tests.**

12. **MemoryDomain invalidation tests.**

13. **IOMMU/IOTLB domain tests.**

14. **Lane6 token/fence/queue tests.**

15. **Lane7 handle/token/completion tests.**

16. **Completion routing tests.**

17. **NestedDomain composition tests.**

18. **Migration/checkpoint tests.**

19. **Evidence discard/rebuild tests.**

20. **Strict negative security tests.**

21. **Documentation lint for non-VMX substrate naming.**

22. **Build failure on unmapped VMX alias.**

23. **Build failure on projection-owned authoritative state.**

---

## 11. Основные риски

1. **VMCSv2 снова станет substrate object.**

2. **VMX frontend начнёт владеть state.**

3. **`VmxCaps` превратится в independent capability source.**

4. **Legacy naming просочится в public architecture.**

5. **VMCS12/VMCS02 останутся normative nested model.**

6. **MMU/TLB снова будут описываться через NPT/VPID vocabulary.**

7. **IOMMU/IOTLB станут VMX-specific.**

8. **Lane6/Lane7 будут подчинены VMCS state blocks.**

9. **VMFUNC станет bypass around descriptor validation.**

10. **VMREAD начнёт открывать runtime evidence.**

11. **Migration начнёт serialise projection state instead of domain state.**

12. **Generated projection будет неполным.**

13. **CapabilityDescriptorSet превратится в bitmap без typed grants.**

14. **Domain descriptors станут слишком абстрактными и неисполняемыми.**

15. **Compatibility mode начнёт диктовать substrate design.**

16. **Conformance будет проверять legacy ABI, но не substrate invariants.**

17. **Nested mode будет реализован как VMX nesting, а не domain composition.**

18. **Debug/trace counters начнут leaking host evidence.**

19. **Security policy останется пустым placeholder.**

20. **Retire-owned publication будет обойдена через frontend fast paths.**

---

## 12. Признаки неправильной реализации

1. Новый substrate class называется `Vmx*`.

2. Новый substrate class называется `Vmcs*`.

3. `VmcsV2Descriptor` содержит authoritative state.

4. `VmxCaps` хранит state вместо projection.

5. VMX frontend вызывает Lane6/Lane7 backend напрямую.

6. VMX frontend управляет IOMMU domain напрямую.

7. VMX frontend управляет TLB epoch напрямую.

8. Migration writer читает VMCS projection как primary source.

9. Nested docs описывают VMCS12/VMCS02 как основную модель.

10. Capability checks выполняются через VMX CSR bits.

11. VMREAD может достать field без substrate access policy.

12. VMFUNC может выполнить operation без descriptor validation.

13. VMX tests проходят, но domain substrate tests отсутствуют.

14. Projection mapping пишется вручную.

15. Host evidence имеет VMCS field id.

---

## 13. Признаки правильной реализации

1. Substrate namespaces не содержат VMX/VMCS terminology.

2. VMX code находится только в compatibility/frontend layer.

3. VMX frontend зависит от substrate.

4. Substrate не зависит от VMX frontend.

5. `VmcsV2Descriptor` generated.

6. `VmxCaps` generated CSR alias.

7. VMCS fields generated from alias map.

8. VMX exits generated from generic outcome mapping.

9. VMX failures generated from validation failure mapping.

10. Nested public docs используют `NestedDomainDescriptor`.

11. VMCS12/VMCS02 только в compatibility glossary.

12. Lane6/Lane7 state живёт вне VMX.

13. Memory/I/O domains живут вне VMX.

14. Capabilities живут вне VMX.

15. Migration serialises domain checkpoint.

16. Evidence discarded/rebuilt after restore.

17. Every VMX-visible state has substrate owner.

18. Every substrate state has explicit evidence/migration policy.

19. Every frontend projection is conformance-tested.

20. Legacy ABI stable, but not architecturally contagious.

---

## 14. Финальная формула

```text id="gopmob"
VMX in HybridCPU is not virtualization architecture.

VMX is a compatibility frontend.

The virtualization architecture is:
  ExecutionDomainDescriptor
  MemoryDomainDescriptor
  IoDomainDescriptor
  LaneDescriptor
  CapabilityDescriptorSet
  TokenNamespace
  FenceDomain
  CompletionRouteDescriptor
  EvidencePolicy
  MigrationDescriptor
  NestedDomainDescriptor

VMX observes and controls this substrate only through generated,
policy-checked, capability-gated projections.
```

## 15. Нормативное решение

1. **`VmcsV2Descriptor` → generated compatibility projection.**

2. **`VmxCaps` → pure CSR alias over `CapabilityDescriptorSet`.**

3. **`VMCS12/VMCS02` → removed from normative public architecture.**

4. **`NestedDomainDescriptor` / `NestedProjectionService` → primary nested model.**

5. **VMX ABI → preserved.**

6. **VMX architectural ownership → removed.**

7. **Generic domain/descriptor/capability runtime → source of truth.**

## Progress addendum 2026-05-25 task `204`

- Vector-stream compatibility fault DTO identity is neutralized: `VectorStreamExceptionInfo` and `VectorStreamDescriptorFaultInfo` carry `ExecutionDomainTag` / `AddressSpaceTag`; VMCS projection may encode compatibility qualifications but no longer receives `Vmx*Info` DTOs or `vpid` replay parameters.
- Lane7 accelerator helper surfaces are no longer named `VmxAccelerator*` / `VmxLane7*`; handle, token, completion, and admission helpers are neutral runtime namespaces under `Core/Runtime/Lanes/Lane7/Accelerators` and do not use VMID/VPID identity.
- `ChildDomainIntentDescriptor` no longer exposes `TryVmRead`, `TryVmWrite`, `Vmcs12Pointer`, or `VmcsField.Vpid` as substrate API/identity; child intent uses neutral ids such as `AddressSpaceTag`, `SecondStageRootPointer`, and `SecondStageViolationQualification`.
- This closes identity/wrapper pressure only. Lane7 backend host-handle rebuild semantics, compatibility `MemoryTranslationControl`, frozen VMCS field aliases, explicit VMX IOTLB aliases, VMFUNC leaf vocabulary, and physical substrate placement remain separate freeze blockers.

## Progress addendum 2026-05-25 task `205`

- Lane7 host-owned evidence is extracted from the VMX substrate state block: `_hostTokenByVirtualValue`, `_virtualTokenByHostHandle`, `_backendBindings`, and `_submitPollCount` are no longer owned by `Lane7StateBlock`.
- `Lane7HostOwnedEvidenceStore` under `Core/Runtime/Lanes/Lane7/HostOwnedEvidence` owns native token bindings, backend binding cache/epoch, scheduler pressure cache/epoch, and restore/rebuild proof. Epoch advance refuses wraparound.
- `Lane7Checkpoint` and `Lane7VirtualToken` do not expose native accelerator handles, including numeric coincidence with virtual handles/tokens. Restore clears host-owned evidence and requires explicit rebuild.
- `MemoryTranslationControl`, VMCS field aliases, VMX IOTLB/INVVPID aliases, and VMFUNC leaf vocabulary remain frozen compatibility names only; this task does not claim VMX freeze or complete physical substrate relocation.

## Progress addendum 2026-05-25 task `206`

- `DomainRuntimeContext`, `DomainRuntimeOperation`, `DomainRuntimeAuthority`, `RootAuthorityDescriptor`, `DomainLegalityService`, `DomainValidationResult`, `DomainSchedulingAdmission`, and `DomainBindingTable` are extracted from `Core/VMX/Substrate/Runtime` into `Core/Runtime/Domains/*`.
- The neutral domain-runtime files must not contain VMX/VMCS/MemoryTranslationControl/INVVPID/VMFUNC vocabulary or use frozen compatibility aliases as source-of-truth state.
- `MemoryTranslationControl`, VMCS field projection schema, VMX IOTLB/INVVPID aliases, and VMFUNC names stay compiled only as frozen compatibility/projection vocabulary.
- This is a staged substrate extraction, not VMX freeze. Remaining descriptor, capability, memory, I/O, nested, and projection surfaces still need the same treatment.

## Progress addendum 2026-05-25 task `207`

- Execution, memory, I/O, Lane6, Lane7, completion-route, event-queue, evidence-policy, observability, and trap-policy descriptors moved from `Core/VMX/Substrate/Descriptors` to `Core/Runtime/Domains/Descriptors`.
- Execution/memory/I/O/Lane6/Lane7/nested/vector-stream domain admission runtimes moved from `Core/VMX/Substrate/Domains` to `Core/Runtime/Domains/Admission`.
- `NestedDomainDescriptor`, `INestedProjectionService`, `NestedProjectionService`, and `NestedValidationResult` moved to `Core/Runtime/Nested`.
- `CapabilityDescriptorSet` and generated `CapabilityDescriptorSetSchema.VmxCompatibility` remain a separate capability projection placement risk; frozen VMX aliases remain compatibility/projection vocabulary only.
- This is still not VMX freeze. Memory translation/checkpoint services, I/O/DMA services, completion/event services, lane state blocks, capability generated projection, and explicit VMX compatibility aliases remain staged extraction/quarantine work.

## Progress addendum 2026-05-25 task `208`

- Rule application: generated capability-bit projection belongs under `Core/VMX/Compatibility/Generated`, not under generic substrate. `CapabilityDescriptorSetSchema` now lives at `Core/VMX/Compatibility/Generated/CapabilityProjection/CapabilityDescriptorSetSchema.cs` and remains build-time regenerated compatibility projection vocabulary.
- Rule application: neutral memory/I/O/migration/completion/event/Lane6 services must live outside VMX. The selected address-space, nested-translation, DMA/IOTLB, checkpoint/migration/restore, completion routing, event injection/fabric/queue, and Lane6 queue/token/fence surfaces now live under `Core/Runtime/*`.
- Rule application: VMX-shaped names that remain under `Core/VMX/Substrate` are not owners. They are either frozen compatibility projections, denied/quarantined bridges, or residual extraction targets; no new `VmcsManager`, VMCS field store, projection runtime manager, or renamed VMX runtime owner was introduced.
- Next rule pressure: extract or quarantine the remaining capability grant/descriptor services, vector-stream state/save-restore surfaces, Lane7 VMFUNC/VM-exit compatibility state, nested mappers/composition, evidence/debug trace policy, and retire evidence before any VMX freeze claim.

## Progress addendum 2026-05-25 task `209`

- Rule application: typed capability grants/descriptors are runtime substrate. `CapabilityGrantCollection`, `CapabilityDescriptorSet`, `CapabilityNegotiationService`, and `CapabilityPublicationPolicy` now live under `Core/Runtime/Capabilities/*`.
- Rule application: VMX capability masks are frozen compatibility ingress only. `CapabilityCompatibilityProjection` owns the ABI constructor and `FromCompatibilityMasks(...)` projection materialization; it does not become a runtime owner.
- Rule application: neutral evidence, vector-stream state/save-restore, Lane7 token/handle/completion helpers, event trap DTO/timer state, and nested capability/evidence/composition services now live under `Core/Runtime/*`.
- Rule application: remaining `Core/VMX/Substrate` files must be read as quarantine/frozen vocabulary unless a later task removes them. The project file no longer advertises `Core\VMX\Substrate\` as a generic folder owner.
- No freeze claim: retire evidence, VMX completion/trap projection, nested compatibility mappers, `MemoryTranslationControl`, VMX invalidation aliases, and Lane7 VMFUNC/VM-exit state still require semantic authority review.

## Progress addendum 2026-05-25 task `210`

- Rule application: completion routing belongs to neutral runtime descriptors. `CompletionRecord` is neutral runtime state; VMX exit projection helpers are quarantined in the compatibility frontend and do not own routing/publication.
- Rule application: event/trap authority belongs to neutral runtime descriptors/records. `DomainTrapRecord` no longer stores `TrapDecision`; VMX exit reason/qualification encoding remains compatibility projection vocabulary.
- Rule application: nested mapping helpers under VMX are compatibility projection only. VMCSv2 descriptor/block helpers for nested/trap/qualified exit publication were removed without replacement, not renamed into a new manager.
- Rule application: retire evidence publication is gated by neutral evidence and observability descriptors. VMX retire vocabulary may observe/project, but cannot publish host-owned evidence or become completion authority.
- No freeze claim: `MemoryTranslationControl`, VMX invalidation aliases, Lane7 VMFUNC/VM-exit compatibility state, vector-stream VMCS host-evidence helper vocabulary, and final frozen alias classification still remain before VMX freeze.

## Progress addendum 2026-05-25 task `211`

- Rule application: live translation identity, epoch advancement, and invalidation belong to neutral memory runtime descriptors/services. `MemoryTranslationControl` is now frozen read-only VMX vocabulary only, without an executable route back into authority.
- Rule application: VMX IOTLB/invalidation method names may remain only as denied/no-effect compatibility aliases. They do not call neutral host mutation and cannot become a second I/O or memory authority plane.
- Rule application: Lane7 runtime and checkpoint state must not own VMFUNC or VM-exit state. VMCS host-evidence vocabulary for Lane7 and vector-stream images is isolated as false-only compatibility projection, never serialized host evidence.
- Rule application: VMCS field aliases are read-only schema/projection vocabulary. A compatibility write is denied independently of caller flags, and generated-lineage verification enforces that policy.
- Rule application: `Core/VMX/Substrate` has no remaining C# source owner after final alias quarantine; `Legacy/VMX` remains empty. No `VmcsManager`, field store, VMX runtime manager, or renamed replacement was introduced.
- No freeze claim: close the final freeze-readiness inventory of compiled compatibility/generated/debug/lifecycle surfaces and known repository-shape checks before declaring VMX frozen.

## Progress addendum 2026-05-25 task `212`

- Rule application: physical placement is also part of quarantine. Any C# source under `Core/VMX` that carries explicit `legacy` vocabulary must live under `Legacy/VMX` with the same relative structure, even if its behavior is already fail-closed or read-only.
- Rule application: `Legacy/VMX` is now an intentional compiled quarantine, not a forbidden empty directory. Presence there does not confer authority; every retained production surface must remain denied, read-only, frozen ABI/projection, or be removed in a later step.
- Rule application: neutral runtime/domain owners remain outside `Legacy/VMX`; moving a compatibility source into quarantine must not move generic state, capability grants, host evidence, completion authority, memory/I/O invalidation authority, or retire publication authority with it.
- Rule application: removed heavy carriers remain absent. Physical quarantine must never be used to restore `VmxExecutionUnit`, `VmcsManager`, VMCS field stores, active pointer state, or renamed runtime ownership.
- No freeze claim: inspect and reduce the now-centralized compiled quarantine before performing final freeze-readiness certification.

## Progress addendum 2026-05-25 task `213`

- Rule application: a compiled denied compatibility adapter is not retained merely because a test can instantiate it. `LegacyVmxIoVirtualizationBackend` has no production caller and is removed without replacement.
- Rule application: removal of a VMX-shaped I/O shell does not create or move I/O authority. Neutral descriptor admission, IOTLB invalidation, and host backend behavior remain outside `Legacy/VMX`; the removal proof rejects direct compatibility mutations of grants, evidence, lane state, memory/I/O state, completion routing, and checkpoint restore.
- Rule application: retained frozen vocabulary must have a real compatibility path. Current production reachability supports the decode payload, typed retire carrier, and fail-closed Shadow VMCS nested projection bridge; the remaining translation/CSR/v1/v2 shells require further deletion-or-necessity proof.
- The compiled production quarantine has seven files after this removal. The evidence quarantine still has 38 `.cs` files because a focused conformance contract records the deleted shell and its neutral owners. This is a reduction step, not a VMX freeze.

## Progress addendum 2026-05-25 tasks `214`-`217`

- Rule application: no-production-caller shells are not frozen ABI merely because conformance previously instantiated them. The translation invalidation backend, CSR-backed descriptor stub, and v1/v2 adapter boundaries are removed without replacement.
- Rule application: removal cannot relocate authority into quarantine. Memory invalidation remains neutral runtime/host behavior; capability authority remains typed grants; current opcode behavior remains typed fail-closed; nested Shadow VMCS vocabulary remains a denied/read-only projection boundary.
- Rule application: only a demonstrated production compatibility caller permits retained production vocabulary in `Legacy/VMX`. The retained set is now `VmxInstructionPayload`, `VmxRetireModel`, and `ShadowVmcsNestedProjectionService`.
- Rule application: manifest removal status and focused absence contracts are compiled evidence, not runtime ownership. They may keep total evidence-quarantine count at `38` while executable production compatibility surface drops to `3`.
- `VmxExecutionUnit`, `VmcsManager`, field stores, active pointers, renamed VMX runtime managers, and VMX-owned grant/evidence/memory/I/O/lane/completion/checkpoint/retire authority remain prohibited and absent.
- No freeze claim: conduct final necessity and mutation audit of the three retained production compatibility sources plus remaining generated/debug/lifecycle surfaces before declaring VMX frozen.

## Progress addendum 2026-05-25 task `218`

- Rule application: retained compatibility production sources require a real caller and a no-authority mutation proof. `VmxInstructionPayload`, `VmxRetireModel`, and `ShadowVmcsNestedProjectionService` are retained because they are caller-backed; they are not retained as VMX ownership.
- Rule application: carrier vocabulary is allowed to name VMX/VMCS concepts, but it must not mutate grants, evidence, memory/I/O generations, lane state, completion queues, checkpoint state, nested projection/checkpoint services, VMCS managers, or hardware/CSR state.
- Rule application: current production retire behavior remains typed fail-closed. Success-style retire factories may exist as frozen vocabulary, but production `Core` must not call them until a separately admitted generic runtime path exists.
- Rule application: generated/debug/lifecycle surfaces are conformance evidence. ABI freeze, generated-lineage, golden artifact, no-emission/no-mutation, migration replay, fail-trace, VMCS pointer lifecycle, and debug-trace extraction contracts cannot become runtime managers.
- `Legacy/VMX/Compatibility` remains exactly three production files. `Legacy/VMX` total source count becomes `39` because the retained-surface inventory contract is compiled evidence. No VMX freeze claim is made.

## Progress addendum 2026-05-25 task `219`

- Rule application: physical freeze readiness must be tested by removing the quarantine from the compiler, not only by static inventory. The temporary move-away probe moved `Legacy/VMX` to `Desktop/New folder`, built production, and restored the directory.
- Rule result: whole-quarantine deletion is rejected today. The build fails on live production ABI carrier symbols (`VmxInstructionPayload`, `VmxRetireEffect`, `VmxRetireOutcome`, `VmxOperationKind`), so deleting all occurrences would be a semantic VMX frontend rewrite, not a cleanup.
- Rule application: conformance evidence must not be a production dependency. `ShadowVmcsNestedProjectionService` no longer calls `ShadowVmcsBridgeRetirementContract`; it carries its own fail-closed projection behavior while the contract remains evidence-only.
- Rule application: freeze cannot be declared while production still depends on physical `Legacy/VMX`. The retained ABI vocabulary is allowed only as a temporary compatibility carrier with no authority mutation.
- Rule application: broad-filter failures must be classified before freeze. The current `FullyQualifiedName~Vmx` broad filter has one unrelated stale path failure for `Core/Diagnostics/InstructionRegistry.Helpers.Core.cs`; this is not a compatibility authority regression.
- Current rule state: `Legacy/VMX/Compatibility` has exactly three production carriers, total `Legacy/VMX` has `40` `.cs` files after the certification contract, `Core/VMX` has no legacy-marked `.cs`, and removed heavy carriers remain absent.
- Next rule step before freeze: remove the production need for the physical `Legacy/VMX` directory by generated neutral compatibility vocabulary or no-legacy rehoming, then repeat the move-away build probe and broad conformance matrix.

## Progress addendum 2026-05-25 task `220`

- Rule application: production compatibility carriers may live in `Core/VMX/Compatibility` only if they do not carry legacy vocabulary and do not own runtime authority. The retained decode, retire, and Shadow VMCS projection carriers were rehomed under that rule.
- Rule application: physical `Legacy/VMX` must not be required for production build. The repeated move-away probe now succeeds, proving production no longer depends on the physical quarantine directory.
- Rule application: compatibility defaulting must not force legacy vocabulary into Core sources. `VmxRootDescriptorReference.CompatibilityDefault` is now used by the rehomed carriers, while the older spelling remains outside `Core/VMX` for compatibility.
- Rule application: `Legacy/VMX/Compatibility` now contains no `.cs` files. The remaining `Legacy/VMX` files are conformance/evidence only; they cannot be treated as runtime managers or ABI carriers.
- Rule application: the broad VMX test filter was rerun after carrier exit; the only failure remains the known stale repository-shape path in `Phase09DirectFactoryCallerBoundaryTests`, not compatibility authority evidence.
- Rule application: no freeze declaration is allowed merely because the physical move-away probe passes. Broad-filter debt and the final compatibility/generated/conformance matrix must still be explicitly closed before freeze.

## Progress addendum 2026-05-25 task `221`

- Rule application: stale repository-shape tests must be corrected to the actual source owner rather than papered over by recreating old compatibility paths. The VMX broad-filter path repair points diagnostics and decoder contracts to `NonRTL` locations.
- Rule result: the VMX broad matrix now passes, the physical move-away probe passes, and the freeze-readiness contract declares `CanDeclareFreeze == true`.
- Normative decision: VMX compatibility frontend freeze is declared for the current compiled VMX surface. This is a freeze of ABI/projection/conformance vocabulary, not a promotion of VMX into the virtualization architecture.
- Rule constraint after freeze: all new live authority still belongs to neutral runtime/domain owners first; VMX can only observe or expose it through generated, read-only, denied, or fail-closed compatibility projection.
- Rule separation: Phase12 VLIW/ISA compatibility-freeze debt remains outside this VMX decision and must not be cited as VMX authority evidence.

## Progress addendum 2026-05-25 task `222`

- Rule application: physical deletion must be proven separately for production and tests. `Legacy/VMX/Conformance` is production-independent, but it is still compiled evidence for the VMX test suite.
- Rule result: moving `Legacy/VMX/Conformance` away lets production build pass and tests build fail with direct missing evidence symbols. This proves the folder is not runtime authority, but also proves deletion is not safe without test-evidence decoupling.
- Rule constraint: do not delete the conformance folder merely because production no longer needs it. First replace or retire direct test dependencies on the evidence contracts and manifest; then rerun the same move-away probe.
- Freeze remains valid for the current VMX compatibility frontend. Conformance-folder deletion is post-freeze cleanup, not a prerequisite for the already declared frontend freeze.

## Progress addendum 2026-05-28 task `236`

- Rule application: trap/intercept authority belongs to neutral runtime trap results, not VMX exit vocabulary. `TrapRequest` and `NeutralTrapResult` now live under `Core/Runtime/Events/Traps`.
- Rule application: `TrapPolicyBitmap.Evaluate` and `SchedulingBudgetTimer.TryConsumeExpired` return neutral trap results. Runtime trap files do not depend on `VmExitReason`, `VmxExitQualification`, or `TrapDecision`.
- Rule application: VMX-compatible exit projection is explicit and late. `VmxTrapProjectionMapper` maps neutral trap result kinds to `VmExitReason` and `TrapDecision` only at the compatibility frontend boundary.
- Rule constraint: VMCALL/trap/intercept remains fail-closed unless a later admitted path passes through `RuntimeBoundaryAdmissionService`, neutral runtime ownership, and then VMX projection. This task did not create a VMCALL backend or successful VMX intercept path.
- Removed leakage: VMX exit reason selection was removed from runtime trap policy/timer code. VMX operation convenience aliases remain compatibility projection vocabulary only.

## Progress addendum 2026-05-28 task `237`

- Rule application: `VMCALL` trap admission is projection-only. `VmxCompatibilityAdmissionService.AdmitVmCallTrapProjection` validates decode, frozen alias projection, runtime admission, neutral trap policy, and then projects through `VmxTrapProjectionMapper`.
- Rule application: runtime admission uses neutral `DomainRuntimeOperationKind.ProjectCompatibilityTrap`, not `VmExitReason` or VMCS state.
- Rule application: the generated compat alias schema/map now contains frozen `Opcode/VMCALL -> VmxTrapProjectionMapper.Project`; the build-time lineage verifier accepts the regenerated artifact.
- Rule constraint: the admitted result is `TrapProjectionDeniedBackend`. It does not call `VmxRetireEffect.InterceptExit`, `VmxRetireEffect.VmCall`, or any VMCS manager/backend/field-store surface.
- Rule constraint: production VMX dispatch and retire remain fail-closed. The new path is an explicit compatibility admission/projection API, not a pipeline backend.
