# VMX Current Model Completion Audit

Date: 2026-05-28
Scope: current VMX model after closures `159`-`237`.

## Executive Status

VMX compatibility frontend freeze is declared for the current compiled frontend/projection surface.

This is not a declaration that VMX is the virtualization architecture. VMX remains a frozen compatibility frontend over neutral runtime/domain owners.

The current source of truth is:

- `Core/Runtime/Domains/*` for domain descriptors, admission, legality, scheduling, binding, validation, and runtime context;
- `Core/Runtime/Capabilities/*` for typed capability grants, descriptor sets, negotiation, and publication;
- `Core/Runtime/Memory/*` and `Memory/MMU/*` for memory identity, translation, invalidation, IOTLB, and host I/O-domain mechanics;
- `Core/Runtime/IO/*` for neutral I/O/DMA/IOTLB ownership;
- `Core/Runtime/Lanes/*` for Lane6/Lane7/vector-stream runtime state and host-owned evidence;
- `Core/Runtime/Nested/*` for nested descriptors, projection, checkpoint, and validation;
- `Core/Runtime/Completion/*` plus pipeline retire code for completion/retire publication;
- `Core/VMX/Compatibility/*` for VMX ABI vocabulary, generated/read-only projection, denied aliases, fail-closed opcode/retire compatibility, and projection metadata;
- `HybridCPU_ISE.Tests/VmxRefactoring/*Evidence*` for historical/static/file evidence that must not become production authority.

## Current Facts

- `Core/VMX/Substrate` contains no C# sources.
- `Core/VMX` contains no legacy-marked C# source.
- `Legacy/VMX/Compatibility` contains no C# sources.
- `Legacy/VMX/Conformance` contains `0` C# sources and no longer exists physically.
- Total `Legacy/VMX` C# source count is `0`.
- Physical `Legacy/VMX` is absent.
- Physical `Legacy/VMX-v2` is absent.
- Production builds without physical `Legacy/VMX`.
- Production builds without physical `Legacy/VMX/Conformance`.
- Tests build without physical `Legacy/VMX/Conformance`; deletion is no longer blocked by direct compiled evidence dependencies.
- Tests no longer compile-depend on `LegacyVmxQuarantineManifest`, `LegacyVmxQuarantineEntry`, or `LegacyReverseImportRequest` for quarantine-manifest evidence; that evidence is now test-local static path evidence.
- Tests no longer require the first fast removal evidence contract sources in `Legacy/VMX/Conformance`; those seven sources were deleted in closure `224`.
- Tests no longer require the retained-surface and freeze-readiness evidence contract sources in `Legacy/VMX/Conformance`; those two sources were deleted in closure `226`.
- Tests no longer require the capability/substrate extraction and frozen-alias quarantine evidence contract sources in `Legacy/VMX/Conformance`; those three sources were deleted in closure `227`.
- The nested-composition conformance sources were deleted in closure `228`; static references now resolve through test-local evidence for `NestedDomainProjectionCheckpointOwnerContract`.
- The VMCS/shadow/manager, IOMMU-return, reverse-import, remaining execution-unit, and runtime-epoch conformance sources were deleted in closure `229`; static references now resolve through test-local evidence or were obsolete.
- Full `Legacy/VMX/Conformance` move-away verification before deletion passed with production build exit `0` and tests build exit `0`.
- `VmcsV2Descriptor` no longer exposes dead guest-state or host-evidence mutator helpers after closure `225`.
- `VirtualCpuBlock` no longer exposes VMCS-owned guest register capture/materialization helpers after closure `225`.
- `VmcsV2Blocks` no longer exposes VMCS-owned root/NPT/bundle/event/debug public mutator helpers after closure `230`.
- `VmcsV2Blocks` no longer exposes residual vector/dirty/security/capability private-set backing state after closure `231`; `ExitInfoBlock.Record*` is internal and descriptor-mediated as retire-publication-only projection.
- `Core/VMX/Compatibility/Generated/*` and `Core/VMX/Compatibility/Frontend/Projection/*` are now covered by an exact test-local projection inventory after closures `232`-`234` and `240`: `32` files total and `0` forbidden-authority extraction targets.
- `SchedulingBudgetTimer.cs` and `TrapPolicyBitmap.cs` were deleted from `Core/VMX/Compatibility/Frontend/Projection/Events` and rehomed under `Core/Runtime/Events/Traps` in closure `233`.
- `VmcsV2Header` launch/invalidation epoch mutators and `ChildDomainIntentDescriptor` child-intent field store/write/snapshot state were removed in closure `234`.
- Closure `235` added the first narrow VMX compatibility runtime admission path: `VMREAD` projection admission through `RuntimeBoundaryAdmissionService`, gated by compatibility-alias evidence policy.
- Closure `236` split neutral trap results from VMX exit projection before any admitted VMCALL/trap/intercept path. Runtime trap policy now returns `NeutralTrapResult`; VMX `TrapDecision`, `VmExitReason`, and exit qualification are produced only by `VmxTrapProjectionMapper` in the compatibility frontend.
- Closure `237` added admitted-denied VMCALL trap projection through `RuntimeBoundaryAdmissionService`, neutral `NeutralTrapResult`, and `VmxTrapProjectionMapper`, with backend execution still denied.
- Closure `238` added a neutral retire/completion publication fence for that VMCALL trap projection. The projected trap result cannot become a VMX completion record or successful intercept retire effect while backend publication is denied.
- Closure `240` added generated read-only VMREAD value projection for completion-owned fields only: `ExitReason`, `ExitQualification`, `GuestPhysicalAddress`, and `EptViolationQualification`, sourced from neutral `CompletionRecord` via `CompletionProjectionService` after runtime admission.
- Closure `241` added the first memory-owned generated read-only VMREAD value projection for `GuestCr3` and `EptPointer`, sourced only from neutral `MemoryDomainDescriptor.TryCreateReadOnlyTranslationView()` over `MemoryDomainTranslationControl.AddressSpaceRoot` and owned `SecondStageRoot` after runtime admission and schema owner lookup.
- Closure `242` defines VPID neutral semantics: `Vpid` VMREAD projects only from neutral `MemoryDomainReadOnlyTranslationView.AddressSpaceTag` when address-space tagging is enabled and non-zero; otherwise it remains denied. The same closure adds a neutral `CompatibilityControlDescriptor` skeleton, but control fields remain denied until a materialized neutral control view exists.
- Closure `243` materializes `CompatibilityControlDescriptor` with explicit fail-closed neutral policy semantics, while leaving all control VMREAD fields denied because no compatibility control-field mapper is admitted.
- Closure `245` resolves the control-field fork by keeping control VMREAD fields denied explicitly. `VmcsReadOnlyValueProjectionService` now returns `CompatibilityControlValueProjectionDenied` for `CompatibilityControlDescriptor`-owned fields instead of treating them as a generic missing source.
- Closure `246` adds the neutral runtime-owned trap completion route design. `TrapCompletionRouteService` authorizes publication only from runtime-owned route descriptors after runtime admission, neutral trap result, domain validation, backend execution authorization, completion-publication permission, and retire-publication permission. The VMCALL compatibility path uses only `ProjectionOnlyDenied`, so no real publication opens.
- Closure `247` adds a neutral hypercall backend admission policy and chooses the clean fail-closed option: production VMCALL passes `MissingNeutralOwner`, gets `HypercallBackendAdmissionDecision.MissingBackendDescriptor`, and remains admitted-denied with no backend execution, completion publication, or intercept retire publication.
- Closure `248` continues the honest VMREAD value-source path instead of opening hypercall backend success. `Cr3TargetCount` now projects only from neutral `MemoryDomainReadOnlyTranslationView.AddressSpaceTargetCount` after runtime admission, generated schema owner lookup, compatibility-alias evidence, and neutral memory translation validation; `HostCr3` and execution/control fields remain denied.
- Closure `249` keeps `HostCr3` denied with an explicit `HostAddressSpaceOwnerMissing` decision because no neutral host-address-space owner or read-only host-root source exists. The denial occurs before guest/domain translation view materialization, so `AddressSpaceRoot` cannot be reused as host CR3 authority.
- Closure `250` adds the narrow execution-owned VMREAD value projection for `GuestPc`, `GuestSp`, and `GuestFlags`, sourced only from neutral `ExecutionDomainDescriptor.TryCreateReadOnlyStateView()` / `ExecutionDomainReadOnlyStateView` after runtime admission, generated schema owner lookup, and guest-architectural-state evidence. `GuestCr0` and `GuestCr4` remain denied with `PrivilegedExecutionStateProjectionDenied`.
- Closure `251` keeps host execution VMREAD aliases explicitly denied. `HostPc`, `HostSp`, `HostFlags`, and `HostCr0` now return `HostExecutionStateOwnerMissing` after runtime admission and compatibility-alias evidence, before any guest read-only state view can be materialized as a source.
- Closure `252` adds a conformance fence for all remaining control-like VMREAD fields. `GuestCr0`/`GuestCr4`, `HostCr0`/`HostCr3`, and compatibility-control fields remain denied with their explicit decisions unless a real neutral owner/value source is introduced by a later closure.
- Closure `253` closes descriptor readiness policy audit as fail-closed conformance. Migration/nested readiness, restore validation, and nested checkpoint readiness do not derive from VMREAD projection values, VMCS scalar stores, or compatibility projection metadata; they require neutral materialized state, checkpoints, migration policy, and evidence policy.
- Closure `254` closes migration/evidence proof for recomputed completion-owned compatibility fields. `ExitReason`, `ExitQualification`, `GuestPhysicalAddress`, and `EptViolationQualification` are recomputed from neutral `CompletionRecord` projection only and are not checkpoint payload classes, VMCS projection state, or host-owned evidence.
- Closure `255` hardens the execution-owned snapshot source. `ExecutionDomainReadOnlyStateView` now carries explicit materialization metadata (`IsMaterialized`, `HasCompleteGuestPcSpFlags`, `StateEpoch`), while `StateEpoch`, `GuestCr0`, and `GuestCr4` remain outside VMREAD value projection.
- `VmxExecutionUnit.cs`, `VmcsManager.cs`, and `IVmcsManager.cs` remain absent.
- Test-local freeze-readiness evidence is now historical proof only; whole-folder conformance deletion is closed.

## Completed Closures

The earlier long-form audit contained detailed task-by-task closure notes. Those historical notes are now superseded by the closure files under `docs/VMXRefactoring/SuccessClosed/` and by the current invariants below.

### Removed Without Replacement

- Legacy `VmxExecutionUnit` frontend/opcode shell.
- Legacy `VmcsManager` and `IVmcsManager`.
- Dead compatibility shells:
  - `LegacyVmxIoVirtualizationBackend`;
  - `LegacyVmxTranslationInvalidationBackend`;
  - `LegacyCsrBackedVmxCapabilityDescriptorSource`;
  - `LegacyVmxV1AdapterBoundary`;
  - `LegacyVmxV2AdapterBoundary`.
- VMCS-shaped checkpoint/scalar restore helpers that made VMCSv2 a migration authority.
- VMCSv2 descriptor guest-state and host-evidence mutator helpers.
- VMCSv2 root descriptor binding, NPT control binding, bundle binding, VMCS-owned event queue/remap/delivery/snapshot helpers, and debug trace configure/record/reset helpers.
- VMCSv2 residual vector-stream, dirty-log, security-isolation, and capability-negotiation backing state in compatibility projection blocks.
- VMCSv2 header launch/invalidation epoch authority.
- Child-domain intent descriptor field dictionary, write API, raw field access, generation, and snapshot/restore state.
- VMCSv2 scalar write/cache authority.
- VMX-shaped Lane6/Lane7/vector-stream host-evidence and helper carriers that acted as runtime owners.
- Generic domain/runtime/capability/memory/I/O/nested/lane substrate previously under `Core/VMX/Substrate`.

### Retained as Frozen Compatibility Vocabulary

- `VmxInstructionPayload` under `Core/VMX/Compatibility/Frontend/Decode`.
- `VmxRetireEffect`, `VmxRetireOutcome`, and `VmxOperationKind` under `Core/VMX/Compatibility/Frontend/Retire`.
- VMCS field projection schema and aliases under generated compatibility projection.
- VmxCaps projection metadata over typed capability grants.
- Shadow VMCS nested compatibility projection bridge as denied/fail-closed projection vocabulary.
- VMX IOTLB/invalidation spellings as denied/no-effect compatibility aliases.
- VMFUNC/VM-exit/VMCS vocabulary only where it is frozen ABI/projection/conformance language.

### Generated Lineage

`VerifyVmxProjectionLineage` runs before `CoreCompile` and verifies drift for:

- VMCS field projection schema/source;
- compat alias map;
- VmxCaps capability-bit schema/source;
- compat spec artifact manifest.

Future generated/projection surfaces must either join this verifier or be explicitly marked `ProjectionContractOnly` with conformance that proves no runtime authority.

Closure `232` adds an exact projection inventory around this rule. Closure `233` removed the first two forbidden-authority projection carriers from that scope. Closure `234` reduced the remaining two to contract-only or denied-only compatibility surfaces. Closure `235` added a runtime-admitted VMREAD projection path outside the generated/projection inventory scope, with explicit conformance that it uses admission. Closure `236` adds a VMX trap projection mapper and leaves trap authority in neutral `Core/Runtime/Events/Traps` result vocabulary. Closure `237` adds `Opcode/VMCALL -> VmxTrapProjectionMapper.Project` to the generated compat alias lineage and admits only a backend-denied trap projection. Closure `238` adds the neutral completion/retire publication fence for that projection without adding a new generated/projection inventory source. Closure `240` adds `VmcsReadOnlyValueProjectionService` as a contract-only frontend projection source for completion-owned VMREAD values. The generated-lineage bucket is now mechanically tied to `GeneratedProjectionLineageBuildContract.RequiredGeneratedOutputs`; files outside that bucket must remain contract-only or denied-only unless a new extraction fence is added first.

### Test-Local Conformance Evidence

Closures `223`, `224`, `226`, `227`, `228`, and `229` decoupled test evidence dependencies from compiled production conformance contracts:

- `VmxProjectionSchemaAndQuarantineTests` now uses test-local `VmxQuarantineEvidenceManifest`, `VmxQuarantineEvidenceEntry`, and `VmxQuarantineReturnProof`.
- `VmxLegacyFastRemovalEvidenceContracts` now carries test-local static path/marker evidence for the first fast removal-contract pool.
- `VmxRetainedSurfaceAndFreezeEvidenceContracts` now carries test-local static path/marker evidence for retained compatibility surface and freeze-readiness proof.
- `VmxCapabilitySubstrateAndAliasEvidenceContracts` now carries test-local static path/marker evidence for capability/substrate extraction and frozen-alias quarantine proof.
- `VmxNestedCompositionEvidenceContracts` now carries test-local static path/marker evidence for nested projection/checkpoint ownership proof.
- `VmxVmcsShadowManagerEvidenceContracts` now carries test-local static path/marker evidence for VMCS/shadow/manager, IOMMU-return, reverse-import, and V1 execution-adapter proof.
- `LegacyVmxQuarantineManifest.cs`, six fast removal contract sources, `LegacyVmxRetainedCompatibilitySurfaceInventoryContract.cs`, `LegacyVmxFreezeReadinessCertificationContract.cs`, `CapabilityProjectionPlacementServiceSubstrateExtractionContract.cs`, `CoreVmxSubstrateResidualExtractionContract.cs`, `FinalFrozenAliasQuarantineContract.cs`, the three nested-composition conformance sources, VMCS/shadow/manager contracts, reverse-import proof, remaining execution-unit contracts, and `RuntimeEpochAdvanceFailClosedContract.cs` were deleted from `Legacy/VMX/Conformance`.
- The physical `Legacy/VMX/Conformance` folder was removed after a full move-away production/tests build probe passed; the empty `Legacy/VMX` and `Legacy/VMX-v2` directory trees were then removed after tests stopped requiring an empty compatibility root.
- This did not create a runtime owner, a VMCS field store, a manager, or a VMX backend path. It is only static path evidence for tests.

## Current Compatibility Frontend Freeze

The freeze declaration means:

- current VMX ABI/projection vocabulary is stable for compatibility;
- deleted heavy carriers must not return;
- VMX opcode routing remains typed fail-closed except for explicitly admitted compatibility projection paths through neutral runtime boundaries;
- VMX cannot own execution, memory, I/O, lane, nested, capability, evidence, checkpoint, completion, or retire authority;
- new runtime authority must appear first under neutral `Core/Runtime` owners and only then be projected through VMX compatibility vocabulary.

The freeze declaration does not mean:

- VMX is the virtualization architecture;
- Shadow VMCS becomes a nested runtime owner;
- VMCSv2 becomes a mutable domain-state store;
- VMREAD/VMWRITE may bypass generated projection/access policy;
- VMX opcodes are feature-complete admitted execution paths.

## Open Post-Freeze Risks

### 1. VMCSv2 Mutable Helper Surface

`VmcsV2Descriptor` and `VmcsV2Blocks` still require method-level authority tracking. Closure `225` removed the dead descriptor guest-state and host-evidence mutator slice:

- `CaptureGuestStateEager`;
- `BeginLazyGuestStateSave`;
- `MaterializeLazyGuestRegisters`;
- `MaterializeVmExitGuestState`;
- `RecordHostEvidence`;
- `GuestVisibleStateContainsHostEvidence`;
- `DiscardHostEvidenceAfterRestore`;
- `ResetForClear`;
- `VirtualCpuBlock.CaptureEager`;
- `VirtualCpuBlock.BeginLazySave`;
- `VirtualCpuBlock.TryMaterializeLazyRegisters`;
- `VirtualCpuBlock.SnapshotGuestIntegerRegisters`.

Closure `230` removed the next dead block-mutator slice:

- `VmxRootControlBlock.BindRootDescriptor`;
- `VmxRootControlBlock.AdvanceEpoch`;
- `VmxNptBlock.BindControl`;
- `BundleExecutionBlock.BindBundle`;
- `VirtualInterruptFabricBlock.Fabric`;
- `VmxEventInjectionBlockSnapshot`;
- `EventInjectionBlock.ConfigureInterruptRemap`;
- `EventInjectionBlock.RemoveInterruptRemap`;
- `EventInjectionBlock.ClearInterruptRemaps`;
- `EventInjectionBlock.TryQueue`;
- `EventInjectionBlock.TryDeliver`;
- `EventInjectionBlock.CreateSnapshot`;
- `EventInjectionBlock.RestoreSnapshot`;
- `DebugTraceBlock.ConfigureExport`;
- `DebugTraceBlock.Record*`;
- `DebugTraceBlock.SnapshotCounters`;
- `DebugTraceBlock.ResetCounters`;
- `DebugTraceBlock.DiscardTraceHandles`.

Closure `231` closed the residual block-state inventory:

- `VectorStreamStateBlock` backing state and private-set surfaces;
- `DirtyLogBlock` backing state and private-set surfaces;
- `SecurityIsolationBlock` epoch/backing state;
- `CapabilityNegotiationBlock` epoch/backing state;
- public `ExitInfoBlock.Record*` methods.

Current classification:

- generated/read-only projection: block status properties and static compatibility vocabulary;
- denied/fail-closed compatibility ABI: `TryReadScalarField`, migration/nested readiness, and VMREAD fields outside the admitted completion-owned and memory-owned slices while no neutral projection supplies materialized state;
- retire-publication-only helper: `RecordVectorExceptionExit`, `RecordStreamDescriptorFaultExit`, `RecordStreamReplayRequiredExit`;
- deleted dead shell: root descriptor binding, NPT control binding, bundle binding, VMCS-owned event queue/remap/delivery/restore, VMCS-owned debug trace configure/record/reset, and residual vector/dirty/security/capability backing state.

No audited VMCSv2 block currently exposes public owner-like mutators or private-set backing state except the intentionally mutable exit-publication projection itself. `ExitInfoBlock.Record*` is internal and may only be reached through descriptor `Record*Exit` helpers at retire/publication boundaries.

### 2. Conformance Folder Deletion

`Legacy/VMX/Conformance` deletion is closed. A temporary move-away probe before deletion proved:

- production build passes without the folder;
- tests build passes without the folder;
- no physical `.cs` remains under `Legacy/VMX`.
- empty physical `Legacy/VMX` and `Legacy/VMX-v2` directory trees are gone.

Closures `223`, `224`, `226`, `227`, `228`, and `229` removed compiled dependencies from tests by moving useful proof into test-local static/file evidence and retiring obsolete historical proof contracts. Do not recreate this folder for new proof.

### 3. First Admitted VMX Runtime Path

Closure `235` adds the first admitted compatibility path as a VMREAD projection-admission path. The operation passes decode, frozen alias projection, and `RuntimeBoundaryAdmissionService` as `ReadCompatibilityProjection`.

Closure `240` extends that path only for completion-owned generated read-only values. After runtime admission, `VmcsReadOnlyValueProjectionService` performs `VmcsFieldProjectionSchema` owner lookup and projects `ExitReason`, `ExitQualification`, `GuestPhysicalAddress`, and `EptViolationQualification` only from a neutral `CompletionRecord` compatibility projection source via `CompletionProjectionService`. Fields without an admitted neutral value source remain denied with no fallback to a VMCS field store or `TryReadScalarField`.

Closure `241` extends the same path only for two memory-owned generated read-only values. `GuestCr3` projects from neutral `MemoryDomainTranslationControl.AddressSpaceRoot` with `GuestArchitecturalState` evidence allowed, and `EptPointer` projects from neutral `MemoryDomainTranslationControl.SecondStageRoot` only when `MemoryDomainDescriptor.OwnsSecondStageTranslation` is true and compatibility-alias evidence is allowed.

Closure `242` extends the memory-owned slice only for explicit VPID semantics. `Vpid` projects from neutral `MemoryDomainReadOnlyTranslationView.AddressSpaceTag` after runtime admission and generated owner lookup, but only when `AddressSpaceTaggingEnabled` is true and the tag is non-zero. Disabled/unmaterialized tagging remains denied with `MemorySourceDenied`. `HostCr3`, `Cr3TargetCount`, execution-owned fields, and compatibility-control fields remain denied/fail-closed until a neutral owner exposes an explicit read-only value source.

Closure `248` extends the same memory-owned path only for explicit CR3-target-count semantics. `Cr3TargetCount` projects from neutral `MemoryDomainReadOnlyTranslationView.AddressSpaceTargetCount` after runtime admission, generated owner lookup, compatibility-alias evidence, and valid memory translation control. Zero is a valid neutral "no materialized targets" count; counts above `MemoryDomainTranslationControl.MaxAddressSpaceTargetCount` are denied by the neutral memory view gate. `HostCr3`, execution-owned fields, compatibility-control fields, unknown fields, and all writes remain denied/fail-closed.

Closure `249` makes the `HostCr3` denial explicit. `VmcsReadOnlyValueProjectionService` returns `HostAddressSpaceOwnerMissing` after runtime admission and alias/evidence validation, but before guest/domain translation view materialization. There is no neutral host-address-space owner in the current runtime model, and `MemoryDomainReadOnlyTranslationView.AddressSpaceRoot` remains the guest/domain root used for `GuestCr3`, not host CR3 authority.

Closure `250` extends the same generated/read-only path only for the safe execution-owned architectural-state slice. `GuestPc`, `GuestSp`, and `GuestFlags` project from neutral `ExecutionDomainReadOnlyStateView` only when `ExecutionDomainDescriptor.TryCreateReadOnlyStateView()` materializes that state and `GuestArchitecturalState` evidence is allowed. Default/unmaterialized execution descriptors remain denied, and `GuestCr0` / `GuestCr4` are explicitly kept closed until neutral privileged execution-state semantics exist.

Closure `251` keeps the next execution-owned branch denied rather than inventing host execution semantics. `HostPc`, `HostSp`, `HostFlags`, and `HostCr0` return `HostExecutionStateOwnerMissing` after runtime admission, schema owner lookup, and compatibility-alias evidence. The denial is intentionally earlier than guest read-only state view materialization, so `ExecutionDomainReadOnlyStateView.GuestPc` / `GuestSp` / `GuestFlags` cannot become host state.

Closure `252` consolidates the remaining control-like VMREAD posture without opening a new value field. `GuestCr0` and `GuestCr4` remain `PrivilegedExecutionStateProjectionDenied`; `HostCr0` remains `HostExecutionStateOwnerMissing`; `HostCr3` remains `HostAddressSpaceOwnerMissing`; compatibility control fields remain `CompatibilityControlValueProjectionDenied`. The schema remains read-only/write-denied for these aliases, and conformance proves no opened neutral source is reused as control-like authority.

Closure `253` keeps descriptor readiness separate from VMREAD value projection. Even when `GuestPc` can be projected from neutral execution state, `VmcsV2Descriptor.ValidateMigrationReadiness()` and `ValidateNestedEnablementReadiness()` remain fail-closed without materialized guest GPR state. Restore validation rejects compatibility-projection checkpoints and compatibility projection metadata as authoritative state; nested checkpoint readiness still requires neutral projection admission, checkpoint image, migration policy, and restore/evidence policy.

Closure `254` keeps recomputed compatibility completion fields outside checkpoint authority. Completion-owned VMREAD fields are schema-marked `RecomputedCompletion`; migration payload classes do not include completion projection values, `CompletionRecord`, `VmxCompletionProjection`, `VmExitReason`, or VMCS fields. Migration/checkpoint/evidence sources do not depend on completion projection services or VMREAD projection services.

Closure `255` reconciles the execution-owned audit with current code. The safe `GuestPc`/`GuestSp`/`GuestFlags` source already exists, so the code step hardens the neutral snapshot shape with materialization/epoch metadata. That metadata is not a compatibility field value source. Privileged execution controls remain denied until a separate neutral owner/value contract exists.

Closure `242` also introduces neutral `CompatibilityControlDescriptor` as the future compatibility-control owner under `Core/Runtime/Capabilities/CompatibilityControls`. This does not open any control VMREAD field.

Closure `243` materializes that neutral owner as a fail-closed policy view rather than a VMCS control-bit source. The materialized view records runtime admission requirements, read-projection-only execution, write/backend/mutation denial, neutral trap/result/fence requirements, neutral completion/publication requirements, entry/admission validation requirements, nested-intent requirements, memory-owner requirements, and an explicit `ControlValueProjectionDenied` policy.

Closure `245` keeps the control path closed as the cleaner option for the current code state. `VmcsReadOnlyValueProjectionService` recognizes `VmcsFieldProjectionOwner.CompatibilityControlDescriptor` only to return `CompatibilityControlValueProjectionDenied`; it does not introduce a control-bit mapper, fake zero control value, VMCS field store, or control-field backend success. `PinBasedControls`, `ProcBasedControls`, `ExitControls`, `EntryControls`, and `SecondaryProcControls` remain denied after runtime admission and generated owner lookup.

Closure `237` adds the first VMCALL/trap admitted-denied projection path. The operation passes decode, frozen alias projection, and `RuntimeBoundaryAdmissionService` as projection-only `ProjectCompatibilityTrap`, then requires a neutral compatibility-operation intercept and projects the resulting `NeutralTrapResult` through `VmxTrapProjectionMapper`. The final result is backend-denied and cannot publish a successful VMX retire effect.

Closure `238` adds the publication fence for the same path. `TrapCompletionPublicationFence` lives under neutral `Core/Runtime/Completion/Records` and carries no VMX vocabulary. The admitted-denied VMCALL path now returns `TrapCompletionPublicationDecision.DeniedBackendExecution`; compatibility completion helpers require that neutral fence before creating a `CompatibilityExit` completion, and the intercept retire helper fails closed while the fence denies publication.

Closure `246` adds the missing runtime-owned trap completion route layer before any successful VMCALL/intercept publication. `TrapCompletionRouteDescriptor` and `TrapCompletionRouteService` live under neutral `Core/Runtime/Completion/Routing`; they carry no VMX exit vocabulary and authorize publication only after runtime admission, neutral trap result, runtime-owned route authority, domain validation, backend execution authorization, completion publication permission, and retire publication permission. The VMCALL compatibility path now exposes `CompletionRoute` evidence, but it uses `TrapCompletionRouteDescriptor.ProjectionOnlyDenied`, so the final fence remains `DeniedBackendExecution`. `TrapCompletionRouteDescriptor.RuntimeOwnedPublication` is not used by the VMX frontend.

Closure `247` adds `HypercallBackendAdmissionService` under neutral `Core/Runtime/Events/Hypercalls`. It validates runtime admission, neutral trap result, runtime-owned backend authority, domain validation, typed capability requirement, and neutral evidence requirement, but it does not authorize execution because no neutral hypercall backend owner semantics are materialized. The VMCALL compatibility path now exposes `BackendAdmission`; production passes `HypercallBackendAdmissionRequest.MissingNeutralOwner`, so backend execution remains denied before the route and publication fence.

Current VMX opcode behavior remains frozen and fail-closed beyond this narrow projection-admission path. Future feature work may add an admitted compatibility path only through:

```text
decode VMX opcode
-> compatibility projection/access policy
-> RuntimeBoundaryAdmissionService
-> neutral runtime/domain operation
-> neutral completion/retire publication
-> VMX-compatible projected result
```

No future admitted path may restore `VmxExecutionUnit`, `VmcsManager`, active VMCS pointer state, VMCS field stores, success retire factories without neutral runtime ownership, or renamed VMX runtime managers.

### 3a. Forbidden-Authority Projection Carriers

The generated/frontend projection inventory currently has no forbidden-authority targets.

Closure `234` reduced the last two:

- `Core/VMX/Compatibility/Generated/VmcsProjection/VmcsV2Header.cs` is read-only compatibility metadata with no launch/epoch mutators.
- `Core/VMX/Compatibility/Frontend/Projection/Nested/ChildDomainIntentDescriptor.cs` is a fail-closed compatibility probe with no child-intent field store, write API, generation, or snapshot restore.

`SchedulingBudgetTimer.cs` and `TrapPolicyBitmap.cs` were extracted out of VMX projection scope in closure `233` and are now runtime trap carriers under `Core/Runtime/Events/Traps`. The inventory test rejects unclassified files in the generated/projection scope and rejects manager/store/backend authority markers across that scope.

### 4. Neutral Trap Result Split

Closure `236` closed the pre-VMCALL trap result split:

- `TrapRequest`, `NeutralTrapResult`, `TrapPolicyBitmap`, and `SchedulingBudgetTimer` now live as neutral runtime trap vocabulary without `VmExitReason`, `VmxExitQualification`, or `TrapDecision` dependencies.
- `TrapPolicyBitmap.Evaluate` and `SchedulingBudgetTimer.TryConsumeExpired` return `NeutralTrapResult`.
- `VmxTrapProjectionMapper` maps neutral result kinds to VMX-compatible `VmExitReason` and `TrapDecision` only at the compatibility frontend boundary.
- VMX compatibility aliases such as `TrapRequest.ForVmxOperation` and `TrapPolicyBitmap.EnableVmxOperation` remain projection vocabulary, not runtime authority.
- No VMCALL backend, VMCS manager, active pointer, VMCS field store, or success intercept execution path was introduced.

### 4a. Admitted-Denied VMCALL Trap Projection

Closure `237` closes the admitted-denied trap projection slice:

- `DomainRuntimeOperationKind.ProjectCompatibilityTrap` is a neutral projection-only runtime admission kind.
- `CompatAliasMap` now declares frozen `Opcode/VMCALL -> VmxTrapProjectionMapper.Project` generated lineage.
- `VmxCompatibilityAdmissionService.AdmitVmCallTrapProjection` routes decode -> alias projection -> runtime admission -> neutral trap policy -> `NeutralTrapResult` -> VMX trap projection.
- The success-shaped result is deliberately not used: the API returns `TrapProjectionDeniedBackend`, and production VMX dispatch/retire remains fail-closed.
- No `VmxRetireEffect.InterceptExit`, `VmxRetireEffect.VmCall`, VMCS manager, active pointer, field store, or backend execution path was introduced.

### 4b. Trap Projection Publication Fence

Closure `238` closes the retire/completion publication fence:

- neutral `TrapCompletionPublicationFence` decides whether a neutral trap may publish a completion record and retire-visible effect;
- admitted-denied VMCALL carries `PublicationFence` with `DeniedBackendExecution`;
- `CompletionRecord.TryFromCompatibilityExit` / `FromCompatibilityExit` require that neutral fence before producing VMX-compatible completion records;
- `CompletionProjectionService` no longer treats arbitrary nonzero neutral reason codes as VMX exit reasons;
- `VmxRetireEffect.InterceptExit` requires the neutral fence and returns a security fault while publication is denied.

This is a fence, not a backend. It creates no `VmxExecutionUnit`, no `VmcsManager`, no active VMCS pointer, no VMCS field store, and no production success path.

### 5. Nested Compatibility Runtime Wiring

Neutral nested projection/checkpoint services exist. Shadow VMCS compatibility remains fail-closed. Future nested enablement must route through neutral nested descriptors, projection/checkpoint validation, runtime admission, and neutral completion/retire publication.

## No-Noise Current Baseline Checks

Recommended baseline before the next refactoring step:

```powershell
rg -il "legacy" "\HybridCPU ISE\HybridCPU_ISE\CloseToHSL\Core\VMX" --glob "*.cs" --glob "!bin/**" --glob "!obj/**"
rg --files "\HybridCPU ISE\HybridCPU_ISE\Legacy\VMX" --glob "*.cs"
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
dotnet build "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-restore
dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-build --filter "FullyQualifiedName~Vmx"
```

Expected:

- Core/VMX legacy-marked C# matches: `0`.
- `Legacy/VMX` C# count: `0`.
- `Legacy/VMX/Compatibility` C# count: `0`.
- production build passes.
- tests build passes.
- broad VMX filter passes.

## Next Recommended Step

The next useful step is not another production/conformance legacy shell deletion, and the audited VMCSv2 block mutator/backing-state pool is now exhausted. The first admitted VMREAD projection-admission path exists, the completion-owned generated read-only VMREAD value slice is closed, the memory-owned `GuestCr3`/`EptPointer`/`Vpid`/`Cr3TargetCount` slices are closed, `HostCr3` is explicitly denied until a neutral host-address-space owner exists, the execution-owned `GuestPc`/`GuestSp`/`GuestFlags` slice is closed and hardened through neutral `ExecutionDomainReadOnlyStateView`, host execution aliases are explicitly denied until a neutral host-execution owner exists, remaining control-like fields are fenced as denied unless a neutral owner/value source exists, descriptor readiness is audited fail-closed and does not consume VMREAD projection values, migration/evidence proof for recomputed completion-owned fields is closed, the neutral trap-result split is closed, VMCALL has an admitted-denied trap projection path, that path is now fenced against completion/retire publication, and closure `246` adds the missing neutral trap completion route owner. The next step should be one of:

1. extend VMREAD only for residual fields whose neutral owner exposes an explicit value source; `HostCr3` must stay denied until a separate neutral host-address-space owner exists, host execution aliases must stay denied until a separate neutral host-execution owner exists, and control-like fields must stay denied until neutral semantics and value sources exist;
2. define a field-by-field compatibility-control mapper only if neutral control semantics can be projected without making VMCS control bits authority; otherwise keep controls denied;
3. materialize real neutral hypercall backend owner semantics, typed capability contract, and evidence policy before production stops using `MissingNeutralOwner`; or
4. design a neutral `Core/Runtime/Nested/*` child-intent owner only if a future admitted nested path needs real child-intent state; or
5. harden migration/evidence for future neutral descriptor/policy payloads only when those owners expose explicit serializable state.

Preferred order: keep VMREAD scalar value projection denied outside admitted neutral value sources; any trap publication work must preserve the closure `237` backend-denied boundary, the closure `238` publication fence, the closure `246` route gate, and the closure `247` hypercall backend admission denial until a real neutral backend owner is admitted.
