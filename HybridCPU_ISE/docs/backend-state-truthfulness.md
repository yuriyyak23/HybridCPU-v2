# Backend State Truthfulness

## Scope

This document names the backend truthfulness boundary that Phase 05 relies on.

It does not introduce a new memory model, exception model, or retire algorithm. It keeps the repository-facing
story aligned with the live backend substrate that still owns rename, commit, and physical-register lifetime.

The short rule is:

Legality, certificates, typed-slot scheduling, stable retire order, and bounded exception ordering constrain
publication. This evidence does not replace the rename/commit/free-list substrate. It does not remove the backend substrate.

## Live Backend Substrate

The current implementation still has explicit backend state machinery.

- `PhysicalRegisterFile` stores physical register values. Physical register zero is hardwired to zero.
- `RenameMap` maps architectural registers to physical registers for each virtual thread.
- `CommitMap` records the committed architectural mapping and can restore that mapping into a `RenameMap`.
- `FreeList` owns allocation and release of physical registers.
- `RetireCoordinator` is the retire-side architectural publication surface for typed retire records.

These are active runtime surfaces, not historical vocabulary. Repository-facing prose must therefore describe
memory visibility, exception ordering, rollback, and retire effects as constraints around this substrate, not
as proof that the substrate disappeared.

## Legality And Certificates

Legality and certificate checks decide whether a micro-op, packet, or typed slot is admissible for the current
execution contract. They are scheduling and admission evidence.

They are not rename state.

They do not allocate physical registers, free physical registers, restore a committed map, or publish a retire
record. A legal certified packet can still depend on `RenameMap`, `CommitMap`, `FreeList`, `PhysicalRegisterFile`,
and `RetireCoordinator` to make architectural state visible at the correct boundary.

This is why repository-facing text should say that legality and certificate success constrain backend effects.
It must not say or imply that they replace backend ownership machinery.

## Retire Publication

Retire ordering defines which already materialized retire-side effects may become architecturally visible and
in what order.

Architectural publication remains separate from scheduler legality:

- resolved register results are carried to retirement as typed retire records;
- memory effects are applied at retire-time visibility, not merely because execution produced an address or
  value;
- `RetireCoordinator` is the publication point for retire records;
- the backend substrate still owns register identity and lifetime while publication happens.

This keeps resolved retire effects, architectural register writeback, and memory visibility as distinct
surfaces. A prose surface that collapses them into one unqualified "precise commit" claim is too strong for
the current evidence.

## Memory Visibility

The Phase 05 memory model says memory mutation becomes architecturally committed at retire-time apply.

That statement does not remove the backend substrate. It says a scalar store or resolved atomic retire effect
does not become architecturally visible merely because execute-time address calculation or reservation checking
ran. The retire path still decides publication, and the register backend still tracks the physical identities
that surround the retiring operation.

Atomic retire handling follows the same shape: reservation checks and atomic resolution are preconditions for
the retiring effect, while memory visibility is the retire-time application of the resolved effect.

## Exception Ordering

The Phase 05 exception model is a bounded stage-aware retire/exception model.

It names eligibility, retire authority, stable order, and fault precedence across the currently materialized
live subset. That evidence is compatible with `PhysicalRegisterFile`, `RenameMap`, `CommitMap`, and `FreeList`.
It does not prove that every hidden backend contour is gone, and it does not prove that every future exception
source has already been precisely modeled.

The safe claim is precise ordering within the current helper and proof-test envelope. Anything broader must
be backed by new code and tests before it appears in repository-facing prose.

## Rollback

Rollback is bounded.

`ReplayToken` can restore explicitly captured architectural register values and fully bound exact main-memory
byte ranges. It fails closed for unbound, partial, or out-of-range memory capture surfaces.

That rollback boundary does not reset the whole backend substrate. It does not claim universal recovery for
rename maps, commit maps, free-list contents, cache carrier state, external device state, or hidden pipeline
queues unless those surfaces are explicitly captured and proven by code.

## Assist Operations

Non-retiring assist operations can be architecturally retire-invisible without being globally unobservable.

The assist boundary is therefore also a backend truthfulness boundary:

- a non-retire-visible assist does not publish ordinary architectural retire state;
- it can still participate in bounded cache, replay, and telemetry behavior;
- its invisibility claim does not remove the ordinary retire and backend machinery used by visible work.

## Repository-Facing Non-Claims

The repository must not claim that Phase 05 has removed or bypassed the live backend substrate.

- It does not claim a complete precise-exception theorem.
- It does not claim universal rollback.
- It does not claim a global memory-order theorem.
- It does not claim that legality, certificates, or typed-slot scheduling replace rename, commit, or free-list
  machinery.
- It does not treat speculative, replay-side, or non-retiring assist effects as architecturally committed
  retire effects.

If future implementation work changes rename ownership, commit-map restore behavior, free-list lifetime, or
retire publication, this document and the proof tests must change with that code.
