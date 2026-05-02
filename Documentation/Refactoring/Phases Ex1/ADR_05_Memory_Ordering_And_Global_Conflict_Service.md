# ADR 05: Memory Ordering And Global Conflict Service Gate

## Status

Proposed design gate.

This ADR is implementation-oriented, but it does not approve CPU/ISE code changes by itself. Any executable DSC, executable L7, asynchronous DMA overlap, or coherent DMA/cache claim remains blocked until the prerequisites and tests in this document are satisfied.

## Context

Phase 05 covers TASK-004 and TASK-009 from the refactoring plan: memory ordering, fences, waits, polls, resource masks, and the future mandatory global conflict service required before executable overlap can be claimed.

The current system exposes several ordering and footprint surfaces, but those surfaces are evidence for future scheduling and conflict checks, not proof of executable ordering.

## Current Contract

- Lane6 `DmaStreamComputeMicroOp` remains a fail-closed descriptor carrier. It is not executable DSC ISA.
- Lane7 `ACCEL_*` micro-ops remain fail-closed carriers for production execution. Their serialization classes are scheduling metadata, not proof of executable accelerator overlap.
- `SerializationClass.MemoryOrdered`, `SerializationClass.FullSerial`, resource masks, normalized memory ranges, and footprint hashes are current metadata surfaces only.
- `ExternalAcceleratorConflictManager` is an optional model/runtime conflict surface. It is not installed as mandatory global CPU load/store/atomic truth.
- `MemoryUnit` executes scalar loads/stores without a mandatory global accelerator conflict hook.
- `AtomicMemoryUnit` models atomic retire effects and reservation invalidation, but it is not integrated with a global DSC/L7/DMA conflict authority.
- Current DSC runtime/helper behavior uses physical main memory and model/runtime token surfaces. It does not define executable pipeline admission, overlap, fence, wait, or poll semantics.
- Cache/prefetch/SRF/assist surfaces do not constitute a coherent DMA/cache hierarchy.
- Compiler/backend production lowering to executable DSC or executable L7 remains forbidden.

## Decision Under Review

The decision under review is whether Phase 05 should enable a future executable overlap model for lane6 DSC, lane7 accelerators, and DMA/stream activity by requiring one installable global ordering authority.

### Recommended Decision

Adopt a staged `GlobalMemoryConflictService` architecture as a mandatory prerequisite for any executable overlap claim.

The recommended position is:

- Current implementation stays in absent-service mode.
- Existing metadata and optional conflict APIs remain design evidence only.
- A future `GlobalMemoryConflictService` must become the single architectural authority for CPU loads/stores/atomics, DSC tokens, DMAController transfers, StreamEngine/SRF/assist-resident ranges, and L7 accelerator footprints before executable overlap is enabled.
- Executable mode must be feature-gated and must not silently fall back to unordered overlap.
- Ambiguous or incomplete conflict information must serialize, stall, reject, or fault according to explicit policy. It must not be treated as successful overlap.

## Accepted Direction

### Service Installation Modes

The architecture must distinguish three modes:

- `Absent`: current contract. No executable overlap claim. Current behavior and tests must remain unchanged.
- `PresentPassive`: diagnostic or conformance-only mode. The service may observe and report conflicts, but it has no architectural effects.
- `PresentEnforcing`: executable feature-gated mode. All covered active memory interactions must pass through the service before issue/admission, access, commit, retire, release, or cancellation.

### Mandatory Visibility Set

In `PresentEnforcing` mode, the service must observe:

- CPU scalar loads.
- CPU scalar stores.
- CPU atomics, including LR/SC-style reservations and reservation invalidation.
- DSC token issue/admission reservations.
- DSC runtime steps that may read or write memory.
- DSC completion, commit validation, retire publication, release, replay, squash, trap, and cancellation.
- DMAController transfers if they can overlap with CPU or DSC/L7 activity.
- StreamEngine/SRF prefetch or assist-resident ranges if they can affect visibility or ordering.
- L7 accelerator submit, active footprint, completion, commit validation, and release.
- Future cache flush/invalidate participants once Phase 09 defines a non-coherent DMA/cache protocol.

### Footprint Record

The service must operate on structured footprints, not ad hoc string or descriptor matching.

Each active footprint record must include, at minimum:

- source kind: CPU load, CPU store, CPU atomic, DSC token, DMAController, StreamEngine, SRF/assist, L7 accelerator, cache participant;
- operation kind: read, write, read-write, atomic, reservation, commit, fence, wait, poll, invalidate, flush;
- address-space kind: physical, IOMMU-translated, or unresolved;
- normalized address ranges;
- read/write classification per range;
- memory domain tag;
- owner identity: core, pod, virtual thread, process/context, or device as applicable;
- device identity for DMA/L7/IOMMU participants;
- mapping epoch or address-translation generation;
- token id or command id when applicable;
- descriptor id, footprint hash, or submission sequence;
- issue/admission age for ordering and fairness;
- cancellation and replay generation.

### Required Hook Points

The future implementation must define hook points before code can be approved:

- CPU load pre-access conflict query.
- CPU store pre-access conflict query and retired-store publication.
- Atomic reservation creation, validation, commit, and invalidation.
- DSC issue/admission reservation.
- DSC runtime step validation before memory access.
- DSC completion publication without architectural memory commit.
- DSC commit/retire validation and release.
- DSC replay, squash, trap, and context-switch cancellation.
- L7 submit reservation, active footprint validation, completion, commit, release, and cancellation.
- DMAController transfer registration and release when overlap is executable.
- Fence, wait, and poll dispatch through the same authority.
- Phase 09 cache flush/invalidate integration once non-coherent DMA/cache rules are approved.

### Conflict Policies

The service must return explicit policy decisions:

- `Accept`: no conflict under the active ordering model.
- `Stall`: delay the younger participant until the blocking footprint changes state.
- `Replay`: squash and replay younger speculative work.
- `Serialize`: drain prior participants or force full ordering for an ambiguous case.
- `Reject`: reject issue/admission because safety cannot be proven.
- `Fault`: publish an architectural violation through the precise fault path.
- `Cancel`: cancel active work on trap, squash, context switch, or explicit cancel.

The conservative MVP policy for enforcing mode is:

- serialize or stall ambiguous same-domain overlaps;
- reject unresolved translation/domain/epoch cases that cannot be proven safe;
- fault only when an architectural violation is defined by the ISA contract;
- never treat partial completion as successful architectural completion;
- never allow poll to publish memory effects early.

### Fence, Wait, And Poll Semantics

Current code does not provide executable fence, wait, or poll semantics for DSC/L7 overlap.

Future semantics must specify:

- `poll` observes token or command state only and must not cause early memory commit;
- `wait` may block until completion, fault, cancellation, or timeout according to the ISA contract;
- `fence` drains or orders prior active footprints within an explicit scope: full system, memory domain, address range, device, stream, or token group;
- `fence` does not imply cache coherence unless Phase 09 defines the required flush/invalidate protocol;
- full serialization is allowed as an MVP fallback, but it is not the final proof of safe executable overlap.

### Deadlock, Fairness, And Backpressure

Executable overlap requires explicit liveness rules:

- conflicts must have a deterministic age or priority order;
- circular waits between CPU, DSC, DMA, L7, and fences must be rejected or broken;
- blocked tokens must remain cancellable on trap, squash, context switch, or explicit cancel;
- admission must apply bounded capacity limits;
- starvation-sensitive participants must have forward-progress rules;
- the service must expose enough state for conformance tests to prove that a stall is not silent completion.

## Rejected Alternatives

### Alternative 1: Treat Existing Serialization Classes As Sufficient

Rejected.

`MemoryOrdered`, `FullSerial`, resource masks, and normalized footprints are useful evidence. They do not install a mandatory CPU load/store/atomic conflict authority and do not prove executable overlap.

### Alternative 2: Keep `ExternalAcceleratorConflictManager` As The Final Authority

Rejected as stated.

The existing conflict manager is useful model evidence, but executable overlap requires an installable global service with mandatory CPU, DSC, DMA, StreamEngine/SRF, L7, and atomic hook points.

### Alternative 3: Use Full Serialization Forever

Rejected as the final architecture.

Full serialization is acceptable as a conservative MVP fallback, but it does not satisfy the intended async token-based executable DSC/L7 architecture.

### Alternative 4: Let Poll Or Wait Commit Memory Effects

Rejected.

Polling and waiting may observe state or block for completion. They must not publish architectural memory effects outside the commit/retire boundary.

### Alternative 5: Claim Coherency From Cache/Prefetch Surfaces

Rejected.

Existing cache, prefetch, SRF, and assist surfaces do not define coherent DMA/cache hierarchy behavior. That belongs to Phase 09.

## Exact Non-Goals

- Do not implement CPU/ISE code in this ADR.
- Do not claim lane6 DSC is executable.
- Do not claim lane7 L7 accelerators are executable production ISA.
- Do not claim async DMA overlap is implemented.
- Do not claim IOMMU integration with executable DSC/L7.
- Do not claim cache/prefetch/SRF surfaces provide coherent DMA/cache behavior.
- Do not authorize compiler/backend production lowering to executable DSC or executable L7.
- Do not define partial completion as a successful architectural mode.
- Do not replace Phase 03 token lifecycle, Phase 04 precise fault/retire, Phase 06 IOMMU/backend, Phase 09 cache protocol, or Phase 11 compiler/backend contracts.

## Compatibility Impact

Absent-service mode must preserve the current contract and existing behavior.

Present-passive mode may add diagnostics and conformance observation, but it must not change architectural results.

Present-enforcing mode is a future feature-gated architecture. Enabling it without all required hooks and conformance tests would be an architectural contract violation.

## Implementation Phases Enabled Only After Approval

Approval of this ADR would only allow follow-on design and implementation work. It would not itself make DSC/L7 executable.

The enabled follow-on phases would be:

- define the `GlobalMemoryConflictService` interface and installation model;
- add passive observation mode without changing architectural behavior;
- connect CPU load/store/atomic hook points behind a feature gate;
- connect DSC token admission, step validation, commit validation, release, and cancellation;
- connect L7 submit/active/commit/release paths once L7 execution is separately approved;
- connect DMAController and StreamEngine/SRF participants only when their overlap model is approved;
- connect Phase 09 cache flush/invalidate rules after the non-coherent protocol is approved;
- add litmus and conformance tests before any executable overlap claim.

## Required Tests Before Any Executable Claim

- Absent service preserves current behavior.
- Present-passive service observes without changing architectural results.
- DSC write conflicts with younger CPU load in the same domain.
- CPU store conflicts with active DSC read in the same domain.
- CPU atomic reservation is protected from overlapping DSC/DMA/L7 writes.
- Retired CPU store invalidates relevant active reservations.
- Non-overlapping footprints proceed without false serialization.
- Same address with different memory domain is handled according to domain rules.
- Mapping epoch mismatch rejects, serializes, or faults according to policy.
- DSC completion does not imply architectural memory commit before retire.
- Poll observes completion state without publishing early memory effects.
- Wait blocks until completion, fault, cancellation, or timeout according to the contract.
- Fence drains or orders the correct scope and does not imply cache coherence.
- Replay and squash release or cancel active reservations.
- Trap and context switch cancel active executable tokens.
- L7 and DSC overlapping ranges use the same global authority once both are executable.
- Litmus tests cover load/store, store/load, atomic, fence, wait, poll, and cancellation races.

## Documentation Migration Rule

Documentation must keep current implemented behavior separate from future approved architecture:

- Current-contract documents may cite serialization classes, resource masks, normalized footprints, and optional conflict manager methods only as evidence surfaces.
- Future-design documents may describe `GlobalMemoryConflictService` only as proposed or gated until implementation and conformance tests land.
- No documentation may state that executable DSC/L7 overlap, coherent DMA/cache behavior, or compiler production lowering exists until the corresponding implementation and tests are complete.

## Code Evidence

- `HybridCPU_ISE\Core\Pipeline\MicroOps\DmaStreamComputeMicroOp.cs`
  - `SerializationClass = Arch.SerializationClass.MemoryOrdered`.
  - `ReadMemoryRanges` and `WriteMemoryRanges` are populated from normalized descriptor ranges.
  - `ResourceMask` is built from DMA, stream, load, store, and memory-domain evidence.
  - These surfaces support future ordering design, but `Execute` remains fail-closed.
- `HybridCPU_ISE\Core\Pipeline\MicroOps\SystemDeviceCommandMicroOp.cs`
  - Lane7 carriers use `MemoryOrdered`, `CsrOrdered`, and `FullSerial` classes for different accelerator commands.
  - `ACCEL_WAIT`, `ACCEL_CANCEL`, and `ACCEL_FENCE` use full-serial scheduling metadata.
  - Direct execution remains fail-closed and does not implement production accelerator execution.
- `HybridCPU_ISE\Core\Execution\ExternalAccelerators\Conflicts\ExternalAcceleratorConflictManager.cs`
  - Provides optional model methods such as `TryReserveOnSubmit`, `NotifyCpuLoad`, `NotifyCpuStore`, `NotifyDmaStreamComputeAdmission`, `ValidateBeforeCommit`, and `ReleaseTokenFootprint`.
  - It is useful design evidence, but it is not currently mandatory global CPU memory truth.
- `HybridCPU_ISE\Core\Memory\MemoryUnit.cs`
  - Scalar load/store execution exists without mandatory global accelerator conflict-service integration.
- `HybridCPU_ISE\Core\Memory\AtomicMemoryUnit.cs`
  - Atomic retire-effect and reservation invalidation surfaces exist, including physical write notification.
  - They are not yet integrated with a global DSC/L7/DMA overlap authority.
- `HybridCPU_ISE\Core\Execution\DmaStreamCompute\DmaStreamComputeRuntime.cs`
  - Runtime/helper execution remains model-only and must not be treated as executable ISA overlap.
- `HybridCPU_ISE\Core\Execution\DmaStreamCompute\DmaStreamComputeToken.cs`
  - Token commit surfaces exist for the model/runtime path, but current tokens are not pipeline issue allocations.

## Strict Prohibitions

This ADR must not be used to claim:

- lane6 DSC is already executable;
- lane7 L7 accelerators are already executable production ISA;
- async DMA overlap is implemented;
- IOMMU is already integrated with executable DSC or executable L7;
- cache/prefetch/SRF surfaces are a coherent DMA/cache hierarchy;
- compiler/backend may production-lower to executable DSC or executable L7;
- partial completion is a successful architectural mode;
- direct runtime/helper execution is sufficient for pipeline memory ordering.
