# Memory, Atomic, Fence, And Fence-I Model

Updated: 2026-05-14.

## Scope

Этот файл описывает закрытую memory-ordering часть после Phase 08 и Phase 10.
Модель намеренно bounded: она закрывает текущую main-memory retire visibility
и current-core VLIW fetch visibility, но не заявляет cache hierarchy, DMA,
TLB, VM или global coherence.

## Phase 08 Base Memory Model

Phase 08 установил базовые retire semantics для memory:

- scalar loads honor typed signedness/zero-extension;
- scalar stores truncate by access size;
- stores publish physical memory at writeback-retire;
- execute/capture can prepare memory intent but does not publish store truth;
- rollback/replay can cancel captured memory effects.

Эта база нужна для atomics и fences: release/acquire нельзя доказывать поверх
runtime, который публикует память раньше retire.

## LR/SC Semantics

Closed LR/SC contour:

- `LR.W`, `LR.D` create reservation state according to audited runtime model;
- `SC.W`, `SC.D` consume reservation and report success/failure;
- overlapping write invalidates reservation as specified by runtime tests;
- success path publishes store effect at retire;
- failure path does not publish the store effect;
- W-result behavior follows required sign-extension.

Acquire/release bits on LR/SC are not proof by themselves. They become
architectural only because retire-window ordering tests show the expected
visibility boundaries.

## AMO W/D Semantics

Closed AMO contour:

- AMO W/D families perform read-modify-write at retire;
- old value/writeback result semantics are tied to retire records;
- W-result return values are sign-extended where required;
- no early register or memory publication occurs during execute/capture;
- acquire/release ordering is proven for acquire-only, release-only and
  acquire+release carriers.

## aq/rl Carrier Model

`aq` and `rl` bits are transported into runtime IR and MicroOp state. Это
необходимо для ordering, но не является достаточным доказательством.

Correct interpretation:

- bits are carrier facts;
- materialization may preserve them;
- retire-owned ordering decides architectural truth;
- tests prove that metadata, `InternalOp`, typed-slot facts, decoder facts and
  compiler facts do not imply ordering enforcement by themselves.

Incorrect interpretation:

- "bit set means acquire/release is implemented";
- "metadata says ordered, therefore runtime ordered";
- "compiler emitted it, therefore ISA semantics are correct".

## Release Ordering

Release ordering for published atomic carriers means prior memory effects in the
retire window are visible before the release atomic publishes. The closed model
requires the runtime either to prove this through retire ordering or fail closed.

Практически это исключает сценарий, где:

1. prior store is captured;
2. release AMO or SC publishes first;
3. later observer can see the release without the prior memory effect.

Phase 10 закрывает этот контур для AMO W/D and LR/SC carriers.

## Acquire Ordering

Acquire ordering means later memory effects must not become visible before the
acquire atomic retires. В текущей модели acquire also belongs to retire-owned
ordering, not to decoder metadata.

Практически это исключает сценарий, где:

1. acquire LR/AMO is captured;
2. later load/store effect becomes architectural before the acquire retires;
3. runtime claims acquire semantics only because `aq` bit exists.

## Acquire + Release

Acquire+release composition закрыта для опубликованных AMO W/D and LR/SC
surfaces. This means the same carrier can enforce both sides:

- prior memory effects cannot float after release publication;
- later memory effects cannot publish before acquire retire;
- the combined behavior is proven without hidden full-serial promotion.

## No Hidden Lock Or Broad Serializing Promotion

Phase 10 explicitly avoids using a hidden global lock or broad serializing
boundary as an accidental substitute for modeled semantics. Atomics should not
gain `HasSerializingBoundaryEffect` unless the runtime surface provides an
explicit fence/system event for that behavior.

This distinction matters because a broad serializing fallback can hide bugs in
typed ordering, lane behavior and replay. The closed model proves only the
required atomic ordering, not an unbounded global serialization theorem.

## FENCE Model

`FENCE` is executable only in canonical zero-payload form:

```text
Immediate     == 0
PredicateMask == 0
Flags         == 0
Word1         == 0
Word2         == 0
Word3 payload == 0, except VT transport hint bits [49:48]
```

Semantic contour:

- lane7/system singleton;
- `SystemEventOrderGuarantee.DrainMemory`;
- prior retired memory and atomic effects publish before the fence event;
- later memory effects do not publish before the fence retires;
- effect is captured first and applied only at retire;
- rollback/replay cancels non-retired fence effects.

Non-zero masks, payloads, flags or unsupported sideband fail closed.

## FENCE_I Model

`FENCE_I` uses the same canonical zero-payload acceptance rule. Its semantic
contour:

- lane7/system singleton;
- `SystemEventOrderGuarantee.FlushPipeline`;
- includes data-memory ordering in the current retire domain;
- retired instruction-memory writes become visible to subsequent VLIW fetch
  through current fetch path;
- retire path invalidates current materialized VLIW fetch state and bundle
  caches;
- capture without retire apply does not invalidate fetch state.

`FENCE_I` is a bounded current-core fetch-state invalidation model. It is not a
universal cache coherence theorem and does not open `ICACHE_INVAL`.

## Reserved Adjacent Instructions

The following remain reserved or optional-disabled until a complete model exists:

```text
SFENCE.VMA
DCACHE_CLEAN
DCACHE_INVAL
DCACHE_FLUSH
ICACHE_INVAL
```

Future enablement must define decode, materialization, memory/fetch/cache/TLB
visibility, retire publication, rollback/replay and tests before compiler
emission is considered.

