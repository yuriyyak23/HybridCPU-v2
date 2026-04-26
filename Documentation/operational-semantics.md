# Operational Semantics

## Goal of this artifact

This artifact externalizes one paper-facing operational semantics for the current HybridCPU ISE runtime.
It does not invent a new machine. It names the live state tuple, the cycle-step relation, and the bounded
sub-relations that already exist in code and proof surfaces.

The active frontend for this semantics is native VLIW only. Compat and DBT history may still exist in the
repository, but they are not the active mainline operational story.

## Scope and supporting bounded specs

This operational semantics is deliberately layered.

- `Documentation/WhiteBook/20. legality-predicate.md` defines the canonical legality predicate.
- `Documentation/WhiteBook/23. certificate-semantics-and-legality-evidence.md` defines the legality evidence chain.
- `HybridCPU_ISE/docs/replay-envelope.md` bounds replay reuse and determinism claims.
- `HybridCPU_ISE/docs/memory-model.md` bounds retire-time memory visibility.
- `HybridCPU_ISE/docs/exception-model.md` bounds stage-aware fault ordering.
- `HybridCPU_ISE/docs/rollback-boundaries.md` bounds rollback.
- `HybridCPU_ISE/docs/backend-state-truthfulness.md` keeps legality and retire claims honest relative to the live backend substrate.

This means the artifact is one operational seam, not one oversized theorem. When the story narrows for replay,
memory, exception, rollback, or backend truthfulness, those supporting specs remain authoritative.

## Machine state tuple

The canonical runtime state is described as:

```text
MachineState =
  <ArchitecturalState,
   FrontendState,
   SchedulerState,
   PipelineState,
   ReplayState,
   BackendState,
   EvidenceState>
```

Where:

- `ArchitecturalState` contains architecturally visible register, PC, memory, and trap/retire publication state.
- `FrontendState` contains fetch/decode inputs and the canonical decoded-bundle transport, especially `DecodedBundleRuntimeState`, `DecodedBundleDerivedIssuePlanState`, and `BundleLegalityDescriptor`.
- `SchedulerState` contains typed-slot nomination, `LegalityDecision` authority, class-capacity state, Stage A / Stage B admission state, and current issue-packet materialization candidates.
- `PipelineState` contains the live stage latches `pipeIF`, `pipeID`, `pipeEX`, `pipeMEM`, `pipeWB`, plus cycle/stall control.
- `ReplayState` contains `LoopBuffer`, `ReplayPhaseContext`, class-template reuse, legality cache reuse, and replay invalidation state.
- `BackendState` contains the live backend substrate: `PhysicalRegisterFile`, `RenameMap`, `CommitMap`, `FreeList`, and `RetireCoordinator`.
- `EvidenceState` contains trace/timeline and evidence surfaces such as contour certificates, legality telemetry, replay invalidation telemetry, and trace exports.

This repository-facing tuple is intentionally explicit about the backend substrate. The current runtime is not renaming-free.

## Inputs and step relation

External cycle inputs are described as:

```text
Inputs_t =
  <FetchPc_t,
   BoundMemoryAndIo_t,
   SchedulerNominations_t,
   AssistEvents_t,
   ReplayControl_t,
   SerializingOrTrapSignals_t>
```

The main cycle relation is:

```text
Step(MachineState_t, Inputs_t) -> MachineState_t+1
```

The concrete owner of this relation is `ExecutePipelineCycle()`.

### Two top-level step kinds

The live pipeline exposes two top-level cycle outcomes.

1. `STALL step`
   `ResolvePipelineHazardStallDecision()` or a decode-local stall blocks forward progress.
   The runtime records a `STALL` timeline sample, increments stall counters, and does not advance fetch in that cycle.
2. `CYCLE step`
   The runtime advances the pipeline shell in reverse stage order:
   `WB -> MEM -> EX -> ID -> IF`
   Then it records a `CYCLE` timeline sample, closes the loop-buffer cycle, and republishes replay-phase deactivation when needed.

This distinction is part of the semantics because cycle-visible stall accounting and normal stage advancement are not the same transition.

## Sub-relations inside the cycle step

### 1. Fetch / replay ingress

Fetch first selects between replay reuse and live bundle ingress.

- On replay hit, `LoopBuffer.TryReplay(...)` republishes cached decoded micro-ops for the requested PC.
- On replay miss, the runtime fetches the 256-byte native VLIW bundle and carries optional bundle annotations forward.

This is the first mainline branch in the operational semantics: replay is a bounded alternative ingress, not a hidden optimization that bypasses the rest of the machine model.

### 2. Decode and canonical bundle transport

Decode is bundle-aware. The decode sub-relation:

- decodes a full bundle into canonical transport;
- updates `DecodedBundleRuntimeState`;
- preserves `DecodedBundleDerivedIssuePlanState` as runtime-only derived packing state;
- produces a `BundleLegalityDescriptor` and related dependency/typed-slot facts for the current bundle.

The decode relation therefore separates canonical bundle truth from later issue and densification choices.

### 3. Stage A legality and Stage B lane materialization

The scheduler relation stays split:

- Stage A is `TryClassAdmission(...)`;
- Stage B is `TryMaterializeLane(...)`.

Stage A consumes `LegalityDecision` through `IRuntimeLegalityService`. It checks class capacity, runtime legality,
and outer-cap dynamic gates. Stage B only materializes a concrete lane from the already admitted class/topology envelope.

The legality relation is therefore:

```text
Decoded bundle facts
  -> certificate and replay context
  -> LegalityDecision
  -> Stage A admission
  -> Stage B lane materialization
```

Guard-plane checks remain earlier than replay/certificate reuse, and replay/certificate reuse remains earlier than lane materialization.

### 4. Stage-latch materialization

Materialization is a separate sub-relation after scheduler admission.

- issue-packet lanes become concrete execute/memory/writeback lane state;
- execution-surface guards reject unsupported VMX/stream paths fail closed;
- widened packet occupancy and explicit packet order remain runtime-visible evidence surfaces.

This keeps "admitted into a bundle" separate from "materialized into a live pipeline lane".

### 5. Execute, memory, and writeback evolution

Execute, memory, and writeback each evolve the current live subset of work.

- execute computes results, addresses, and stage-local fault candidates;
- memory carries deferred load/store and atomic state;
- writeback carries the retire-authoritative subset used by the retire/fault helpers.

These stage-local updates do not by themselves publish all architectural effects.

### 6. Retire publication

Architectural publication is a distinct retire sub-relation.

- `ResolveRetireEligibleWriteBackLanes(...)` and `ResolveStableRetireOrder(...)` define which writeback lanes are eligible and in what order;
- `TryResolveExceptionDeliveryDecisionForRetireWindow(...)` selects the current stage-aware fault winner;
- `RetireCoordinator` publishes typed retire records;
- `ApplyRetireEffect(...)` performs retire-time atomic memory apply;
- scalar store commit also becomes architecturally visible only at the retire-side apply boundary.

This keeps retire visibility, memory mutation, and architectural register publication separate instead of collapsing them into one vague commit event.

## Replay, memory, exception, and rollback boundaries

The operational semantics depends on bounded subsidiary relations.

- Replay and determinism claims stay inside the replay/evidence envelope from `HybridCPU_ISE/docs/replay-envelope.md`.
- Memory visibility follows the retire-time apply rule from `HybridCPU_ISE/docs/memory-model.md`; this is not a global memory-order theorem.
- Exception ordering follows the bounded stage-aware retire/exception model from `HybridCPU_ISE/docs/exception-model.md`; this is not a complete precise-exception theorem.
- Rollback follows `ReplayToken` capture/restore limits from `HybridCPU_ISE/docs/rollback-boundaries.md`.

These are not optional footnotes. They are part of why the operational semantics is truthful rather than overclaimed.

## Backend truthfulness

The operational semantics must remain compatible with the live backend substrate.

- legality and certificate success constrain publication;
- retire order constrains which effects become architecturally visible and when;
- `PhysicalRegisterFile`, `RenameMap`, `CommitMap`, `FreeList`, and `RetireCoordinator` still exist and still matter.

So the current operational story is not renaming-free, and it does not pretend that legality or typed-slot scheduling replaces backend ownership machinery.

## Typed-slot facts and current normative status

Compiler-emitted typed-slot facts participate in decode and agreement checking, and their canonical staging surface remains `ValidationOnly` through `TypedSlotFactStaging.CurrentMode`.
At the bridge boundary, however, `CompilerContract.CurrentTypedSlotPolicy` remains `CompatibilityValidation` after the fail-closed version handshake enforced by `DeclareCompilerContractVersion(...)` and `EnsureCompilerContractHandshake(...)`.

This means missing facts remain compatible with canonical execution, while present facts may be validated, recorded, and quarantine-logged without displacing runtime legality authority.
Runtime legality remains the final decision maker for Stage A admission.

## Repository-facing non-claims

This operational semantics does not claim:

- a global memory-order theorem;
- a complete precise-exception theorem;
- universal rollback;
- global determinism outside the replay/evidence envelope;
- that legality/certificates remove the backend substrate;
- that typed-slot facts are already mandatory for canonical execution.

## Evidence anchors

Primary code authorities:

- `HybridCPU_ISE/Core/Pipeline/Core/CPU_Core.PipelineExecution.StageFlow.cs`
- `HybridCPU_ISE/Core/Pipeline/Core/CPU_Core.Pipeline.cs`
- `HybridCPU_ISE/Core/Pipeline/Core/CPU_Core.Pipeline.Helpers.cs`
- `HybridCPU_ISE/Core/Pipeline/Core/CPU_Core.PipelineExecution.Materialization.cs`
- `HybridCPU_ISE/Core/Pipeline/Core/CPU_Core.PipelineExecution.Retire.cs`
- `HybridCPU_ISE/Core/Pipeline/Scheduling/MicroOpScheduler.Admission.cs`
- `HybridCPU_ISE/Core/Pipeline/Safety/SafetyVerifier.SmtLegality.cs`
- `HybridCPU_ISE/Core/Legality/BundleLegalityAnalyzer.cs`
- `HybridCPU_ISE/Core/Legality/BundleLegalityDescriptor.cs`

Representative proof surfaces:

- `HybridCPU_ISE.Tests/tests/Phase09OperationalSemanticsDocumentationTests.cs`
- `HybridCPU_ISE.Tests/tests/Phase09LegalityPredicateDocumentationTests.cs`
- `HybridCPU_ISE.Tests/tests/Phase09CertificateSemanticsDocumentationTests.cs`
- `HybridCPU_ISE.Tests/tests/Phase09RetireContractClosureTests.cs`
- `HybridCPU_ISE.Tests/tests/Phase09WriteBackFaultOrderingProofTests.cs`
- `HybridCPU_ISE.Tests/tests/Phase09MemoryModelDocumentationTests.cs`
- `HybridCPU_ISE.Tests/tests/Phase09ExceptionModelDocumentationTests.cs`
- `HybridCPU_ISE.Tests/tests/Phase09RollbackBoundariesDocumentationTests.cs`
- `HybridCPU_ISE.Tests/tests/Phase09ReplayEnvelopeDocumentationTests.cs`
- `HybridCPU_ISE.Tests/tests/Phase09BackendStateTruthfulnessDocumentationTests.cs`

If code and proof change, this artifact must change with them.
