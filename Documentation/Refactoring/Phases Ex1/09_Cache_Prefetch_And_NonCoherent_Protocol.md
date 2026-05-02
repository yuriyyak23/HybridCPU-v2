# Phase 09 - Cache Prefetch And NonCoherent Protocol

Status:
Foundation implemented. Non-coherent explicit protocol first. Coherent DMA is Future gated / Rejected until ADR.

Scope:
Cover TASK-010: treat existing cache/prefetch surfaces truthfully and define the future non-coherent flush/invalidate protocol required before executable DMA/accelerator memory visibility claims.

Current code evidence:
- `CPU_Core.Cache.cs` contains L1/L2 data cache-like arrays and L1/L2 VLIW bundle cache-like arrays.
- `PrefetchVLIWBundle(...)` and `InvalidateVliwFetchState(...)` exist for VLIW fetch state.
- `FlushDomainFromDataCache(...)` invalidates data cache lines by domain.
- `MaterializeCacheDataLine(...)` exists, but exact execution paths may bypass cache materialization.
- `CPU_Core.Cache.Assist.cs` exposes assist-resident prefetch lines and budgets.
- `SyncSMPChache()` is present as a stub and does not prove coherency.
- `MemoryRangeOverlap` provides deterministic non-wrapping overlap checks for modeled cache/SRF windows.
- `InvalidateDataCacheRange(...)` invalidates overlapping L1/L2 data-cache lines by explicit range/domain.
- `FlushDataCacheRange(...)` is a no-op proof for current read-materialized/non-dirty data cache, and fails closed if dirty lines appear before writeback exists.
- `MemoryCoherencyObserver` is an explicit opt-in non-coherent fan-out for modeled write publication. It invalidates registered data-cache and SRF windows only when callers route notifications through it.
- DSC all-or-none commit, L7 model commit, `DMAController`, and `PhysicalMainMemoryBurstBackend` can notify the observer when an explicit observer is supplied.
- CPU/atomic physical writes explicitly invalidate LR/SC reservations through `MainMemoryAtomicMemoryUnit.NotifyPhysicalWrite(...)`; they do not automatically claim data-cache coherency.

Architecture decision:
Current contract:
- Cache/prefetch surfaces exist.
- These surfaces do not constitute a coherent CPU/DMA/accelerator hierarchy.
- Exact execution paths can bypass cache materialization.

Foundation installed:
- First approved memory-visibility protocol is non-coherent and explicit:
  - flush/writeback before DMA or accelerator reads is currently a no-op proof only while data cache remains read-materialized/non-dirty;
  - invalidate after DMA/DSC/L7 modeled writes happens only through explicit `MemoryCoherencyObserver` routing;
  - VLIW fetch invalidation remains a separate path for code/bundle writes;
  - assist/SRF/prefetch windows invalidate on overlapping observer-routed writes.
- Coherent DMA/snooping/writeback hierarchy is rejected until a separate ADR.

Non-goals:
- Do not claim coherent DMA/cache.
- Do not treat prefetch materialization as authoritative memory visibility.
- Do not use VLIW invalidation as a replacement for data-cache invalidation.
- Do not silently skip flush semantics if CPU dirty lines later become possible.

Required design gates:
- Range-based `InvalidateDataCacheRange(address, length, domainTag)` semantics.
- Range-based `FlushDataCacheRange(address, length, domainTag)` semantics.
- Decision whether current data cache can contain dirty CPU store data or is read-materialized only.
- `MemoryCoherencyObserver` or equivalent fan-out for explicitly routed DMA writes, DSC commits, L7 commits, and model/test write notifications. CPU/atomic physical writes currently publish LR/SC reservation invalidation only.
- Assist-resident and SRF prefetch invalidation policy.
- Separate VLIW/code invalidation policy.
- Future coherent-DMA ADR if true coherency is claimed.

Implementation plan:
1. Add range-overlap helpers for data cache objects.
2. Add data-cache range invalidation.
3. Add data-cache range flush/writeback. It may be a documented no-op only if the data cache is proven read-materialized/non-dirty.
4. Add memory/coherency observer and route DMA/DSC/L7 commit notifications through it.
5. Invalidate assist/SRF/prefetch windows on overlapping writes.
6. Keep `InvalidateVliwFetchState` separate for code/bundle writes.
7. Add tests before any documentation claims memory visibility discipline.

Affected files/classes/methods:
- `CPU_Core.Cache.cs`
- `CPU_Core.Cache.Assist.cs`
- `DmaStreamComputeToken.TryCommitAllOrNone`
- `StreamEngine.BurstIO`
- `AcceleratorCommitModel`
- `AtomicMemoryUnit.NotifyPhysicalWrite`
- `MemoryUnit`
- `MemoryCoherencyObserver`
- `MemoryRangeOverlap`
- `StreamRegisterFile.InvalidateOverlappingRangeAndCount(...)`

Testing requirements:
- DMA/DSC write invalidates overlapping L1/L2 data lines.
- Assist-resident lines overlapping writes are invalidated.
- Non-overlapping data lines survive.
- Domain flush still works.
- VLIW invalidation is separate from data-only DMA writes.
- Flush before DMA read is called or documented as no-op under read-materialized cache proof.
- No coherent-DMA claim exists without a coherent-DMA test suite and ADR.

Documentation updates:
Replace "cache absent" with "cache/prefetch surfaces exist." Replace "coherent cache hierarchy" with "non-coherent explicit flush/invalidate protocol until future ADR." Document exact flush/invalidate obligations for runtimes and compiler/backend.

Compiler/backend impact:
Current compiler/backend must not assume coherent DMA/cache, automatic snooping, or automatic flush of CPU store buffers/cache lines. Future lowering may rely on explicit flush/invalidate/fence only after ABI/runtime support and tests exist.

Compatibility risks:
Incorrect invalidation gives stale reads. Over-invalidating can hurt performance but is safer. Dirty-line ambiguity must be resolved before DMA reads are documented as safe.

Exit criteria:
- Non-coherent protocol specified.
- Range flush/invalidate semantics approved.
- VLIW invalidation kept separate.
- Coherent DMA remains future ADR only.
- DSC/L7/DSC2 executable gates remain closed.

Blocked by:
Executable DSC/L7 memory publication gates for integration. Coherent DMA is blocked by a separate future ADR.

Enables:
Truthful non-coherent DMA/accelerator memory visibility, safe compiler barriers after approval, and future coherent-cache design groundwork.
