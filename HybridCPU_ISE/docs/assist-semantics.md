# Assist Semantics

This document is the Phase 04 reviewer-facing closure surface for assist legality. It freezes the current runtime story without inventing a new assist model beyond what live code already implements.

## Scope

An assist is a runtime micro-op that is:

- architecturally invisible
- non-retiring
- replay-discardable
- legality-bound and placement-bound like any other scheduled micro-op

The legality matrix below covers the explicit four-tuple:

- `AssistKind`
- `AssistExecutionMode`
- `AssistCarrierKind`
- `AssistDonorSourceKind`

This matrix does not replace owner or domain guards. It describes only which assist tuples the landed runtime may materialize before the later Phase 04 guard plane applies.

## Visibility Envelope

Assist is not zero-observable. The truthful boundary is:

| Surface | Architectural state visibility | Cache/prefetch visibility | Replay trace visibility | Telemetry visibility |
| --- | --- | --- | --- | --- |
| Assist execution itself | none | bounded yes: assist may warm or occupy assist-owned cache lines and prefetch carriers | yes: assist ownership signature and tuple counters can appear in replay evidence | yes: scheduler and report counters expose injections, rejects, invalidations, and ownership signature |
| Retire path | none | none | indirect only through absence of retire records and assist-specific evidence | indirect only through assist counters, not retired-instruction counters |
| Assist invalidation and reject surfaces | none | none unless a prior assist already warmed cache state | yes: invalidation reason and assist tuple divergence are replay-comparable | yes: quota, backpressure, domain, and invalidation counters remain exportable |

The repository must therefore say:

- assists are architecturally invisible
- assists are retire-invisible
- assists are microarchitecturally observable through bounded cache/prefetch effects
- assists are replay-trace-visible through ownership signature and tuple counters
- assists are telemetry-visible through scheduler and report counters

## Phase 05 Retire And Fault Ordering Interaction

The Phase 05 exception model treats assists as architecturally retire-invisible work.

`AssistMicroOp` makes that boundary explicit:

- `IsRetireVisible` is `false`;
- `IsReplayDiscardable` is `true`;
- `SuppressesArchitecturalFaults` is `true`.

The write-back helper surface uses `IsRetireVisible` as part of retire authority. As a result,
`ResolveStableRetireOrder(...)` does not include non-retiring assist lanes in stable retire truth, and
`CanRetireLanePrecisely(...)` returns false for an occupied assist lane that is not retire-visible.

This also bounds fault ordering. A non-retiring assist does not create an architectural exception winner in
`TryResolveExceptionDeliveryDecisionForRetireWindow(...)`, and it does not publish retire-visible architectural state through the ordinary retire path.

This is a bounded invisibility claim, not a zero-observability claim. Assist execution may still affect assist-owned cache/prefetch
carriers, replay evidence, invalidation counters, and telemetry. The safe repository-facing statement is
therefore narrow: assist is architecturally retire-invisible, but it remains boundedly observable through
the assist runtime surfaces named in this document.

## Admission, Quota, And Backpressure

Assist nomination is not assist admission. A nominated assist becomes part of a
bundle only after ordinary legality, typed-slot placement, assist-specific
backpressure, and assist quota all accept it.

The active single-cycle intra-core assist path is foreground-subordinate.
`PackBundleIntraCoreSmt(...)` performs foreground typed-slot packing first,
refreshes the working bundle certificate, metadata, boundary state, and class
occupancy after each successful foreground mutation, and only then calls
`TryInjectAssistCandidates(...)`. An assist is therefore evaluated only against
residual legality and residual lane capacity in the already mutated working
bundle; it is not symmetrically arbitrated with foreground work.

The current scheduler enforces `AssistBundleQuota = 1`, so at most one assist can
be injected into a bundle. This bundle quota is separate from `AssistMemoryQuota`,
which tracks assist-only issue and prefetch-line credits.

Quota rejects and backpressure rejects are admission failures. They do not make
the assist retire-visible, and they do not create an architectural exception
winner. They are visible through typed-slot reject classification, scheduler
counters, trace/evidence comparison, and performance telemetry.

| Surface | Reject taxonomy | Counter/evidence surface | Meaning |
| --- | --- | --- | --- |
| assist issue quota | `AssistQuotaRejectKind.IssueCredits` | `AssistQuotaRejects`, `AssistQuotaIssueRejects`, `TypedSlotAssistQuotaRejects`, `TypedSlotRejectReason.AssistQuotaReject` | Assist-only issue credit was exhausted before admission. |
| assist line quota | `AssistQuotaRejectKind.LineCredits` | `AssistQuotaRejects`, `AssistQuotaLineRejects`, `AssistQuotaLinesReserved`, `TypedSlotAssistQuotaRejects`, `TypedSlotRejectReason.AssistQuotaReject` | Assist prefetch line demand exceeded the assist-only line budget. |
| shared outer-cap pressure | `AssistBackpressureRejectKind.SharedOuterCap` | `AssistBackpressureRejects`, `AssistBackpressureOuterCapRejects`, `TypedSlotAssistBackpressureRejects`, `TypedSlotRejectReason.AssistBackpressureReject` | Assist could not borrow shared LSU/outer memory issue capacity. |
| outstanding memory pressure | `AssistBackpressureRejectKind.OutstandingMemory` | `AssistBackpressureRejects`, `AssistBackpressureMshrRejects`, `TypedSlotAssistBackpressureRejects`, `TypedSlotRejectReason.AssistBackpressureReject` | Assist would exceed outstanding memory/MSHR pressure for the widened owner. |
| lane-6 stream-register pressure | `AssistBackpressureRejectKind.DmaStreamRegisterFile` | `AssistBackpressureRejects`, `AssistBackpressureDmaSrfRejects`, `TypedSlotAssistBackpressureRejects`, `TypedSlotRejectReason.AssistBackpressureReject` | Lane6/DMA assist could not allocate assist-owned SRF space without evicting foreground-owned registers. |

In the typed-slot path the order is:

1. class admission
2. lane materialization
3. assist backpressure reservation
4. assist quota reservation
5. mutation of bundle certificate, metadata, boundary guard, and telemetry

This order keeps assist pacing subordinate to legality and guard evidence. It
also keeps quota and backpressure distinct from donor provenance and carrier
selection.

This ordering is specific to the active single-cycle intra-core assist path. In
the pipelined FSP contour, `PackBundleIntraCoreSmt(...)` returns after
`PipelineFspStage2_Intersect(...)` and clears assist nomination ports, so
`SCHED2` does not currently integrate a trailing intra-core assist pass.

## Carrier Tuple Matrix

Only the following `(AssistKind, AssistExecutionMode, AssistCarrierKind)` combinations are legal:

| AssistKind | AssistExecutionMode | AssistCarrierKind |
| --- | --- | --- |
| `DonorPrefetch` | `CachePrefetch` | `LsuHosted` |
| `DonorPrefetch` | `StreamRegisterPrefetch` | `Lane6Dma` |
| `Ldsa` | `CachePrefetch` | `LsuHosted` |
| `Ldsa` | `StreamRegisterPrefetch` | `Lane6Dma` |
| `Vdsa` | `StreamRegisterPrefetch` | `Lane6Dma` |

All other carrier tuples are illegal.

## Donor-Source Binding Invariants

The donor-source enum is not descriptive sugar. Its name is enforced against the bound donor and target scope:

| Donor-source family | Required donor/target relation | Explicit core source required |
| --- | --- | --- |
| `SameThreadSeed` | donor VT == target VT | no |
| `IntraCoreVtDonorVector` | donor VT != target VT | no |
| `InterCoreSameVt*` | donor VT == target VT | yes |
| `InterCoreVtDonor*` | donor VT != target VT | yes |

If manual assist construction violates these binding rules, the constructor must fail closed.

## Four-Tuple Legality Matrix

The definitive legality matrix is:

| AssistKind | AssistExecutionMode | AssistCarrierKind | Legal `AssistDonorSourceKind` values |
| --- | --- | --- | --- |
| `DonorPrefetch` | `CachePrefetch` | `LsuHosted` | `SameThreadSeed` |
| `DonorPrefetch` | `StreamRegisterPrefetch` | `Lane6Dma` | `InterCoreSameVtSeed`, `InterCoreVtDonorSeed`, `InterCoreSameVtHotLoadSeed`, `InterCoreVtDonorHotLoadSeed`, `InterCoreSameVtHotStoreSeed`, `InterCoreVtDonorHotStoreSeed`, `InterCoreSameVtDefaultStoreSeed`, `InterCoreVtDonorDefaultStoreSeed` |
| `Ldsa` | `CachePrefetch` | `LsuHosted` | `SameThreadSeed` |
| `Ldsa` | `StreamRegisterPrefetch` | `Lane6Dma` | `InterCoreSameVtSeed`, `InterCoreVtDonorSeed`, `InterCoreSameVtColdStoreSeed`, `InterCoreVtDonorColdStoreSeed` |
| `Vdsa` | `StreamRegisterPrefetch` | `Lane6Dma` | `SameThreadSeed`, `IntraCoreVtDonorVector`, `InterCoreSameVtVector`, `InterCoreVtDonorVector`, `InterCoreSameVtVectorWriteback`, `InterCoreVtDonorVectorWriteback` |

Every four-tuple not named in this table is illegal.

## Separation Boundaries

The matrix keeps three semantics surfaces separate:

- bundle densification: who receives a bundle slot this cycle
- donor provenance: where the assist seed came from
- carrier semantics: which physical execution class hosts the assist

That separation is required for audit truthfulness. A donor kind does not implicitly pick a carrier, and a carrier choice does not rewrite donor provenance.

## Authoritative Code And Proof Surfaces

Primary code authority:

- `HybridCPU_ISE/Core/Pipeline/Assist/AssistRuntime.cs`
- `HybridCPU_ISE/Core/Pipeline/Assist/AssistMicroOp.cs`
- `HybridCPU_ISE/Core/Pipeline/Scheduling/MicroOpScheduler.Assist.cs`
- `HybridCPU_ISE/Core/Pipeline/Scheduling/MicroOpScheduler.AssistBackpressure.cs`

Primary proof surfaces:

- `HybridCPU_ISE.Tests/PhasingAndExtensions/AssistRuntimeTests.Part6.cs`
- `HybridCPU_ISE.Tests/PhasingAndExtensions/AssistRuntimeTests.cs`
- `HybridCPU_ISE.Tests/PhasingAndExtensions/AssistRuntimeTests.Part2.cs`
- `HybridCPU_ISE.Tests/PhasingAndExtensions/AssistRuntimeTests.Part3.cs`

The exhaustive tuple proof is expected to stay aligned with this document. If code and prose diverge, code plus proof wins and this document must be updated.
