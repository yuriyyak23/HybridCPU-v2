## Остаточные риски / что не закрыто после 28 closed

1. **`closed` по 28 пунктам не равен architectural closure.**
   В приложенном списке почти все прежние риски помечены `Статус: closed`, но часть закрытий выглядит как локализация/оборачивание риска, а не полное удаление риска из модели. Особенно это касается `ShadowVmcs`, bitmap-capability, VMX-shaped translation и generated scaffolding. 
   Прогресс 2026-05-24: legacy V1/V2 adapter boundary files возвращены из `Legacy/VMX` в `Core/VMX/Compatibility/Adapters` только как frozen ABI / compatibility frontend contracts with reverse-import proof. Heavy execution/VMCS/IOMMU legacy files остаются в quarantine.
   Прогресс 2026-05-24: legacy CSR-backed `VmxCaps` descriptor source возвращен в `Core/VMX/Compatibility/Generated/CsrProjection` только как fail-closed compatibility stub: constructor ABI сохранен, но CSR read/`VmxCaps` authority удалены, результат всегда `CapabilityDescriptorSet.Empty`.
   Progress 2026-05-24: legacy translation invalidation backend returned to `Core/VMX/Compatibility/Adapters/MemoryInvalidation` only as a compatibility adapter over generic `TranslationInvalidationHostBackend`; direct `IOMMU.`, `ApplyVmxInvalidation`, `VmxInvalidationScope`, and `InvalidateVmxIotlb` markers are absent from the returned Core file.
   Progress 2026-05-24: legacy I/O virtualization backend returned to `Core/VMX/Compatibility/Adapters/IO` only as a compatibility adapter over generic `IoVirtualizationHostBackend`; direct `IOMMU.`, `BindVmx`, `UnbindVmx`, `InvalidateVmx`, `ApplyVmx`, and `TryTranslateVmxDma` markers are absent from the returned Core file.
   Progress 2026-05-24: legacy `IOMMU.DomainBinding.partial.cs` removed from `Legacy/VMX` and returned as generic host-side `Memory/MMU/IOMMU.DomainBinding.cs`; VMX-shaped binding/invalidation method names are confined to `Memory/MMU/IOMMU.VmxCompatibilityAliases.cs` outside `Core/VMX`, and the generic host implementation has no `Vmx`/`VMX` authority markers.
   Progress 2026-05-24: `LegacyVmcsMemoryTranslationControlProjection.cs` removed without replacement; the later `VmxExecutionUnit` admission cleanup also removed the remaining VMCS-field-derived intercept/event domain snapshot, while reusable construction uses generic `MemoryTranslationControl.CreateRuntimeProjection(...)` and conformance checks that no `IVmcsManager` / `VmcsField` dependency was introduced into the generic control factory.
   Progress 2026-05-24: `ShadowVmcsBlock.cs` removed without replacement; nested enable, nested VMREAD/VMWRITE, and checkpoint restore now fail closed until a generic nested-domain projection/checkpoint service exists, and `.ShadowVmcs.` has no allowed Core authority path.
   Progress 2026-05-24: live compile probe moved the four remaining `Legacy/VMX` files outside the repo; compiler errors showed `VmxExecutionUnit` and `VmcsManager` are hard anchors, while the two legacy V1 execution adapter partials can be removed from quarantine and replaced by narrow current Core execution/retire routing surfaces with conformance.
   Progress 2026-05-24: first `VmxExecutionUnit` removal-without-replacement slice completed. The quarantined handler no longer owns direct INVEPT/INVVPID host invalidation, no longer stores `EptInvalidationEpoch` / `VpidInvalidationEpoch`, and those opcodes now fail closed with `SecurityPolicyViolation` until generic runtime invalidation admission exists.
   Progress 2026-05-24: second `VmxExecutionUnit` removal-without-replacement slice completed. The unused public `CompleteNestedTranslationFault(...)` API was removed, so nested translation fault VM-exit completion/publication is no longer owned by the legacy VMX frontend.
   Progress 2026-05-24: third `VmxExecutionUnit` removal-without-replacement slice completed. `ResolveActiveMemoryTranslationControl()` was removed, and intercept/event admission no longer derives domain tags from `SecondaryProcControls`, `GuestCr3`, `EptPointer`, or `Vpid` VMCS fields.
   Progress 2026-05-25: fourth `VmxExecutionUnit` removal-without-replacement slice completed. `ApplyInterceptExit(...)` no longer performs standalone intercept publication through `RecordInterceptExit` or `VmxEventKind.InterceptExit`; it stays on the common VM-exit completion retire path.
   Progress 2026-05-25: fifth `VmxExecutionUnit` removal-without-replacement slice completed. The legacy frontend no longer resolves Lane7 VMFUNC leaves through VMCS helper state and no longer validates/saves/restores vector-stream extended state through VMCS helper calls; unsupported VMFUNC leaves and `VMSAVEX`/`VMRESTX` fail closed until generic lane/vector runtime admission exists.
   Progress 2026-05-25: sixth `VmxExecutionUnit` removal-without-replacement slice completed. The legacy frontend no longer calls VMCS-backed pending virtual-event delivery or publishes `VmxEventKind.EventDelivered`; the public safe-boundary ABI is now a fail-closed no-op until generic event-delivery runtime admission exists.
   Progress 2026-05-25: seventh `VmxExecutionUnit` removal-without-replacement slice completed. The legacy frontend no longer routes `VMREAD`/`VMWRITE` through direct or nested VMCS field helper calls; those opcodes fail closed until generated projection/access-policy admission owns VMCS field access.
   Progress 2026-05-25: eighth `VmxExecutionUnit` removal-without-replacement slice completed. The common qualified VM-exit completion helper was removed; intercept/VMCALL/VMFUNC exit paths no longer publish VM-exit state through legacy frontend VMCS/CSR/trace shortcuts.
   Progress 2026-05-25: ninth `VmxExecutionUnit` removal-without-replacement slice completed. The legacy frontend no longer owns `VMCLEAR`/`VMPTRLD`/`VMPTRST` VMCS pointer lifecycle; those opcodes fail closed until generic execution-domain binding/projection-handle admission exists.
   Progress 2026-05-25: tenth `VmxExecutionUnit` removal-without-replacement slice completed. The legacy frontend no longer owns `VMLAUNCH`/`VMRESUME` VM-entry authority; those opcodes fail closed until generic `DomainEnter` admission exists.
   Progress 2026-05-25: eleventh `VmxExecutionUnit` removal-without-replacement slice completed. The legacy frontend no longer owns `VMXON`/`VMXOFF` root-switch authority; those opcodes fail closed until generic runtime-domain admission owns root compatibility activation.
   Progress 2026-05-25: twelfth `VmxExecutionUnit` removal-without-replacement slice completed. The legacy frontend no longer owns common VMFail / trace / VMX CSR publication; fault retire returns typed outcomes without frontend `HardwareWrite`, `RecordVmxFailForObservability`, or `RecordVmxEvent`.
   Progress 2026-05-25: thirteenth `VmxExecutionUnit` removal-without-replacement slice completed. The legacy frontend no longer owns VMCALL/VMFUNC capability-query gates or capability readback; those opcodes fail closed until generic runtime capability admission owns the compatibility operation.
   Progress 2026-05-25: fourteenth `VmxExecutionUnit` removal-without-replacement slice completed. The legacy frontend no longer constructs guest-intercept requests and no longer uses VMCS-backed guard checks for the guest-intercept route; the safe-boundary hook now fails closed.
   Progress 2026-05-25: fifteenth and final `VmxExecutionUnit` removal-without-replacement slice completed. `Legacy/VMX/Compatibility/Frontend/Handlers/VmxExecutionUnit.cs` is deleted, no constructor/opcode shell was returned to Core, and current routing emits typed fail-closed `SecurityPolicyViolation` effects for frozen VMX opcodes. `VmcsManager.cs` is now the only remaining file under `Legacy/VMX`.
   Progress 2026-05-25: first `VmcsManager` removal-without-replacement slice completed. The quarantined manager and `IVmcsManager` no longer expose nested/intercept/qualified-exit publication, standalone virtual-event queue/delivery, or public VMX debug/fail/abort/invalidation observability entry points. The file remains quarantined because active Core lane completion, vector dirty, memory dirty, Lane7, and DMA paths still depend on its VMCS-shaped carrier.
   Progress 2026-05-25: final `VmcsManager` heavy removal completed. `Legacy/VMX/Substrate/Runtime/Binding/VmcsManager.cs` and `IVmcsManager` are deleted without replacement; production Core no longer constructs or calls a VMCS state carrier. Frozen compatibility guest Lane6/Lane7 execution now fails closed before token/backend/result effects, while native `DmaStreamComputeRuntime` and `ExternalAcceleratorRuntime` paths remain independent. `Legacy/VMX` is empty.

2. **`Core/VMX/Substrate` всё ещё физически держит generic substrate внутри VMX-дерева.**
   Даже если код стал чище, расположение поддерживает неверную архитектурную модель: substrate выглядит частью VMX, а не VMX — frontend поверх substrate. Это всё ещё надо выносить в нейтральные зоны вроде `Core/Runtime/Domains`, `Core/Runtime/Capabilities`, `Core/Runtime/Memory`, `Core/Runtime/IO`, `Core/Compatibility/VMX`.
   Прогресс 2026-05-24: создан начальный нейтральный `Core/Runtime` boundary набор (`Domains`, `Capabilities`, `Evidence`, `Services`) и добавлен `RuntimeBoundaryAdmissionService`, который fail-closed соединяет domain/capability/evidence/root-authority gates. Полный вынос substrate из `Core/VMX/Substrate` остается открытым.

3. **`ShadowVmcs` больше не является исполняемым VMCS-backed bridge.**
   Progress 2026-05-24: `Legacy/VMX/Compatibility/Generated/VmcsProjection/ShadowVmcsBlock.cs` удален без replacement, `ShadowVmcsNestedProjectionService` больше не хранит/не дергает `VmcsV2Descriptor.ShadowVmcs`, а active descriptor/manager/checkpoint paths не содержат `.ShadowVmcs.`. Остаточный риск теперь другой: nested runtime нуждается в generic nested-domain projection/checkpoint service, пока compatibility paths fail-closed.

4. **`ShadowVmcsBridgeRetirementContract` остался historical fence vocabulary, но дырка закрыта removal.**
   Progress 2026-05-24: общий `CoreVmxAuthorityBoundaryContract` больше не имеет allowed path для `.ShadowVmcs.`, а новый removal conformance проверяет отсутствие legacy file и former bridge markers. Остаток — почистить/переименовать старые ShadowVmcs-named conformance contracts после появления generic nested-domain service.

5. **Nested substrate всё ещё слишком VMX-shaped.**
   `NestedEnablementRequest` всё ещё содержит `PublishedVmxCaps`, а `PublishedCapabilityWord` остаётся алиасом над VMX capability word. Это лучше, чем прямой `VmxCaps`, но всё ещё не чистая typed capability authority. 

6. **Nested capability requirement всё ещё жёстко привязан к `VmxV2InstructionCaps.NestedVmx`.**
   Статус: closed
   `NestedCompatibilityCapabilityRequirement.NestedCompatibility` hardcodes VMX-specific capability mask. Для substrate это неправильный центр: generic nested authority должна зависеть от typed `NestedDomainCapability`, а VMX bit должен быть только projection. 

7. **Nested proof всё ещё строится из masks.**
   `NestedEnablementProof.FromCompatibilityMasks(...)` создаёт grants/gates из `NestedCapabilityGrantMask` и `NestedEnablementGate`. Это лучше оформлено, но authority всё ещё выводится из enum masks, а не из canonical typed grants, evidence policy, memory composition descriptor, completion route policy. 

8. **`CreateGrant(...)` в nested proof автоматически ставит `DescriptorBacked` и `EvidencePolicyBound` из наличия бита.**
   Статус: closed
   Это слабая security-модель: наличие mask-bit не должно автоматически доказывать descriptor-backed и evidence-bound authority. Эти свойства должны приходить из реальных descriptors/policies, а не вычисляться из того же mask. 

9. **`EvidenceVisibilityClass.CompatibilityAlias` используется как authoritative gate evidence.**
   Статус: closed
   В `CreateGate(...)` gate proof получает `EvidenceVisibilityClass.CompatibilityAlias`. Это риск: compatibility alias не должен сам по себе быть authoritative evidence. Нужна явная runtime/evidence policy source. 

10. **`CapabilityDescriptorSet` всё ещё mask-backed.**
    Добавлен `TypedGrants`, но constructor строит его через `CapabilityGrantCollection.FromMasks(...)`, а authoritative поля всё ещё `ulong GlobalHardwareCaps`, `RuntimeEnabledCaps`, `DomainGrantedCaps`. Значит typed grants пока производны от masks, а не наоборот. 
    Прогресс 2026-05-24: legacy CSR-backed `VmxCaps` source больше не может создавать authority из CSR/raw mask; возвращенный Core stub fail-closed и не читает `CsrAddresses.VmxCaps`. Общий риск mask-backed `CapabilityDescriptorSet` остается открытым.

11. **`EffectiveCaps` всё ещё является bitwise authority-looking field.**
    Статус: closed
    Даже если intended как compatibility/cache, его наличие рядом с `HasEffectiveCapability()` сохраняет риск, что callers будут использовать bitmap как источник authority. Нужно явно отделить: `EffectiveCapsProjection` или `CompatibilityCapsCache`, не `EffectiveCaps` как будто canonical. 

12. **Fallback в `CreateGrant(...)` всё ещё возвращает mask-derived grant.**
    Статус: closed
    Если `TypedGrants.TryGetGrant(...)` не находит grant, код создаёт `new(capabilityMask, scope, HasEffectiveCapability(capabilityMask))`. Это прямой путь регресса к bitmap authority. В финальной модели отсутствие typed grant должно быть denial/fail-closed, а не fallback к mask. 

13. **Owner-aware overload `CreateGrant(...)` тоже использует `HasEffectiveCapability(capabilityMask)`.**
    Статус: closed
    Даже с owner/delegation/revocation/migration/evidence metadata, granted-state всё ещё берётся из bitmap intersection. Это не полностью typed-authority модель. 

14. **`MemoryTranslationControl` всё ещё содержит compatibility-shaped backing fields.**
    Внутри остаются `NptEnabled`, `VpidEnabled`, `GuestCr3`, `NptRoot`, `Vmid`, `Vpid`, `VmcsIdentity`, `VmcsEpoch`. Generic aliases добавлены, но canonical record всё ещё VMX/NPT/VPID/VMCS-shaped. 
    Progress 2026-05-24: legacy translation invalidation backend no longer calls VMX-shaped IOMMU invalidation from `Core/VMX`; the host mechanics are behind generic `ApplyTranslationInvalidation` / `TranslationInvalidationHostBackend`. The `MemoryTranslationControl` vocabulary risk itself remains open.
    Progress 2026-05-24: quarantined `VmxExecutionUnit` no longer calls `IOMMU.ApplyVmxInvalidation` or advances frontend-owned EPT/VPID invalidation epochs. The remaining `MemoryTranslationControl` risk is vocabulary/API shape, not active INVEPT/INVVPID authority inside the legacy frontend.
    Progress 2026-05-24: quarantined `VmxExecutionUnit` no longer calls `MemoryTranslationControl.CreateRuntimeProjection(...)` or reads `SecondaryProcControls` / `GuestCr3` / `EptPointer` / `Vpid` to build intercept/event admission domains. Tagged admission must come from a generic domain-admission source.

15. **`MemoryDomainTranslationControl.ToCompatibilityControl()` всё ещё конвертирует domain state обратно в NPT/VPID/VMCS-shaped control.**
    Это допустимо на projection boundary, но рискованно внутри substrate file: generic memory-domain state и compatibility control живут в одном типе/файле и могут смешиваться active callers. 

16. **`VmcsEpoch` всё ещё используется как `AddressSpaceGeneration`.**
    Alias стал лучше, но backing field остаётся `VmcsEpoch`. Если где-то он участвует в `AddressSpaceId`, invalidation key или cache identity, VMCS снова влияет на memory-domain semantics. 

17. **`VmcsIdentity` всё ещё присутствует как `CompatibilityProjectionIdentity`.**
    Само переименование снижает риск, но поле остаётся в substrate translation control. Это должно быть projection-only sideband/evidence, не часть canonical memory translation record. 

18. **Старый `AdvanceRuntimeEpoch()` всё ещё существует рядом с fail-closed версией.**
    Статус: closed
    Добавлен `TryAdvanceRuntimeEpochFailClosed()`, но прежний `AdvanceRuntimeEpoch()` по-прежнему возвращает `1` при wraparound. Это оставляет опасный API рядом с безопасным. Его нужно удалить, deprecated+forbidden или закрыть static analyzer’ом. 

19. **`ToAddressSpaceId(ulong eptEpoch, ulong vpidEpoch)` всё ещё использует EPT/VPID vocabulary.**
    Статус: closed
    Даже если это compatibility naming, метод расположен в substrate translation control. Для active substrate API должны быть generic names: `secondStageEpoch`, `addressSpaceTagEpoch`. 

20. **`CompatAliasMap` всё ещё coarse-grained.**
    Все VMCS fields представлены одной entry: `VmcsField -> GeneratedVmcsProjection`. Это не доказывает field-by-field owner mapping и не закрывает риск “field exists but no substrate owner”. 
    Прогресс 2026-05-24: добавлена отдельная VMCS field projection schema с owner/access/evidence/migration policy и conformance-тестом. Сам `CompatAliasMap` остается coarse-grained, поэтому пункт не закрыт.

21. **`CompatAliasMap` всё ещё выглядит как checked-in generated artifact.**
    Есть `CanonicalSchemaHash` и `GeneratedArtifactHash`, но сама таблица — ручной C# `EntryTable`. Без внешнего schema source и build-time generator это остаётся “generated by naming”, а не generated by pipeline. 
    Прогресс 2026-05-24: добавлены physical canonical schema artifacts для compat alias, VMCS field projection и VmxCaps bit projection, а также `ProjectionSchemaPipelineContract` с проверкой schema path/generator/hash/entry count/parity/ABI freeze. Пункт остается открытым до появления реального executable/build-time generator.

22. **Не видно VMCS-field projection schema на уровне каждого поля.**
    Статус: closed
    Закрытие coarse alias map не равно закрытию VMCS projection correctness. Нужна таблица: каждый VMCS field → exact substrate owner/access policy/evidence policy/migration policy/unsupported/denied.

23. **Не видно VmxCaps-bit schema на уровне каждого capability bit.**
    Статус: closed
    Нужна таблица: каждый compatibility cap bit → typed grant source → publication policy → frontend projection rule. Иначе `VmxCaps` остаётся потенциальным bitmap authority.

24. **No-emission semantics для rejected/no-effect writes ещё нужно доказывать end-to-end.**
    Статус: closed
    Даже если task помечен closed, нужен тестовый инвариант: write в compatibility projection не меняет descriptors, grants, evidence, completion queues, memory-domain generation, lane state, migration state.

25. **Legacy quarantine закрыт организационно, но не архитектурно.**
    Статус: closed
    Само наличие `Legacy/VMX` допустимо, но финально нужен machine-enforced импортный запрет: старый file не может вернуться в `Core/VMX`, пока не доказаны descriptor owner, capability policy, evidence policy, retire boundary, no-emission tests и projection-only behavior.
    Прогресс 2026-05-24: `LegacyVmxV1AdapterBoundary.cs` и `LegacyVmxV2AdapterBoundary.cs` возвращены в `Core/VMX/Compatibility/Adapters` с `RequiredCoreReturnProof`; manifest/test теперь различают still-quarantined и returned-to-Core entries и проверяют отсутствие returned files в `Legacy/VMX`.
    Прогресс 2026-05-24: `LegacyCsrBackedVmxCapabilityDescriptorSource.cs` возвращен в Core только после proof metadata и conformance check: файл физически отсутствует в `Legacy/VMX`, находится в `Core/VMX/Compatibility/Generated/CsrProjection`, не содержит CSR `VmxCaps` reads и reject/fail-closed для capability authority.
    Progress 2026-05-24: `LegacyVmxTranslationInvalidationBackend.cs` returned to Core only after proof metadata and conformance check: the legacy file is absent from `Legacy/VMX`, the Core adapter is free of direct VMX/IOMMU authority markers, and invalidation is admitted by memory-domain descriptors before reaching the host backend.
    Progress 2026-05-24: `LegacyVmxIoVirtualizationBackend.cs` returned to Core only after proof metadata and conformance check: the legacy file is absent from `Legacy/VMX`, the Core adapter delegates to generic host I/O transport, and IOTLB/DMA authority remains admitted by I/O-domain descriptors before any host backend call.
    Progress 2026-05-24: `IOMMU.DomainBinding.partial.cs` is no longer a legacy-quarantined VMX-shaped host-mechanics partial; its generic I/O-domain/IOTLB implementation lives under `Memory/MMU`, the transitional generic-to-VMX wrapper was removed, and conformance now checks legacy absence plus marker-free generic host mechanics.
    Progress 2026-05-24: `LegacyVmcsMemoryTranslationControlProjection.cs` is now a removed-without-replacement manifest entry: the file is absent from `Legacy/VMX`, no Core return path exists, and `VmxExecutionUnit` no longer references the old `FromVmcs` helper.
    Progress 2026-05-24: `ShadowVmcsBlock.cs` is now a removed-without-replacement manifest entry: the file is absent from `Legacy/VMX`, no Core return path exists, former bridge markers are rejected by conformance, and `.ShadowVmcs.` has no Core authority exception.
    Progress 2026-05-24: `ExecutionDispatcherV4.Vmx.cs` and `CPU_Core.PipelineExecution.Vmx.cs` are no longer present under `Legacy/VMX`; their replacements live as current Core execution/retire routing surfaces, and conformance rejects reintroduction of `VmcsManager`, raw VMCS field access, IOMMU hooks, or Shadow VMCS authority in those surfaces.
    Progress 2026-05-24: `VmxExecutionUnit.cs` remains quarantined, but its direct invalidation authority slice is removed without replacement. Conformance now rejects reintroduction of `IOMMU.ApplyVmxInvalidation`, frontend EPT/VPID invalidation epoch state, old invalidation resolver payload decoding, and non-fail-closed INVEPT/INVVPID behavior.
    Progress 2026-05-24: `VmxExecutionUnit.cs` also no longer exposes `CompleteNestedTranslationFault(...)` or calls `RecordNestedTranslationExit`; conformance rejects nested translation fault completion/publication inside the legacy frontend.
    Progress 2026-05-24: `VmxExecutionUnit.cs` also no longer exposes `ResolveActiveMemoryTranslationControl()` or VMCS-field-backed admission domain derivation; conformance rejects reintroduction of `CreateRuntimeProjection`, `SecondaryProcControls`, `GuestCr3`, `EptPointer`, or `Vpid` in the legacy frontend.
    Progress 2026-05-25: `VmxExecutionUnit.cs` also no longer calls `RecordInterceptExit` or emits `VmxEventKind.InterceptExit`; conformance rejects standalone intercept-exit publication inside the legacy frontend.
    Progress 2026-05-25: `VmxExecutionUnit.cs` also no longer calls `TryResolveLane7VmFunc`, `TryValidateVectorStreamExtendedStateMask`, `TrySaveVectorStreamState`, or `TryRestoreVectorStreamState`; conformance rejects frontend-owned Lane7/vector-stream runtime paths inside the legacy frontend.
    Progress 2026-05-25: `VmxExecutionUnit.cs` also no longer calls `TryDeliverVirtualEvent` or emits `VmxEventKind.EventDelivered`; conformance rejects frontend-owned virtual-event delivery inside the legacy frontend.
    Progress 2026-05-25: `VmxExecutionUnit.cs` also no longer calls `ReadFieldValue`, `WriteFieldValue`, `TryNestedVmRead`, or `TryNestedVmWrite`; conformance rejects frontend-owned VMREAD/VMWRITE routing inside the legacy frontend.
    Progress 2026-05-25: `VmxExecutionUnit.cs` also no longer exposes `CompleteQualifiedVmExit(...)` or calls `RecordQualifiedVmExit`; conformance rejects common qualified VM-exit completion/publication inside the legacy frontend.
    Progress 2026-05-25: `VmxExecutionUnit.cs` also no longer calls `ClearPointer`, `LoadPointer`, or `StorePointer`, and no longer emits `VmxEventKind.VmClear` / `VmxEventKind.VmPtrLd`; conformance rejects frontend-owned VMCS pointer lifecycle inside the legacy frontend.
    Progress 2026-05-25: `VmxExecutionUnit.cs` also no longer calls `BeginVmEntry`, consumes `VmEntryTransitionResult`, restores `GuestPc` / `GuestSp`, emits `VmxEventKind.VmEntry` / `VmxEventKind.VmResume`, or moves through frontend-owned entry pipeline triggers; conformance rejects frontend-owned VM-entry authority.
    Progress 2026-05-25: `VmxExecutionUnit.cs` also no longer calls `ActivateRootDescriptor`, writes `CsrAddresses.VmxEnable`, emits `VmxEventKind.VmxOn` / `VmxEventKind.VmxOff`, or moves through `PipelineTransitionTrigger.VmxOff`; conformance rejects frontend-owned VMXON/VMXOFF root-switch authority.
    Progress 2026-05-25: `VmxExecutionUnit.cs` also no longer calls `HardwareWrite`, `RecordVmxFailForObservability`, or `RecordVmxEvent`; conformance rejects frontend-owned common VMFail / trace / VMX CSR publication.
    Progress 2026-05-25: `VmxExecutionUnit.cs` also no longer owns VMCALL/VMFUNC capability-query gates or capability readback; conformance rejects `VmxFunctionLeaf.CapabilityQuery`, `ReadProjectedVmxCaps`, `IsVmxV2CapabilityEnabled`, and VMFUNC/VMCALL capability-bit gates in the legacy frontend.
    Progress 2026-05-25: `VmxExecutionUnit.cs` also no longer constructs guest-intercept requests or checks `_csr` / `_vmcs` active VMCS state for that route; conformance rejects `TrapRequest.For*`, `TryResolveGuestIntercept*`, `HasActiveVmcs`, and `CsrAddresses.VmxEnable` in the legacy frontend.
    Progress 2026-05-25: `VmxExecutionUnit.cs` is now removed without replacement; manifest and conformance require its absence, reject a replacement constructor/opcode shell in current Core routing, and prove typed fail-closed behavior for every published VMX opcode.
    Progress 2026-05-25: `VmcsManager.cs` remains quarantined but no longer exposes `RecordNestedTranslationExit`, `TryResolveIntercept`, `RecordInterceptExit`, `RecordQualifiedVmExit`, standalone virtual-event APIs, or public VMX observability APIs. Conformance requires those markers to remain absent while live lane/vector/dirty ownership is still unresolved.
    Progress 2026-05-25: task `190` removes the final carrier and `IVmcsManager` without replacement. Conformance now requires no production `core.Vmcs`/manager binding, denies compatibility guest Lane6/Lane7 work before native effects, and requires the `Legacy/VMX` tree to be empty.

26. **`ShadowVmcsCompatibilityBridge` больше не активный error surface.**
    Статус: closed-by-removal
    Progress 2026-05-24: former bridge markers are absent from active service source and covered by removal conformance. Typed generic nested failures still matter for the future generic nested-domain service, but the old Shadow VMCS bridge should not be restored to provide them.

27. **`NestedProjectionService` больше не implemented через VMCS-backed bridge, но generic owner ещё не готов.**
    Progress 2026-05-24: production implementation now fail-closes instead of calling `VmcsV2Descriptor.ShadowVmcs`. Следующий архитектурный шаг — generic nested-domain projection/checkpoint owner, а не восстановление Shadow VMCS runtime block.

28. **`NestedCompatibilityCapabilityRequirement.NestedCompatibility` использует `DefaultRuntimeOwnerDomainId`.**
    Статус: closed
    Default owner полезен для legacy-compatible construction, но опасен как финальная authority-модель. Nested grants должны иметь реального owner domain из runtime context, а не default runtime owner. 

29. **Compatibility vocabulary всё ещё слишком близко к substrate logic.**
    Даже после закрытий в active substrate остаются `VmReadVmWriteBitmaps`, `PublishedVmxCaps`, `VmxV2InstructionCaps.NestedVmx`, `GuestCr3`, `NptRoot`, `Vpid`, `VmcsEpoch`. Это значит, что cleanup ещё не завершён.
    Progress 2026-05-24: legacy V1 execution adapter partials no longer keep VMX retire routing under `Legacy/VMX`; active routing now lives in Core files with explicit no-raw-authority conformance. Compatibility vocabulary remains because VMX opcode/effect names are still frozen frontend vocabulary.
    Progress 2026-05-24: INVEPT/INVVPID remain frozen VMX opcode vocabulary, but they no longer carry host invalidation authority inside `VmxExecutionUnit`; they now fail closed pending generic runtime invalidation admission.
    Progress 2026-05-24: nested translation fault completion is no longer frozen into `VmxExecutionUnit`; future behavior must be provided through generic nested-domain/domain-fault routing rather than restoring VMX frontend authority.
    Progress 2026-05-24: intercept/event domain tagging is no longer frozen into `VmxExecutionUnit` through VMCS field reads; future tagged admission must be provided through generic domain-admission descriptors rather than restoring VMCS-field authority.
    Progress 2026-05-25: standalone intercept-exit publication is no longer frozen into `VmxExecutionUnit`; future intercept observability/publication must be provided through generic domain-trap publication rather than restoring VMCS-specific intercept trace authority.
    Progress 2026-05-25: Lane7 VMFUNC helper resolution and vector-stream extended-state save/restore are no longer frozen into `VmxExecutionUnit`; future behavior must come from generic lane/vector descriptors, tokens, fences, and runtime admission rather than restoring VMCS helper authority.
    Progress 2026-05-25: pending virtual-event delivery is no longer frozen into `VmxExecutionUnit`; future delivery must come from generic event-queue descriptors, remap policy, and runtime admission rather than restoring VMCS helper authority.
    Progress 2026-05-25: VMREAD/VMWRITE field access is no longer frozen into `VmxExecutionUnit`; future field access must come from generated projection access policy rather than restoring frontend VMCS helper authority.
    Progress 2026-05-25: common VM-exit completion/publication is no longer frozen into `VmxExecutionUnit`; future completion must come from generic domain-trap/domain-fault routing rather than restoring frontend `CompleteQualifiedVmExit`.
    Progress 2026-05-25: VMCLEAR/VMPTRLD/VMPTRST pointer lifecycle is no longer frozen into `VmxExecutionUnit`; future pointer-handle binding must come from generic execution-domain binding/projection-handle admission rather than restoring frontend VMCS pointer helper authority.
    Progress 2026-05-25: VMLAUNCH/VMRESUME entry is no longer frozen into `VmxExecutionUnit`; future entry must come from generic `DomainEnter` admission/completion routing rather than restoring frontend VMCS entry helper authority.
    Progress 2026-05-25: VMXON/VMXOFF root switching is no longer frozen into `VmxExecutionUnit`; future root compatibility activation must come from generic runtime-domain admission rather than restoring frontend root descriptor / `VmxEnable` authority.
    Progress 2026-05-25: VMFail / trace / VMX CSR publication is no longer frozen into `VmxExecutionUnit`; future publication must come from generic retire/domain-fault publication rather than restoring frontend CSR or trace shortcuts.
    Progress 2026-05-25: VMCALL/VMFUNC capability-query gates are no longer frozen into `VmxExecutionUnit`; future behavior must come from generic runtime capability admission and typed publication policy rather than restoring frontend CSR/capability readback.
    Progress 2026-05-25: guest-intercept request construction is no longer frozen into `VmxExecutionUnit`; future behavior must come from generic domain-trap admission/routing rather than restoring frontend VMCS-backed guard checks.
    Progress 2026-05-25: the broad `VmxExecutionUnit` opcode shell itself has been deleted without replacement. Frozen VMX opcode naming remains only in typed compatibility metadata/effects; no returned frontend class owns execution or retire authority.
    Progress 2026-05-25: the surviving `VmcsManager` carrier no longer provides a VMX publication/event/observability back door for the deleted frontend. Remaining VMCS-shaped lane/vector/dirty/DMA state must be moved to neutral runtime owners before final file removal.
    Progress 2026-05-25: task `190` completes that removal. No manager, interface, global VMCS dirty sink, lane-completion bridge, or VMCS descriptor lookup remains in the former live production paths; guest compatibility Lane6/Lane7 work is explicitly denied and native generic runtime paths remain available outside guest compatibility execution.

30. **Финальная архитектурная позиция всё ещё не закреплена структурой проекта.**
    Пока generic domain/capability/memory/nested files лежат в `Core/VMX/Substrate`, архитектура всё ещё выглядит как “VMX owns substrate”. Это надо исправлять до freeze, иначе legacy naming вернётся через будущие contributors.
    Прогресс 2026-05-24: добавлены первые neutral runtime files under `Core/Runtime/*` with file-level descriptions and conformance coverage. Это закрепляет направление, но пока не закрывает физическое расположение всех descriptor/runtime типов.

31. **Документационный риск: closed-list может создать ложное ощущение завершённости.**
    Пользовательский audit показывает 28 пунктов как closed, но сам факт closed не доказывает, что архитектурные invariants стали machine-enforced. Нужен отдельный статус: `closed-by-refactor`, `closed-by-quarantine`, `closed-by-facade`, `closed-by-generator`, `closed-by-conformance`. Сейчас эти типы закрытия смешаны. 
    Прогресс 2026-05-24: completion audit обновлен с явным разделением closed-by-conformance/artifact и остаточного риска по real generator executable/build-time regeneration. Полная taxonomy для всех старых closure-файлов еще не проставлена.

32. **Freeze пока нельзя объявлять.**
    Нельзя закрывать VMX refactor как завершённый, пока остаются: heavy `VmcsManager`, missing generic nested-domain projection/checkpoint service, mask-derived capability authority, VMX-shaped translation backing fields, checked-in generated scaffolding без real generator executable/build-time regeneration, и substrate under VMX path. `VmxExecutionUnit` полностью удалён без replacement: frozen VMX opcode routing теперь сохраняется только как typed fail-closed compatibility effect surface, а `Legacy/VMX` содержит только `VmcsManager.cs`.
    Progress 2026-05-25: `VmcsManager` publication/intercept/standalone-event/observability API slice is removed without replacement, but full removal is explicitly not claimed while Core still constructs and calls the carrier for lane/vector/dirty/DMA paths.
    Progress 2026-05-25: task `190` removes that final heavy `VmcsManager` anchor without replacement and makes `Legacy/VMX` empty. Freeze is still not claimed: remaining VMCSv2 projection/model/checkpoint/vector compatibility vocabulary, generic nested-domain projection/checkpoint work, build-time generation, and namespace extraction remain separate audit items.
