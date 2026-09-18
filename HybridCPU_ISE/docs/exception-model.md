# Exception Model

## Scope

This document names the bounded stage-aware retire/exception model implemented by the current
HybridCPU ISE pipeline helpers.

The model explains how the repository currently determines:

- which write-back lanes are eligible for ordinary retirement;
- which lanes are authoritative retire carriers;
- how stable retire order is derived across explicit packet lanes;
- how fault precedence is selected across write-back, memory, and execute stages;
- where the current evidence stops.

This is not a full ISA-wide exception theorem. It is a repository-facing description of the
precise-ordering evidence that live code and proof tests currently support.

## Named Model

The current model is the bounded stage-aware retire/exception model.

It separates four surfaces that must not be collapsed into one vague precise-retire claim.

- Eligibility: a lane is considered for ordinary retirement only if the current write-back window is valid,
  the lane is occupied, the lane is part of the live retire-authoritative subset, and the lane is not faulted.
- Authority: only retire-authoritative lanes can publish architectural retire effects. In the current helper
  surface this means lanes 0..5 and lane 7, with non-retire-visible assist micro-ops excluded.
- Order: eligible lanes retire in stable order. Explicit packet lanes use slot order first and lane index only
  as a deterministic tie-breaker. The reference fallback remains a conservative single-lane drain.
- Fault precedence: faulted lanes are removed from ordinary retire eligibility and are delivered through the
  stage-aware exception winner path.

## Retire Eligibility And Authority

`ResolveRetireEligibleWriteBackLanes(...)` is the main eligibility entry point for the write-back window.
It starts from the occupied retire-authoritative lane mask and removes faulted lanes through
`ResolveWriteBackFaultMask(...)`.

The authoritative subset is deliberately narrower than "anything that appears in write-back".

- Lanes 0..5 are the live scalar/LSU retire subset.
- Lane 7 is the live singleton branch/control/system carrier.
- Lane 6 is not an ordinary retire-authoritative lane.
- A lane occupied by a micro-op whose `IsRetireVisible` property is false is not an authoritative retire lane.

`CanRetireLanePrecisely(...)` is a lane-local query over that same model. It does not grant new authority.
It answers whether a specific lane belongs to the current retire-eligible set after authority and fault
filtering have already been applied.

## Stable Retire Order

`ResolveStableRetireOrder(...)` converts the eligibility mask into a deterministic retire order.

For explicit packet lanes, slot order is the architectural ordering key. Lane index is a tie-breaker only
when the slot key is unavailable or equal. This distinction matters because lane 7 can be older than lane 4
when its packet slot says so; lane number alone is not the ordering authority.

For the reference sequential fallback, the model remains conservative: it drains one eligible scalar lane
instead of pretending that the reference path has the same multi-lane explicit packet authority.

## Fault Masks And Fault Precedence

Fault detection is stage-local before it becomes a stage-aware winner.

- `ResolveWriteBackFaultMask(...)` collects occupied faulted write-back lanes.
- `ResolveMemoryFaultMask(...)` collects occupied faulted memory-stage lanes.
- `ResolveExecuteFaultMask(...)` collects occupied faulted execute-stage lanes.

`TryResolveStageAwareExceptionWinner(...)` then chooses the authoritative winner across stages.
The current precedence is:

1. write-back stage faults;
2. memory stage faults;
3. execute stage faults.

Within a stage, the winner is the oldest ordered lane available to that stage model. Write-back uses the
same explicit-packet slot order discipline as stable retire ordering. Memory and execute lanes use their
stage-local ordered lane helpers.

`TryResolveStageAwareExceptionWinnerMetadata(...)` attaches the winning VT, owner thread, fault address,
fault direction, and PC to the selected winner. This identity information is evidence for deterministic
fault delivery, not a proof that every possible hidden backend contour has been rolled back.

## Retire Window Delivery Decision

`TryResolveExceptionDeliveryDecisionForRetireWindow(...)` is the named delivery decision for the current
retire window.

It does three things:

- builds the write-back, memory, and execute fault masks;
- selects the stage-aware exception winner;
- computes whether younger live-subset work should be suppressed.

`ShouldSuppressYoungerWorkForExceptionWinner(...)` and `ResolveExceptionWinnerSuppressedLaneMask(...)`
bound the squash surface. A write-back winner can suppress younger memory and execute work in the current
live precise subset. A memory winner can suppress younger execute work. An execute winner does not make a
claim about older-stage rollback because it is already the youngest stage in this decision.

## Assist Interaction

Non-retiring assist operations are outside ordinary architectural retire.

`AssistMicroOp` reports:

- `IsRetireVisible == false`;
- `IsReplayDiscardable == true`;
- `SuppressesArchitecturalFaults == true`.

The helper model respects this by excluding non-retire-visible assist lanes from retire authority. Assist
execution may remain cache, replay-trace, and telemetry visible, but it does not create a retire record and
does not publish architectural register or memory state through ordinary retirement.

This is a bounded invisibility claim. It means "architecturally retire-invisible", not "impossible to observe
through all implementation counters or cache-carrier effects".

## Relationship To Memory And Rollback

Fault ordering and memory visibility are related but distinct.

The memory model defines when scalar and atomic memory effects become architecturally committed. In short,
memory mutation is retire-time apply, not execute-time visibility. A faulted lane removed from ordinary retire
eligibility must not be described as having already committed its retire-side memory effect.

Rollback is also bounded. `ReplayToken` can restore explicitly captured architectural register values and
fully bound exact main-memory byte ranges. This exception model does not claim universal rollback for hidden
pipeline, rename, cache, or external-device state.

## Backend Truthfulness

The current ordering evidence coexists with the explicit backend substrate.

Architectural publication flows through `RetireCoordinator`, and the repository still has live backend
state machinery such as `PhysicalRegisterFile`, `RenameMap`, `CommitMap`, and `FreeList`.

Legality, certificate checks, stable retire order, and stage-aware exception winner selection do not remove
that substrate. They constrain which effects may be published and in which order; they do not erase rename,
commit, or physical-register ownership machinery.

## Evidence Anchors

The model above is grounded in code and tests.

- `CPU_Core.Pipeline.Helpers.cs` contains the retire eligibility, stable order, fault mask, winner, and
  delivery-decision helpers.
- `CommitUnit.cs` applies typed retire records through `RetireCoordinator`.
- `Phase09WriteBackFaultOrderingProofTests` proves lane 4/lane 7 slot-order fault precedence and suppression
  cases.
- `Lane7RetireAuthorityTests` proves lane 7 belongs to the live retire-authoritative subset and that faulted
  lane 7 is excluded from ordinary retire eligibility.
- `AssistRuntimeTests.Part4` proves non-retire-visible assist lanes are excluded from stable retire truth.
- `Phase09VmxRetireOrderingTests` proves the retire-window fault decision wins before a deferred VMX retire
  effect.
- `Phase09ExecutionFaultContractTests`, `Phase09ExplicitPacketExecuteFaultTailTests`, and
  `Phase09SingleLaneExecuteFaultTailTests` cover bounded execute fault translation and delivery behavior.

## Boundaries And Non-Claims

The safe repository-facing claim is precise ordering within the currently materialized live subset.

- It does not claim a complete precise-exception theorem.
- It does not claim universal rollback.
- It does not claim a global memory-order theorem.
- It does not treat speculative, replay-side, or non-retiring assist effects as architecturally committed
  retire effects.
- It does not claim that legality or certificate success replaces backend rename/commit/free-list state.

If future code broadens the live exception subset, changes lane authority, or adds a new retire carrier, this
document and its proof tests must change with that code.
