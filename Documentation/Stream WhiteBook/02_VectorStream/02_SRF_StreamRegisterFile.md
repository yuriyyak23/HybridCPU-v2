# SRF - StreamRegisterFile

## Role

`StreamRegisterFile` is a bounded transient stream buffer used for ingress
warming, exact-window bypass, assist prefetch, and MTILE typed transfer
staging.

Default model:

- 8 registers;
- 32 elements maximum;
- 8 bytes maximum element size;
- 256 bytes per register;
- states `Invalid`, `Loading`, `Valid`, `Dirty`.

Each entry tracks source address, valid bytes, element geometry, recency, and
assist ownership.

## Foreground Semantics

Foreground allocation uses exact source/geometry matches and LRU replacement.
An SRF bypass is legal only for a valid packed contiguous window with sufficient
byte coverage.

Writes or committed stores invalidate overlapping windows so stale ingress
cannot be reused.

## Assist Partition

Assist allocation is separately budgeted and cannot evict foreground-owned
entries. Resident, loading, and victim availability are explicit rejection
reasons.

## MatrixTile Use

MTILE load stages memory rows through typed SRF windows. MTILE store stages
architectural tile rows before retire. The transfer record fingerprints those
windows, but SRF state does not become guest architectural evidence.

## Non-Authority

`Valid`, `Dirty`, bypass hits, warm success, telemetry, and host-visible bytes
do not authorize execution, retire, replay, or commit.
