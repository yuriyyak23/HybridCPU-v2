# Scheduler Contours And Boundaries

## Scope

This document fixes the paper-facing scheduler contour without promoting one
path into a universal scheduler theorem. The active repository still contains
multiple runtime contours with different roles.

`TypedSlotEnabled` is the master contour toggle for the typed-slot path. In the
current mainline it is a backward-compatible opt-in rollout / A-B comparison
surface. Default `false` does not throw or halt execution; it reverts the
runtime to the exact-slot compatibility path.

## Three Live Contours

### 1. Single-Cycle Typed-Slot Admission/Materialization Path

When `TypedSlotEnabled` is true, `PackBundleIntraCoreSmt(...)` performs the
foreground typed-slot path inside one call:

- prepare class capacity, `BundleResourceCertificate4Way`,
  `SmtBundleMetadata4Way`, and `BoundaryGuardState`;
- publish that state through `PrepareSmt(...)`;
- nominate and order candidates;
- run Stage A `TryClassAdmission(...)`;
- run Stage B `TryMaterializeLane(...)`;
- perform bundle-local mutation into the working bundle;
- refresh legality/replay state after each successful mutation;
- run the trailing single-cycle intra-core assist pass through
  `TryInjectAssistCandidates(...)`.

### 2. Pipelined FSP Contour

The pipelined contour is not identical to the single-cycle path.

- `PipelineFspStage1_Nominate(...)` is nomination-only. It records ready
  foreground candidates and the owner virtual thread for the next cycle.
- `PipelineFspStage2_Intersect(...)` reloads the live candidates and performs
  foreground typed-slot Stage A, Stage B, and bundle-local mutation into the
  working bundle.
- `PackBundleIntraCoreSmt(...)` then clears assist nomination ports and
  returns. `SCHED2` does not currently integrate the trailing intra-core assist
  pass.

### 3. Exact-Slot Compatibility Path

When `TypedSlotEnabled == false`, the active repository retains an exact-slot
compatibility contour.

- `ResolveNextInjectableSlot(...)` searches a concrete slot first.
- The checker-owned legality path is then consumed for that exact-slot
  opportunity state.
- `TryPassOuterCap(...)` still applies as a later dynamic gate.
- Disabling the typed-slot contour therefore falls back to compatibility rather
  than failing closed.

This contour remains part of the live repository. It is compatibility scope, not
the architecture-defining Stage A / Stage B path.

## Stage A / Stage B Scope

Stage A / Stage B terminology is intentionally narrow.

- Stage A means `TryClassAdmission(...)`.
- Stage B means `TryMaterializeLane(...)`.
- Stage A performs class admission and does not choose a physical lane.
- Stage B materializes one concrete lane inside the admitted class envelope.
- Stage B does not widen legality after Stage A succeeds.
- `LateBindingConflict` means that the current bundle could not preserve typed
  packet structure under class masks, hard pinning, lane aliasing, occupancy,
  and replay-aware lane constraints; it is not generic port arbitration.
- These names apply only to the typed-slot admission/materialization path.
- They do not describe the exact-slot compatibility path.

## Meaning Of Scheduler-Local Commit

Scheduler-local `Commit` comments do not mean architectural retirement; they
are not architectural retirement.

In the scheduler files, `Commit` denotes bundle-local mutation into the working
bundle together with legality-witness state, metadata, boundary, occupancy, and
telemetry refresh. Architectural retirement remains a later retire-side
publication surface.

## Repository-Facing Non-Claims

This document does not claim that:

- the repository already has one universal scheduler path;
- Stage A / Stage B is a theorem over every retained contour;
- the pipelined FSP contour and the single-cycle assist path are identical;
- scheduler-local mutation into the working bundle is architectural retirement.

## Code And Proof Surfaces

Primary code authority:

- `HybridCPU_ISE/Core/Pipeline/Scheduling/MicroOpScheduler.SMT.cs`
- `HybridCPU_ISE/Core/Pipeline/Scheduling/MicroOpScheduler.Admission.cs`
- `HybridCPU_ISE/Core/Pipeline/Scheduling/MicroOpScheduler.FSPPipeline.cs`
- `HybridCPU_ISE/Core/Pipeline/Scheduling/MicroOpScheduler.Assist.cs`

Representative proof surfaces:

- `HybridCPU_ISE.Tests/tests/Phase09RuntimeLegalityServiceReachabilityProofTests.cs`
- `HybridCPU_ISE.Tests/PhasingAndExtensions/AssistRuntimeTests.Part3.cs`
- `HybridCPU_ISE.Tests/tests/Phase12VliwCompatFreezeTests.cs`
