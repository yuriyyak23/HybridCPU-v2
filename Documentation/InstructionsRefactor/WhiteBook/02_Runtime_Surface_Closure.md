# Runtime Surface Closure

Updated: 2026-05-14.

## Обзор

Этот файл описывает закрытую runtime ISA-поверхность после завершенных фаз. Он
фиксирует, какие контуры считаются executable или bounded executable, а какие
сознательно оставлены неисполняемыми.

## Scalar Integer64 Repair

Mandatory scalar repair surface закрыт до `ZEXT.W = 321`.

| Instruction | Opcode |
|---|---:|
| `SRA` | 300 |
| `ADDIW` | 301 |
| `ADDW` | 302 |
| `SUBW` | 303 |
| `SLLW` | 304 |
| `SRLW` | 305 |
| `SRAW` | 306 |
| `SLLIW` | 307 |
| `SRLIW` | 308 |
| `SRAIW` | 309 |
| `MULW` | 310 |
| `DIVW` | 311 |
| `DIVUW` | 312 |
| `REMW` | 313 |
| `REMUW` | 314 |
| `SEXT.W` | 320 |
| `ZEXT.W` | 321 |

Закрытая цепочка включает canonical encoding, decoder projection, registry
factory, MicroOp materialization, scalar ALU semantics, resource/safety facts и
retire/writeback publication.

Live compiler также эмитит этот tracked scalar tail. Это подтверждает
compiler-facing ABI для данного контура, но не создает authority для других ISA
областей.

## Branch And Control

Branch/control runtime surface закрыт для:

```text
JAL, JALR, BEQ, BNE, BLT, BGE, BLTU, BGEU
```

Главная нормализация: target displacement переносится через canonical
`Immediate`. Target sideband в `Src2Pointer` rejected. PC/register effects
публикуются только через lane7 retire path.

Эта модель важна для VLIW/EPIC runtime: branch/control не должен менять PC
раньше retire или обходить lane7 ownership.

## Scalar Load And Store

Typed scalar load/store semantics закрыты:

| Instruction class | Semantics |
|---|---|
| `LB`, `LH`, `LW` | Sign-extend. |
| `LBU`, `LHU`, `LWU` | Zero-extend. |
| `LD` | Preserve doubleword. |
| `SB`, `SH`, `SW`, `SD` | Truncate by access size. |

Stores commit physical memory at writeback-retire. Execute/capture не является
моментом архитектурной публикации памяти. Эта граница затем используется Phase
10 для atomic/fence ordering.

## Atomics

Atomic runtime surface закрыт для опубликованных LR/SC и AMO W/D carriers:

- `LR.W`, `LR.D`;
- `SC.W`, `SC.D`;
- AMO W/D families.

Закрытые свойства:

- `aq/rl` carrier propagation в runtime IR/MicroOp state;
- LR/SC reservation register, consume, failure/success semantics;
- overlapping write invalidation;
- AMO read-modify-write at retire;
- W-result sign-extension where required;
- acquire-only, release-only, acquire+release ordering for published carriers;
- запрет early memory/register truth до `ApplyCapturedRetireWindowBatch`;
- запрет считать `aq/rl` bits, metadata, typed-slot facts, decoder facts или
  compiler facts доказательством ordering.

## FENCE And FENCE_I

`FENCE` и `FENCE_I` закрыты как bounded lane7/system singleton retire effects.
Executable только canonical zero-payload form:

```text
Immediate     == 0
PredicateMask == 0
Flags         == 0
Word1         == 0
Word2         == 0
Word3 payload == 0, except VT transport hint bits [49:48]
```

`FENCE` имеет `DrainMemory` order guarantee для текущей main-memory retire
visibility model.

`FENCE_I` имеет `FlushPipeline` order guarantee и bounded current-core
instruction-fetch visibility через invalidation текущего VLIW fetch state после
retired instruction-memory writes.

Не заявлены:

- cache hierarchy coherence;
- DMA/cache coherence;
- TLB/VM ordering;
- global serializing promotion;
- compiler substitution as proof.

## Vector Surface

Vector runtime surface закрыт только для scoped audited 1D contours:

- compute;
- reduction;
- FMA;
- current executable dot-product contour;
- predicate;
- movement;
- transfer.

Indexed/2D vector forms вне audited matrix fail closed. `VGATHER` и `VSCATTER`
остаются descriptor-only и non-executable.

Текущий executable `VDOT*` contour является только 1D scalar-footprint form с
destination aliasing source1. Future destination ABI variants закрыты.

## Lane6

Lane6 `DmaStreamCompute` закрыт как descriptor-required, lane6 hard-pinned,
direct-execute fail-closed contour.

Это значит:

- descriptor sideband обязателен;
- owner/domain guard должен пройти;
- lane placement должен быть lane6;
- прямое execution без полноценного backend/staged commit запрещено;
- production Lane6 execution не открыт.

## Lane7

Lane7 `ACCEL_SUBMIT` закрыт как descriptor-required, lane7 hard-pinned,
system-device carrier. Direct execution fail closed. L7-SDC
query/poll/wait/cancel/fence commands остаются carrier-only.

Production Lane7 execution требует отдельного backend, capability/token
lifecycle, staged writeback, commit semantics, replay/rollback и fence
interaction.

## Matrix, Cache, TLB, Coherence

Matrix rows:

```text
MTILE_LOAD, MTILE_STORE, MTILE_MACC, MTRANSPOSE
```

сохраняются как `OptionalDisabled / decoder-rejected`.

Cache/TLB/coherency rows:

```text
SFENCE.VMA, DCACHE_CLEAN, DCACHE_INVAL, DCACHE_FLUSH, ICACHE_INVAL
```

сохраняются как `Reserved`. Они не получают support из Phase 10 `FENCE` /
`FENCE_I`.

