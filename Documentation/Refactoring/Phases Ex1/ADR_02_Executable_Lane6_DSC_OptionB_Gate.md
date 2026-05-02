# ADR 02 - Executable Lane6 DSC Option B Gate

Status:
Proposed architecture gate. Not implementation approval.

Phase:
Phase 02 - Executable Lane6 DSC ADR Gate.

Decision date:
Not accepted. This document records the design gate and recommended position.

Scope:
Decide whether a future refactoring may move lane6
`DmaStreamComputeMicroOp` from a fail-closed typed-slot descriptor carrier to
an executable async token-based DSC ISA surface.

This ADR is implementation-oriented so that later code work has a precise
entry checklist. It does not authorize any CPU/ISE code changes by itself.

## Current Contract

The current implemented contract remains authoritative until this ADR is
explicitly approved and a separate implementation phase lands code and tests.

- `DmaStreamComputeMicroOp.Execute(ref Processor.CPU_Core core)` remains
  fail-closed.
- `DmaStreamComputeMicroOp` is a lane6 typed-slot descriptor carrier. It
  preserves descriptor, owner/domain, lane placement, resource, and normalized
  footprint evidence.
- `DmaStreamComputeMicroOp.WritesRegister` is `false`.
- `DmaStreamComputeMicroOp.SerializationClass` is
  `SerializationClass.MemoryOrdered`, but this is ordering metadata for the
  carrier, not proof of DMA execution.
- `DmaStreamComputeDescriptorParser.ExecutionEnabled == false`.
- DSC1 is immutable: `InlineContiguous` range encoding, `AllOrNone` partial
  completion policy, fixed v1 ABI rules, and reserved fields rejected.
- `DmaStreamComputeRuntime.ExecuteToCommitPending(...)` is helper/model-only.
  It is explicit runtime orchestration, not ISA execution, not pipeline issue,
  and not canonical retire publication.
- The current runtime/helper path uses physical main memory through
  `Processor.MainMemoryArea` and the physical helpers
  `TryReadPhysicalRange(...)` / `TryWritePhysicalRange(...)`.
- Tokens exist as model/runtime surfaces through `DmaStreamComputeToken`, but
  they are not allocated by normal pipeline issue/admission.
- Existing token fault records can create retire-style exceptions, and the
  pipeline exposes an explicit retired token commit helper, but there is no
  normal pipeline allocation, completion observation, priority, or retire path
  for executable lane6 DSC tokens.
- `IBurstBackend` and `IOMMUBurstBackend` exist, but current DSC helper code
  does not use them and they do not prove executable DSC/IOMMU integration.
- Cache and prefetch surfaces exist, but they do not form a coherent DMA/cache
  hierarchy.
- Compiler/backend production lowering to executable lane6 DSC remains
  forbidden.

## Decision Under Review

Two options are under review.

### Option A - Keep Fail-Closed Carrier

Keep the current contract unchanged:

- lane6 DSC remains descriptor/decode evidence only;
- `DmaStreamComputeMicroOp.Execute(...)` continues to throw fail-closed;
- no token is allocated by pipeline issue/admission;
- helper/model APIs remain explicit test/orchestration surfaces;
- compiler/backend production lowering to executable DSC remains prohibited.

This option is already selected for the current contract by the base Phase 3
decision.

### Option B - Make Lane6 DSC Executable

Future architecture target if approved:

- lane6 DSC becomes an executable async token-based ISA surface;
- issue/admission allocates a durable token only after typed-slot, descriptor,
  owner/domain, resource, footprint, quota, and replay authority checks;
- execution progresses through a scheduler/engine, not through a monolithic
  long-latency mutation inside `MicroOp.Execute`;
- completion is observed through explicitly specified poll, wait, fence,
  interrupt, retire observation, or an approved mix;
- memory publication occurs only through an approved commit/retire boundary;
- faults become precise architectural faults only through an approved retire
  publication model.

Direct synchronous routing from
`DmaStreamComputeMicroOp.Execute(...)` to
`DmaStreamComputeRuntime.ExecuteToCommitPending(...)` is rejected and
forbidden. It would bypass issue/admission allocation, scheduler ownership,
retire, replay, squash, ordering, cache visibility, backend selection, and
precise exception semantics. This direct call is not an acceptable
implementation of Option B.

## Recommended Decision

Recommended position:

- keep Option A as the current implemented contract;
- accept Option B only as the target Future Architecture;
- do not approve executable lane6 DSC implementation until every prerequisite
  in this ADR is designed, reviewed, and linked to a later implementation
  phase;
- when Option B is approved, require an async token-based design where
  `Execute` is at most an admission/scheduling boundary or carrier dispatch
  metadata path, never a direct monolithic runtime helper call.

This recommendation preserves the fail-closed baseline while giving Option B a
concrete implementation checklist.

## Accepted And Rejected Alternatives

Accepted for current code:

- Option A, fail-closed typed-slot carrier.
- Explicit helper/model execution through `DmaStreamComputeRuntime` in tests or
  runtime orchestration outside canonical micro-op execution.
- DSC1 `InlineContiguous` and `AllOrNone` as the only current successful helper
  mode.
- Physical main-memory helper path as the current model/runtime path.

Accepted only as future target after approval:

- Option B, async token-based executable lane6 DSC.
- Token allocation at issue/admission, not decode.
- Token store plus scheduler/engine-driven progress.
- Commit/retire publication after precise fault, replay, ordering, backend,
  cache, and compiler contracts are approved.

Rejected:

- Calling `DmaStreamComputeRuntime.ExecuteToCommitPending(...)` directly from
  `DmaStreamComputeMicroOp.Execute(...)`.
- Treating helper/model token creation as pipeline issue allocation.
- Allocating durable DSC tokens at decode.
- Publishing memory before an architectural commit/retire boundary exists.
- Treating partial completion as a successful architectural mode.
- Treating `IBurstBackend` / `IOMMUBurstBackend` existence as executable DSC
  IOMMU integration.
- Treating cache/prefetch surfaces as coherent DMA/cache hierarchy.
- Allowing compiler/backend production lowering before executable semantics
  and conformance tests exist.

## Required Prerequisites Before Code

No executable lane6 DSC code may be written until the following prerequisites
are designed and approved.

- Token scheduler:
  an owner for active async DSC progress, backpressure, quotas, fairness,
  timeout/progress diagnostics, and engine ticks.
- Token store:
  durable active token storage with lookup by token ID, owner binding,
  descriptor identity, lane/slot metadata, issuing PC, virtual thread,
  context/core/pod identity, and cancellation state.
- Issue/admission allocation boundary:
  exact point where pipeline issue/admission validates typed lane, descriptor,
  owner/domain guard, normalized footprints, resource masks, conflict
  registration, token quota, and replay authority before creating a token.
- Runtime step decomposition:
  split helper behavior into validation, source read, compute, stage,
  completion, and commit-ready steps. The existing
  `ExecuteToCommitPending(...)` helper must not become canonical ISA
  execution.
- Completion model:
  define whether completion is observed by poll, wait, fence, interrupt,
  retire observation, or an explicit mix; define token visibility and status
  result encodings.
- Commit/retire boundary:
  define when staged writes become architecturally visible and how all-or-none
  commit is sequenced against normal retire.
- Precise fault publication:
  define fault priority, issuing PC, slot/lane identity, virtual thread,
  context, descriptor address, fault address, read/write direction, and
  ordering versus other bundle/lane faults.
- Replay, squash, trap, and context-switch cancellation:
  define active token cancellation, late completion suppression, cleanup,
  rollback, restartability, and ownership transfer rules.
- Ordering, fence, wait, and poll semantics:
  define interaction with CPU loads, stores, atomics, store buffers,
  serializing operations, `MemoryOrdered` carrier metadata, waits, polls, and
  fences.
- Physical versus IOMMU backend selection:
  define descriptor/address-space fields or capability gates, device ID
  binding, translation/fault mapping, no-silent-fallback rules, and explicit
  selection between physical helper and `IBurstBackend`/`IOMMUBurstBackend`.
- Cache flush/invalidate protocol:
  define non-coherent explicit data flush, writeback if required, invalidate,
  assist/SRF visibility, VLIW fetch invalidation separation, and future
  coherent DMA gate if coherency is ever claimed.
- Compiler/backend lowering contract:
  define feature/capability detection, ABI versioning, fallback, scheduling
  constraints, fences/waits emitted by the backend, and the exact point where
  production lowering becomes legal.
- Conformance and migration tests:
  define fail-closed compatibility tests, executable positive tests,
  cancellation tests, fault/retire tests, ordering litmus tests, backend/IOMMU
  tests, cache protocol tests, compiler conformance tests, and documentation
  claim-safety tests.

## Implementation Phases Enabled Only After Approval

Approval of this ADR would enable planning for later implementation phases. It
would not merge Future Design into Current Implemented Contract.

Enabled workstreams after approval:

1. Phase 03 token lifecycle and issue/admission infrastructure:
   token store, allocation boundary, active-token lookup, quotas, cancellation
   hooks, telemetry, and no decode allocation.
2. Phase 04 precise faults and retire publication:
   fault priority table, issuing metadata, retire observation, and token fault
   publication.
3. Phase 05 ordering and global conflict service:
   CPU load/store/atomic hooks, active token footprint registration, wait,
   poll, fence, and overlap rules.
4. Phase 06 addressing backend and IOMMU integration:
   explicit physical/IOMMU backend resolver, descriptor/address-space contract,
   device ID binding, and fault mapping.
5. Phase 09 cache/prefetch and non-coherent protocol:
   explicit flush/invalidate surfaces and observer integration, with coherent
   DMA still requiring a separate ADR.
6. Phase 11 compiler/backend lowering contract:
   capability-gated backend lowering only after executable semantics and tests
   exist.
7. Phase 12 conformance and documentation migration:
   positive/negative conformance suites and controlled migration from Future
   Design to Current Implemented Contract after code lands.

## Exact Non-Goals

This ADR does not:

- implement executable DSC;
- change `DmaStreamComputeMicroOp.Execute(...)`;
- change `DmaStreamComputeDescriptorParser.ExecutionEnabled`;
- make `DmaStreamComputeRuntime.ExecuteToCommitPending(...)` canonical ISA
  execution;
- allocate tokens from decode or current pipeline issue;
- introduce a token store;
- add async overlap, queueing, pause, resume, wait, poll, or fence ISA;
- add IOMMU-translated DSC execution;
- add coherent DMA/cache hierarchy;
- add successful partial completion;
- approve DSC2 stride/tile/scatter-gather/address-space extensions;
- approve executable L7 `ACCEL_*`;
- approve compiler/backend production lowering.

## Compatibility Impact

Option A is compatible with existing behavior.

Option B is breaking relative to the Phase 3 current contract because direct
lane6 `Execute` currently throws fail-closed and existing tests protect that
boundary. Any future Option B implementation must define:

- whether fail-closed tests remain as compatibility-mode tests or are retired;
- how explicit helper/runtime tests map to architectural token execution;
- how DSC1 ABI compatibility is preserved while future DSC2/capability fields
  are introduced;
- how old code that preserved descriptors but did not execute them continues
  to behave;
- how production compiler/backend lowering remains gated until executable
  semantics are conformance-tested;
- how documentation text migrates only after code and tests prove the new
  implemented behavior.

## Tests Required Before Any Executable Claim

No documentation, compiler, or code path may claim lane6 DSC is executable
until tests cover the following.

- Existing negative direct-execute tests remain authoritative until the
  compatibility plan explicitly changes them.
- Parser tests prove `ExecutionEnabled` remains false until the approved phase
  changes it.
- Positive executable copy/add/mul/fma/reduce tests after Option B
  implementation.
- Token issue/admission allocation tests proving allocation happens after
  typed-slot, guard, resource, footprint, and replay checks.
- Token store lifetime tests for lookup, completion, cancellation, cleanup,
  and ownership.
- Async scheduler/progress tests with completion observation.
- All-or-none commit and rollback tests.
- Precise retire fault tests covering priority against other VLIW lanes and
  stages.
- Replay, squash, trap, and context-switch cancellation tests.
- Ordering litmus tests against CPU loads, stores, atomics, fences, waits, and
  polls.
- Physical backend tests and IOMMU backend tests with explicit no-silent-
  fallback behavior.
- Cache flush/invalidate tests for the approved non-coherent protocol.
- Compiler/backend conformance tests proving production lowering is disabled
  without the executable capability and correct when enabled.
- Documentation claim-safety tests proving Future Design did not move into
  Current Implemented Contract before code and tests landed.

## Documentation Migration Rule

Until code and tests land, all executable DSC text must remain under Future
Design, Future Architecture, Future gated, or Design required headings.

Current Implemented Contract text may be updated only after:

1. this ADR is approved;
2. implementation phases land code for token allocation, scheduler/store,
   retire publication, ordering, backend/addressing, cache protocol, and
   compiler gating;
3. conformance tests pass;
4. compatibility decisions for existing fail-closed tests are recorded;
5. docs explicitly separate physical helper behavior, IOMMU-backed executable
   behavior, and cache protocol claims.

No documentation migration may imply:

- lane6 DSC is executable before code and tests prove it;
- async DMA overlap exists before scheduler/completion/order tests exist;
- DSC memory access is IOMMU-translated before backend selection and fault
  mapping land;
- cache/prefetch surfaces are coherent DMA hierarchy before a separate coherent
  DMA ADR and implementation;
- compiler/backend may production-lower before the lowering contract and
  conformance tests are complete;
- partial completion is successful before a separate future ADR approves that
  mode.

## Code Evidence

Current fail-closed carrier:

- `HybridCPU_ISE\Core\Pipeline\MicroOps\DmaStreamComputeMicroOp.cs`
  - `DmaStreamComputeMicroOp` is documented as the canonical lane6 typed-slot
    carrier for descriptor-backed memory-memory compute.
  - Constructor validates owner/domain guard evidence and mandatory read/write
    footprints.
  - `SerializationClass = SerializationClass.MemoryOrdered`.
  - `WritesRegister = false`.
  - `Execute(ref Processor.CPU_Core core)` throws with a fail-closed message
    and explicitly states that `DmaStreamComputeRuntime` is a helper, not
    `MicroOp.Execute`, with no StreamEngine or DMAController fallback implied.

Current descriptor parser:

- `HybridCPU_ISE\Core\Execution\DmaStreamCompute\DmaStreamComputeDescriptorParser.cs`
  - `ExecutionEnabled => false`.
  - Parser requires guard-backed owner/domain acceptance.
  - Parser rejects reserved fields.
  - Parser accepts only `DmaStreamComputeRangeEncoding.InlineContiguous`.
  - Parser accepts only `DmaStreamComputePartialCompletionPolicy.AllOrNone`.

Current runtime/helper:

- `HybridCPU_ISE\Core\Execution\DmaStreamCompute\DmaStreamComputeRuntime.cs`
  - `ExecuteToCommitPending(...)` is documented as an explicit runtime/model
    helper that is intentionally not wired into
    `DmaStreamComputeMicroOp.Execute` or a hidden StreamEngine/DMA path.
  - The default overload creates `new DmaStreamAcceleratorBackend(Processor.MainMemory, ...)`.
  - The helper creates a `DmaStreamComputeToken`, calls `MarkIssued()`, reads
    operands, computes, stages destination writes, and returns after
    `MarkComputeComplete()` reaches `CommitPending` or a fault state.

Current token model:

- `HybridCPU_ISE\Core\Execution\DmaStreamCompute\DmaStreamComputeToken.cs`
  - Token states include `Admitted`, `Issued`, `ReadsComplete`,
    `ComputeComplete`, `CommitPending`, `Committed`, `Faulted`, and
    `Canceled`.
  - `TryAdmit(...)`, `MarkIssued()`, `StageDestinationWrite(...)`,
    `MarkComputeComplete()`, `Cancel(...)`, `Commit(...)`, and fault
    publication APIs exist as model/runtime surfaces.
  - `Commit(...)` takes `Processor.MainMemoryArea` and a commit guard decision.
  - `TryCommitAllOrNone(...)` snapshots and writes physical main memory through
    `TryReadPhysicalRange(...)` / `TryWritePhysicalRange(...)`, with rollback
    on partial write failure.
  - Fault results can request retire exception publication, but this does not
    prove normal pipeline token allocation or executable DSC retire semantics.

Current backend/helper memory path:

- `HybridCPU_ISE\Core\Execution\DmaStreamCompute\DmaStreamAcceleratorBackend.cs`
  - Holds `Processor.MainMemoryArea`.
  - `TryReadRange(...)` calls `TryReadPhysicalRange(...)`.
  - This backend is a current physical helper path, not IOMMU-backed executable
    DSC.

Backend infrastructure that is not current DSC integration:

- `HybridCPU_ISE\Core\Execution\BurstIO\IBurstBackend.cs`
  - Defines burst `Read(...)` / `Write(...)` plus accelerator DMA API surface.
- `HybridCPU_ISE\Core\Execution\BurstIO\IOMMUBurstBackend.cs`
  - `Read(...)` and `Write(...)` delegate to `Memory.IOMMU`.
  - `RegisterAcceleratorDevice(...)` and `InitiateAcceleratorDMA(...)` remain
    fail-closed.
  - Existence of this backend is infrastructure only; current DSC helper does
    not use it.

Pipeline/retire evidence:

- `HybridCPU_ISE\Core\Pipeline\Core\CPU_Core.PipelineExecution.Memory.cs`
  - `ApplyRetiredDmaStreamComputeTokenCommit(...)` exists as an explicit token
    commit helper at a retired boundary and calls `token.Commit(...)`.
  - This helper does not prove that normal pipeline issue allocates DSC tokens
    or observes asynchronous completion.
- `HybridCPU_ISE\Core\Pipeline\Core\CPU_Core.PipelineExecution.Retire.cs`
  - Existing retire machinery captures and applies retire effects for current
    pipeline lanes. Option B must integrate with this boundary explicitly.
- `HybridCPU_ISE\Core\Decoder\ClusterIssuePreparation.cs` and
  `HybridCPU_ISE\Core\Pipeline\Scheduling\MicroOpScheduler.Admission.cs`
  - Current decode/issue/admission and replay-aware scheduling surfaces exist.
    Option B must define where token allocation happens here; current code does
    not allocate DSC tokens from these surfaces.

## Strict Prohibitions

The following claims must not appear in Current Implemented Contract,
compiler/backend production docs, or test names until code and tests prove
them:

- lane6 DSC is already executable;
- async DMA overlap is implemented;
- IOMMU is already integrated with executable DSC;
- cache/prefetch surfaces are a coherent DMA hierarchy;
- compiler/backend may production-lower to executable DSC;
- partial completion is a successful architectural mode;
- `DmaStreamComputeRuntime.ExecuteToCommitPending(...)` is canonical ISA
  execution;
- helper/model token creation is pipeline issue allocation;
- fake/test or infrastructure backends are production executable DSC protocol.

## Exit Criteria For ADR Approval

This ADR can be marked approved only when the approval record includes:

- selected option;
- exact instruction semantics;
- token scheduler and token store design;
- issue/admission allocation boundary;
- runtime step decomposition plan;
- completion model;
- commit/retire boundary;
- precise fault publication and priority;
- replay/squash/trap/context-switch cancellation;
- ordering/fence/wait/poll semantics;
- physical/IOMMU backend selection;
- cache flush/invalidate protocol;
- compiler/backend lowering contract;
- conformance and documentation migration test plan;
- compatibility plan for current fail-closed tests.

Until all exit criteria are met, this ADR remains a gate, not an approval.
