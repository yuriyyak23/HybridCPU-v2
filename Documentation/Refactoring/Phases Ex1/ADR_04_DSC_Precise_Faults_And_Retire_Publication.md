# ADR 04 - DSC Precise Faults And Retire Publication

Status:
Proposed design gate. Not implementation approval.

Phase:
Phase 04 - DSC Precise Faults And Retire Publication.

Decision date:
Not accepted. This document records the fault/retire publication contract for
future Option B work only.

Scope:
Define the metadata, fault normalization, priority, retire observation, and
publication rules required before future executable lane6 DSC can claim precise
architectural exceptions.

This ADR does not authorize executable DSC. Phase 02 and Phase 03 approval
remain required before any CPU/ISE implementation can change lane6 behavior.

## Current Contract

The current implemented contract remains unchanged.

- `DmaStreamComputeMicroOp.Execute(ref Processor.CPU_Core core)` remains
  fail-closed.
- Normal pipeline issue/admission does not allocate DSC tokens.
- Normal pipeline retire does not observe asynchronous DSC token completion.
- `DmaStreamComputeFaultRecord.RequiresRetireExceptionPublication` exists, but
  it is a model/retire-style surface, not proof of full pipeline-precise DSC
  exceptions.
- `DmaStreamComputeFaultRecord.CreateRetireException()` maps model fault
  records to exception objects. It does not supply a complete issuing-PC,
  bundle-age, slot/lane, or retire-priority contract for executable DSC.
- `DmaStreamComputeValidationResult` exposes descriptor, carrier,
  owner/domain, range, alignment, quota/backpressure, token-cap, and
  execution-disabled fault classes.
- `DmaStreamComputeToken.PublishFault(...)` records token faults and clears
  staged writes.
- `DmaStreamComputeToken.Commit(...)` rejects canceled/faulted tokens, requires
  `CommitPending`, performs all-or-none physical main-memory commit, and
  returns a commit result.
- `CPU_Core.PipelineExecution.Memory.ApplyRetiredDmaStreamComputeTokenCommit`
  and test-support commit helpers exist as explicit publication surfaces.
- Current helper/runtime faults must not be described as full architectural
  precise exceptions.
- Backend/helper host exceptions must not become architectural behavior by
  escaping from async code; future executable mode must normalize them first.

## Decision

If executable lane6 DSC is approved later, DSC faults must be precise retire
publications.

Future executable DSC must satisfy these rules:

- async/runtime/backend errors are recorded as token fault records;
- backend exceptions are converted into `DmaStreamComputeFaultRecord` or an
  approved architectural fault carrier before retire;
- no async engine, backend, helper, or scheduler code may throw an
  unprioritized architectural exception directly into host control flow;
- faulted tokens do not publish memory;
- canceled tokens do not publish memory;
- `CommitPending` does not mean memory is visible;
- memory publication happens only at the approved commit/retire boundary;
- commit failure must roll back all-or-none state before fault publication;
- younger token completion or fault cannot preempt an older architectural fault.

Recommended MVP publication model:

- the issuing lane6 instruction remains retire-pending until its token reaches
  `CommitPending`, `Faulted`, or `Canceled`;
- `Admitted`, `Issued`, `ReadsComplete`, and `ComputeComplete` at retire cause
  a retire stall/drain, not success and not a fault;
- `CommitPending` is observed by retire, then all-or-none commit is attempted
  at the retire/commit owner;
- `Faulted` is published as a precise architectural exception at retire;
- `Canceled` is handled according to its cancellation cause. Cancellation due
  to replay/squash/trap/context switch must not publish a new architectural
  DSC success or memory effect.

Early-retire token handles, fire-and-forget publication, interrupts, and
wait/poll/fence-only completion are rejected for the first executable DSC
claim unless a later ADR defines a complete architectural token result and
completion-observation contract.

## Required Metadata

Every future executable token and every publishable DSC fault must retain
enough metadata to compete in the retire window.

Required token metadata:

- token ID;
- issuing PC;
- bundle identity or serial;
- architectural instruction age;
- slot index and physical lane index;
- slot class/lane class;
- virtual thread ID;
- owner thread ID if distinct;
- owner context ID;
- owner core ID;
- owner pod ID;
- owner domain tag;
- active domain certificate or equivalent authority evidence;
- device ID;
- descriptor address/reference;
- descriptor identity hash;
- normalized footprint hash;
- issue/admission cycle;
- completion cycle if complete;
- cancellation reason if canceled;
- replay epoch/generation;
- backend/address-space selection once Phase 06 exists.

Required fault metadata:

- token ID if allocated;
- fault kind;
- validation fault source if applicable;
- fault address;
- read/write direction;
- issuing PC, bundle identity, slot/lane, and age;
- owner virtual thread/context/core/pod/domain;
- device ID;
- descriptor identity and descriptor address;
- source phase: carrier decode, descriptor parse, admission, read, compute,
  stage, completion, commit, backend, IOMMU, ordering, cancellation;
- whether staged writes were discarded;
- whether rollback was attempted and completed for commit faults.

Current `DmaStreamComputeFaultRecord` does not carry all of this data. A future
implementation must extend it or wrap it in a retire publication record before
claiming precise executable DSC faults.

## Fault Priority

Architectural priority is age-first, then stage/slot deterministic.

Global rule:

- an older bundle/instruction fault wins over a younger token completion or
  token fault;
- within the same bundle, the approved slot order wins;
- within the same slot/token, the table below defines DSC fault class priority;
- host/backend exceptions are never priority participants until normalized
  into token fault records.

Recommended same-token fault priority:

| Priority | Class | Publication rule |
|---|---|---|
| 1 | Pre-issue replay/squash cancellation | No architectural DSC fault; no token publication if allocation did not occur. |
| 2 | Carrier decode and typed-slot legality | Publish against issuing bundle/slot if the carrier reaches architectural fault handling. |
| 3 | Descriptor ABI, reserved fields, range, alignment, alias, owner/domain guard | Admission-time architectural fault, before async side effects. |
| 4 | Quota/backpressure/token-cap | Default is deterministic stall/retry or telemetry-only reject with no token. Architectural fault only if a later approval explicitly selects that policy. |
| 5 | Runtime read, translation, permission, alignment, IOMMU, DMA device/backend faults | Token fault held until retire publication. |
| 6 | Compute, stage, exact-coverage, partial-completion violations | Token fault; no memory visibility. |
| 7 | Commit guard, owner/domain/device drift | Retire/commit fault before memory publication. |
| 8 | Commit physical/write failure after rollback | Retire/commit fault only after all-or-none rollback succeeds or a fatal rollback failure is escalated. |
| 9 | Post-issue cancellation | Follows cancellation cause. Replay/squash/trap/context-switch cancellation suppresses DSC success and memory publication. |

Cross-pipeline priority:

- existing older WB/MEM/EX precise fault carriers remain older-stage winners
  according to the current stage-aware pipeline policy;
- a DSC token fault for a lane6 instruction must be injected into the retire
  decision with its issuing instruction age, not with the async completion
  time;
- a younger scalar/vector/memory/system fault must not overtake an older DSC
  token fault once that older DSC instruction is retire-observable;
- a younger DSC token completion must not commit before older unresolved
  retire faults.

## Fault Mapping

Validation fault mapping:

| Validation source | Future token/publication class |
|---|---|
| `DescriptorDecodeFault`, `DescriptorCarrierDecodeFault`, `DescriptorReferenceLost`, `ReservedFieldFault`, `RangeOverflow`, `ZeroLengthFault` | Descriptor/carrier architectural fault at admission or carrier fault handling. |
| `UnsupportedAbiVersion`, `UnsupportedOperation`, `UnsupportedElementType`, `UnsupportedShape`, `ExecutionDisabled` | Unsupported ABI/operation or execution-disabled architectural fault. |
| `AlignmentFault` | Alignment/page-style architectural fault. |
| `AliasOverlapFault` | Descriptor footprint/alias architectural fault. |
| `OwnerDomainFault` | Domain or owner-context architectural fault. |
| `QuotaAdmissionReject`, `BackpressureAdmissionReject`, `TokenCapAdmissionReject` | No token by default; deterministic stall/retry or telemetry-only reject unless later ADR promotes to architectural fault. |

Runtime/backend/commit mapping:

| Runtime source | Future publication class |
|---|---|
| source read failure | read memory/backend fault held in token |
| translation or permission failure after Phase 06 | IOMMU/page-style token fault |
| unsupported runtime ABI reached after admission | unsupported ABI/operation token fault |
| stage coverage mismatch | partial-completion/coverage token fault, no memory visibility |
| commit guard mismatch | owner/domain/device commit fault |
| physical write failure | all-or-none commit fault after rollback |
| rollback failure | fatal implementation error path; cannot be reported as successful architectural partial completion |
| backend host exception | converted to token backend/device fault before retire publication |

Exception object mapping must be reviewed during implementation. The current
model maps domain/owner faults to `DomainFaultException`, memory-like faults
to `PageFaultException`, and other faults to `InvalidOperationException`. That
mapping is insufficient by itself because it lacks issuing PC and retire
priority metadata.

## Retire Observation Rule

Future executable DSC retire observation must be explicit.

Recommended MVP rules:

| Token state at retire | Retire behavior |
|---|---|
| no token allocated | fail closed; this is an implementation bug unless instruction was canceled before issue |
| `Admitted` | stall/drain; no success, no exception, no memory publication |
| `Issued` | stall/drain; no success, no exception, no memory publication |
| `ReadsComplete` | stall/drain; no success, no exception, no memory publication |
| `ComputeComplete` | stall/drain; no success, no exception, no memory publication |
| `CommitPending` | attempt all-or-none commit at retire; success retires, failure publishes precise commit fault |
| `Faulted` | publish precise exception using issuing metadata and priority |
| `Canceled` before architectural issue | no architectural DSC fault, no memory publication |
| `Canceled` after issue | publish or suppress according to cancellation cause and Phase 03 cancellation contract; never publish memory |
| `Committed` before retire owner observes it | illegal for MVP; commit must be retire-owned |

The retire observer must not use async completion time as instruction age. It
must use issuing metadata captured at issue/admission.

## Retire Publication Algorithm

Future implementation must be equivalent to this algorithm:

1. At issue/admission, capture issuing metadata required by this ADR.
2. The scheduler/backend records async progress only into the token.
3. Any runtime/backend exception is normalized into token fault state.
4. The retire window reaches the lane6 instruction.
5. Retire looks up the token by token ID and owner metadata.
6. If the token is not retire-observable, retire stalls or drains according to
   the scheduler policy.
7. If the token is `Faulted`, retire inserts a DSC fault candidate using the
   issuing PC/slot/lane/age.
8. If the token is `CommitPending`, retire performs all-or-none commit through
   the approved memory/backend path.
9. Commit success publishes architectural memory and retires the instruction.
10. Commit fault rolls back before publishing one precise fault candidate.
11. The retire priority resolver selects the oldest/highest-priority fault
    across DSC, scalar, vector, memory, system, trap, and other lane carriers.
12. Younger work is suppressed, squashed, or canceled according to the existing
    stage-aware policy plus future Phase 03 cancellation hooks.

## Exact Non-Goals

This ADR does not:

- implement precise DSC exceptions;
- change `DmaStreamComputeFaultRecord`;
- change `DmaStreamComputeToken`;
- change `DmaStreamComputeMicroOp.Execute(...)`;
- wire DSC tokens into normal pipeline issue or retire;
- define Phase 05 memory ordering, wait, poll, or fence behavior;
- define Phase 06 IOMMU/backend address selection;
- define successful partial completion;
- allow backend exceptions to escape as architectural exceptions;
- approve compiler/backend production lowering.

## Implementation Phases Enabled Only After Approval

Approval of this ADR would enable later implementation planning for:

1. Extended token/fault publication metadata.
2. A DSC retire observer that consumes the Phase 03 token store.
3. Fault normalization wrappers for runtime/backend/IOMMU/commit failures.
4. Fault priority integration with existing stage-aware pipeline machinery.
5. All-or-none commit fault rollback publication.
6. Cancellation-to-retire interaction for replay, squash, trap, and context
   switch.
7. Conformance tests for multi-slot and async completion ordering.

These remain blocked from code implementation until Phase 02, Phase 03, and
this Phase 04 design are all approved.

## Tests Required Before Implementation

Before any precise executable DSC fault claim, tests must cover:

- descriptor decode fault maps to the approved architectural exception;
- reserved-field and unsupported-ABI faults map deterministically;
- owner/domain mismatch maps to domain/owner fault with issuing PC metadata;
- scalar memory fault versus DSC descriptor fault in the same VLIW bundle;
- older DSC token fault versus younger scalar/vector/memory/system fault;
- younger DSC completion cannot preempt older architectural fault;
- runtime read fault is held in token until retire;
- backend host exception is converted to token fault;
- IOMMU translation/permission fault after Phase 06 maps through token fault;
- compute/stage coverage fault clears staged writes and cannot publish memory;
- faulted token cannot commit;
- canceled token cannot publish memory;
- commit guard failure publishes one precise fault and no memory success;
- physical write failure rolls back and publishes one precise commit fault;
- rollback failure is not reported as partial successful completion;
- token still `Issued` or `ReadsComplete` at retire stalls/drains, not retires;
- `Committed` before retire owner observation is rejected in MVP;
- documentation claim-safety tests prove helper/model faults were not migrated
  into Current Implemented Contract as full precise exceptions.

## Documentation Migration Rule

Until implementation and tests land, documentation must say:

- current DSC faults are model/retire-style surfaces;
- current `CreateRetireException()` is not full pipeline-precise executable DSC;
- current token commit helpers are explicit/test-support publication surfaces;
- future executable DSC faults require issuing metadata and retire priority;
- backend and runtime faults must be normalized before architectural
  publication.

Current Implemented Contract must not say:

- executable DSC faults are precise today;
- async token faults are automatically published at retire today;
- helper/runtime exceptions are architectural precise exceptions;
- commit-pending token state means memory has been published;
- backend host exceptions are architectural DSC behavior;
- partial completion is a successful architectural mode.

## Code Evidence

Fault record and token surfaces:

- `HybridCPU_ISE\Core\Execution\DmaStreamCompute\DmaStreamComputeToken.cs`
  - `DmaStreamComputeTokenFaultKind` defines descriptor, unsupported ABI,
    translation, permission, domain, owner-context, alignment, alias, DMA
    device, partial completion, replay invalidation, memory, and
    execution-disabled fault kinds.
  - `DmaStreamComputeFaultRecord.RequiresRetireExceptionPublication` returns
    true.
  - `DmaStreamComputeFaultRecord.CreateRetireException()` maps model records to
    exception objects.
  - `DmaStreamComputeCommitResult.RequiresRetireExceptionPublication` delegates
    to the fault record.
  - `DmaStreamComputeToken.PublishFault(...)` records `LastFault`, clears
    staged writes, marks `Faulted`, and returns a faulted commit result.
  - `DmaStreamComputeToken.Commit(...)` returns canceled/faulted results before
    commit and requires `CommitPending`.
  - `TryCommitAllOrNone(...)` snapshots, writes, rolls back on write failure,
    and reports a memory fault instead of successful partial completion.

Validation surfaces:

- `HybridCPU_ISE\Core\Execution\DmaStreamCompute\DmaStreamComputeValidationResult.cs`
  - Exposes validation faults for descriptor decode, unsupported ABI/op/type/
    shape, carrier decode, descriptor reference loss, reserved fields, range
    overflow, alignment, zero length, alias overlap, owner/domain, quota,
    execution disabled, backpressure, and token cap.

Runtime/helper fault production:

- `HybridCPU_ISE\Core\Execution\DmaStreamCompute\DmaStreamComputeRuntime.cs`
  - Runtime validation/read/compute/stage helpers call `token.PublishFault(...)`
    for unsupported ABI, descriptor shape/count failures, memory faults, and
    partial-completion/coverage faults.
  - This is explicit helper/model behavior, not pipeline-precise publication.

Retire/test publication seams:

- `HybridCPU_ISE\Core\Pipeline\Core\CPU_Core.PipelineExecution.Memory.cs`
  - `ApplyRetiredDmaStreamComputeTokenCommit(...)` calls
    `token.Commit(...)` and throws `result.CreateRetireException()` if the
    commit result requires retire exception publication.
- `HybridCPU_ISE\Core\Pipeline\Core\CPU_Core.TestSupport.cs`
  - `TestApplyDmaStreamComputeTokenCommit(...)` exposes the retired token
    commit seam for tests.

Existing pipeline exception machinery:

- `HybridCPU_ISE\Core\Pipeline\Core\CPU_Core.Pipeline.Helpers.cs`
  - `StageAwareExceptionWinnerMetadata` carries winner stage, lane, owner
    thread, virtual thread, fault address, write direction, and PC.
  - `TryResolveStageAwareExceptionWinnerMetadata(...)` and
    `TryResolveExceptionDeliveryDecisionForRetireWindow(...)` implement
    current stage-aware fault winner selection for materialized stage lanes.
- `HybridCPU_ISE\Core\Pipeline\Core\CPU_Core.PipelineExecution.Exceptions.cs`
  - Stage-aware execute/memory page fault delivery marks active lanes, resolves
    older-stage winners, flushes on trap, and throws the selected exception.
- `HybridCPU_ISE\Core\Pipeline\Core\CPU_Core.PipelineExecution.Retire.cs`
  - Retire finalization can deliver stage-aware retire-window faults and uses
    `RetireCoordinator.Retire(...)` for retire records.
  - Current machinery does not allocate or prioritize DSC token faults from
    normal pipeline issue.

## Strict Prohibitions

Do not claim any of the following until code and tests prove them:

- DSC helper faults are full pipeline-precise exceptions;
- async DSC backend faults publish architectural exceptions directly;
- normal pipeline retire observes DSC token faults today;
- token completion time is architectural instruction age;
- `CommitPending` means memory is visible;
- faulted or canceled tokens can publish memory;
- commit failure can report successful partial completion;
- backend host exceptions are architectural DSC exceptions;
- compiler/backend may rely on precise executable DSC faults.

## Exit Criteria

This Phase 04 design can be marked approved only when the approval record
includes:

- final fault priority table;
- final issuing metadata format;
- final token fault publication record shape;
- validation/runtime/backend/commit fault mapping;
- retire observation rule for every token state;
- multi-slot and cross-stage priority policy;
- backend host-exception normalization policy;
- commit rollback and fatal rollback handling policy;
- replay/squash/trap/context-switch fault suppression/cancellation policy;
- test matrix and documentation migration rule.

Until then, this file is a design gate only.
