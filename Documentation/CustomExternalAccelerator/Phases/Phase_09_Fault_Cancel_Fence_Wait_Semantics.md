# Phase 09 - Fault, cancel, fence, and wait semantics

Status: closed on 2026-04-28.

Goal:

- Implement deterministic token observation and control operations while
  preserving guard authority and staged commit boundaries.

ISE files likely touched:

- `HybridCPU_ISE/Core/Pipeline/MicroOps/SystemDeviceCommandMicroOp.cs`
- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Tokens/*`
- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Fences/*`
- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Backends/*`
- `HybridCPU_ISE/Core/Pipeline/Core/CPU_Core.*.cs`

Compiler files likely touched:

- hazard/serialization metadata if compiler emits wait/fence/cancel sequences

New ISE types:

- `AcceleratorWaitPolicy`
- `AcceleratorCancelPolicy`
- `AcceleratorFenceScope`
- `AcceleratorFenceResult`
- `AcceleratorExceptionPublication`
- `AcceleratorFaultPublicationResult`

Implemented or intentionally fail-closed surfaces:

- `AcceleratorPollMicroOp`, `AcceleratorWaitMicroOp`,
  `AcceleratorCancelMicroOp`, and `AcceleratorFenceMicroOp` exist as lane7
  `SystemSingleton` carriers; direct `Execute` remains fail-closed.
- `AcceleratorTokenStore.TryPoll(...)`
- `AcceleratorTokenStore.TryWait(...)`
- `AcceleratorTokenStore.TryCancel(...)`
- `AcceleratorFenceCoordinator.TryFence(...)`
- `AcceleratorExceptionPublication.TryPublish(...)`
- `AcceleratorLane7PressureThrottle.TryAdmit(...)`

Semantic requirements:

- poll revalidates owner/domain and mapping epoch before exposing state
- wait is serializing and must not publish direct device writes
- wait returns packed status on completion, timeout, cancel, or fault
- cancel cannot grant commit authority
- cancel of running token is cooperative unless backend can prove safe abort
- fence drains, commits, cancels, faults, or rejects scoped active tokens
- fault publication to invalid owner is forbidden; privileged diagnostics may
  record the fault

Lane7 pressure policy:

- L7-SDC commands must be coarse-grain
- scheduler/runtime must throttle submit/poll storms
- `WAIT` and `FENCE` are serializing and should be rare
- compiler/runtime should prefer batching or low-rate polling where possible

Implemented tests:

- `L7SdcPollWaitCancelFenceTests`
- `L7SdcFaultPublicationTests`
- `L7SdcLane7PressureTests`

Test cases:

- poll returns packed status without commit
- wait timeout reports timeout, not success
- cancel queued token
- cancel running token with cooperative fake backend
- cancel running token with non-cooperative backend faults or drains
- fence rejects/drains active conflict
- invalid-owner fault is not published to old context
- repeated poll storm triggers throttle evidence or scheduling rejection

Must not break:

- system instruction serialization
- branch/system lane7 alias legality
- baseline branch/control progress

Phase closure validation:

- Run focused tests:

```powershell
dotnet test --filter "L7SdcPollWaitCancelFence|L7SdcFaultPublication|L7SdcLane7Pressure"
```

- Run affected baseline filters:

```powershell
dotnet test --filter "Phase09|Phase12|CompilerV5ContractAlignment"
```

- Run diagnostics console:

```powershell
dotnet run --project TestAssemblerConsoleApps/TestAssemblerConsoleApps.csproj -- matrix-smoke --iterations 200 --telemetry-logs minimal
```

- Compare with `Documentation/AsmAppTestResults.md`; explicitly inspect
  branch/control/system progress and pinned-constraint NOPs for lane7 pressure
  regressions.

Definition of done:

- every token observation/control path revalidates authority
- fault/cancel/wait/fence cannot bypass commit-plane checks

Rollback rule:

- disable wait/cancel/fence success paths and keep poll/fault observation only
  if control semantics are incomplete

## Validation closure evidence - 2026-04-28

Focused gate:

```powershell
dotnet test HybridCPU_ISE.Tests/HybridCPU_ISE.Tests.csproj --filter "L7SdcPollWaitCancelFence|L7SdcFaultPublication|L7SdcLane7Pressure" --no-restore
```

Result: passed, `23/23`, `0` skipped.

Regressions:

- Phase 08 commit/rollback/SRF-cache invalidation: passed, `17/17`.
- Phase 07 backend/queue/direct-write violation: passed, `14/14`.
- Phase 06 token/register ABI/token-handle authority: passed, `22/22`.
- Phase 05 owner/domain and mapping/IOMMU epoch guards: passed, `20/20`.
- Phase 04 descriptor parser/carrier/transport: passed, `50/50`.
- Phase 03 opcode/hard-pinning/no-BranchControl authority: passed, `43/43`.
- Phase 09/Phase 12/compiler affected baseline: passed, `1451/1451`.
- Existing affected baseline: passed, `189/189`.

Diagnostics:

```powershell
dotnet run --project TestAssemblerConsoleApps/TestAssemblerConsoleApps.csproj -- matrix-smoke --iterations 200 --telemetry-logs minimal
```

Result: succeeded, `3` child runs, artifacts under
`TestResults/TestAssemblerConsoleApps/20260428_170625_896_matrix-smoke`.

Diagnostics shape note:

- `matrix-smoke` still emits `safety`, `replay-reuse`, and `assistant`.
- It does not emit separate branch/control/system progress or
  pinned-constraint NOP counter blocks. This diagnostics shape was already
  documented in `Documentation/AsmAppTestResults.md`, so that file was not
  updated for this phase.
