# Phase 15 - Full validation baseline and rollback

Status: closed on 2026-04-28.

Goal:

- Validate the complete L7-SDC migration against focused tests, existing
  regression surfaces, and TestAssemblerConsoleApps diagnostics.
- Preserve rollback switches that disable execution while keeping parse, guard,
  token, telemetry, and fail-closed surfaces.

ISE files likely touched:

- test configuration files
- optional `ExternalAcceleratorFeatureSwitch`
- validation docs
- no runtime behavior changes unless final validation exposes a fail-closed fix

Compiler files likely touched:

- compiler test configuration
- optional feature switch integration for accelerator emission

Final validation filters:

```powershell
dotnet test --filter "L7Sdc"
dotnet test --filter "DmaStreamCompute"
dotnet test --filter "Phase09"
dotnet test --filter "Phase12"
dotnet test --filter "AssistRuntime"
dotnet test --filter "StreamRegisterFile"
dotnet test --filter "CompilerV5ContractAlignment"
dotnet test --filter "Phase4Extensibility"
```

Full validation:

```powershell
dotnet test
```

Diagnostics validation:

```powershell
dotnet run --project TestAssemblerConsoleApps/TestAssemblerConsoleApps.csproj -- matrix-smoke --iterations 200 --telemetry-logs minimal
dotnet run --project TestAssemblerConsoleApps/TestAssemblerConsoleApps.csproj -- matrix-memory --iterations 200 --telemetry-logs minimal
```

Phase closure validation:

- Run all final validation filters listed above.
- Run full repository validation.
- Run `TestAssemblerConsoleApps` `matrix-smoke` and `matrix-memory` diagnostics
  with fixed iterations and minimal logs.
- Compare each diagnostics run against `Documentation/AsmAppTestResults.md`.
- Treat unexplained IPC, cycle, stall, legality-reject, issue-width,
  branch/system progress, lane6, or memory-counter drift as release-blocking.
- Record accepted diagnostics results or explicit no-change confirmation in
  `Documentation/AsmAppTestResults.md` according to repository convention.

Regression comparison:

- compare new output and artifact summaries with
  `Documentation/AsmAppTestResults.md`
- review IPC, cycle count, effective issue width, stalls, load-use bubbles,
  branch mispredicts, frontend stalls, pinned-constraint NOPs, resource
  conflict NOPs, memory stalls, SMT legality rejects, lane6 counters,
  branch/system progress, and telemetry shape
- update `Documentation/AsmAppTestResults.md` only when deltas are intentional,
  explained, and tied to a specific L7-SDC change

Release gate:

- all L7-SDC tests pass
- all legacy quarantine tests pass
- DmaStreamCompute remains lane6-only and green
- StreamEngine/SRF/assist tests remain green
- compiler alignment tests remain green
- no production path calls `ICustomAccelerator.Execute()`
- no direct backend write can commit
- no token handle can commit without guard
- mapping epoch drift prevents detach/suspend commit
- no runtime fallback test can succeed
- TestAssemblerConsoleApps diagnostics show no unexplained regression

Rollback controls:

- feature switch can disable accelerator submit/backend execution
- parser and carrier validation may remain enabled for negative tests
- guard checks remain enabled
- token store may reject new tokens while preserving status/fault tests
- backend defaults to null reject mode
- compiler emission can be disabled independently

Rollback rules:

- do not revert unrelated files
- do not delete fail-closed seams prematurely
- do not weaken owner/domain guards
- do not weaken mapping epoch checks
- do not reinterpret telemetry/replay/certificates/tokens/registry as authority
- if execution risk appears, disable execution while preserving parse, guard,
  token, telemetry, and fail-closed surfaces
- if commit risk appears, stop at `CommitPending` and fault/reject commit
- if conflict coverage is incomplete, reject overlapping submits
- if compiler emission is unsafe, disable accelerator lowering
- if MatMul migration is unsafe, unregister the provider

Definition of done:

- validation results are recorded
- `Documentation/AsmAppTestResults.md` contains the accepted diagnostics
  baseline or an explicit no-change confirmation according to repository
  convention
- rollback plan has been exercised at least once in tests or documented as a
  manual feature-switch procedure

Closure evidence:

- Added explicit `ExternalAcceleratorFeatureSwitch` rollback control for
  accelerator submit admission and backend execution.
- Rollback controls are covered by `L7SdcPhase15RollbackControlsTests`:
  submit can be disabled after parser/carrier/guard validation without token
  creation, backend submit can be disabled before queue/staging, and backend
  tick can be disabled without draining queued commands.
- Phase 15 selective filters passed:
  `L7Sdc`, `DmaStreamCompute`, `Phase09`, `Phase12`, `AssistRuntime`,
  `StreamRegisterFile`, `CompilerV5ContractAlignment`, and
  `Phase4Extensibility`.
- Full repository validation passed with `dotnet test`: 5917 passed, 2
  skipped, 0 failed, 5919 total.
- Diagnostics passed:
  `matrix-smoke` artifact
  `TestResults\TestAssemblerConsoleApps\20260428_202008_353_matrix-smoke`;
  `matrix-memory` artifact
  `TestResults\TestAssemblerConsoleApps\20260428_202017_546_matrix-memory`.
- Accepted diagnostics comparison is recorded in
  `Documentation/AsmAppTestResults.md`.
