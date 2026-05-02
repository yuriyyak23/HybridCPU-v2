# VDSA Assist, Warming, Prefetch, SRF, And Data Ingress

## Scope

This file summarizes the assist-only warming and data-ingress contour around:

- `AssistKind.Vdsa`
- donor prefetch and LDSA assist neighbors
- lane6 DMA/stream assist carriers
- LSU-hosted cache prefetch carriers
- SRF warm/prefetch allocation
- assist quota and backpressure
- owner/domain/replay invalidation

VDSA assist is not `DmaStreamCompute`. It may use lane6/SRF carrier resources,
but it does not use the `DmaStreamCompute` descriptor ABI and does not grant
descriptor-backed execution or commit authority.

## Primary Code Surfaces

Assist runtime and scheduler:

- `HybridCPU_ISE/Core/Pipeline/Assist/AssistRuntime.cs`
- `HybridCPU_ISE/Core/Pipeline/Assist/AssistMicroOp.cs`
- `HybridCPU_ISE/Core/Pipeline/Core/CPU_Core.Assist.cs`
- `HybridCPU_ISE/Core/Cache/CPU_Core.Cache.Assist.cs`
- `HybridCPU_ISE/Core/Pipeline/Scheduling/MicroOpScheduler.Assist.cs`
- `HybridCPU_ISE/Core/Pipeline/Scheduling/MicroOpScheduler.AssistBackpressure.cs`
- `HybridCPU_ISE/Core/Pipeline/Scheduling/MicroOpScheduler.Assist.InterCore.cs`

SRF and StreamEngine ingress:

- `HybridCPU_ISE/Core/Execution/StreamEngine/StreamEngine.Prefetch.cs`
- `HybridCPU_ISE/Core/Execution/StreamEngine/StreamEngine.BurstIO.cs`
- `HybridCPU_ISE/Memory/Registers/StreamRegisterFile.cs`
- `HybridCPU_ISE/Memory/Registers/StreamRegisterFile.Assist.cs`
- `HybridCPU_ISE/Memory/Registers/StreamRegisterFile.Telemetry.cs`

Primary docs and tests:

- `HybridCPU_ISE/docs/assist-semantics.md`
- `HybridCPU_ISE.Tests/PhasingAndExtensions/AssistRuntimeTests*.cs`
- `HybridCPU_ISE.Tests/tests/Phase09AssistTupleSemanticsDocumentationTests.cs`
- `HybridCPU_ISE.Tests/tests/Phase09AssistVisibilityDocumentationTests.cs`
- `HybridCPU_ISE.Tests/tests/Phase09ExplicitPacketAssistLaneTests.cs`
- `HybridCPU_ISE.Tests/tests/Phase09StreamIngressWarmupTests.cs`
- `HybridCPU_ISE.Tests/tests/Phase09StreamEngineMemorySubsystemBindingSeamTests.cs`

## Assist Visibility Boundary

Assist micro-ops are:

- architecturally invisible
- non-retiring
- replay-discardable
- placement-bound
- legality-bound
- telemetry-visible
- replay-trace-visible
- allowed to have bounded cache/prefetch effects

`AssistMicroOp` makes this explicit:

- `IsAssist = true`
- `IsRetireVisible = false`
- `IsReplayDiscardable = true`
- `SuppressesArchitecturalFaults = true`
- `WritesRegister = false`
- no architectural write registers

Assists do not publish retire-visible architectural state and do not create an
architectural exception winner. Rejects, invalidations, cache residency, SRF
warming, replay signatures, and counters can still be observed.

## Assist Tuple Matrix

The active tuple axes are:

- `AssistKind`
- `AssistExecutionMode`
- `AssistCarrierKind`
- `AssistDonorSourceKind`

Legal `(AssistKind, AssistExecutionMode, AssistCarrierKind)` tuples:

| AssistKind | AssistExecutionMode | AssistCarrierKind |
| --- | --- | --- |
| `DonorPrefetch` | `CachePrefetch` | `LsuHosted` |
| `DonorPrefetch` | `StreamRegisterPrefetch` | `Lane6Dma` |
| `Ldsa` | `CachePrefetch` | `LsuHosted` |
| `Ldsa` | `StreamRegisterPrefetch` | `Lane6Dma` |
| `Vdsa` | `StreamRegisterPrefetch` | `Lane6Dma` |

All other carrier tuples are illegal.

VDSA legal donor-source families:

- `SameThreadSeed`
- `IntraCoreVtDonorVector`
- `InterCoreSameVtVector`
- `InterCoreVtDonorVector`
- `InterCoreSameVtVectorWriteback`
- `InterCoreVtDonorVectorWriteback`

The donor-source identity must match donor/target VT relation and explicit core
source requirements. Constructor validation fails closed when a tuple is not
legal.

## Carrier Semantics

`AssistCarrierKind.LsuHosted` resolves to `SlotClass.LsuClass` and uses cache
prefetch.

`AssistCarrierKind.Lane6Dma` resolves to `SlotClass.DmaStreamClass` and uses
stream-register prefetch.

Carrier choice is explicit. Donor provenance does not implicitly pick a carrier,
and a carrier does not rewrite donor provenance.

Lane6 assist carriers must stay lane6/DMA typed-slot materialized. In the legacy
slot-agnostic path, lane6/DMA assist carriers are rejected instead of silently
degrading into an arbitrary slot.

## Assist Seed Classification

Assist seed creation rejects:

- null seeds
- assist seeds
- non-memory, non-vector seeds
- vector seeds with no memory ranges unless inter-core vector writeback is
  explicitly allowed

Vector seeds classify as VDSA:

- `AssistKind.Vdsa`
- `AssistExecutionMode.StreamRegisterPrefetch`
- `AssistCarrierKind.Lane6Dma`
- element size derived from vector datatype and clamped into a supported range
- element count derived from stream length and capped
- prefetch length at least element size times element count

Load/store/load-store seeds classify into donor prefetch or LDSA depending on
locality and inter-core widening rules:

- ordinary same-thread scalar load/store assist can be LSU cache prefetch
- inter-core scalar load/store assist can widen to lane6 stream-register prefetch
- cold store inter-core assist can become `Ldsa` on lane6
- hot/default store and hot load inter-core assist can become lane6
  `DonorPrefetch`

This is assist-owned data ingress only. It is not vector execution and not
`DmaStreamCompute` execution.

## Owner, Domain, And Replay Epoch

`AssistOwnerBinding` carries:

- carrier VT
- donor VT
- target VT
- owner context id
- domain tag
- replay epoch id
- assist epoch id
- locality hint
- donor source descriptor
- optional carrier core id
- optional target core id
- pod id

Execution validation checks:

- assist epoch matches current assist runtime epoch
- replay epoch matches current replay phase
- carrier VT can issue in foreground
- explicit core/pod ownership still matches local core and pod
- donor source remains legal for the assist tuple
- inter-core donor snapshot still matches donor VT, owner context, domain tag,
  and donor assist epoch
- donor and target VTs can issue in foreground
- domain tag is covered by the active memory domain certificate when required

Invalidation reasons include:

- replay
- trap
- fence
- VM transition
- serializing boundary
- owner invalidation
- manual invalidation
- pipeline flush
- inter-core owner drift
- inter-core boundary drift

Invalidated assists are killed and nomination state is cleared. They do not
retire architecturally.

## Scheduler Admission

Assist nomination is not admission. Admission occurs only after the scheduler
accepts the candidate against current bundle state.

The intra-core typed-slot admission order is:

1. foreground bundle packing already mutated bundle certificate and metadata
2. assist class admission
3. lane materialization
4. assist backpressure reservation
5. assist memory quota reservation
6. bundle certificate, metadata, boundary guard, and telemetry mutation

`AssistBundleQuota = 1`, so at most one assist is injected into a bundle.

Inter-core assist uses explicit `AssistInterCoreTransport` and validates donor
core/pod/snapshot/domain before constructing a local assist candidate.

## Quota And Backpressure

Default assist memory quota:

- issue credits: 1
- line credits: 4
- hot line cap: 2
- cold line cap: 4

Quota rejects:

- `AssistQuotaRejectKind.IssueCredits`
- `AssistQuotaRejectKind.LineCredits`

Default assist backpressure policy:

- shared outer-cap credits: 1
- LSU carrier credits: 1
- DMA carrier credits: 1
- DMA SRF partition policy: resident 2, loading 1

Backpressure rejects:

- `AssistBackpressureRejectKind.SharedOuterCap`
- `AssistBackpressureRejectKind.OutstandingMemory`
- `AssistBackpressureRejectKind.DmaStreamRegisterFile`

Backpressure reservation samples:

- remaining hardware memory issue budget
- remaining hardware load issue budget
- consumed read budget by bank
- per-bank read budget masks
- projected outstanding memory count by VT
- projected outstanding memory capacity by VT

Lane6 DMA assist additionally checks whether the SRF can allocate an assist
register under the assist SRF partition policy. If no `MemorySubsystem` is
passed, the scheduler treats SRF allocation as available and does not fall back
to global `Processor.Memory`.

Quota and backpressure rejects are admission failures. They are not
architectural faults and do not create retire-visible winners.

## Lane6 SRF Warming Path

The lane6 hosted assist execution path is:

```text
AssistMicroOp.Execute
-> CPU_Core.ExecuteAssistMicroOp
-> TryValidateAssistMicroOp
-> ExecuteLane6HostedAssistMicroOp
-> StreamEngine.ScheduleLane6AssistPrefetch
-> StreamEngine.TryPrefetchToAssistStreamRegister
-> StreamRegisterFile.TryAllocateAssistRegister
-> IOMMU.TryWarmTranslationForAssistRange
-> BurstIO.TryReadThroughActiveBackend
-> StreamRegisterFile.LoadRegister
```

The element budget is bounded by:

- requested assist element count
- reserved prefetch-line budget
- assist cache line size
- element size
- `StreamEngine.ResolveSrfResidentChunkBudget(...)`

The SRF warm succeeds only when:

- request has non-zero element size and count
- requested chunk fits SRF resident chunk budget
- assist-owned register allocation or reuse succeeds
- read translation warming succeeds
- active backend read succeeds

Translation/backend failure invalidates the loading register and records reject
telemetry. Oversize chunks can reject before a warm attempt is published.

The lane6 hosted assist path is assist-only. IOMMU translation warming and
backend reads in this path do not prove executable DSC/L7 IOMMU integration,
async overlap, coherent DMA/cache, or production compiler/backend lowering.

## LSU Cache Prefetch Path

The LSU hosted assist execution path is:

```text
AssistMicroOp.Execute
-> CPU_Core.ExecuteAssistMicroOp
-> TryValidateAssistMicroOp
-> ExecuteLsuHostedAssistMicroOp
-> AssistPrefetchDataWindow
-> TryPrefetchAssistDataLine
```

Cache prefetch uses 32-byte line alignment and caps line count by assist memory
quota/locality. It allocates assist-resident L1 data cache lines under
`AssistCachePartitionPolicy`.

Default cache partition:

- total assist line budget: 8
- LSU-hosted line budget: 6
- DMA-hosted line budget: 2

If a line is already present in L1 with compatible domain, prefetch succeeds as
a reuse. Otherwise, assist cache allocation can reject on:

- carrier line budget
- total line budget
- no assist victim

Assist cache victims are assist-resident lines. Foreground cache ownership is
not silently overwritten by the assist policy.

## SRF/Data-Ingress Semantics

SRF data ingress is a pre-execution warm/bypass optimization with explicit
visibility boundaries:

- warmed data can later be consumed by StreamEngine `BurstRead` when source,
  element size, and byte coverage match exactly
- `BurstRead2D` and gather paths can consume warmed contiguous windows
- write paths invalidate overlapping SRF entries
- assist-owned entries cannot evict foreground-owned entries
- loading and resident budgets bound assist occupancy
- SRF telemetry records what happened but does not authorize execution

VDSA assist warms data for future vector/stream use. It does not:

- execute VectorALU operations
- publish architectural registers
- publish retire records
- commit memory writes
- bypass owner/domain/replay validation
- substitute for `DmaStreamCompute` descriptor acceptance

## Telemetry And Evidence

Scheduler counters include:

- assist nominations
- assist injections
- assist rejects
- assist boundary rejects
- assist invalidations
- inter-core nominations/injections/rejects
- domain rejects
- pod-local and cross-pod injection/reject splits
- VDSA, LDSA, and donor-prefetch injection counts
- same-VT and donor-VT injection counts
- assist quota rejects and reserved lines
- assist backpressure rejects
- DMA SRF rejects

SRF telemetry includes:

- assist warm attempts
- assist warm successes
- assist warm reuse hits
- assist bypass hits
- translation rejects
- backend rejects
- resident/loading/no-victim rejects

These counters are evidence only. They cannot grant authority.

## Rejected Patterns

Invalid patterns:

- treating VDSA assist as architectural vector execution
- treating assist as a retiring operation
- using assist telemetry as owner/domain authority
- using lane6 assist as `DmaStreamCompute` descriptor acceptance
- using SRF warm success as replay or commit authority
- allowing lane6 assist to run on non-lane6 slots
- letting lane6 assist degrade into slot-agnostic legacy scheduling
- evicting foreground-owned SRF entries for assist-owned warm traffic
- reporting warm success after translation/backend failure
- reporting no-op success for oversize or zero-size SRF ingress

## Operational Summary

VDSA assist is a lane6 stream-register prefetch mechanism for vector-derived
assist seeds. It is useful for warming data ingress into SRF under explicit
owner, domain, replay, quota, backpressure, and SRF partition constraints. It is
architecturally invisible and non-retiring. Its effects are bounded to
cache/SRF warming plus replay and telemetry evidence, and it must remain separate
from both VectorALU execution and `DmaStreamCompute` descriptor-backed compute.

Under Ex1 Phase13, assist/SRF/cache/prefetch observations are downstream
evidence only. They cannot close executable DSC, executable L7, DSC2 execution,
IOMMU-backed execution, coherent DMA/cache, async overlap, or production
compiler/backend lowering gates.
