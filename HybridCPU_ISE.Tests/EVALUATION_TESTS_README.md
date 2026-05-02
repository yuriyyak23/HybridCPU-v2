# Evaluation Test System

## Purpose

This document describes the current evaluation-test surface for HybridCPU ISE.
It is a repository-facing evidence guide, not a paper-result claim sheet.

The authoritative smoke baseline is documented in `Documentation/validation-baseline.md` and
runs through the repository-declared `VSTest` policy with `dotnet test`.
Live source and test-count evidence is tracked by `Documentation/evidence-matrix.md` and
`build/recount-validation-evidence.ps1`.

## Truthfulness Boundaries

The live ISE backend retains explicit rename and commit state.
Runtime state includes `PhysicalRegisterFile`, `RenameMap`, `CommitMap`, and `FreeList`.
Evaluation artifacts must therefore be read as evidence for typed-slot legality, bundle admission,
replay behavior, assist-lane legality, diagnostics, and execution-policy surfaces.
They must not be cited as proof that rename/commit machinery has been eliminated.

Legacy counter names can still appear in counters and older test family names.
When a retained counter uses `FSP`, the current architectural reading is typed-slot slack reclaim
and bundle-compositional SMT densification under legality/certificate constraints.

## Current Evaluation Framing

Current evaluation language is organized around the mechanisms implemented and validated in the
live tree:

- typed-slot reject taxonomy and reject-rate telemetry;
- structural identity reuse and certificate-pressure evidence;
- assist quota and assist backpressure legality;
- replay certificate coordination and replay invalidation;
- bounded replay-stable lane reuse, not universal deterministic scheduling;
- native-VLIW frontend boundaries with retired compatibility paths frozen.

Do not present fixed IPC multipliers as current measured evidence unless they come from a freshly
captured run and are tied to an artifact path.
Use `Documentation/AsmAppTestResults.md` and `TestAssemblerConsoleApps` for the current runtime
sanity baseline.

## Running The Evaluation Family

Run focused evaluation tests through the declared runner path:

```powershell
dotnet test ".\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --filter "FullyQualifiedName~EvaluationTests" -v minimal
```

Run the smoke baseline:

```powershell
powershell -ExecutionPolicy Bypass -File ".\build\run-validation-baseline.ps1" -NoRestore
```

Run the current runtime sanity baseline:

```powershell
dotnet run --project ".\TestAssemblerConsoleApps\TestAssemblerConsoleApps.csproj" -- --iterations 200
```

Compare the runtime key values with `Documentation/AsmAppTestResults.md`.

## Reviewer-Facing Evidence Vocabulary

Use the current vocabulary when summarizing evaluation results:

- `TypedSlotRejectReason.StaticClassOvercommit`;
- `TypedSlotRejectReason.DynamicClassExhaustion`;
- `TypedSlotRejectReason.ResourceConflict`;
- `TypedSlotRejectReason.DomainReject`;
- `TypedSlotRejectReason.ScoreboardReject`;
- `TypedSlotRejectReason.BankPendingReject`;
- `TypedSlotRejectReason.HardwareBudgetReject`;
- `TypedSlotRejectReason.SpeculationBudgetReject`;
- `TypedSlotRejectReason.AssistQuotaReject`;
- `TypedSlotRejectReason.AssistBackpressureReject`;
- `TypedSlotRejectReason.PinnedLaneConflict`;
- `TypedSlotRejectReason.LateBindingConflict`;
- replay invalidation reasons and certificate-pressure breakdowns from the live diagnostics surfaces.

## What These Tests Can Prove

- Typed-slot legality decisions are visible through named reject reasons.
- Slack reclaim and structural reuse can be measured without hiding rejects.
- Assist work is bounded by quota and backpressure legality.
- Replay reuse is bounded by explicit invalidation reasons.
- Native-VLIW active frontend behavior remains separated from retired compatibility paths.

## What These Tests Do Not Prove By Themselves

- They do not establish a current full-suite pass.
- They do not publish fixed IPC multipliers without fresh artifacts.
- They do not eliminate rename/commit machinery from the backend.
- They do not make historical `FSP` terminology authoritative.
- They do not turn file counts or declaration counts into coverage percentages.
