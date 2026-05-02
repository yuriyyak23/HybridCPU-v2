# ADR 09: Cache Prefetch And NonCoherent Protocol Gate

## Status

Accepted for Phase09 foundation.

This ADR approves only foundation/guard CPU/ISE code changes for explicit non-coherent flush/invalidate surfaces. It does not approve executable DSC/L7/DSC2, runtime issue, automatic coherency, or compiler production lowering assumptions. Coherent DMA remains future-gated and requires a separate ADR.

## Context

Phase 09 covers TASK-010: treat existing cache/prefetch surfaces truthfully and define the future non-coherent protocol required before executable DSC, DMA, StreamEngine, or L7 memory visibility claims.

The repository has L1/L2 data and VLIW cache-like surfaces, assist-resident prefetch state, and domain flush helpers. These do not prove a coherent CPU/DMA/accelerator hierarchy.

## Current Contract

- Cache/prefetch materialization surfaces exist.
- `CPU_Core.Cache.cs` exposes L1/L2 data cache-like arrays and L1/L2 VLIW bundle cache-like arrays.
- `PrefetchVLIWBundle(...)` and `InvalidateVliwFetchState(...)` exist for VLIW fetch-side state.
- `FlushDomainFromDataCache(...)` invalidates data cache lines by domain.
- `MaterializeCacheDataLine(...)` materializes data from main memory into cache-like state.
- `CPU_Core.Cache.Assist.cs` exposes assist-resident data prefetch lines and budgets.
- `SyncSMPChache()` is present as a stub and does not prove coherency.
- Current DSC helper/token commit writes physical main memory through AllOrNone only; data-cache invalidation is available only when an explicit `MemoryCoherencyObserver` is supplied to the model/helper surface.
- `InvalidateDataCacheRange(...)` and `FlushDataCacheRange(...)` exist as explicit data-cache operations.
- Current data-cache flush is a no-op proof for read-materialized/non-dirty lines and fails closed if dirty lines appear before writeback exists.
- `MemoryRangeOverlap` supplies deterministic non-wrapping range overlap checks for cache and SRF/prefetch invalidation.
- `MemoryCoherencyObserver` is an opt-in non-coherent fan-out for registered data caches and SRF windows; it is not installed as global snooping or coherent cache hierarchy.
- Current exact execution paths may bypass cache materialization.
- Cache/prefetch/SRF/assist surfaces are not coherent DMA/cache hierarchy.
- Compiler/backend production lowering must not assume coherent DMA/cache, automatic snooping, or automatic flush/invalidate behavior.

## Decision

Adopt a future explicit non-coherent protocol as the first approved cache visibility model.

The required future direction is:

- flush/writeback before DMA/DSC/L7 reads if CPU dirty cache state can exist;
- invalidate data-cache lines after DMA/DSC/L7 writes;
- invalidate assist-resident and SRF/prefetch windows on overlapping writes;
- keep VLIW fetch invalidation separate from data-cache invalidation;
- route DMA, DSC, StreamEngine, L7, and future CPU/atomic cache-publication events through a memory/coherency observer only when the producing surface explicitly supplies that observer;
- keep CPU/atomic physical write notification policy explicit: current physical writes invalidate LR/SC reservations, not data-cache lines, unless a separate observer route is added later;
- reject coherent DMA/snooping/writeback claims until a separate coherent-DMA ADR exists.

## Accepted Direction

### Non-Coherent Protocol

Future executable memory producers and consumers must use explicit operations:

- `FlushDataCacheRange(address, length, domainTag)` before external reads when dirty CPU data may exist.
- `InvalidateDataCacheRange(address, length, domainTag)` after external writes.
- `InvalidateAssistPrefetchRange(address, length, domainTag)` for assist-resident and SRF-like prefetch surfaces.
- `InvalidateVliwFetchState(address)` or a range equivalent for code/bundle writes.
- `MemoryCoherencyObserver.NotifyWrite(...)` or equivalent fan-out for CPU stores, atomics, DMA writes, DSC commits, L7 commits, and domain revocation.

Flush may be documented as a no-op only if implementation proves the data cache is read-materialized/non-dirty and no dirty CPU store data can reside there.

### Separation Of Data And VLIW Fetch State

Data cache invalidation and VLIW fetch invalidation are separate architectural obligations.

A DMA/DSC/L7 data write does not automatically imply VLIW fetch invalidation unless the write overlaps executable code/bundle memory under an approved code-modification protocol.

### Assist And Prefetch State

Assist-resident and SRF/prefetch state must be invalidated or proven non-authoritative for any overlapping write.

Prefetch state must never be treated as coherent visibility or as a substitute for data-cache invalidation.

### Coherent DMA Gate

True coherency requires a separate ADR defining:

- snooping or ownership protocol;
- dirty-line writeback;
- invalidation acknowledgement;
- memory-ordering interaction;
- CPU store buffer interaction;
- atomics/reservations;
- IOMMU/domain interaction;
- compiler-visible barriers;
- conformance tests.

Until then, coherent DMA is rejected as a current claim.

## Rejected Alternatives

### Alternative 1: Claim Coherency From Existing Cache Surfaces

Rejected. L1/L2 data arrays, VLIW bundle caches, assist prefetch, and domain flush helpers do not define a coherent hierarchy.

### Alternative 2: Use VLIW Invalidation As Data Invalidation

Rejected. Fetch-side and data-side visibility are different contracts.

### Alternative 3: Ignore Flush Because Writes Are Physical

Rejected. Physical writes do not invalidate existing cache/prefetch materialization by themselves.

### Alternative 4: Treat Prefetch As Authoritative Memory

Rejected. Prefetch and assist-resident state are replay/discardable or cache-like evidence, not committed memory authority.

## Exact Non-Goals

- Do not implement CPU/ISE code in this ADR.
- Do not claim coherent DMA/cache.
- Do not define true snooping protocol.
- Do not approve executable DSC/L7.
- Do not make cache materialization authoritative for all memory execution.
- Do not merge VLIW fetch invalidation with data-cache invalidation.
- Do not authorize compiler/backend assumptions about automatic coherence.

## Required Prerequisites Before Visibility Claims

- Range overlap helper semantics.
- Data-cache range invalidate semantics.
- Data-cache range flush/writeback semantics or proof that flush is a no-op.
- Memory/coherency observer fan-out.
- CPU store and atomic write notification policy.
- DSC token commit notification policy.
- L7 commit notification policy.
- DMAController/StreamEngine write notification policy.
- Assist/SRF/prefetch invalidation policy.
- Separate VLIW/code invalidation policy.
- Phase 05 conflict service integration.
- Phase 11 compiler/backend barrier contract.
- Phase 12 conformance tests.

## Required Tests Before Any Cache Visibility Claim

- Overlapping DMA/DSC/L7 write invalidates L1/L2 data lines.
- Non-overlapping cache lines survive.
- Domain flush preserves domain isolation behavior.
- Assist-resident overlapping lines are invalidated.
- SRF/prefetch windows overlapping writes are invalidated or proven non-authoritative.
- VLIW fetch invalidation is separate from data-only writes.
- Code/bundle write invalidates VLIW fetch state.
- Flush before external read is called or proven no-op by read-materialized cache tests.
- Atomic/CPU physical writes have explicit reservation-invalidation policy and do not silently claim data-cache coherency.
- No coherent-DMA claim exists without coherent-DMA ADR tests.

## Documentation Migration Rule

Documentation must say:

- cache/prefetch surfaces exist;
- current cache/prefetch surfaces are not coherent DMA/cache hierarchy;
- the first approved target is explicit non-coherent flush/invalidate;
- coherent DMA is future architecture only.

Documentation must not move cache visibility or coherency claims into Current Implemented Contract until implementation and tests exist.

## Code Evidence

- `HybridCPU_ISE\Core\Cache\CPU_Core.Cache.cs`
  - Defines L1/L2 data cache-like arrays.
  - Defines L1/L2 VLIW bundle cache-like arrays.
  - Provides `PrefetchVLIWBundle(...)`.
  - Provides `InvalidateVliwFetchState(...)`.
  - Provides `FlushDomainFromDataCache(...)`.
  - Provides `InvalidateDataCacheRange(...)` and `FlushDataCacheRange(...)`.
  - Provides `MaterializeCacheDataLine(...)`.
  - Contains `SyncSMPChache()` as a stub with no coherency semantics.
- `HybridCPU_ISE\Core\Cache\CPU_Core.Cache.Assist.cs`
  - Provides assist-resident data prefetch state and budgets.
- `HybridCPU_ISE\Core\Memory\MemoryCoherencyObserver.cs`
  - Provides explicit non-coherent observer fan-out for registered data-cache and SRF windows.
- `HybridCPU_ISE\Core\Memory\MemoryRangeOverlap.cs`
  - Provides non-wrapping range overlap helpers for cache/prefetch surfaces.
- `HybridCPU_ISE\Core\Execution\DmaStreamCompute\DmaStreamComputeToken.cs`
  - Current commit path writes physical main memory through all-or-none helper code and can notify an explicit observer after successful commit only.
- `HybridCPU_ISE\Core\Memory\AtomicMemoryUnit.cs`
  - Atomic reservation invalidation is explicit physical-write policy, not proof of cache coherency.
- `HybridCPU_ISE\Core\Execution\ExternalAccelerators\Commit\AcceleratorCommitModel.cs`
  - L7 model commit surfaces can notify an explicit observer but remain explicit model APIs, not current coherent cache integration.

## Strict Prohibitions

This ADR must not be used to claim:

- cache hierarchy is coherent for DMA/accelerators;
- existing prefetch surfaces are authoritative memory visibility;
- exact execution paths always use cache materialization;
- `SyncSMPChache()` implements coherency;
- VLIW invalidation replaces data-cache invalidation;
- compiler/backend may assume automatic DMA/cache coherence.
