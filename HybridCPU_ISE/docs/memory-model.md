# Memory Model

## Scope

This chapter externalizes the current repository-facing memory story for the HybridCPU ISE retire path.
It explains when a memory effect becomes architecturally committed, how atomic retire effects are resolved
and applied, and how the current backend separates memory visibility from architectural register writeback.

This document is intentionally narrower than a full ISA-level memory-consistency spec. It explains the
retire-local visibility model that is implemented and tested today. It does not prove a global memory-order
theorem across every execution surface, every inter-core interaction, or every speculative contour.

## Current retire-local visibility rule

A memory effect becomes architecturally committed only when the retire path applies the authoritative
retire-side effect against the bound main-memory surface.

- For scalar stores, the execute and memory stages may carry deferred store data, but the physical write is
  performed by the retire/writeback path.
- For atomics, execution resolves an `AtomicRetireEffect`, but memory is not mutated until
  `ApplyRetireEffect(...)` runs during retirement.
- Speculative, replay, or pre-retire carriers are not treated as architecturally committed memory visibility.

This means the repository-facing memory story is "retire-time apply", not "execute-time mutation".

## Atomic retire semantics

The atomic path is intentionally split into two repository-visible steps.

### 1. Resolve retire intent

`ResolveRetireEffect(...)` packages validated atomic intent into an `AtomicRetireEffect`.
That effect carries the opcode, access size, address, source value, destination register, core id,
and virtual-thread id.

At this point:

- address validation has happened against the bound `MainMemoryAtomicMemoryUnit`;
- the effect is ready to travel through the retire path;
- memory has not been mutated;
- the architectural destination register has not been updated.

This boundary is also visible through tests such as
`AtomicMicroOp_Execute_ResolvesRetireEffectWithoutMutatingMemory`.

### 2. Apply the retire effect

`ApplyRetireEffect(...)` is the authoritative retire-time mutation point.
It runs against the bound `MainMemoryAtomicMemoryUnit` and returns an `AtomicRetireOutcome`.

The outcome separates three different facts:

- whether memory was mutated;
- whether an architectural register writeback exists;
- which value must be written back if the instruction publishes a destination register.

This separation matters because memory visibility and register writeback are related but not identical
surfaces in the current backend.

### LR / SC behavior

`LR` and `SC` are resolved at retire time, not earlier.

- `LR` registers a reservation when the retire effect is applied.
  It does not mutate memory and returns the previously read memory value for architectural register writeback.
- `SC` consumes the reservation when the retire effect is applied.
  If the reservation does not match, memory remains unchanged and the architectural destination register
  receives status `1`.
- If the `SC` reservation matches, memory is written and the destination register receives status `0`.

An overlapping physical memory write invalidates existing reservations through the reservation registry,
so `SC` fail/succeed semantics are also retire-time effects.

### AMO arithmetic / logical behavior

For `AMO*` operations other than `LR` and `SC`:

- the old value is read from the bound memory surface when the retire effect is applied;
- the new value is computed from the old value and the source operand;
- memory is mutated at that retire-time apply point;
- the previous value is returned as the architectural destination-register payload;
- word operations sign-extend the previous 32-bit value before register writeback.

## Relationship between memory visibility and register writeback

The retire batch keeps atomic effects separate from ordinary retire records.

- `RetireWindowBatch` captures the `AtomicRetireEffect` as a typed retire-side effect.
- `CPU_Core.ApplyRetireBatchImmediateEffects(...)` first retires ordinary `RetireRecord` publications.
- The same method then calls `ApplyRetiredAtomicEffect(...)`, which delegates to `ApplyRetireEffect(...)`
  on the bound atomic memory unit.
- If the returned `AtomicRetireOutcome` contains architectural register writeback, the destination register
  is published through `RetireCoordinator` as a follow-on `RetireRecord.RegisterWrite(...)`.

So the current repository-facing order for atomics is:

1. ordinary retire records for the retire window;
2. atomic reservation check and memory apply;
3. atomic destination-register writeback through `RetireCoordinator`.

This is why the repository must not collapse resolved retire intent, memory visibility, and register
writeback into one opaque "atomic commit" blob.

## Contrast with non-atomic memory operations

Scalar loads and stores already follow the same high-level discipline:

- a load may resolve a value before retirement, but the architectural destination register is published
  only when the retire path commits it;
- a store may carry address/data earlier, but `ApplyRetiredScalarStoreCommit(...)` is the physical
  memory-mutation point that makes the store architecturally visible.

Atomics add reservation semantics and a distinct `AtomicRetireOutcome`, but they still follow the same
retire-time visibility boundary.

## Evidence anchors

The current model is grounded in live code and proof-style tests, not in prose alone.

- `Phase04DirectCompatRetireTransactionTests.AtomicTransaction_CarriesTypedAtomicEffect`
  proves that direct publication captures a typed atomic retire effect instead of mutating memory directly.
- `Phase09RetireContractClosureTests.AtomicMicroOp_Execute_ResolvesRetireEffectWithoutMutatingMemory`
  proves that execute-time atomic resolution is not a memory mutation.
- `Phase09RetireContractClosureTests.AtomicMicroOp_RetiresThroughExplicitPacketLaneOnRealCore`
  proves that the explicit-packet retire path performs the architectural register writeback and memory update.
- `Phase09RetireContractClosureTests.DirectCompatAtomicLrScTransactions_SucceedWithoutInterveningWrite`
  and
  `Phase09RetireContractClosureTests.DirectCompatAtomicScTransaction_FailsAfterOverlappingPhysicalWriteInvalidatesReservation`
  prove the retire-time reservation discipline.
- `Phase09AtomicMemoryDefaultBindingSeamTests` and
  `PhaseAuditMainMemoryAtomicMemoryUnitBindingTests`
  prove that retire-time atomic apply uses the explicitly bound main-memory surface rather than a mutable
  ambient fallback.

## Boundaries and non-claims

This chapter deliberately stops at the boundary supported by the current code and tests.

- It does not prove a global memory-order theorem.
- It does not treat speculative or replay-side carriers as architecturally committed memory effects.
- It does not claim that the atomic path removes the rename/commit substrate; architectural register
  publication still flows through `RetireCoordinator` and the live backend state.
- It does not upgrade the current exception evidence into a complete precise-exception theorem.

The safe repository-facing claim today is narrower: atomic memory visibility is a retire-time apply event,
and atomic register publication is a distinct retire-time writeback derived from `AtomicRetireOutcome`.
