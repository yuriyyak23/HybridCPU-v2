**HybridCPU-v2 Stream / DMA / Accelerator  
Детальный анализ и рекомендуемые решения основных нерешённых задач**

_Исправленная версия с учётом прикреплённого кода IOMMU / Cache / MemoryUnit / AtomicMemoryUnit_

Дата подготовки: 2026-04-30  
_Scope: TASK-001 … TASK-010, Phase 6 architecture backlog, executable lane6 DSC, L7 external accelerators, IOMMU/cache correction_

**Краткий результат:** рекомендуемая целевая архитектура - асинхронная token-based execution model для lane6 DmaStreamCompute, unified explicit completion/fence/order model, backend-selectable memory path с поддержкой IOMMU burst backend, и non-coherent cache/prefetch surfaces с явным flush/invalidate protocol. Текущий fail-closed контракт должен сохраняться до утверждения отдельной implementation phase.

# 1\. Область анализа и исправленная factual baseline

Документ корректирует ранее подготовленный анализ основных нерешённых задач. Главная правка: формулировки "IOMMU отсутствует" и "кэша нет" больше нельзя считать корректными после проверки прикреплённого кода. В кодовой базе уже есть IOMMU-backed burst backend и cache/prefetch surfaces, но они не равны завершённой executable DMA/cache-coherency архитектуре.

| **Область**   | **Исправленный факт**                                                                                                                                                                                   | **Архитектурное следствие**                                                                                                                                                                    |
| ------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| IOMMU         | IBurstBackend принимает deviceID и virtual address; IOMMUBurstBackend делегирует Read/Write в Memory.IOMMU.ReadBurst/WriteBurst. Accelerator DMA registration/transfer в IOMMUBurstBackend fail-closed. | IOMMU надо считать существующим memory backend, но не доказанной lane6 DSC / L7 executable integration. TASK-005 меняется с "добавить IOMMU" на "ввести explicit backend/addressing contract". |
| Cache         | CPU_Core содержит L1/L2 data/VLIW cache-like surfaces, assist-resident prefetch, domain-aware FlushDomainFromDataCache и VLIW fetch invalidation.                                                       | TASK-010 меняется с "добавить кэш" на "формализовать non-coherent cache/prefetch surfaces и добавить explicit DMA flush/invalidate protocol".                                                  |
| Scalar memory | MemoryUnit работает через IMemoryBus и не доказывает единый coherent cache datapath.                                                                                                                    | DMA/order model нельзя строить на предположении автоматической cache coherency.                                                                                                                |
| Atomics       | AtomicMemoryUnit работает через MainMemoryArea.TryReadPhysicalRange/TryWritePhysicalRange и имеет reservation invalidation через NotifyPhysicalWrite.                                                   | Есть полезный hook для reservation invalidation, но не cache-line coherency. Его надо расширить/связать с global memory conflict service.                                                      |

| **Источник**                                                             | **Классы / методы**                                                                                              | **Значение для решений**                                                                                                      |
| ------------------------------------------------------------------------ | ---------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------- |
| HybridCPU_ISE/Core/Pipeline/MicroOps/DmaStreamComputeMicroOp.cs          | DmaStreamComputeMicroOp.Execute                                                                                  | Текущий lane6 carrier fail-closed; descriptor/footprint evidence only.                                                        |
| HybridCPU_ISE/Core/Execution/DmaStreamCompute/DmaStreamComputeRuntime.cs | ExecuteToCommitPending, TryValidateRuntimeDescriptor, TryReadOperands, TryCompute\*, StageOutput                 | Runtime/helper уже имеет model execution path до CommitPending, но не pipeline Execute.                                       |
| HybridCPU_ISE/Core/Execution/DmaStreamCompute/DmaStreamComputeToken.cs   | DmaStreamComputeToken, Commit, Cancel, PublishFault, TryCommitAllOrNone                                          | Существующий token lifecycle и all-or-none commit - база для будущего async engine.                                           |
| HybridCPU_ISE/Core/Pipeline/MicroOps/SystemDeviceCommandMicroOp.cs       | ACCEL\_\* carriers, Execute                                                                                      | L7 carriers fail-closed; no rd writeback, no backend dispatch.                                                                |
| IBurstBackend.cs                                                         | IBurstBackend.Read/Write, RegisterAcceleratorDevice, InitiateAcceleratorDMA                                      | Burst backend abstraction уже содержит IOMMU-oriented deviceID/virtual-address surface, но accelerator DMA API ещё premature. |
| IOMMUBurstBackend.cs                                                     | Read, Write, RegisterAcceleratorDevice, InitiateAcceleratorDMA                                                   | IOMMU Read/Write реализованы, accelerator DMA fail-closed.                                                                    |
| CPU_Core.Cache.cs                                                        | L1_Data, L2_Data, GetDataByPointer, MaterializeCacheDataLine, InvalidateVliwFetchState, FlushDomainFromDataCache | Есть cache/prefetch surfaces и domain invalidation; нет общей DMA coherency policy.                                           |
| CPU_Core.Cache.Assist.cs                                                 | TryPrefetchAssistDataLine, AssistResident, AssistCarrierKind budgets                                             | Assist prefetch имеет собственную residency/partitioning модель.                                                              |
| MemoryUnit.cs                                                            | IMemoryBus, MemoryUnit.Execute, ResolveArchitecturalAccess                                                       | Scalar memory abstraction; не общий DMA/cache coherent datapath.                                                              |
| AtomicMemoryUnit.cs                                                      | MainMemoryAtomicMemoryUnit, NotifyPhysicalWrite, ValidateAccess                                                  | Physical main-memory atomic path; reservation invalidation hook.                                                              |

# 2\. Матрица рекомендуемых решений

| **ID**   | **Тема**                                 | **Оптимальное решение**                                                                                                                           | **Совместимость**                          |
| -------- | ---------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------ |
| TASK-001 | Executable lane6 DmaStreamComputeMicroOp | Выбрать async token-based engine как целевую архитектуру. Не подключать runtime напрямую к Execute без Phase 6 approval.                          | Breaking / gated                           |
| TASK-002 | Token allocation lifecycle               | Allocate/admit token на issue/admission boundary; execution token store + async state machine; retire only observes completion/fault.             | Breaking / gated                           |
| TASK-003 | Precise exceptions                       | Faults становятся precise retire publications; execute path не бросает произвольные исключения после issue.                                       | Breaking / gated                           |
| TASK-004 | Memory ordering/fence                    | Explicit fence/wait/poll + normalized footprint conflict manager; no implicit full coherency.                                                     | Breaking / gated                           |
| TASK-005 | IOMMU/addressing                         | Hybrid backend-selection: PhysicalBurstBackend + existing IOMMUBurstBackend; descriptor explicitly declares address space.                        | Non-breaking if DSC1 untouched; new ABI v2 |
| TASK-006 | Descriptor ABI v2                        | DSC1 immutable; DSC2 for stride/tile/scatter-gather + extension blocks/capabilities.                                                              | Potential ABI addition, not DSC1 break     |
| TASK-007 | Partial completion                       | Keep AllOrNone as only successful architectural commit initially; add progress evidence; successful partial completion only with DSC2 async mode. | Gated                                      |
| TASK-008 | External accelerator ISA                 | Phased L7 command protocol: read-only QUERY_CAPS/POLL first, then async SUBMIT/WAIT/FENCE/CANCEL using token store.                               | Breaking / gated                           |
| TASK-009 | Global conflict hook                     | Installable global conflict service mandatory for executable DMA/ACCEL; absent path remains current non-executable baseline.                      | Gated                                      |
| TASK-010 | Cache/coherency                          | Non-coherent explicit flush/invalidate protocol over existing cache/prefetch surfaces; coherent DMA deferred.                                     | Additive first, coherent future            |

# 3\. Архитектурная философия, на которой основан выбор

- HybridCPU не должен превращать model/helper API в executable ISA без явного pipeline/retire/memory контракта. Поэтому текущий fail-closed carrier остаётся baseline до новой Phase 6 implementation phase.
- Typed-slot/VLIW философия требует, чтобы side-effecting long-latency операции не выполнялись "внутри Execute как функция", а имели явный token, resource mask, conflict evidence и precise retire publication.
- Stream/Vector философия благоприятствует overlap, event/cycle execution и descriptor-based scheduling; поэтому async token-based DMA ближе к целевой модели, чем blocking synchronous Execute.
- Owner/domain guard является частью authority model, а не telemetry. Admission и commit guard должны оставаться обязательными, особенно при async tokens и IOMMU deviceID.
- IOMMU и cache/prefetch surfaces уже присутствуют, но их нельзя документировать как завершённую coherent DMA architecture: нужен explicit backend/addressing и explicit coherency protocol.
- DSC1/SDC1 должны оставаться immutable compatibility anchors; новые возможности идут через DSC2/SDC2 или capability-gated extensions.

## TASK-001. Исполняемый lane6 DmaStreamComputeMicroOp

**Статус:** Future architecture, implementation не начинать без отдельного approval.

**Выбранное решение:** Вариант C: асинхронное выполнение DMA/stream-compute на основе токенов, с event/cycle engine и retire-observed completion/fault.

#### Почему это оптимально для HybridCPU

- Архитектурно ближе к настоящему DMA: работа может перекрываться с CPU и не блокировать VLIW pipeline как обычная функция.
- Естественно использует уже существующий DmaStreamComputeToken lifecycle: Admitted, Issued, ReadsComplete, ComputeComplete, CommitPending, Committed, Faulted, Canceled.
- Сохраняет separation между descriptor carrier, runtime/backend и architectural commit.
- Позволяет ввести poll/wait/fence/interrupt позже без переписывания synchronous Execute.

#### Затрагиваемые файлы / классы / методы

| **Файл**                            | **Классы / методы**                                                | **Роль изменения**                                                                                                             |
| ----------------------------------- | ------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------ |
| DmaStreamComputeMicroOp.cs          | DmaStreamComputeMicroOp.Execute                                    | Оставить fail-closed до Phase 6B; позже Execute только submit/admit token, не выполняет весь DMA synchronously.                |
| DmaStreamComputeRuntime.cs          | ExecuteToCommitPending, TryReadOperands, TryCompute\*, StageOutput | Разделить на reusable async steps: Validate, IssueReads, ComputeStep, StageWrites. Не использовать как direct Execute монолит. |
| DmaStreamComputeToken.cs            | DmaStreamComputeToken, Commit, Cancel, PublishFault                | Расширить для async progress, issue cycle, completion cycle, cancel/squash reasons, optional backend fault metadata.           |
| NEW: DmaStreamComputeAsyncEngine.cs | DmaStreamComputeAsyncEngine.Tick/Submit/Cancel/Complete            | Cycle/event execution owner для active lane6 tokens.                                                                           |
| NEW: DmaStreamComputeTokenStore.cs  | Allocate, Lookup, RetireComplete, CancelByThread/Domain            | Архитектурное хранилище токенов per core/pod/domain.                                                                           |
| NEW: DmaStreamComputeIssueQueue.cs  | Enqueue, TryIssue, ApplyBackpressure                               | Queue/backpressure для lane6 DSC tokens.                                                                                       |

#### Архитектурная техника

- DmaStreamComputeMicroOp остаётся carrier, но при approved executable mode становится submit micro-op: проверяет owner guard, normalized footprint, resource mask, создаёт token и помещает его в engine/queue.
- DMA/compute progression выполняется вне обычного scalar Execute: engine вызывается из cycle model или scheduler tick.
- Commit не происходит сразу после compute; token переходит в CommitPending, а retire/fence/wait решают публикацию.
- Direct all-or-none memory visibility сохраняется: staged writes становятся visible только через commit boundary.

#### Рекомендуемая последовательность реализации

- Создать Phase 6A design spec без кода: state machine, timing model, precise exceptions, memory ordering.
- Добавить DmaStreamComputeTokenStore и async engine skeleton с fail-closed feature flag.
- Разделить DmaStreamComputeRuntime на step APIs без изменения текущих tests.
- Подключить engine tick в CPU cycle/scheduler только под explicit feature flag.
- После conformance tests перевести часть WhiteBook Future Design в Current Implemented Contract.

#### Тесты / критерии приёмки

- Fail-closed tests остаются authoritative до approval.
- Positive async tests: copy/add/mul/fma/reduce token reaches CommitPending, commit publishes all-or-none.
- Overlap tests: CPU executes independent arithmetic while token active.
- Cancel/squash/trap tests: active token canceled without memory visibility.
- Retire fault tests: memory fault/guard mismatch become precise retire exception, not silent backend error.

#### Риски и ограничения

- Breaking relative to current fail-closed baseline.
- Нельзя просто вызвать ExecuteToCommitPending из Execute: это создаст hidden synchronous side effects и обойдёт pipeline/retire semantics.

## TASK-002. Token allocation point и lifecycle integration

**Статус:** Future architecture, зависит от TASK-001.

**Выбранное решение:** Token allocation на issue/admission boundary: не decode, не retire. TokenStore владеет lifetime; retire только публикует completion/fault.

#### Почему это оптимально для HybridCPU

- Decode может быть speculative; выделять токен там опасно для squash/replay.
- Issue/admission уже имеет typed-slot, owner/domain, resource mask и footprint evidence.
- Текущий DmaStreamComputeToken уже начинается в Admitted и умеет MarkIssued, Cancel, Commit; его можно расширить без полной замены.

#### Затрагиваемые файлы / классы / методы

| **Файл**                            | **Классы / методы**                                    | **Роль изменения**                                                                                                    |
| ----------------------------------- | ------------------------------------------------------ | --------------------------------------------------------------------------------------------------------------------- |
| DmaStreamComputeToken.cs            | TryAdmit, MarkIssued, Cancel, Commit                   | Сделать TryAdmit canonical allocation result; добавить async metadata.                                                |
| DmaStreamComputeMicroOp.cs          | constructor, Execute                                   | Constructor остаётся validation/evidence; Execute в executable mode вызывает token allocation/admission.              |
| NEW: DmaStreamComputeTokenStore.cs  | AllocateTokenId, Admit, MarkIssued, CancelByVt, Retire | Единый owner lifecycle tokens.                                                                                        |
| Processor / CPU_Core pipeline files | issue/execute/retire integration points                | Подключить token allocation на issue/admission path. Конкретный файл зависит от текущего dispatcher/scheduler layout. |

#### Архитектурная техника

- TokenId должен быть уникальным per core/pod/domain или global monotonic с owner binding.
- Admission может вернуть Accepted, TelemetryReject или ArchitecturalFault; architectural faults идут в retire exception path.
- Replay/squash/trap/context switch обязаны вызывать Cancel(reason) для active non-committed tokens.
- CommitPending token не публикуется в memory до retire/fence/wait boundary.

#### Рекомендуемая последовательность реализации

- Добавить TokenStore как isolated unit с tests без pipeline.
- Связать TokenStore с DmaStreamComputeToken.TryAdmit.
- Подключить queue/backpressure counters.
- Внедрить pipeline callbacks: OnIssue, OnSquash, OnTrap, OnContextSwitch, OnRetire.

#### Тесты / критерии приёмки

- Token allocation uniqueness and lifetime tests.
- No token allocation on decode-only/discarded bundle.
- Squash before commit clears staged writes.
- Commit after cancel fails/canceled and does not mutate memory.

#### Риски и ограничения

- Потребуется явная ownership model для SMT/virtual threads, иначе token leaks across context switch возможны.

## TASK-003. Precise exceptions и fault priority

**Статус:** Future architecture, зависит от TASK-001/TASK-002.

**Выбранное решение:** Все architectural faults от executable DSC публиковать на retire boundary как precise exceptions; backend faults остаются token fault records до retire.

#### Почему это оптимально для HybridCPU

- Текущий DmaStreamComputeFaultRecord уже умеет CreateRetireException и RequiresRetireExceptionPublication.
- VLIW/typed-slot философия требует deterministic priority между slot faults, DMA token faults и scalar load/store faults.
- Async execution не должна бросать произвольные исключения вне retire context.

#### Затрагиваемые файлы / классы / методы

| **Файл**                                | **Классы / методы**                                             | **Роль изменения**                                                            |
| --------------------------------------- | --------------------------------------------------------------- | ----------------------------------------------------------------------------- |
| DmaStreamComputeToken.cs                | DmaStreamComputeFaultRecord.CreateRetireException, PublishFault | Расширить fault metadata: issuing PC/bundle/slot, token age, operation phase. |
| Pipeline retire / ExecutionDispatcherV4 | CaptureRetireWindowPublications, retire publication structures  | Добавить DmaTokenRetirePublication или memory-side effect publication.        |
| NEW: DmaStreamComputeFaultPriority.cs   | CompareFaults                                                   | Таблица приоритетов между descriptor/admission/runtime/commit faults.         |
| SystemDeviceCommandMicroOp.cs           | ACCEL\_\* future path                                           | Для L7 использовать аналогичную precise exception discipline.                 |

#### Архитектурная техника

- Descriptor decode/admission fault: retire exception attached to issuing micro-op.
- Runtime memory fault: token Faulted, no memory writes visible, retire observes token fault.
- Commit guard mismatch: fault at commit/retire boundary, staged writes cleared.
- Rollback failure remains fatal emulator invariant, not normal architectural partial success.

#### Рекомендуемая последовательность реализации

- Define fault priority table before code.
- Add fault metadata fields to token/fault record.
- Add retire publication type and conversion from token fault to architectural exception.
- Add multi-slot bundle tests.

#### Тесты / критерии приёмки

- Precise exception order tests with scalar load fault + DSC fault in same bundle.
- Faulted token cannot commit.
- DomainViolation maps to DomainFaultException; MemoryFault maps to PageFaultException.

#### Риски и ограничения

- Precise exception integration is a breaking semantic change and must not be hidden behind current fail-closed docs.

## TASK-004. Memory ordering, fence и footprint conflicts

**Статус:** Future architecture, depends on TASK-001/002/003.

**Выбранное решение:** Explicit fence/wait/poll semantics plus normalized footprint conflict manager. Для MVP можно включить conservative serialization, но целевая модель - range/footprint-based ordering.

#### Почему это оптимально для HybridCPU

- Descriptor уже содержит normalized read/write memory ranges; это естественная база для conflict detection.
- Full serialization проще, но противоречит Stream/DMA overlap philosophy.
- Explicit fences/poll/wait лучше масштабируются к external accelerators.

#### Затрагиваемые файлы / классы / методы

| **Файл**                              | **Классы / методы**                                  | **Роль изменения**                                                         |
| ------------------------------------- | ---------------------------------------------------- | -------------------------------------------------------------------------- |
| DmaStreamComputeMicroOp.cs            | ReadMemoryRanges, WriteMemoryRanges, ResourceMask    | Использовать как conflict evidence, не как proof of execution.             |
| ExternalAcceleratorConflictManager.cs | existing optional manager                            | Сделать installable global service для executable modes.                   |
| NEW: GlobalMemoryConflictService.cs   | RegisterTokenFootprint, CheckLoadStore, ResolveFence | Единый footprint conflict hook для DSC/L7.                                 |
| MemoryUnit.cs                         | Execute, ResolveArchitecturalAccess                  | Перед load/store проверять active conflicts только если service installed. |
| AtomicMemoryUnit.cs                   | NotifyPhysicalWrite, ApplyResolvedRetireEffect       | Уведомлять conflict/cache services при physical write.                     |
| SystemDeviceCommandMicroOp.cs         | ACCEL_FENCE/WAIT/POLL future                         | Перевести model fence в executable fence после global ordering spec.       |

#### Архитектурная техника

- Load after active DSC write range: either stall/replay/wait or fault depending on approved policy.
- Store overlapping active DSC read/write range: conflict manager decides stall/replay/serialize.
- Fence waits for all relevant tokens in domain/thread/order scope.
- Poll reads token state without forcing visibility unless explicit policy says otherwise.

#### Рекомендуемая последовательность реализации

- Define memory order classes: none, memory-ordered, full-serial, domain-scoped fence.
- Implement service as optional hook with current behavior unchanged when absent.
- Connect executable DSC first; L7 later.
- Add litmus tests.

#### Тесты / критерии приёмки

- Load/store litmus: overlapping ranges observe correct ordering.
- Non-overlapping ranges allow overlap.
- Fence blocks until CommitPending/Committed/Faulted as specified.
- Absent conflict manager does not change current fail-closed baseline.

#### Риски и ограничения

- If global conflict service is mandatory too early, it may accidentally change scalar memory behavior. Use feature gates.

## TASK-005. Addressing model: physical vs IOMMU

**Статус:** Corrected by attached code: IOMMU backend exists, but executable integration is not proven.

**Выбранное решение:** Hybrid explicit backend selection: DSC1 remains physical/current; executable DSC2 supports declared AddressSpace = Physical или IOMMUTranslated through IBurstBackend. No implicit translation.

#### Почему это оптимально для HybridCPU

- IBurstBackend already models deviceID + virtual address burst Read/Write.
- IOMMUBurstBackend already delegates to Memory.IOMMU.ReadBurst/WriteBurst.
- IOMMUBurstBackend accelerator DMA API is still fail-closed, so integration must be explicit and truthful.
- HybridCPU owner/domain guard can map naturally to deviceID/domain translation authority.

#### Затрагиваемые файлы / классы / методы

| **Файл**                            | **Классы / методы**                                           | **Роль изменения**                                                                                                          |
| ----------------------------------- | ------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------- |
| IBurstBackend.cs                    | Read(deviceID,address,buffer), Write(deviceID,address,buffer) | Canonical backend abstraction for future executable DSC memory access.                                                      |
| IOMMUBurstBackend.cs                | Read, Write                                                   | Use for AddressSpace.IOMMUTranslated. Keep RegisterAcceleratorDevice/InitiateAcceleratorDMA fail-closed until real runtime. |
| NEW: PhysicalBurstBackend.cs        | Read, Write                                                   | Wrap Processor.MainMemoryArea.TryReadPhysicalRange/TryWritePhysicalRange for AddressSpace.Physical.                         |
| DmaStreamComputeDescriptorParser.cs | descriptor fields/ABI parser                                  | DSC2 must parse AddressSpace, DeviceId, TranslationDomain/ASID if approved.                                                 |
| DmaStreamComputeRuntime.cs          | TryReadOperands, StageOutput                                  | Replace direct MainMemory backend assumption with IBurstBackend abstraction for executable DSC2.                            |
| DmaStreamComputeToken.cs            | Commit                                                        | Commit must know whether writes are physical or IOMMU-translated and how to validate guard/domain.                          |

#### Архитектурная техника

- DSC1 remains immutable and physical-runtime/helper-only unless already documented otherwise.
- DSC2 descriptor carries explicit AddressSpace enum: Physical, IOMMUTranslated; future CoherentVirtual reserved.
- DeviceId in descriptor must match OwnerBinding.DeviceId / guard context.
- Translation faults from IOMMU map to DmaStreamComputeTokenFaultKind.TranslationFault or PermissionFault.

#### Рекомендуемая последовательность реализации

- Introduce AddressSpace enum and PhysicalBurstBackend without changing DSC1 parser.
- Add backend selector service: DmaStreamComputeMemoryBackendResolver.
- Wire DSC2 execution path to IBurstBackend.
- Keep IOMMUBurstBackend accelerator DMA methods fail-closed until TASK-008/TASK-001 device protocol exists.

#### Тесты / критерии приёмки

- Physical backend reads/writes exact ranges and faults on bounds.
- IOMMU backend calls Memory.IOMMU with deviceID; translation failure maps to token fault.
- Descriptor with mismatched DeviceId/OwnerGuard is rejected.
- No silent fallback from IOMMU to physical memory.

#### Риски и ограничения

- Do not document IOMMU as full accelerator DMA support: current code only proves burst Read/Write backend.

## TASK-006. Descriptor ABI v2: stride/tile/scatter-gather

**Статус:** Future descriptor/runtime extension; independent parser work possible, production lowering blocked until executable surface exists.

**Выбранное решение:** Keep DSC1 immutable. Add DSC2 ABI with explicit layout/version and optional extension blocks for Stride1D, Tile2D, ScatterGather.

#### Почему это оптимально для HybridCPU

- DSC1 is already a compatibility anchor: v1, 128B header, 16B range entries, InlineContiguous, AllOrNone.
- Using reserved fields in DSC1 would create ambiguous compatibility behavior.
- Stream/vector philosophy benefits from rich descriptor expressiveness, but parser/runtime must normalize into footprints before scheduling.

#### Затрагиваемые файлы / классы / методы

| **Файл**                                    | **Классы / методы**          | **Роль изменения**                                                               |
| ------------------------------------------- | ---------------------------- | -------------------------------------------------------------------------------- |
| DmaStreamComputeDescriptorParser.cs         | Parse/Validate               | Add DSC2 parser while keeping DSC1 path unchanged.                               |
| DmaStreamComputeDescriptor.cs               | descriptor model             | Add AddressSpace, LayoutKind, ExtensionBlocks, NormalizedFootprintV2.            |
| NEW: DmaStreamComputeDescriptorV2.cs        | strongly typed v2 descriptor | Avoid overloading v1 record with incompatible semantics.                         |
| NEW: DmaStreamComputeFootprintNormalizer.cs | NormalizeStride/Tile/Scatter | Convert complex ranges to normalized read/write footprints for conflict manager. |
| Compiler/backend descriptor emission        | DSC2 lowering path           | Emit DSC2 only when feature flag/capability confirms support.                    |

#### Архитектурная техника

- Parser supports multiple encodings but execution always consumes normalized footprints.
- Stride/tile/scatter descriptors must validate overflow, alignment, aliasing, domain and range-count limits.
- Capability query should expose supported encodings and max ranges.

#### Рекомендуемая последовательность реализации

- Define binary ABI for DSC2 and magic/version.
- Implement parser-only tests first.
- Implement footprint normalizer independent of async engine.
- Allow runtime only after TASK-001/TASK-004 readiness.

#### Тесты / критерии приёмки

- DSC1 still rejects non-InlineContiguous.
- DSC2 stride/tile/scatter positive/negative parser tests.
- Overflow/alias/alignment/domain negative tests.
- Normalizer produces deterministic sorted non-overlapping footprints.

#### Риски и ограничения

- Do not reinterpret DSC1 reserved fields as DSC2 without magic/version split.

## TASK-007. Partial completion semantics

**Статус:** Future semantic extension; current architectural success remains AllOrNone.

**Выбранное решение:** Keep AllOrNone as the only successful commit policy for current/executable v1. Add progress evidence telemetry now; successful partial completion only in DSC2 async mode with explicit policy.

#### Почему это оптимально для HybridCPU

- Existing token Commit enforces exact staged coverage and all-or-none rollback.
- Partial success is dangerous for compiler/backend because memory visibility becomes non-atomic.
- Async token engine can track progress without exposing it as successful architectural state.

#### Затрагиваемые файлы / классы / методы

| **Файл**                               | **Классы / методы**                                     | **Роль изменения**                                             |
| -------------------------------------- | ------------------------------------------------------- | -------------------------------------------------------------- |
| DmaStreamComputeToken.cs               | HasExactStagedWriteCoverage, TryCommitAllOrNone, Commit | Keep as current success rule. Add progress counters only.      |
| DmaStreamComputeRuntime.cs             | StageOutput, TryCompute\*                               | Continue faulting on incomplete materialization for v1.        |
| NEW: DmaStreamComputeProgressRecord.cs | BytesRead/Written/ElementsComputed                      | Telemetry/model evidence, not success status.                  |
| DSC2 descriptor model                  | PartialPolicy enum                                      | Future: AllowPartialSuccess only with explicit ABI/capability. |

#### Архитектурная техника

- Partial progress may be visible to telemetry/poll but not treated as committed memory success.
- If partial success is later approved, it must define retry, visibility, error reporting, and compiler contract.
- AllOrNone remains the default for stream compute operations that write memory.

#### Рекомендуемая последовательность реализации

- Add progress metadata to token/telemetry without changing commit semantics.
- Define DSC2 PartialPolicy but keep unsupported until explicit feature approval.
- Add tests proving partial output faults in v1.

#### Тесты / критерии приёмки

- AllOrNone rollback remains exact.
- Poll/progress can report bytes/elements but Succeeded=false until full commit.
- Partial success descriptors are rejected unless DSC2 feature enabled.

#### Риски и ограничения

- Premature partial success would break backend assumptions about memory atomicity.

## TASK-008. External accelerator executable ISA

**Статус:** Future architecture; current L7 ACCEL\_\* carriers remain fail-closed.

**Выбранное решение:** Phased L7 protocol: first executable read-only QUERY_CAPS/POLL if approved; then async SUBMIT/WAIT/FENCE/CANCEL using token store, queue, backend dispatch and commit coordinator.

#### Почему это оптимально для HybridCPU

- SystemDeviceCommandMicroOp already enumerates QueryCaps, Submit, Poll, Wait, Cancel, Fence and pins carriers to lane7 SystemSingleton.
- Direct Execute currently fail-closed and WritesRegister=false; changing this is a clear ISA breaking point.
- External accelerators should reuse token/queue/fence/order disciplines from lane6 DSC rather than invent separate semantics.

#### Затрагиваемые файлы / классы / методы

| **Файл**                                      | **Классы / методы**             | **Роль изменения**                                                              |
| --------------------------------------------- | ------------------------------- | ------------------------------------------------------------------------------- |
| SystemDeviceCommandMicroOp.cs                 | ACCEL\_\* classes, Execute      | Keep fail-closed until feature approval; later dispatch to L7 command executor. |
| AcceleratorRegisterAbi.cs                     | model packing                   | Promote to architectural rd writeback only after register ABI decision.         |
| AcceleratorCommandQueue.cs                    | queue model                     | Upgrade from model queue to executable queue with capacity/backpressure.        |
| AcceleratorCommitModel.cs / CommitCoordinator | commit result                   | Bridge staged writes to retire boundary like DSC tokens.                        |
| ExternalAcceleratorConflictManager.cs         | conflict model                  | Installable global conflict hook for executable L7.                             |
| NEW: ExternalAcceleratorTokenStore.cs         | Allocate/Lookup/Complete/Cancel | Token lifecycle for L7 device commands.                                         |
| NEW: ExternalAcceleratorDeviceRegistry.cs     | RegisterBackend, QueryCaps      | Capability/device discovery.                                                    |

#### Архитектурная техника

- Tier 0: carriers remain fail-closed; model APIs only.
- Tier 1: QUERY_CAPS/POLL can be read-only and side-effect-minimal, but require explicit rd writeback and CSR/order semantics.
- Tier 2: SUBMIT allocates token and dispatches backend asynchronously; WAIT/FENCE observe completion/order; CANCEL cancels non-committed tokens.

#### Рекомендуемая последовательность реализации

- Approve L7 register ABI and rd writeback first.
- Implement QueryCaps/Poll with no staged writes.
- Add token store and queue/backpressure.
- Implement Submit/Wait/Fence/Cancel after commit/retire model exists.

#### Тесты / критерии приёмки

- Current fail-closed tests remain for unapproved opcodes.
- QUERY_CAPS writes only specified rd and has no memory side effects.
- SUBMIT returns token id/status, not computed result.
- Faulted external token maps to precise retire exception only when approved.

#### Риски и ограничения

- Do not make FakeMatMul backend a production external accelerator protocol by accident.

## TASK-009. Global conflict/load-store hook

**Статус:** Future architecture, required for executable overlap and fences.

**Выбранное решение:** Installable GlobalMemoryConflictService: mandatory for executable DSC/L7 modes, absent in current fail-closed baseline. Range/footprint based, domain-aware.

#### Почему это оптимально для HybridCPU

- Current conflict manager optionality is correct for model-only APIs.
- Executable async DMA needs a global view of CPU load/store/atomic and accelerator footprints.
- Installable service avoids changing scalar behavior until executable feature is enabled.

#### Затрагиваемые файлы / классы / методы

| **Файл**                              | **Классы / методы**                                        | **Роль изменения**                                                    |
| ------------------------------------- | ---------------------------------------------------------- | --------------------------------------------------------------------- |
| ExternalAcceleratorConflictManager.cs | existing manager                                           | Reuse concepts but do not make it L7-only.                            |
| NEW: GlobalMemoryConflictService.cs   | RegisterFootprint, CheckAccess, CompleteToken, FenceDomain | Shared conflict service for DSC/L7/DMAController.                     |
| MemoryUnit.cs                         | Execute/ResolveArchitecturalAccess                         | Before bus Read/Write, query service if installed.                    |
| AtomicMemoryUnit.cs                   | ApplyResolvedRetireEffect, NotifyPhysicalWrite             | Notify conflict and invalidate reservations/cache on physical writes. |
| DmaStreamComputeTokenStore.cs         | active tokens                                              | Register active read/write footprints.                                |
| CPU_Core.Cache.cs                     | future invalidation API                                    | Conflict service triggers cache invalidation/flush hooks.             |

#### Архитектурная техника

- Conflict policy must be explicit: stall, replay, serialize, or trap. Default for executable MVP should be stall/serialize, not silent reorder.
- Service must understand domainTag and owner binding.
- Atomic reservations and DMA writes must interact: DMA write overlapping LR/SC reservation invalidates reservation.

#### Рекомендуемая последовательность реализации

- Build service as pure library with unit tests.
- Wire it into token stores first, not scalar pipeline.
- Add scalar load/store/atomic hooks behind feature flag.
- Turn on for executable DSC only after litmus tests pass.

#### Тесты / критерии приёмки

- Absent service preserves current behavior.
- Overlapping CPU store vs active DMA read causes specified conflict response.
- DMA write invalidates overlapping atomic reservations.
- Non-overlapping ranges do not serialize unnecessarily.

#### Риски и ограничения

- A mandatory always-on hook without feature gate risks regressions across existing scalar tests.

## TASK-010. Cache/coherency model с учётом прикреплённого кода

**Статус:** Corrected: cache/prefetch surfaces exist, but coherent DMA model does not.

**Выбранное решение:** Non-coherent explicit protocol: introduce range flush/invalidate APIs over existing L1/L2 data/VLIW/assist surfaces. Coherent DMA is a later feature, not the first solution.

#### Почему это оптимально для HybridCPU

- CPU_Core.Cache.cs already has L1_Data/L2_Data, VLIW cache, MaterializeCacheDataLine, VLIW fetch invalidation and domain data-cache flush.
- CPU_Core.Cache.Assist.cs already has assist-resident prefetch lines with partition policy.
- MaterializeCacheDataLine comments explicitly say exact execution surfaces still use fail-closed bound-memory helpers, so cache is not a universal execution datapath.
- A non-coherent explicit protocol is simpler, truthful and compatible with current memory paths.

#### Затрагиваемые файлы / классы / методы

| **Файл**                        | **Классы / методы**                                                                            | **Роль изменения**                                                                                  |
| ------------------------------- | ---------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------- |
| CPU_Core.Cache.cs               | FlushDomainFromDataCache, InvalidateVliwFetchState, GetDataByPointer, MaterializeCacheDataLine | Add InvalidateDataCacheRange and FlushDataCacheRange; preserve VLIW fetch invalidation as separate. |
| CPU_Core.Cache.Assist.cs        | TryPrefetchAssistDataLine, AssistResident                                                      | Invalidate assist-resident lines on overlapping DMA writes or domain revocation.                    |
| AtomicMemoryUnit.cs             | NotifyPhysicalWrite                                                                            | Extend or call a new memory observer so DMA/atomic writes invalidate reservations and cache ranges. |
| MemoryUnit.cs                   | Execute                                                                                        | Define whether scalar stores update/invalidate data-cache surfaces.                                 |
| DmaStreamComputeToken.cs        | Commit/TryCommitAllOrNone                                                                      | After successful commit, notify cache/conflict services for written ranges.                         |
| NEW: MemoryCoherencyObserver.cs | OnCpuWrite, OnDmaWrite, OnFence                                                                | Central fan-out: reservations, cache invalidation, conflict service.                                |

#### Архитектурная техника

- Flush before DMA reads if CPU stores may be buffered/cache-resident; if data cache is read-materialized only, FlushDataCacheRange can initially be no-op but documented.
- Invalidate after DMA writes for overlapping L1_Data/L2_Data and assist lines.
- VLIW/code writes use InvalidateVliwFetchState separately from data-cache invalidation.
- Coherent DMA/snooping is explicitly future and requires separate cache state/writeback design.

#### Рекомендуемая последовательность реализации

- Add overlap helper for Cache_Data_Object ranges.
- Implement InvalidateDataCacheRange(address,length,domainTag).
- Add MemoryCoherencyObserver and connect AtomicMemoryUnit.NotifyPhysicalWrite.
- In DSC Commit success path, call observer.OnDmaWrite for every staged write.
- Add tests for cache invalidation without claiming coherent reads.

#### Тесты / критерии приёмки

- Existing cache lines overlapping DMA write are invalidated.
- Non-overlapping cache lines survive.
- Domain flush still works.
- VLIW fetch invalidation remains separate and is not accidentally called for data-only DMA.
- No docs claim coherent DMA until FEATURE-010 coherent mode exists.

#### Риски и ограничения

- If scalar stores do not populate/write back through L1_Data/L2_Data, flush semantics must explicitly say "currently no-op for read-materialized cache".

# 4\. Dependency graph

| **Feature**                    | **Depends on**                                                                            | **Blocks / Enables**                                                               |
| ------------------------------ | ----------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------- |
| TASK-001 async lane6 DSC       | TASK-002 token store, TASK-003 precise faults, TASK-004 ordering, TASK-005 memory backend | Enables production DSC compiler lowering, async overlap, future poll/wait/fence.   |
| TASK-002 token lifecycle       | Baseline DmaStreamComputeToken                                                            | Required by TASK-001, TASK-003, TASK-004, TASK-007.                                |
| TASK-003 precise exceptions    | Token fault metadata, retire publication path                                             | Required before executable side effects become architectural.                      |
| TASK-004 memory ordering/fence | Token store, conflict service                                                             | Required before async overlap and executable fences.                               |
| TASK-005 IOMMU/addressing      | IBurstBackend/IOMMUBurstBackend, PhysicalBurstBackend                                     | Required for truthful virtual/IOMMU DSC2 and external accelerator memory.          |
| TASK-006 DSC2 ABI              | Parser design, capability model                                                           | Enables stride/tile/scatter-gather and explicit AddressSpace.                      |
| TASK-007 partial progress      | Token progress metadata                                                                   | Can be added before partial success; successful partial depends on async + ABI v2. |
| TASK-008 L7 executable ISA     | Register ABI, token store, queue, conflict service, commit/retire                         | Enables real external accelerators.                                                |
| TASK-009 conflict hook         | Active token stores, MemoryUnit/Atomic hooks                                              | Required for safe overlap and fences.                                              |
| TASK-010 cache protocol        | Cache range invalidation helpers, memory observer                                         | Required before docs can claim DMA/cache visibility discipline.                    |

# 5\. Phased implementation strategy

| **Phase**                                      | **Goal**                                                                                                              | **Key changes**                                                                                        | **Exit criteria**                                                       |
| ---------------------------------------------- | --------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------ | ----------------------------------------------------------------------- |
| Phase 6A: Architecture spec only               | Approve target semantics without changing current fail-closed behavior.                                               | State machines, token allocation, retire faults, ordering, IOMMU/cache protocol, compiler constraints. | Spec accepted; fail-closed tests unchanged.                             |
| Phase 6B: Infrastructure skeleton              | Add token store, async engine skeleton, backend resolver, conflict/coherency observer behind feature flags.           | New classes only; no production executable lowering.                                                   | All current tests pass; new skeleton tests pass.                        |
| Phase 6C: Memory backend correction            | Introduce PhysicalBurstBackend and explicit backend selection; support IOMMUTranslated path in test-only DSC2 parser. | IBurstBackend used by new async code path; IOMMUBurstBackend remains truthful.                         | Physical/IOMMU backend tests pass; no silent fallback.                  |
| Phase 6D: Async executable lane6 MVP           | Enable copy/add/mul/fma/reduce async token execution for DSC2 or feature-gated DSC mode.                              | Issue token, engine tick, CommitPending, retire commit/fault.                                          | Positive/negative DSC conformance suite passes.                         |
| Phase 6E: Ordering/fence integration           | Enable footprint conflicts, wait/fence/poll semantics, scalar memory litmus tests.                                    | GlobalMemoryConflictService, MemoryUnit/Atomic hooks, fence semantics.                                 | Overlap litmus tests deterministic.                                     |
| Phase 6F: Cache protocol integration           | Explicit invalidate/flush hooks around DMA writes/reads.                                                              | InvalidateDataCacheRange, FlushDataCacheRange, MemoryCoherencyObserver.                                | Cache invalidation tests pass; docs say non-coherent explicit protocol. |
| Phase 6G: External accelerator executable tier | L7 QUERY_CAPS/POLL then SUBMIT/WAIT/FENCE/CANCEL with token/queue/backend.                                            | Register ABI, DeviceRegistry, ExternalAcceleratorTokenStore.                                           | L7 conformance tests pass; model-only tests updated/compat-gated.       |

# 6\. Compiler / backend contract

- До включения executable mode backend не имеет права production-lower stream/DMA compute в lane6 DSC Execute path. Он может генерировать descriptors только для tests/model/golden helper, если это явно указано.
- После TASK-001 approval backend должен lower в descriptor + submit micro-op + explicit wait/fence/poll sequence, а не ожидать immediate memory visibility.
- Descriptor must carry explicit AddressSpace. Physical и IOMMUTranslated нельзя смешивать без явного ABI field.
- Backend не должен предполагать stride/tile/scatter-gather в DSC1. Эти encodings допустимы только в DSC2/capability-gated path.
- Backend не должен предполагать partial success. AllOrNone - default success contract; progress является telemetry/poll evidence, а не memory visibility guarantee.
- Для non-coherent cache model backend/runtime обязан вставлять или полагаться на explicit flush/invalidate/fence protocol согласно approved ABI.

# 7\. Обязательная тестовая стратегия

| **Test area**             | **Positive cases**                                                                    | **Negative cases / guards**                                                            |
| ------------------------- | ------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------- |
| Fail-closed compatibility | Current direct Execute throws for lane6/L7 until feature gate enabled.                | No hidden runtime fallback; no backend dispatch from carrier.                          |
| Token lifecycle           | Admitted -> Issued -> ReadsComplete -> ComputeComplete -> CommitPending -> Committed. | Cancel before commit clears staged writes; faulted token cannot commit.                |
| Precise faults            | Fault record maps to correct retire exception.                                        | Backend exception does not escape async engine as unprioritized throw.                 |
| Memory ordering           | Non-overlap can proceed; overlap stalls/serializes/faults per policy.                 | No silent reordering with active write footprint.                                      |
| IOMMU backend             | IOMMUTranslated descriptor calls IOMMU Read/Write with deviceID.                      | Mismatched DeviceId/guard rejects; no fallback to physical.                            |
| Cache protocol            | DMA write invalidates overlapping L1/L2 data and assist lines.                        | No claim of coherent snooping; VLIW invalidation not used for data-only writes.        |
| Descriptor ABI            | DSC1 legacy accepted; DSC2 stride/tile/scatter parse/normalize.                       | DSC1 rejects v2 encodings; overflow/alignment/domain rejects.                          |
| External accelerators     | QUERY_CAPS/POLL read-only when approved; SUBMIT returns token when approved.          | ACCEL\_\* remain fail-closed without feature gate; no rd writeback until ABI approved. |

# 8\. Документационные изменения

- Заменить "IOMMU отсутствует" на: "IOMMU-backed burst backend exists; executable DSC/L7 integration is not implied".
- Заменить "кэша нет" на: "cache/prefetch surfaces exist; coherent DMA/cache model is not implemented; explicit flush/invalidate required".
- В Phase 6 backlog уточнить TASK-005 как "backend/addressing contract", а не "future IOMMU from scratch".
- В TASK-010 уточнить, что future work не добавляет кэш, а формализует existing surfaces и DMA visibility protocol.
- В WhiteBook Current Contract оставить fail-closed и model-only ограничения до реальной implementation phase.
- В Future Design добавить dependency graph: async DSC -> token store -> precise retire -> ordering/fence -> cache/IOMMU -> compiler lowering.

# 9\. Итоговые рекомендации

- Не менять текущий fail-closed contract до Phase 6A approval. Это сохраняет доверие к документации и тестам.
- Выбрать async token-based lane6 DSC как целевую архитектуру, но реализовывать её поэтапно: сначала skeleton/token store/backend resolver, затем executable MVP.
- TASK-005 считать частично готовой инфраструктурой: IOMMUBurstBackend уже есть, но требует explicit descriptor/backend integration.
- TASK-010 формулировать как explicit non-coherent protocol поверх существующих cache/prefetch surfaces.
- DSC1/SDC1 не ломать. Все новые возможности - DSC2/SDC2 или capability-gated extensions.
- Compiler/backend lowering разрешать только после conformance suite и migration of docs from Future Design to Current Implemented Contract.

# Appendix A. Краткие source notes по прикреплённому коду

| **Файл**                 | **Линии / методы**        | **Вывод**                                                                                                              |
| ------------------------ | ------------------------- | ---------------------------------------------------------------------------------------------------------------------- |
| IBurstBackend.cs         | 30-64                     | Backend abstraction декларирует deviceID и virtual address для burst Read/Write.                                       |
| IOMMUBurstBackend.cs     | 22-39                     | Read/Write делегируют в Memory.IOMMU.ReadBurst/WriteBurst.                                                             |
| IOMMUBurstBackend.cs     | 42-58                     | Accelerator DMA registration/transfer fail-closed.                                                                     |
| CPU_Core.Cache.cs        | 14, 77-81, 200, 208-229   | L1/L2 data cache-like surfaces and data line materialization exist.                                                    |
| CPU_Core.Cache.cs        | 136-148                   | VLIW fetch invalidation exists and is separate from data-cache invalidation.                                           |
| CPU_Core.Cache.cs        | 232-270                   | MaterializeCacheDataLine reads bound main memory but comments exclude exact execution surfaces from this materializer. |
| CPU_Core.Cache.cs        | 353-382                   | Domain-aware data cache flush exists.                                                                                  |
| CPU_Core.Cache.Assist.cs | 11-60, 120-181            | Assist-prefetch residency and partition/victim selection exist.                                                        |
| MemoryUnit.cs            | 109-154                   | Scalar MemoryUnit uses IMemoryBus and immediate bus write in compatibility Execute path.                               |
| AtomicMemoryUnit.cs      | 284-350, 369-384, 481-500 | Atomic retire path uses main memory, reservation invalidation, alignment/bounds checks.                                |