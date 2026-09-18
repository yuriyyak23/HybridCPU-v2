# Актуальный внешний аудит VMX-модели HybridCPU-v2

Дата актуализации: 2026-05-28
База сверки: текущее состояние кода после closure `239`.

## Краткий вердикт

VMX compatibility frontend freeze уже объявлен и подтвержден текущей conformance matrix. Это freeze ABI/projection/frontend-поверхности, а не превращение VMX в архитектуру виртуализации и не утверждение feature-complete VMX execution.

Текущий source of truth остается нейтральным:

- execution/memory/I/O/lane/nested domains находятся под `Core/Runtime/*`;
- capability authority grant-first и typed-grant based;
- host-owned evidence не сериализуется как VMX/VMCS authority;
- completion/retire publication идет через neutral pipeline/runtime policy;
- VMX/VMCS vocabulary допустима только как frozen compatibility vocabulary, generated/read-only projection, denied/fail-closed spelling, conformance evidence или explicit physical quarantine.

Сильный факт текущего состояния: production и tests больше не зависят от physical `Legacy/VMX`. Full conformance move-away probe прошел, `Legacy/VMX/Conformance` удален как compiled source surface, а пустые `Legacy/VMX` и `Legacy/VMX-v2` directory trees сняты физически.

## Проверенные факты кода

- `Core/VMX/**/*.cs` с marker `legacy` без учета регистра: `0`.
- `Core/VMX/Substrate/**/*.cs`: `0`.
- `Legacy/VMX/Compatibility/**/*.cs`: `0`.
- `Legacy/VMX/Conformance/**/*.cs`: `0`.
- Total `Legacy/VMX/**/*.cs`: `0`.
- Physical `Legacy/VMX`: absent.
- Physical `Legacy/VMX-v2`: absent.
- `VmxExecutionUnit.cs`: absent.
- `VmcsManager.cs`: absent.
- `IVmcsManager.cs`: absent.
- Test-local freeze-readiness evidence remains historical proof only; whole-folder conformance deletion is now closed by move-away production/tests builds and physical source removal.

## Что внешний аудит устаревшего snapshot оценивал правильно

Внешний аудит верно выделял основные архитектурные линии:

1. VMX не должен быть архитектурной осью HybridCPU.
2. Capability model должен быть grant-first.
3. VMX/NPT/VPID identity не должна быть canonical memory identity.
4. Lane6/Lane7 host-owned evidence должна жить в neutral host-owned stores.
5. Generated projection lineage должен быть build-verifiable или явно contract-only.
6. VMCSv2 block/descriptor helper surface остается зоной риска, если содержит mutable owner-like methods.
7. Nested compatibility execution может оставаться fail-closed, пока neutral nested admission не подключен явно.
8. VMX opcode path может быть frozen/fail-closed, но это не равно feature-complete VMX execution.

## Что изменилось после внешнего snapshot

### Freeze status

Старый вывод "architectural freeze premature" больше нельзя применять без уточнения. Текущее состояние:

- **VMX compatibility frontend freeze declared**: да.
- **VMX as virtualization architecture**: нет.
- **VMX feature-complete admitted execution model**: нет.
- **Post-freeze hardening/refactoring remains**: да.

### Physical quarantine

Раньше physical `Legacy/VMX` был production dependency из-за compatibility carriers. Теперь:

- `VmxInstructionPayload`, `VmxRetireEffect`, `VmxRetireOutcome`, `VmxOperationKind`, `ShadowVmcsNestedProjectionService` rehomed under `Core/VMX/Compatibility`.
- `Legacy/VMX/Compatibility` empty.
- Full `Legacy/VMX` move-away production build probe passes.
- Full `Legacy/VMX/Conformance` move-away probe passes for both production and tests.
- `Legacy/VMX/Conformance` has been physically deleted after its remaining compiled evidence was moved to test-local static/file evidence or found obsolete.
- Empty `Legacy/VMX` and `Legacy/VMX-v2` directory trees have been removed; tests now tolerate absence while rejecting any restored `.cs`.

### Broad VMX matrix

Старый stale broad-filter path закрыт:

- `FullyQualifiedName~Vmx`: `509/509` passed after closure `233`.
- `VmxProjectionSchemaAndQuarantineTests`: `58/58` passed after closure `233`.

Remaining unrelated debt:

- Phase12 VLIW/ISA compatibility-freeze failures are not VMX authority evidence.

## Текущая closure matrix

| Область | Текущее состояние | Статус |
| --- | --- | --- |
| Legacy `VmxExecutionUnit` | отсутствует, opcode shell не возвращен | closed/removal |
| `VmcsManager` / `IVmcsManager` | отсутствуют | closed/removal |
| `Core/VMX/Substrate` | no C# sources | closed physical extraction |
| `Legacy/VMX/Compatibility` | no C# sources | closed production quarantine |
| VMX production carrier dependency on `Legacy/VMX` | absent, move-away build passes | closed |
| VMX broad filter | `509/509` passed | closed for VMX |
| Capability authority | typed grants + grant-first projection | closed with guardrails |
| Memory identity | neutral domain/address-space/second-stage tags | closed with guardrails |
| Lane6/Lane7 host-owned evidence | neutral host-owned stores, rebuild semantics | strong, keep regression tests |
| Generated VMCS/VmxCaps/alias artifacts | build-time lineage verifier | closed for covered artifacts |
| VMCS field write aliases | read-only/denied projection | closed with lineage |
| VMX opcode execution | typed fail-closed frozen frontend, plus narrow VMREAD and VMCALL compatibility projection admissions through `RuntimeBoundaryAdmissionService` | frozen ABI, admitted-denied projection paths; not feature-complete execution |
| First admitted VMX compatibility path | `VMREAD` can pass decode, frozen alias projection, runtime admission, generated schema owner lookup, field-alias evidence policy, and generated read-only value projection for admitted completion-owned, memory-owned, and narrow execution-owned fields; non-admitted owner/value-source fields remain denied | closed for completion-owned fields plus `GuestCr3`, `EptPointer`, `Vpid`, `Cr3TargetCount`, `GuestPc`, `GuestSp`, and `GuestFlags`; no backend success |
| Neutral trap result split | runtime trap policy/timer return `NeutralTrapResult`; VMX `TrapDecision`/`VmExitReason` projection is isolated in `VmxTrapProjectionMapper` | closed before any admitted VMCALL/trap/intercept backend |
| Admitted-denied VMCALL trap projection | `VMCALL` can pass decode, frozen alias projection, runtime admission, neutral trap policy, `NeutralTrapResult`, and `VmxTrapProjectionMapper`, then stops as backend-denied projection | closed for admitted-denied trap projection; no intercept/backend success |
| Trap projection publication fence | admitted-denied trap projection returns a neutral `TrapCompletionPublicationFenceResult`; compatibility completion/retire helpers require that fence and remain denied for VMCALL | closed; no completion or intercept retire publication |
| Nested compatibility execution | neutral projection/checkpoint owner exists, compatibility bridge fail-closed | safe, not feature-complete |
| Empty `CloseToHSL/Core/Virtualization/Substrate/*` project placeholders | empty substrate folder includes removed from `HybridCPU_ISE.csproj`; substrate authority remains under neutral `Core/Runtime/*` owners | closed for quick audit4 cleanup; no authority moved |
| `Legacy/VMX/Conformance` deletion | full move-away production/tests probe passed; physical folder removed | closed |
| VMCSv2 blocks/descriptor mutable helpers | descriptor guest-state/host-evidence, root/NPT/bundle/event/debug mutators, and residual vector/dirty/security/capability backing state removed or fenced; `ExitInfoBlock.Record*` is internal retire-publication only | strong hardening; descriptor-owned scalar VMREAD remains denied without neutral value source |
| Generated/frontend projection inventory | 32 files classified as generated-lineage, contract-only, or denied-only; manager/store/backend authority markers fenced; timer/trap bitmap extracted; header/child-intent authority removed; trap projection mapper and VMREAD value projection service included | closed; 0 forbidden-authority targets remain |

## Простые legacy deletion candidates

Production simple legacy pool is exhausted:

- no compiled production `.cs` remains under `Legacy/VMX/Compatibility`;
- old dead production shells are already removed without replacement;
- no legacy-marked source was returned to `Core/VMX`.

The remaining simple-looking legacy pool is no longer compiled production or conformance code:

1. `Legacy/VMX/Conformance` contracts are deleted from the production project tree.
2. Historical test-local files still contain retired `VmxExecutionUnit` / `VmcsManager` vocabulary as static path/marker evidence only.
3. Some docs may still contain stale pre-freeze statements or old counts and should be cleaned opportunistically.

Do not recreate physical `Legacy/VMX/Conformance` for new proof. New evidence belongs either in neutral runtime/domain owners, generated read-only VMX compatibility projection, denied/fail-closed compatibility tests, or test-local static/file evidence.

## Current real risks

### Risk 1: VMCSv2 mutable helper surface

`VmcsV2Descriptor` no longer exposes the dead guest-state and host-evidence owner-like helpers removed in closure `225`:

- `CaptureGuestStateEager`;
- `BeginLazyGuestStateSave`;
- `MaterializeLazyGuestRegisters`;
- `MaterializeVmExitGuestState`;
- `RecordHostEvidence`;
- `GuestVisibleStateContainsHostEvidence`;
- `DiscardHostEvidenceAfterRestore`;
- `ResetForClear`.

Remaining descriptor methods are classified as:

- `TryReadScalarField`: denied/fail-closed compatibility ABI;
- `ValidateMigrationReadiness`: denied/fail-closed validation while no neutral generated projection materializes guest state;
- `ValidateNestedEnablementReadiness`: denied/fail-closed compatibility validation;
- `RecordVectorExceptionExit`, `RecordStreamDescriptorFaultExit`, `RecordStreamReplayRequiredExit`: retire-publication-only compatibility projection over neutral vector/stream identities.

Closure `230` removed the next high-risk VMCSv2 block mutator slice without replacement:

- `VmxRootControlBlock.BindRootDescriptor` and `AdvanceEpoch`;
- `VmxNptBlock.BindControl`;
- `BundleExecutionBlock.BindBundle`;
- `VirtualInterruptFabricBlock.Fabric`;
- `VmxEventInjectionBlockSnapshot`;
- `EventInjectionBlock` queue/remap state plus `ConfigureInterruptRemap`, `RemoveInterruptRemap`, `ClearInterruptRemaps`, `TryQueue`, `TryDeliver`, `CreateSnapshot`, and `RestoreSnapshot`;
- `DebugTraceBlock` trace-plane state plus `ConfigureExport`, `Record*`, `SnapshotCounters`, `ResetCounters`, and `DiscardTraceHandles`.

The affected blocks are now read-only compatibility projection shells over neutral runtime owners. Real event posting/delivery/remap remains under `Core/Runtime/Events/Injection`, not under VMCSv2. Real debug/observability publication still needs a neutral runtime owner before any future compatibility projection can expose it.

Closure `231` closed the residual VMCSv2 block-state inventory:

- `VectorStreamStateBlock`, `DirtyLogBlock`, `SecurityIsolationBlock`, and `CapabilityNegotiationBlock` are generated/read-only compatibility projection shells with no instance backing fields;
- `ExitInfoBlock.Record*` is no longer public API; descriptor `Record*Exit` remains the public retire-publication-only projection boundary;
- no remaining audited VMCSv2 block may grow a public mutator or backing state without a neutral runtime owner and conformance fence first.

### Risk 2: First admitted VMX path is narrow and not feature-complete

Closure `235` added the first narrow VMX compatibility path through neutral runtime admission:

- `VMREAD` is decoded by `VmxCompatDecodeBoundary`;
- the frozen alias map is validated as `Opcode/VMREAD -> VmcsFieldAliasProjection.Read`;
- `RuntimeBoundaryAdmissionService` admits `DomainRuntimeOperationKind.ReadCompatibilityProjection` only when compatibility-alias evidence policy is open;
- closure `240` now continues only for completion-owned fields through `VmcsFieldProjectionSchema` owner lookup, `VmcsReadOnlyValueProjectionService`, `CompletionProjectionService`, and compatibility-alias evidence policy.

This closes the first generated read-only value projection slice, but it does not make VMX feature-complete and it does not create a successful VMREAD backend. `ExitReason`, `ExitQualification`, `GuestPhysicalAddress`, and `EptViolationQualification` can be projected only from a neutral `CompletionRecord` compatibility projection source after runtime admission. Execution-owned, memory-owned, control-owned, unknown, ungenerated, or completion-source-missing fields remain denied/fail-closed. Current pipeline opcode routing still retires typed fail-closed effects for the frozen VMX opcode surface.

Closure `237` adds the first admitted-denied VMCALL/trap projection path:

- `VMCALL` is decoded by `VmxCompatDecodeBoundary`;
- the frozen alias map validates `Opcode/VMCALL -> VmxTrapProjectionMapper.Project`;
- `RuntimeBoundaryAdmissionService` admits `DomainRuntimeOperationKind.ProjectCompatibilityTrap` only when compatibility-alias evidence policy is open;
- runtime trap policy must provide a neutral `CompatibilityOperation` intercept;
- the neutral `NeutralTrapResult` is projected to VMX `TrapDecision` / `VmExitReason.VmCall` only through `VmxTrapProjectionMapper`;
- the result is explicitly `TrapProjectionDeniedBackend`, so no backend execution, `VmxRetireEffect.InterceptExit`, `VmxRetireEffect.VmCall`, VMCS manager, field store, or active pointer is introduced.

Closure `238` adds the retire/completion publication fence for that path:

- `TrapCompletionPublicationFence` is a neutral runtime completion record fence with no VMX vocabulary;
- `VmxCompatibilityAdmissionService.AdmitVmCallTrapProjection` now returns a `PublicationFence`;
- admitted-denied VMCALL gets `TrapCompletionPublicationDecision.DeniedBackendExecution`;
- `CompletionRecord.FromCompatibilityExit` / `TryFromCompatibilityExit` require the neutral fence before creating a compatibility-exit completion record;
- `CompletionProjectionService` no longer projects arbitrary nonzero neutral reason codes as VMX exits;
- `VmxRetireEffect.InterceptExit` requires the neutral fence and returns a security fault while publication remains denied.

Future implementation must admit any real VMX operation through neutral runtime boundaries first, not through VMCS/VMX-owned authority.

### Risk 2a: Projection inventory forbidden-authority carriers closed

Closure `232` inventoried all `Core/VMX/Compatibility/Generated/*` and `Core/VMX/Compatibility/Frontend/Projection/*` C# sources.

Current classification:

- generated-lineage: `4` files, all covered by `GeneratedProjectionLineageBuildContract.RequiredGeneratedOutputs`;
- contract-only: `23` files;
- denied-only: `3` files;
- forbidden-authority: `0` files.

Closure `233` extracted the first two mutable carriers out of VMX projection scope:

- `Core/VMX/Compatibility/Frontend/Projection/Events/SchedulingBudgetTimer.cs` was deleted and rehomed as `Core/Runtime/Events/Traps/SchedulingBudgetTimer.cs`;
- `Core/VMX/Compatibility/Frontend/Projection/Events/TrapPolicyBitmap.cs` was deleted and rehomed as `Core/Runtime/Events/Traps/TrapPolicyBitmap.cs`.

Closure `234` reduced the remaining two carriers:

- `Core/VMX/Compatibility/Generated/VmcsProjection/VmcsV2Header.cs`: launch state and invalidation epoch mutators removed; header is read-only compatibility metadata;
- `Core/VMX/Compatibility/Frontend/Projection/Nested/ChildDomainIntentDescriptor.cs`: field dictionary, write API, raw field access, generation, and snapshot/restore state removed; remaining read surface is fail-closed without neutral runtime-owned nested intent state.

No generated/frontend projection file is currently classified as forbidden-authority. The inventory test still covers the whole scope exactly and rejects new unclassified files or authority markers.

### Risk 3: Nested compatibility wiring remains fail-closed

Neutral `NestedDomainProjectionCheckpointService` exists, but Shadow VMCS compatibility admission remains denied/fail-closed. That is safe. Any future enablement must route through:

```text
VMCS12/VMCS02 compatibility projection
-> NestedDomainProjectionCheckpointService
-> RuntimeBoundaryAdmissionService
-> neutral completion/retire publication
```

### Risk 4: Historical evidence vocabulary remains test-local

`Legacy/VMX/Conformance` no longer exists as a compiled source folder. Some historical removal vocabulary remains in test-local evidence helpers to prove no reintroduction of old paths, managers, VMCS field stores, active pointer state, or VMX backend paths. Keep that vocabulary out of `Core/VMX` runtime authority.

### Progress 2026-05-25 task `223`

The first conformance decoupling slice is closed for the quarantine manifest evidence only.

- `HybridCPU_ISE.Tests/VmxRefactoring/VmxProjectionSchemaAndQuarantineTests.cs` no longer has compiled references to `LegacyVmxQuarantineManifest`, `LegacyVmxQuarantineEntry`, or `LegacyReverseImportRequest`.
- `HybridCPU_ISE.Tests/VmxRefactoring/VmxQuarantineEvidenceManifest.cs` now carries test-local static path evidence for the quarantine manifest and a test-local return-proof shape. This is file/static evidence, not a runtime owner and not production compatibility vocabulary.
- The selected move-away probe temporarily moved `Legacy/VMX/Conformance/AuthorityBoundary/LegacyVmxQuarantineManifest.cs` to `Desktop/New folder` and both production and tests builds passed; the file was restored in `finally`.
- `Legacy/VMX` still contains `37` C# sources. No wholesale conformance-folder deletion is safe yet because tests still directly compile against other conformance contracts.
- `Core/VMX` remains free of legacy-marked C# sources, and `VmxExecutionUnit.cs`, `VmcsManager.cs`, and `IVmcsManager.cs` remain absent.

### Progress 2026-05-25 task `224`

The next deletion-oriented conformance slice removed the first fast static evidence pool from `Legacy/VMX/Conformance/AuthorityBoundary`.

- Deleted without replacement from production/conformance sources: `LegacyVmxQuarantineManifest.cs`, `LegacyVmxV1AdapterBoundaryRemovalContract.cs`, `LegacyVmxV2AdapterBoundaryRemovalContract.cs`, `LegacyCsrBackedVmxCapabilityDescriptorSourceRemovalContract.cs`, `LegacyVmxTranslationInvalidationBackendRemovalContract.cs`, `LegacyVmxIoVirtualizationBackendRemovalContract.cs`, and `LegacyVmxExecutionUnitRemovalContract.cs`.
- Added test-local static evidence in `HybridCPU_ISE.Tests/VmxRefactoring/VmxLegacyFastRemovalEvidenceContracts.cs`. This preserves path/marker assertions only; it is not a runtime owner, VMCS field store, manager, adapter, or VMX backend path.
- `Legacy/VMX` now contains `30` C# sources, all under conformance/evidence.
- Full `Legacy/VMX/Conformance` move-away probe after the deletion: production build exit `0`, tests build exit `1`. First remaining blockers are retained-surface and freeze-readiness evidence contracts.
- `Core/VMX` remains free of legacy-marked C# sources, and `VmxExecutionUnit.cs`, `VmcsManager.cs`, and `IVmcsManager.cs` remain absent.

### Progress 2026-05-25 task `225`

The VMCSv2 mutable helper authority audit is closed for the descriptor guest-state and host-evidence helper slice.

- Removed without replacement from `VmcsV2Descriptor`: `CaptureGuestStateEager`, `BeginLazyGuestStateSave`, `MaterializeLazyGuestRegisters`, `MaterializeVmExitGuestState`, `RecordHostEvidence`, `GuestVisibleStateContainsHostEvidence`, `DiscardHostEvidenceAfterRestore`, and `ResetForClear`.
- Removed without replacement from `VirtualCpuBlock`: `CaptureEager`, `BeginLazySave`, `TryMaterializeLazyRegisters`, and `SnapshotGuestIntegerRegisters`.
- Added `VmcsV2MutableHelperAuthorityTests` to fence the removed methods and to prove scalar VMREAD remains denied/fail-closed.
- No runtime owner, VMCS manager, VMCS field store, active pointer, or VMX backend path was introduced.
- At closure `225` time, event/debug/root/NPT/bundle helpers were classified as runtime-owner-to-extract or delete/deny candidates; closure `230` later removed that helper pool without replacement.

### Progress 2026-05-26 task `226`

The retained compatibility surface and freeze-readiness evidence slice is no longer compiled from `Legacy/VMX/Conformance`.

- Deleted without replacement from conformance sources: `LegacyVmxRetainedCompatibilitySurfaceInventoryContract.cs` and `LegacyVmxFreezeReadinessCertificationContract.cs`.
- Added test-local static evidence in `HybridCPU_ISE.Tests/VmxRefactoring/VmxRetainedSurfaceAndFreezeEvidenceContracts.cs`. This keeps path/marker assertions only; it is not a runtime owner, VMCS field store, manager, adapter, active pointer, or successful VMX backend path.
- `Legacy/VMX` now contains `28` C# sources, all under conformance/evidence.
- Full `Legacy/VMX/Conformance` move-away probe after the deletion: production build exit `0`, tests build exit `1`. First reported remaining blockers are `CapabilityProjectionPlacementServiceSubstrateExtractionContract`, `CoreVmxSubstrateResidualExtractionContract`, and `FinalFrozenAliasQuarantineContract`.
- `Core/VMX` remains free of legacy-marked C# sources, and `VmxExecutionUnit.cs`, `VmcsManager.cs`, and `IVmcsManager.cs` remain absent.

### Progress 2026-05-26 task `227`

The capability/substrate extraction and frozen-alias quarantine evidence slice is no longer compiled from `Legacy/VMX/Conformance`.

- Deleted without replacement from conformance sources: `CapabilityProjectionPlacementServiceSubstrateExtractionContract.cs`, `CoreVmxSubstrateResidualExtractionContract.cs`, and `FinalFrozenAliasQuarantineContract.cs`.
- Added test-local static evidence in `HybridCPU_ISE.Tests/VmxRefactoring/VmxCapabilitySubstrateAndAliasEvidenceContracts.cs`. This preserves path/marker assertions only; it is not a runtime owner, VMCS field store, manager, adapter, active pointer, or successful VMX backend path.
- Updated the prior retained-surface test-local inventory so it no longer requires the deleted `CoreVmxSubstrateResidualExtractionContract.cs` as live conformance evidence.
- `Legacy/VMX` now contains `25` C# sources, all under conformance/evidence.
- Full `Legacy/VMX/Conformance` move-away probe after the deletion: production build exit `0`, tests build exit `1`. First reported remaining blockers are `NestedDomainProjectionCheckpointOwnerContract`, `LegacyIommuDomainBindingReturnContract`, `LegacyVmcsMemoryTranslationProjectionRemovalContract`, `LegacyShadowVmcsBlockRemovalContract`, `LegacyVmxV1ExecutionAdapterSurfaceReturnContract`, and `LegacyVmcsManagerVmxPublicationAuthorityRemovalContract`.
- `Core/VMX` remains free of legacy-marked C# sources, and `VmxExecutionUnit.cs`, `VmcsManager.cs`, and `IVmcsManager.cs` remain absent.

### Progress 2026-05-26 task `228`

The nested-composition evidence folder is no longer compiled from `Legacy/VMX/Conformance`.

- Deleted without replacement from conformance sources: `NestedDomainProjectionCheckpointOwnerContract.cs`, `NestedCompositionContract.cs`, and `ShadowVmcsBridgeRetirementContract.cs`.
- Added test-local static evidence in `HybridCPU_ISE.Tests/VmxRefactoring/VmxNestedCompositionEvidenceContracts.cs` for the remaining `NestedDomainProjectionCheckpointOwnerContract` assertions. This preserves path/marker and host-evidence restore rejection checks only; it is not a runtime owner, VMCS field store, manager, adapter, active pointer, or successful VMX backend path.
- `NestedCompositionContract` and `ShadowVmcsBridgeRetirementContract` had no remaining compiled callers outside their own deleted files. Remaining mentions are string evidence only: the golden artifact manifest name `NestedCompositionContract` and a forbidden marker string for `ShadowVmcsBridgeRetirementContract`.
- `Legacy/VMX` now contains `22` C# sources, all under conformance/evidence.
- Fresh build/test/move-away verification is currently blocked before VMX validation by unrelated non-VMX duplicate member definitions in `CloseToHSL/Core/ISA/Instructions/NonVmx/Lane06DmaStream/QueueLifecycle/Dsc*Instruction.cs`.
- `Core/VMX` remains free of legacy-marked C# sources, and `VmxExecutionUnit.cs`, `VmcsManager.cs`, and `IVmcsManager.cs` remain absent.

### Progress 2026-05-26 task `229`

The VMCS/shadow/manager, IOMMU-return, reverse-import, remaining execution-unit, and runtime-epoch conformance evidence pool is no longer compiled from `Legacy/VMX/Conformance`; the physical folder was deleted.

- Added test-local static evidence in `HybridCPU_ISE.Tests/VmxRefactoring/VmxVmcsShadowManagerEvidenceContracts.cs` for VMCS/shadow/manager, IOMMU-return, and V1 execution adapter path/marker assertions. This is not a runtime owner, VMCS field store, active pointer, manager, adapter, or VMX backend path.
- Deleted the remaining compiled conformance sources under `Legacy/VMX/Conformance`, including VMCS/shadow/manager contracts, reverse-import proof, execution-unit removal contracts, and `RuntimeEpochAdvanceFailClosedContract.cs`.
- Full `Legacy/VMX/Conformance` move-away probe before deletion passed: production build exit `0`, tests build exit `0`, folder restored in `finally`.
- After deletion, `Legacy/VMX` contains `0` C# sources, `Legacy/VMX/Conformance` no longer exists physically, and the empty `Legacy/VMX` / `Legacy/VMX-v2` directory trees were removed.
- `Core/VMX` remains free of legacy-marked C# sources, and `VmxExecutionUnit.cs`, `VmcsManager.cs`, and `IVmcsManager.cs` remain absent.

### Progress 2026-05-26 task `230`

The VMCSv2 block mutable helper authority audit is closed for root/NPT/bundle/event/debug helpers.

- Removed without replacement from `VmcsV2Blocks`: root descriptor binding/epoch advance, NPT control binding, bundle binding, VMCS-owned virtual interrupt fabric exposure, event injection queue/remap/delivery/snapshot helpers, and debug trace configure/record/reset helpers.
- `EventInjectionBlock`, `VirtualInterruptFabricBlock`, `VmxRootControlBlock`, `VmxNptBlock`, `BundleExecutionBlock`, and `DebugTraceBlock` are now read-only compatibility projection shells with default/zero state.
- Added `VmcsV2MutableHelperAuthorityTests` fences for removed methods, removed backing fields, removed snapshot type, and default read-only projection behavior.
- Strengthened `EventTrapDomainIdentityAuthorityRemovalContract` so VMCSv2 compatibility event projection forbids `TryQueue`, `TryDeliver`, remap configuration/removal/clear, and snapshot restore helpers.
- No runtime owner, VMCS manager, VMCS field store, active pointer, VMX runtime manager, or admitted VMX backend path was introduced.

### Progress 2026-05-26 task `231`

The residual VMCSv2 block-state inventory is closed for vector/dirty/security/capability shells and `ExitInfoBlock` retire publication.

- Converted `VectorStreamStateBlock`, `DirtyLogBlock`, `SecurityIsolationBlock`, and `CapabilityNegotiationBlock` to read-only/default projection shells with no instance backing fields.
- Kept vector-stream and dirty-log status vocabulary as generated/read-only compatibility projection only; no VMCS-owned descriptor table, epoch, dirty-page, security, or capability negotiation state remains in those blocks.
- Made `ExitInfoBlock.RecordVectorException`, `RecordStreamDescriptorFault`, and `RecordStreamReplayRequired` internal so public publication remains descriptor-mediated through `Record*Exit` retire projection helpers.
- Added `VmcsV2MutableHelperAuthorityTests` fences for residual no-backing-state shells and internal-only `ExitInfoBlock.Record*` helpers.
- No runtime owner, VMCS manager, VMCS field store, active pointer, VMX runtime manager, or admitted VMX backend path was introduced.

### Progress 2026-05-27 task `232`

The generated/frontend projection inventory slice is closed.

- Added executable test-local inventory for every `.cs` file under `Core/VMX/Compatibility/Generated/*` and `Core/VMX/Compatibility/Frontend/Projection/*`.
- Inventory count: `32` files total; `4` generated-lineage, `22` contract-only, `2` denied-only, and `4` forbidden-authority.
- Generated-lineage entries are cross-checked against `GeneratedProjectionLineageBuildContract.RequiredGeneratedOutputs`.
- Runtime manager/store/backend authority markers are forbidden across the inventory scope.
- At closure `232` time, forbidden-authority targets were `VmcsV2Header.cs`, `SchedulingBudgetTimer.cs`, `TrapPolicyBitmap.cs`, and `ChildDomainIntentDescriptor.cs`.
- No production code was moved and no runtime owner, VMCS manager, VMCS field store, active pointer, VMX runtime manager, or admitted VMX backend path was introduced.

### Progress 2026-05-27 task `233`

The first forbidden-authority projection carrier extraction slice is closed.

- Deleted `Core/VMX/Compatibility/Frontend/Projection/Events/SchedulingBudgetTimer.cs` and `Core/VMX/Compatibility/Frontend/Projection/Events/TrapPolicyBitmap.cs` from the VMX frontend projection scope.
- Rehomed their mutable state into `Core/Runtime/Events/Traps/SchedulingBudgetTimer.cs` and `Core/Runtime/Events/Traps/TrapPolicyBitmap.cs`, pairing the timer with the existing runtime snapshot partial.
- Updated the exact projection inventory to `30` files total: `4` generated-lineage, `22` contract-only, `2` denied-only, and `2` forbidden-authority.
- Added test-local extraction evidence proving the old projection paths stay absent while the runtime owner paths retain the expected timer/trap state markers.
- At closure `233` time, remaining forbidden-authority targets were `VmcsV2Header.cs` and `ChildDomainIntentDescriptor.cs`.
- No VMCS manager, VMCS field store, active pointer, VMX runtime manager, or admitted VMX backend path was introduced.

### Progress 2026-05-27 task `234`

The residual forbidden-authority projection carrier pool is closed.

- `VmcsV2Header` no longer exposes `MarkLaunched`, `ResetLaunchState`, or `AdvanceInvalidationEpoch`; `IsLaunched` and `InvalidationEpoch` are read-only compatibility metadata returning `false` and `0`.
- `VmcsV2Descriptor.RecordVectorExceptionExit`, `RecordStreamDescriptorFaultExit`, and `RecordStreamReplayRequiredExit` no longer advance a VMCS-owned header epoch.
- `ChildDomainIntentDescriptor` no longer owns a field dictionary, generation counter, write API, raw field access, field snapshots, or snapshot restore.
- `ChildDomainIntentAccessPolicy` is immutable compatibility policy data; default L1-visible field evaluation is preserved without mutable bitmap authority.
- `ChildDomainIntentDescriptor.TryReadIntentField` remains as a fail-closed compatibility probe and returns `VmFail` unless a future neutral runtime-owned nested intent state exists.
- Projection inventory now reports `30` files total: `4` generated-lineage, `23` contract-only, `3` denied-only, and `0` forbidden-authority.
- No runtime owner, VMCS manager, VMCS field store, active pointer, VMX runtime manager, or admitted VMX backend path was introduced.

### Progress 2026-05-27 task `235`

The first admitted VMX compatibility path is closed as a narrow VMREAD projection-admission slice.

- Added `Core/VMX/Compatibility/Frontend/Handlers/VmxCompatibilityAdmissionService.cs`.
- The path is `VMREAD` decode -> frozen alias projection -> `RuntimeBoundaryAdmissionService` -> denied `TryReadScalarField` compatibility ABI.
- Runtime admission uses `DomainRuntimeOperationKind.ReadCompatibilityProjection`, `CapabilityBoundaryRequirement.None`, and `EvidenceBoundaryRequirement.GuestVisible(EvidenceVisibilityClass.CompatibilityAlias)`.
- Added `VmxFirstAdmittedCompatibilityPathTests` to prove admission succeeds only with compatibility-alias evidence policy, fails before runtime when projection evidence is not validated, and contains no VMCS manager/backend markers.
- Updated retained-surface test-local evidence so the new production caller is explicitly marker-checked.
- No VMCS manager, VMCS field store, active pointer, VMX runtime manager, success retire factory, or VMX backend path was introduced.

### Progress 2026-05-28 task `236`

The neutral trap result split is closed before any admitted VMCALL/trap/intercept path.

- Added neutral runtime trap result vocabulary under `Core/Runtime/Events/Traps`: `TrapRequest` and `NeutralTrapResult`.
- `TrapPolicyBitmap.Evaluate` and `SchedulingBudgetTimer.TryConsumeExpired` now return `NeutralTrapResult`, not VMX `TrapDecision`.
- Added `VmxTrapProjectionMapper` under the VMX compatibility frontend. It is the explicit boundary that maps neutral trap result kinds to `VmExitReason`, `VmxExitQualification`, and `TrapDecision`.
- VMX-shaped convenience aliases for `ForVmxOperation` / `EnableVmxOperation` remain in the projection mapper file as compatibility vocabulary only.
- Added `VmxNeutralTrapResultSplitTests` to prove neutral runtime trap files do not depend on `VmExitReason`, `VmxExitQualification`, or `TrapDecision`, and production VMX retire callers still use fail-closed effects rather than intercept/VMCALL success factories.
- No VMCALL backend, VMCS manager, VMCS field store, active pointer, renamed VMX runtime manager, or successful VMX intercept path was introduced.

### Progress 2026-05-28 task `237`

The admitted-denied VMCALL/trap projection path is closed without backend success.

- Added `ProjectCompatibilityTrap` as a neutral `DomainRuntimeOperationKind` projection-only admission kind.
- Extended the generated compat alias schema/map with frozen `Opcode/VMCALL -> VmxTrapProjectionMapper.Project` lineage; the build-time projection lineage verifier accepts the regenerated map.
- Added `VmxCompatibilityAdmissionService.AdmitVmCallTrapProjection`.
- The path is `VMCALL` decode -> frozen alias projection -> `RuntimeBoundaryAdmissionService` -> neutral trap policy/bitmap -> `NeutralTrapResult` -> `VmxTrapProjectionMapper`.
- The admitted result is `TrapProjectionDeniedBackend`: it produces a projected VMX trap decision but does not execute VMCALL, publish a successful intercept retire effect, or create a VMCS backend.
- Added `VmxAdmittedDeniedVmCallTrapPathTests` to prove admission, evidence-policy denial, projection-evidence denial, neutral-policy denial, and source fences against VMCS manager/backend markers.

### Progress 2026-05-28 task `238`

The retire/completion publication fence for admitted-denied trap projection is closed without backend success.

- Added neutral `TrapCompletionPublicationFence` and `TrapCompletionPublicationFenceResult` under `Core/Runtime/Completion/Records`.
- The VMCALL trap admission result now carries `PublicationFence`; admitted-denied VMCALL returns `DeniedBackendExecution`, with both completion and retire publication denied.
- `CompletionRecord.TryFromCompatibilityExit` and `FromCompatibilityExit` now require the neutral fence before producing a VMX-compatible completion record.
- `CompletionProjectionService` projects only explicit `CompatibilityExit` records; neutral trap records with VMX-looking reason codes do not become VMX exit authority.
- `VmxRetireEffect.InterceptExit` now requires the neutral fence and fail-closes to `SecurityPolicyViolation` when the fence denies publication.
- Added `VmxTrapProjectionPublicationFenceTests` to prove the denied publication path, the neutral reason-code fence, the permit-only compatibility completion path, and production caller fences.

### Progress 2026-05-28 task `239`

The external `audit4.md` reconciliation slice is closed for quick cleanup and backlog import.

- Analyzed `audit4.md`; it confirms the current architecture direction: VMX is a frozen compatibility frontend over neutral runtime/domain owners, not feature-complete VMX backend execution.
- Closed the quick project-shape task called out by audit4: removed empty `CloseToHSL\Core\Virtualization\Substrate\*` folder placeholders from `HybridCPU_ISE.csproj`.
- No substrate authority was moved under `CloseToHSL/Core/Virtualization`; neutral authority remains under `CloseToHSL/Core/Runtime/*`.
- Imported the remaining audit4 open tasks into this `audit3.md`; closure `248` later imports the audit5 residual backlog into the same active section.
- No VMREAD value path, VMCALL backend, hypercall owner, nested backend, VMCS manager, VMCS field store, active pointer, or VMX runtime manager was introduced.

### Progress 2026-05-28 task `240`

The generated read-only VMREAD value projection slice is closed for completion-owned fields only.

- Added `VmcsReadOnlyValueProjectionService` under the VMX compatibility frontend projection scope.
- `VmxCompatibilityAdmissionService.AdmitVmReadProjection` now continues after decode, frozen alias projection, and `RuntimeBoundaryAdmissionService` into generated `VmcsFieldProjectionSchema` owner lookup.
- The only value-producing fields in this slice are `ExitReason`, `ExitQualification`, `GuestPhysicalAddress`, and `EptViolationQualification`.
- Values come from neutral `CompletionRecord` through `CompletionProjectionService` and require an admitted compatibility projection source.
- `GuestPc`, `GuestSp`, `GuestFlags`, `GuestCr0`, `GuestCr3`, `GuestCr4`, host aliases, memory aliases, and compatibility-control aliases remain denied until a neutral owner exposes a field value source.
- `VmxCompatibilityAdmissionService` no longer falls back to `TryReadScalarField` for admitted VMREAD value projection; `VmcsV2Descriptor.TryReadScalarField` remains only a denied compatibility ABI.
- No VMCS field store, active VMCS pointer, `VmcsManager`, `IVmcsManager`, `VmxExecutionUnit`, VMX runtime manager, or successful VMX backend execution was introduced.
- Added `VmxGeneratedReadOnlyVmReadValueProjectionTests` and updated active projection inventory evidence to include the new projection service.

### Progress 2026-05-29 task `241`

The next VMREAD neutral value-source expansion is closed for a narrow memory-owned slice.

- Added neutral `MemoryDomainReadOnlyTranslationView` under `Core/Runtime/Memory/Translation` and an explicit `MemoryDomainDescriptor.TryCreateReadOnlyTranslationView()` value-source gate.
- `VmxCompatibilityAdmissionService.AdmitVmReadProjection` now passes the admitted runtime context memory descriptor into `VmcsReadOnlyValueProjectionService`; no VMCS projection object or scalar field cache is consulted.
- `VmcsReadOnlyValueProjectionService` projects only `GuestCr3` from `MemoryDomainTranslationControl.AddressSpaceRoot` and `EptPointer` from owned `MemoryDomainTranslationControl.SecondStageRoot` when generated schema owner metadata says `MemoryDomainDescriptor`.
- `GuestCr3` also requires `GuestArchitecturalState` evidence permission after runtime admission; `EptPointer` requires compatibility-alias evidence.
- `HostCr3`, `Vpid`, `Cr3TargetCount`, execution-owned fields, and compatibility-control fields remain denied/fail-closed until a neutral owner exposes an explicit read-only value source.
- All writes remain denied; no VMCS field store, active VMCS pointer, `VmcsManager`, `IVmcsManager`, `VmxExecutionUnit`, VMX runtime manager, or successful VMX backend execution was introduced.
- Added `VmxMemoryOwnedVmReadValueProjectionTests` to prove memory-owned projection values come from neutral descriptor state, field-specific evidence is enforced, invalid translation control is denied, and unsupported memory-owned fields stay closed.

### Progress 2026-05-29 task `242`

The VPID neutral-semantics slice is closed, and compatibility-control ownership is designed without opening control fields.

- `VmcsReadOnlyValueProjectionService` now projects `Vpid` only from neutral `MemoryDomainReadOnlyTranslationView.AddressSpaceTag`.
- VPID projection requires generated schema owner metadata to be `MemoryDomainDescriptor`, runtime admission to be allowed, compatibility-alias evidence to be open, valid memory translation control, `AddressSpaceTaggingEnabled == true`, and a non-zero `AddressSpaceTag`.
- If address-space tagging is not materialized, `Vpid` remains denied/fail-closed with `MemorySourceDenied`; no zero/fake VPID value is projected.
- Added neutral `CompatibilityControlDescriptor` under `Core/Runtime/Capabilities/CompatibilityControls` with an explicit `TryCreateReadOnlyControlView()` materialization gate.
- Compatibility-control VMREAD fields (`PinBasedControls`, `ProcBasedControls`, `ExitControls`, `EntryControls`, `SecondaryProcControls`) remain denied/fail-closed because the VMREAD value projection service does not admit a compatibility-control value source yet.
- `HostCr3`, `Cr3TargetCount`, execution-owned fields, and unmaterialized control fields remain denied/fail-closed.
- Added `VmxCompatibilityControlOwnerDesignTests` and extended `VmxMemoryOwnedVmReadValueProjectionTests` to prove VPID source semantics, control-field denial, and absence of VMCS field store fallback.
- All writes remain denied; no VMCS field store, active VMCS pointer, `VmcsManager`, `IVmcsManager`, `VmxExecutionUnit`, VMX runtime manager, or successful VMX backend execution was introduced.

### Progress 2026-05-29 task `243`

The neutral compatibility-control owner materialization step is closed without opening control VMREAD fields.

- `CompatibilityControlDescriptor` now materializes explicit fail-closed neutral semantics through `CompatibilityControlReadOnlyView.FailClosedProjectionOnly`.
- The materialized view names neutral policy categories only: runtime trap/result/fence routing, runtime admission, read-projection-only execution, write denial, backend-execution denial, neutral completion/publication requirements, entry/admission validation requirements, nested-intent and memory-owner requirements, and denied control-value projection.
- `CompatibilityControlDescriptor.FromNeutralSemantics()` only reports `ReadOnlyProjectionAvailable` when the view is semantically complete.
- `VmcsReadOnlyValueProjectionService` still does not map `VmcsFieldProjectionOwner.CompatibilityControlDescriptor` to any control-bit value source; generated control fields remain denied.
- `PinBasedControls`, `ProcBasedControls`, `ExitControls`, `EntryControls`, and `SecondaryProcControls` remain denied/fail-closed despite the materialized neutral owner.
- No VMCS field store, active VMCS pointer, control-field bit projection, `VmcsManager`, `IVmcsManager`, `VmxExecutionUnit`, VMX runtime manager, or successful VMX backend execution was introduced.

### Progress 2026-05-29 task `245`

The control-field fork is resolved in favor of keeping controls denied.

- `VmcsReadOnlyValueProjectionService` now returns the explicit `CompatibilityControlValueProjectionDenied` decision for `VmcsFieldProjectionOwner.CompatibilityControlDescriptor`.
- `PinBasedControls`, `ProcBasedControls`, `ExitControls`, `EntryControls`, and `SecondaryProcControls` remain denied after runtime admission and generated owner lookup.
- No control-bit mapper, fake zero value, VMCS field store, active VMCS pointer, or VMCS/VMX runtime authority was introduced.
- `VmxCompatibilityControlOwnerDesignTests` prove that materialized neutral control semantics do not imply a frozen VMX control-bit value contract.

### Progress 2026-05-29 task `246`

The runtime-owned trap completion route design step is closed before any real VMCALL/intercept publication.

- Added neutral `TrapCompletionRouteDescriptor`, `TrapCompletionRouteRequest`, `TrapCompletionRouteResult`, and `TrapCompletionRouteService` under `Core/Runtime/Completion/Routing`.
- The route service authorizes trap completion publication only after runtime admission, a neutral trap result, runtime-owned route authority, domain validation, backend execution authorization, completion publication permission, and retire publication permission.
- The admitted VMCALL projection path now reports a `TrapCompletionRouteResult`, but uses `TrapCompletionRouteDescriptor.ProjectionOnlyDenied`, so it remains `DeniedBackendExecution`.
- `TrapCompletionRouteDescriptor.RuntimeOwnedPublication` exists only as a neutral route contract and is not used by the VMX compatibility frontend.
- No successful VMCALL/intercept publication, compatibility exit completion, backend execution, VMCS state store, `VmxExecutionUnit`, `VmcsManager`, or VMX-owned route authority was introduced.

### Progress 2026-05-29 task `247`

The VMCALL/hypercall fork is resolved in favor of keeping VMCALL admitted-denied until a real neutral backend owner exists.

- Added neutral `HypercallBackendDescriptor`, `HypercallBackendAdmissionRequest`, `HypercallBackendAdmissionResult`, and `HypercallBackendAdmissionService` under `Core/Runtime/Events/Hypercalls`.
- The backend admission policy checks runtime admission, neutral trap result, runtime-owned backend authority, domain validation, typed capability requirement, and neutral evidence requirement.
- Even after those gates, backend execution remains denied with `DeniedNeutralBackendOwnerMissing` unless neutral backend owner semantics are materialized by a future closure.
- Production VMCALL compatibility admission passes `HypercallBackendAdmissionRequest.MissingNeutralOwner`, so the result is `MissingBackendDescriptor`, `BackendExecutionAuthorized == false`, route `DeniedBackendExecution`, and fence `DeniedBackendExecution`.
- No successful VMCALL backend execution, compatibility exit completion, intercept retire publication, VMCS state store, `VmxExecutionUnit`, `VmcsManager`, or VMX-owned backend authority was introduced.

### Progress 2026-05-29 task `248`

The next honest heavy step is closed as another narrow VMREAD value-source expansion, not as a real hypercall backend.

- No concrete neutral hypercall backend operation semantics exist yet, so production VMCALL remains on `HypercallBackendAdmissionRequest.MissingNeutralOwner`.
- `MemoryDomainTranslationControl` now exposes neutral `AddressSpaceTargetCount`, with `MemoryDomainTranslationControl.MaxAddressSpaceTargetCount` as the fail-closed validation cap.
- `MemoryDomainReadOnlyTranslationView` carries that count as read-only neutral memory-domain metadata.
- `VmcsReadOnlyValueProjectionService` projects frozen compatibility field `Cr3TargetCount` only when generated schema metadata says `MemoryDomainDescriptor`, runtime admission has already succeeded, compatibility-alias evidence is allowed, and the neutral memory descriptor materializes a valid read-only translation view.
- `AddressSpaceTargetCount == 0` is a valid neutral "no materialized targets" value; invalid counts are denied by the neutral memory view gate.
- `HostCr3`, execution-owned fields, compatibility-control fields, unknown fields, and all writes remain denied/fail-closed.
- No VMCS field store, active VMCS pointer, `VmcsManager`, `IVmcsManager`, `VmxExecutionUnit`, VMX runtime manager, control-bit mapper, or successful VMX/VMCALL backend execution was introduced.
- `audit4.md` and `audit5.md` open work has been imported into this file as the active backlog; the older audit files remain historical evidence and local guardrails.

### Progress 2026-05-29 task `249`

The HostCr3 fork is resolved in favor of keeping it denied until a neutral host-address-space owner exists.

- No neutral host-address-space owner or read-only host-root value source exists in the current runtime model.
- `VmcsReadOnlyValueProjectionService` now returns explicit `HostAddressSpaceOwnerMissing` for `VmcsField.HostCr3`.
- The denial occurs after runtime admission, generated schema owner lookup, and alias/evidence validation, but before guest/domain translation view materialization.
- Valid or invalid guest/domain translation state cannot become a `HostCr3` source or denial reason.
- `HostCr3` is intentionally not mapped in `TryProjectMemoryField`; `AddressSpaceRoot` remains `GuestCr3`/domain translation state, not host CR3 authority.
- No VMCS field store, active VMCS pointer, `VmcsManager`, `IVmcsManager`, `VmxExecutionUnit`, VMX runtime manager, host-address fake value, or successful VMREAD backend was introduced.

### Progress 2026-05-29 task `250`

The execution-owned VMREAD value-source step is closed for the safe architectural-state slice only.

- Added neutral `ExecutionDomainReadOnlyStateView` under `Core/Runtime/Domains/Descriptors/ExecutionDomain`.
- `ExecutionDomainDescriptor` now exposes `TryCreateReadOnlyStateView()` as the only execution-owned VMREAD value source.
- `VmcsReadOnlyValueProjectionService` projects `GuestPc`, `GuestSp`, and `GuestFlags` only after runtime admission, generated schema owner lookup, guest-architectural-state evidence, and materialized neutral execution state.
- A default or unmaterialized execution descriptor returns `ExecutionSourceMissing` or `ExecutionSourceDenied`; there is no fallback to VMCS projection blocks, scalar fields, or cached guest state.
- `GuestCr0` and `GuestCr4` remain denied with `PrivilegedExecutionStateProjectionDenied` until neutral privileged execution-state semantics are designed.
- No VMCS field store, active VMCS pointer, `VmcsManager`, `IVmcsManager`, `VmxExecutionUnit`, VMX runtime manager, legacy helper, or successful VMREAD backend was introduced.

### Progress 2026-05-29 task `251`

The follow-up execution-owned VMREAD fork is resolved in favor of explicit denial for host execution aliases.

- `GuestCr0` and `GuestCr4` remain denied; no neutral privileged execution-state semantics were invented.
- `HostPc`, `HostSp`, `HostFlags`, and `HostCr0` now return explicit `HostExecutionStateOwnerMissing`.
- The denial occurs after runtime admission, generated schema owner lookup, and compatibility-alias evidence, but before guest read-only state view materialization.
- A materialized `ExecutionDomainReadOnlyStateView` cannot become a host execution-state source.
- No host fake value, VMCS field store, active VMCS pointer, VMCS manager, `VmxExecutionUnit`, or successful backend VMREAD was introduced.

### Progress 2026-05-29 task `252`

The remaining control-like VMREAD fields are now guarded as an explicit fail-closed conformance slice.

- No new VMREAD value projection was opened.
- `GuestCr0` and `GuestCr4` remain `PrivilegedExecutionStateProjectionDenied`.
- `HostCr0` remains `HostExecutionStateOwnerMissing`; `HostCr3` remains `HostAddressSpaceOwnerMissing`.
- `PinBasedControls`, `ProcBasedControls`, `ExitControls`, `EntryControls`, and `SecondaryProcControls` remain `CompatibilityControlValueProjectionDenied`.
- The control-like schema entries remain read-only and write-denied; all writes remain denied.
- The test fence proves opened neutral sources (`ExecutionDomainReadOnlyStateView`, `MemoryDomainReadOnlyTranslationView`, and `CompatibilityControlDescriptor`) are not reused as missing control-like authority.

### Progress 2026-05-30 task `253`

The descriptor readiness policy audit is closed as a fail-closed conformance slice.

- No production readiness authority was added.
- `VmcsV2Descriptor.ValidateMigrationReadiness()` and `ValidateNestedEnablementReadiness()` remain fail-closed on missing materialized guest GPR state.
- An admitted `GuestPc` VMREAD value projection does not make VMCSv2 migration or nested readiness successful.
- Restore readiness rejects compatibility-projection checkpoints and compatibility projection metadata as authoritative state.
- Migration readiness requires explicit guest-state preserve policy and rejects host-owned evidence.
- `NestedDomainProjectionCheckpointService` still requires neutral nested projection, checkpoint image, migration policy, and restore policy before allowing nested restore readiness.
- Source conformance proves readiness does not call `AdmitVmReadProjection`, `VmcsReadOnlyValueProjectionService`, `TryReadScalarField`, VMCS field-store APIs, VMCS managers, or VMX execution units.

### Progress 2026-05-30 task `254`

The migration/evidence proof for recomputed compatibility fields is closed.

- No production migration/checkpoint authority was added.
- Completion-owned VMREAD fields (`ExitReason`, `ExitQualification`, `GuestPhysicalAddress`, `EptViolationQualification`) are fenced as `RecomputedCompletion`, `CompletionRecord`-owned, compatibility-alias, read-only, and write-denied.
- `MigrationPayloadClass` has no payload class for completion projection values, VMCS fields, `VmExitReason`, `CompletionRecord`, or `VmxCompletionProjection`.
- Neutral guest architectural state can restore only through `DomainCheckpointImage`, `MigrationValidationPolicy`, `RestoreValidationService`, and `EvidenceRestorePolicy.PreserveGuestArchitecturalState`.
- Compatibility projection metadata and compatibility-projection checkpoint authority are rejected as restore authority.
- Host-owned runtime evidence remains recompute-only and is rejected from checkpoint restore.
- Source conformance proves migration/checkpoint/evidence code does not depend on `CompletionRecord`, `CompletionProjectionService`, `VmxCompletionProjection`, `VmcsReadOnlyValueProjectionService`, VMCS field APIs, VMCS managers, or VMX execution units.

### Progress 2026-05-30 task `255`

The execution-owned VMREAD audit follow-up is closed as snapshot hardening, not as another VMREAD opening.

- The external audit's Step A is already implemented: `GuestPc`, `GuestSp`, and `GuestFlags` project only from neutral `ExecutionDomainReadOnlyStateView` after runtime admission, schema owner lookup, and guest-architectural-state evidence.
- `ExecutionDomainReadOnlyStateView` now carries explicit materialization metadata: `IsMaterialized`, `HasCompleteGuestPcSpFlags`, and neutral `StateEpoch`.
- `StateEpoch` is descriptor metadata only; `VmcsReadOnlyValueProjectionService` does not project it as a VMREAD value.
- The view intentionally has no `GuestCr0`, `GuestCr4`, host PC/SP/flags, or host control-register properties.
- `GuestCr0` and `GuestCr4` remain denied with `PrivilegedExecutionStateProjectionDenied` until a separate neutral privileged execution-state owner/value source exists.
- No VMCS field store, active VMCS pointer, VMCS manager, `VmxExecutionUnit`, legacy helper, or successful backend VMREAD path was introduced.

## Recommended next work order

1. **Next VMREAD neutral value-source expansion**
   Add more VMREAD fields only when `ExecutionDomainDescriptor`, `MemoryDomainDescriptor`, or a neutral compatibility-control owner exposes an explicit read-only value source. Closures `241`, `242`, and `248` admit `GuestCr3`, `EptPointer`, `Vpid`, and `Cr3TargetCount`; closure `249` keeps `HostCr3` explicitly denied until a separate neutral host-address-space owner exists; closure `250` admits only `GuestPc`, `GuestSp`, and `GuestFlags` from neutral execution state; closure `251` keeps `HostPc`, `HostSp`, `HostFlags`, and `HostCr0` denied until a neutral host-execution owner exists; closure `252` keeps remaining control-like fields denied unless a real neutral owner/value source appears; closure `255` hardens the execution snapshot metadata while keeping privileged fields denied. Do not infer values from VMCS projection objects.

2. **Controls stay denied unless a separate neutral control-bit contract is designed**
   The current choice is to keep `PinBasedControls`, `ProcBasedControls`, `ExitControls`, `EntryControls`, and `SecondaryProcControls` denied with `CompatibilityControlValueProjectionDenied`. A mapper is not part of the active path; it would require a new field-by-field neutral control-bit value contract and conformance before any control VMREAD value opens.

3. **Materialize a real neutral hypercall backend owner only when semantics exist**
   `HypercallBackendAdmissionService` now makes the missing owner explicit and fail-closed. Future successful VMCALL work must add neutral runtime operation semantics, a typed capability contract, evidence policy, and then switch production from `MissingNeutralOwner` to a runtime-owned descriptor before any `TrapCompletionRouteDescriptor.RuntimeOwnedPublication` route is used.

4. **Neutral nested intent owner design**
   Only if needed by a real admitted nested path, introduce child-intent state under neutral `Core/Runtime/Nested/*` and project it through VMX compatibility as read-only/denied vocabulary.

5. **Post-proof migration hardening**
   Closure `253` keeps descriptor readiness fail-closed and proves readiness does not consume VMREAD projection values. Closure `254` proves recomputed completion-owned compatibility fields are not checkpoint payload authority. Future migration work should only add neutral materialized descriptor/policy payloads when the runtime owner exposes explicit serializable state.

## Audit4/audit5 imported open backlog

The following open items are imported from `audit4.md` and `audit5.md` after closures `239`-`248`.
They remain open heavy work unless explicitly closed by a later success closure.

1. **Generated read-only VMREAD value projection**
   Closed for the completion-owned slice in task `240`: `ExitReason`,
   `ExitQualification`, `GuestPhysicalAddress`, and
   `EptViolationQualification`. Closed for the first memory-owned slice in
   task `241`: `GuestCr3` from neutral `AddressSpaceRoot` and `EptPointer`
   from neutral `SecondStageRoot`. Closed for VPID in task `242`: `Vpid` from
   neutral `AddressSpaceTag` only when tagging is materialized. Closed for
   task `248`: `Cr3TargetCount` from neutral `AddressSpaceTargetCount`.
   Residual expansion remains open only for fields whose neutral owner exposes
   a real value source. Closure `249` converts `HostCr3` into explicit
   `HostAddressSpaceOwnerMissing` denial until a separate neutral host-address
   owner exists. Closure `250` opens only the execution-owned read-only
   architectural-state slice `GuestPc`, `GuestSp`, and `GuestFlags` from
   `ExecutionDomainReadOnlyStateView`. Closure `251` makes host execution
   aliases explicit denied fields with `HostExecutionStateOwnerMissing`.
   Closure `252` fences remaining control-like fields as denied unless a real
   neutral owner/value source exists. Current explicit residuals are no longer
   generic control-like openings; they are fail-closed decisions awaiting
   separate neutral semantics.
   Do not create a VMCS field store.

2. **Real neutral VMCALL / hypercall owner**
   Closure `247` adds fail-closed backend admission and keeps production on
   `MissingNeutralOwner`. Future VMCALL success still requires materialized
   neutral hypercall/trap owner semantics, typed capability, evidence policy,
   neutral completion record, allowed publication fence, allowed retire
   publication, and then VMX-compatible projection. `VmExitReason.VmCall`
   must not be authority.

3. **Nested neutral child-intent owner**
   Continue nested only through neutral child-domain intent descriptor,
   capability filter, nested memory composition, nested evidence policy, runtime
   admission, and then VMX projection. Shadow VMCS must not become mutable
   runtime state.

4. **Descriptor readiness policy audit**
   Closed by task `253` as fail-closed conformance. Migration/nested
   readiness remains denied until neutral generated projection supplies
   materialized state and conformance evidence; readiness does not consume
   VMREAD projection values, VMCS scalar stores, or compatibility metadata.

5. **Admitted-denied naming and tests**
   Continue strengthening the rule that admitted-denied projection is not
   success. Result names, tests, and docs should make backend-denied states
   unambiguous.

6. **Feature completeness remains open**
   Successful VMX backend execution, successful VMX backend publication, and
   feature-complete nested compatibility execution remain open beyond the current
   admitted-denied projection slices.

7. **Migration/evidence proof for recomputed compatibility fields**
   Closed by task `254`. Checkpoint/migration images can carry neutral
   guest-visible state through migration/evidence policy, but they do not carry
   VMCS projection data as authority, host evidence, or recomputed completion
   fields as independent state owners.

8. **VMWRITE remains closed until a separate neutral write owner exists**
   Any future writable compatibility surface must route through generated schema,
   a neutral owner write request, runtime admission, capability/evidence policy,
   and retire publication. `VMWRITE -> VMCS field store` remains forbidden.

9. **Generated/projection inventory guardrail**
   Every new file under `Compatibility/Generated/*` or
   `Compatibility/Frontend/Projection/*` must be classified as generated-lineage,
   contract-only, or denied-only, and must not become a runtime owner, VMCS field
   store, VMX manager, backend authority, or host-evidence store.

## Current final status

```text
Security posture: strong.
Compatibility frontend freeze: declared.
Production dependency on Legacy/VMX: absent.
Physical Legacy/VMX directory: absent.
Runtime authority ownership: neutral Core/Runtime owners.
Legacy production cleanup: exhausted.
Conformance evidence cleanup: closed for physical Legacy/VMX/Conformance deletion.
Conformance manifest/fast-removal/retained-freeze/capability-substrate/frozen-alias/nested-composition/VMCS-shadow-manager/IOMMU/execution-unit evidence dependencies: test-local or obsolete.
Conformance source count: 0.
VMCSv2 mutable helper cleanup: strong hardening; descriptor guest-state/host-evidence, root/NPT/bundle/event/debug mutators, residual vector/dirty/security/capability backing state removed/fenced, and `ExitInfoBlock.Record*` internal retire-only.
Projection inventory: closed for generated/frontend projection scope including the VMREAD value projection service; 0 forbidden-authority extraction targets remain.
First admitted VMX compatibility path: closed for VMREAD admission and admitted generated read-only value projections after RuntimeBoundaryAdmissionService.
Neutral trap result split: closed; VMX exit reason projection is explicit mapper-only compatibility vocabulary.
Admitted-denied VMCALL trap projection: closed through RuntimeBoundaryAdmissionService -> NeutralTrapResult -> VmxTrapProjectionMapper, with backend denied.
Trap projection publication fence: closed; admitted-denied VMCALL carries a neutral publication fence and cannot publish VMX completion or intercept retire effects.
Audit4/audit5 quick cleanup: closed for empty Virtualization/Substrate project placeholders and active backlog import.
Descriptor readiness policy audit: closed fail-closed; migration/nested readiness does not derive from VMREAD projection values or VMCS projection metadata.
Migration/evidence proof for recomputed compatibility fields: closed; completion-owned VMREAD values are recomputed projection only and not checkpoint payload authority.
Execution snapshot hardening: closed; `ExecutionDomainReadOnlyStateView` now exposes materialization/epoch metadata without making `StateEpoch`, `GuestCr0`, or `GuestCr4` VMREAD values.
Admitted VMX execution completeness: open beyond narrow admitted projection/value slices.
Nested compatibility execution completeness: open.
```

This audit supersedes older "freeze premature" wording for the compatibility frontend. It does not supersede the rule that VMX is only a frozen compatibility frontend over neutral runtime/domain owners.
