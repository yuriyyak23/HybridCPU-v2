# Replay Envelope

This document is the Phase 04 reviewer-facing closure surface for replay reuse,
invalidation, and determinism language. It describes the landed runtime contract;
it does not introduce a new replay architecture.

## Scope

Replay in this repository is an evidence-bounded runtime facility. It is not a
blanket theorem over all hidden state. In short: this is not a global determinism theorem,
and it is not a promise that every hidden emulator, cache, queue, or timing
dimension is frozen.

The bounded replay story is:

- Manuscript-facing prose may call the reusable object a legality witness; this
  document keeps the repository term `certificate` only when exact code-grounded
  traceability matters.
- `LoopBuffer` caches one decoded 8-slot bundle and publishes a `ReplayPhaseContext`.
- `ReplayPhaseContext` carries replay identity through `EpochId`, `CachedPc`,
  `ValidSlotCount`, `StableDonorMask`, and `LastInvalidationReason`.
- Replay-aware witness caches may reuse legality decisions only while their
  phase key and structural witness identity still match the live phase.
- Scheduler-side replay reuse is therefore phase/template-driven. `ReplayToken`
  is a separate bounded rollback carrier documented in
  `HybridCPU_ISE/docs/rollback-boundaries.md`; it is not the sole replay
  substrate.
- `BundleResourceCertificateIdentity4Way` preserves the distinction between
  shared structural state and per-VT register-group hazards because its
  construction incorporates `SharedMask` and `RegMaskVT0..VT3` separately
  rather than flattening them into one undifferentiated hazard domain.
- Domain, owner, and boundary guards remain authority surfaces; replay reuse is
  never allowed to bypass them.
- The guard invariants are closed in `HybridCPU_ISE/docs/domain-guard-invariants.md`.
- `TraceSink`, scheduler counters, and `ReplayEngine` expose replay evidence
  instead of hiding replay as a pure performance optimization.

## Replay Reuse Validity

Replay reuse is valid only when all of the following are true:

- the replay phase is active and the `ReplayPhaseKey` is valid;
- the requested fetch PC still matches the cached loop-buffer PC;
- the replay-stable donor mask and valid slot count match the stored phase key;
- the structural witness identity still matches the cached template;
- SMT bundle metadata, owner/domain scope, and boundary guard state still match
  the stored replay/template key;
- guard-before-reuse has already accepted the candidate through the guard plane;
- no replay, assist, or witness invalidation reason has closed the reuse
  window.

If any of these conditions fails, the runtime must fall back to a live legality
path or publish an invalidation reason. It must not treat replay reuse as
unconditional replay reuse.

The replay/template key therefore preserves, rather than erases, the distinction
between shared structural resources and per-VT register hazards.

## Guard-Before-Reuse Rule

The guard plane is earlier than replay witness acceptance.

For SMT legality, `SafetyVerifier.EvaluateSmtLegality(...)` evaluates:

1. `EvaluateSmtBoundaryGuard(...)`
2. owner/domain guard rejection through `TryRejectSmtOwnerDomainGuard(...)`
3. replay-phase witness reuse when `PhaseCertificateTemplateKey4Way` matches
4. current-bundle structural-witness fallback

For inter-core legality, `SafetyVerifier.EvaluateInterCoreLegality(...)` first
resolves domain, owner, scoreboard, stealability, and ordering guards through
`TryResolveInterCoreGuardDecision(...)`. Only after those guards pass may the
runtime attempt replay certificate reuse.

This ordering is part of the semantics, not an implementation accident.

## Replay Invalidation Reasons

`ReplayPhaseInvalidationReason` is the canonical replay-phase invalidation
taxonomy.

| Reason | Meaning inside the replay envelope |
| --- | --- |
| `None` | No replay invalidation has been published. |
| `Completed` | The loop-buffer replay phase drained normally. |
| `PcMismatch` | A requested fetch PC diverged from the cached replay PC. |
| `Manual` | A generic or explicit invalidation closed the replay window. |
| `CertificateMutation` | A bundle/witness mutation made cached legality evidence stale. |
| `PhaseMismatch` | Scheduler/witness-cache phase identity no longer matches the live replay phase. |
| `InactivePhase` | A reuse path observed an inactive or non-reusable phase. |
| `DomainBoundary` | Owner/domain scope changed across a cached template boundary. |
| `ClassCapacityMismatch` | Live typed-slot class capacity no longer satisfies the cached class template. |
| `ClassTemplateExpired` | A cached class template aged out or was explicitly expired. |
| `SerializingEvent` | Trap, fence, VM transition, or serializing boundary closed replay reuse. |

Loop-buffer invalidation, scheduler class-template invalidation, and
legality-witness cache invalidation all report through this bounded vocabulary.

## Assist-Induced Invalidation

`AssistInvalidationReason` is the assist-side taxonomy. It is related to replay,
but it is not the same enum and must not be collapsed into replay identity.

| Reason | Replay relationship |
| --- | --- |
| `None` | No assist invalidation has been published. |
| `Replay` | Assist nomination became stale because its replay epoch no longer matches the live phase. |
| `Trap` | Pipeline flush maps this to `SerializingEvent` for replay phase invalidation. |
| `Fence` | Pipeline flush maps this to `SerializingEvent` for replay phase invalidation. |
| `VmTransition` | Pipeline flush maps this to `SerializingEvent` for replay phase invalidation. |
| `SerializingBoundary` | Pipeline flush maps this to `SerializingEvent` for replay phase invalidation. |
| `OwnerInvalidation` | Local owner/domain/foreground state invalidated the assist nomination; replay may remain separately valid. |
| `Manual` | Assist-local manual invalidation; generic replay flushes map to `Manual` unless they are serializing. |
| `PipelineFlush` | Generic flush reason; replay invalidation resolves to `Manual` unless the assist reason is serializing. |
| `InterCoreOwnerDrift` | Inter-core donor/carrier ownership snapshot no longer matches the assist tuple. |
| `InterCoreBoundaryDrift` | Inter-core donor assist epoch changed across the transport boundary. |

Assist invalidation is telemetry-visible and replay-trace-visible. It does not
make assist a retiring architectural operation.

## Determinism Boundary

Repository-facing determinism language must stay inside the replay/evidence
envelope:

- `DeterministicLaneChooser.SelectWithReplayHint(...)` is deterministic for the
  supplied free-lane mask and previous-lane hint.
- replay-stable placement means the runtime follows a fixed placement rule while
  the replay phase, guard state, and legality evidence still match;
- `ReplayEngine.CompareRepeatedRunsWithinEnvelope(...)` compares traces under
  an explicit `ReplayEnvelopeConfiguration`;
- perturbations outside that configuration are reported as mismatches,
  invalidations, misses, or named evidence divergence.
- `Acquire/Release` and other ordering surfaces may trigger later legality or
  serializing behavior, but this document does not claim that one `Release`
  invalidates every participant's replay state in a mixed SMT bundle.

The runtime may therefore claim replay-stable or evidence-bounded behavior. It
must not claim that all microarchitectural, telemetry, cache, queue, or hidden
host-state dimensions are globally deterministic.

## Evidence Surfaces

Primary code authority:

- `HybridCPU_ISE/Core/Pipeline/Components/LoopBuffer.cs`
- `HybridCPU_ISE/Core/Pipeline/Certificates/ReplayPhaseSubstrate.cs`
- `HybridCPU_ISE/Core/Pipeline/Certificates/ReplayPhaseSubstrate.Implementations.cs`
- `HybridCPU_ISE/Core/Pipeline/Safety/SafetyVerifier.Guards.cs`
- `HybridCPU_ISE/Core/Pipeline/Safety/SafetyVerifier.RuntimeLegality.cs`
- `HybridCPU_ISE/Core/Pipeline/Safety/SafetyVerifier.SmtLegality.cs`
- `HybridCPU_ISE/Core/Pipeline/Scheduling/DeterministicLaneChooser.cs`
- `HybridCPU_ISE/Core/Diagnostics/TraceSink.cs`
- `HybridCPU_ISE/Core/Diagnostics/ReplayEngine.Analysis.cs`

Primary proof surfaces:

- `HybridCPU_ISE.Tests/tests/Phase09ReplayCertificateCoordinatorProofTests.cs`
- `HybridCPU_ISE.Tests/tests/Phase09ReplayFlushInvalidationReasonTests.cs`
- `HybridCPU_ISE.Tests/tests/Phase09SafetyVerifierGuardMatrixProofTests.cs`
- `HybridCPU_ISE.Tests/FSPAndSpeculative/DeterminismEnvelopeTests.cs`
- `HybridCPU_ISE.Tests/FSPAndSpeculative/PhaseCertificateReuseTelemetryTests.cs`
- `HybridCPU_ISE.Tests/FSPAndSpeculative/LoopPhaseTelemetryTests.cs`

If prose and code diverge, code plus proof wins and this document must be
updated.
